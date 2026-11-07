using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GeoWar.Game;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class UiTests
    {
        private const string UiFile = "Assets/Data/Resources/ui.json";
        private const string ArtResourcesRoot = "Assets/Art/Resources";
        private const float Tolerance = 0.01f;
        private const int HexColorLength = 7;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            CompactKeepsThreeDigitsAndTruncates(check);
            PathCommandsAreParsed(check);
            UiDataIsComplete(check, definition);
            ClockSplitsDaysAcrossCountries(check, definition);
            CityListSetsWorldSize(check, definition);
            BarMovesAndSwapsResources(check);
        }

        private static void BarMovesAndSwapsResources(Action<string, bool> check)
        {
            var bar = new ResourceBar(new[] { 10, 20, 30 });

            bar.Place(1, 99);
            check("bar: yeni kaynak kutuya girer", bar.Items[1] == 99 && !bar.Contains(20));

            bar.Place(0, 30);
            check("bar: bardaki kaynak yer değiştirir", bar.Items[0] == 30 && bar.Items[2] == 10);

            bar.Place(1, 99);
            check("bar: aynı kutuya aynı kaynak değişmez", bar.Items[0] == 30 && bar.Items[1] == 99 && bar.Items[2] == 10);
        }

        private static void CompactKeepsThreeDigitsAndTruncates(Action<string, bool> check)
        {
            check("sayı: 999 kısalmaz", NumberFormat.Compact(999) == "999");
            check("sayı: 1000", NumberFormat.Compact(1000) == "1 B");
            check("sayı: yuvarlamaz, keser", NumberFormat.Compact(1999) == "1,99 B");
            check("sayı: sondaki sıfır yazılmaz", NumberFormat.Compact(1200) == "1,2 B");
            check("sayı: ondalıktaki sıfır korunur", NumberFormat.Compact(1050) == "1,05 B");
            check("sayı: iki basamak", NumberFormat.Compact(12345) == "12,3 B");
            check("sayı: üç basamak", NumberFormat.Compact(123456) == "123 B");
            check("sayı: milyon", NumberFormat.Compact(85300000) == "85,3 Mn");
            check("sayı: milyar", NumberFormat.Compact(2500000000) == "2,5 Mr");
            check("sayı: eksi", NumberFormat.Compact(-1500) == "−1,5 B");
            check("sayı: artı işareti", NumberFormat.Signed(1200) == "+1,2 B");
            check("sayı: binde birim ondalıklı", NumberFormat.Milli(1250) == "1,25");
            check("sayı: binde birim tam", NumberFormat.Milli(3000) == "3");
            check("sayı: binde birim tek ondalık", NumberFormat.Milli(6500) == "6,5");
            check("miktar: küçük değer iki ondalık", NumberFormat.Amount(3330) == "3,33");
            check("miktar: bir birimden az", NumberFormat.Amount(333) == "0,33");
            check("miktar: on birimden sonra bir ondalık", NumberFormat.Amount(45670) == "45,6");
            check("miktar: yüz birimden sonra ondalık yok", NumberFormat.Amount(345670) == "345");
            check("miktar: binden sonra kısaltır", NumberFormat.Amount(12345000) == "12,3 B");
            check("miktar: eksi", NumberFormat.Amount(-2500) == "−2,5");
            check("miktar: sıfır", NumberFormat.Amount(0) == "0");
            check("değişim: artı işareti", NumberFormat.SignedAmount(4850) == "+4,85");
            check("değişim: eksi işareti", NumberFormat.SignedAmount(-17500) == "−17,5");
            check("değişim: sıfırda işaret yok", NumberFormat.SignedAmount(0) == "0");
            check("yazı: küçük harf Türkçe", NumberFormat.Lower("İlaç ve IRAK") == "ilaç ve ırak");
            check("yazı: büyük harf Türkçe", NumberFormat.Upper("Diyarbakır ve İzmir") == "DİYARBAKIR VE İZMİR");
        }

        private static void PathCommandsAreParsed(Action<string, bool> check)
        {
            List<PathCommand> relative = SvgPathParser.Parse("M5 4h14v3h-5z");
            check("yol: göreli komutlar", relative.Count == 5 && relative[3].X == 14 && relative[3].Y == 7);
            check("yol: kapanış başa döner", relative[4].Op == PathOp.Close && relative[4].X == 5 && relative[4].Y == 4);

            List<PathCommand> compact = SvgPathParser.Parse("M15 9c-1-1.5-6-1.5-6 .8s6 2 6 4.400");
            check("yol: bitişik sayılar okunur", compact.Count == 3 && Near(compact[1].X, 9) && Near(compact[1].Y, 9.8f));
            check("yol: s eğrisi yansır", Near(compact[2].X1, 9) && Near(compact[2].Y1, 12.1f));

            List<PathCommand> repeated = SvgPathParser.Parse("M1 1 2 2 3 3");
            check("yol: taşımadan sonraki sayılar çizgidir", repeated.Count == 3 && repeated[2].Op == PathOp.Line);

            // Merkezi 12,12 olan 9 yarıçaplı daire: yarı yolda alt noktaya, sonunda başa ulaşır.
            List<PathCommand> circle = SvgPathParser.Parse("M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z");
            PathCommand half = circle.First(command => Near(command.X, 12) && Near(command.Y, 21));
            check("yol: yay uç noktaya varır", half.Op == PathOp.Cubic);
            check("yol: yay daire üstünde kalır", circle.Where(c => c.Op == PathOp.Cubic).All(c => Near(Distance(c.X, c.Y, 12, 12), 9)));
        }

        private static void UiDataIsComplete(Action<string, bool> check, EconomyDefinition definition)
        {
            UiData data = Load();
            var icons = new HashSet<string>(data.Icons.Select(icon => icon.Key));
            var resources = new HashSet<string>(data.Resources.Select(resource => resource.Key));

            bool allParse = true;
            foreach (IconData icon in data.Icons)
            {
                allParse &= SvgPathParser.Parse(icon.Path).Count > 0;
            }

            check("arayüz: tüm ikon yolları okunur", allParse);
            check("arayüz: her kaynağın görünümü var", definition.ResourceKeys.All(resources.Contains));
            check("arayüz: fazladan kaynak yok", data.Resources.All(resource => definition.ResourceKeys.Contains(resource.Key)));
            check("arayüz: kaynak ikonları var", data.Resources.All(resource => icons.Contains(resource.Icon)));
            check("arayüz: üst bar on altı farklı kaynak", data.StartBar.Distinct().Count() == 16 && data.StartBar.Count == 16 && data.StartBar.All(resources.Contains));
            var buildings = new HashSet<string>(data.Buildings.Select(building => building.Key));
            var tabs = new HashSet<string>(data.Tabs.Select(tab => tab.Key));
            check("arayüz: her tesisin görünümü var", definition.BuildingKeys.All(buildings.Contains));
            check("arayüz: fazladan tesis yok", data.Buildings.All(building => definition.BuildingKeys.Contains(building.Key)));
            check("arayüz: tesis ikonları var", data.Buildings.All(building => icons.Contains(building.Icon)));
            check("arayüz: tesis ürün ikonları var", data.Buildings.All(building => string.IsNullOrEmpty(building.ProducesIcon) || icons.Contains(building.ProducesIcon)));
            check("arayüz: her tesisin ürün satırı dolu", data.Buildings.All(building =>
                !string.IsNullOrEmpty(building.Produces) || definition.BuildingOutputResources(definition.BuildingIndex(building.Key)).Count > 0));
            check("arayüz: her tesisin sekmesi var", definition.BuildingTabs.All(tabs.Contains));
            check("arayüz: malzemeler kaynak", data.BuildMaterials.All(resources.Contains));
            check("arayüz: her ulaşım türünün adı var", definition.TransportKeys.All(key => data.Transports.Any(view => view.Key == key && !string.IsNullOrEmpty(view.Name))));
            check("arayüz: her çıkarma tesisinde kaynak adı var", data.Buildings.All(building =>
                !GeoWar.Simulation.FacilityCommands.IsExtraction(definition, definition.BuildingIndex(building.Key)) || !string.IsNullOrEmpty(building.Deposit)));
            check("arayüz: ülke başlıkları var", data.Countries.All(country => !string.IsNullOrEmpty(country.Title)));
            List<string> grouped = data.ResourceGroups.SelectMany(group => group.Keys).ToList();
            check("arayüz: her kaynak bir gruptadır", resources.All(grouped.Contains));
            check("arayüz: kaynak iki grupta olmaz", grouped.Count == grouped.Distinct().Count());
            check("arayüz: grupta bilinmeyen kaynak yok", grouped.All(resources.Contains));
            check("arayüz: her grubun ikon klasörü var", data.ResourceGroups.All(group =>
                !string.IsNullOrEmpty(group.IconFolder) && Directory.Exists(Path.Combine(ArtResourcesRoot, group.IconFolder))));
            check("arayüz: ikon görseli kendi grubunun kaynağıdır", data.ResourceGroups.All(group =>
                Directory.GetFiles(Path.Combine(ArtResourcesRoot, group.IconFolder), "*.png")
                    .All(file => group.Keys.Contains(Path.GetFileNameWithoutExtension(file)))));
            string[] statIcons = { data.TreasuryIcon, data.MoneyIcon, data.PopulationIcon, data.SatisfactionHighIcon, data.SatisfactionLowIcon };
            check("arayüz: üst bar ve para görselleri var", statIcons.All(path =>
                !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(ArtResourcesRoot, path + ".png"))));
            check("arayüz: on iki ay", data.Months.Count == 12);
            check("arayüz: oyuncunun bayrağı var", data.Flags.Any(flag => flag.Key == data.PlayerCountry));
            check("arayüz: her ülkenin bayrağı var", data.Countries.All(country => data.Flags.Any(flag => flag.Key == country.Key)));
            check("arayüz: bayrak iki kez tanımlanmaz", data.Flags.Select(flag => flag.Key).Distinct().Count() == data.Flags.Count);
            check("arayüz: bayrak ölçüleri aynı", data.Flags.All(flag => flag.Width == 30 && flag.Height == 20));
            check("arayüz: bayrak yolları okunur", data.Flags.All(flag => flag.Layers.All(layer => SvgPathParser.Parse(layer.Path).Count > 0)));
            check("arayüz: bayrak renkleri geçerli", data.Flags.All(flag => flag.Layers.All(layer => layer.Fill.Length == HexColorLength && layer.Fill[0] == '#')));
            check("arayüz: bayrak katmanları sınır içinde", data.Flags.All(flag => flag.Layers.All(layer => FitsFlag(layer.Path, flag))));
            check("arayüz: her ülkenin başkenti var", data.Countries.All(country => !string.IsNullOrEmpty(country.Capital)));
            check("arayüz: oyuncunun başkenti var", data.Countries.Any(country => country.Key == data.PlayerCountry && !string.IsNullOrEmpty(country.Capital)));
        }

        private static void ClockSplitsDaysAcrossCountries(Action<string, bool> check, EconomyDefinition definition)
        {
            WorldState world = WorldGenerator.Create(definition, new WorldGeneratorSettings { CountryCount = 10, CityCount = 40, FacilitiesPerCity = 1, Seed = 5 });
            float secondsPerDay = definition.Balance.SecondsPerDay;
            var clock = new SimulationClock(secondsPerDay) { Speed = 1 };

            check("saat: bir gün 5 saniye", secondsPerDay == 5f);
            check("saat: başta çubuk boş", Math.Abs(clock.DayProgress(world)) < Tolerance);
            check("saat: yarım günde gün bitmez", clock.Advance(definition, world, secondsPerDay * 0.5f) == 0 && world.Day == 0);
            check("saat: yarım günde çubuk yarıda", Math.Abs(clock.DayProgress(world) - 0.5f) < Tolerance);
            check("saat: kalan yarımda gün biter", clock.Advance(definition, world, secondsPerDay * 0.5f) == 1 && world.Day == 1);
            check("saat: yeni günde çubuk sıfırlanır", clock.DayProgress(world) < Tolerance);

            clock.Speed = 0;
            float paused = clock.DayProgress(world);
            check("saat: durunca ilerlemez", clock.Advance(definition, world, secondsPerDay) == 0 && world.Day == 1);
            check("saat: durunca çubuk durur", clock.DayProgress(world) == paused);

            clock.Speed = 3;
            check("saat: 3x hızda üç kat hızlı", clock.Advance(definition, world, secondsPerDay) == 3 && world.Day == 4);

            WorldState whole = WorldGenerator.Create(definition, new WorldGeneratorSettings { CountryCount = 10, CityCount = 40, FacilitiesPerCity = 1, Seed = 5 });
            WorldState split = WorldGenerator.Create(definition, new WorldGeneratorSettings { CountryCount = 10, CityCount = 40, FacilitiesPerCity = 1, Seed = 5 });
            for (int day = 0; day < 20; day++)
            {
                EconomySimulator.AdvanceDay(definition, whole);
            }

            // Her gün iki kareye bölünür. Yarım saniye ikilik tabanda tam yazıldığı için bütçe yuvarlanmaz.
            var splitClock = new SimulationClock(1f) { Speed = 1 };
            for (int step = 0; step < 40; step++)
            {
                splitClock.Advance(definition, split, 0.5f);
            }

            check("saat: dağıtmak sonucu değiştirmez", split.Day == 20 && split.Checksum() == whole.Checksum());
        }

        private static void CityListSetsWorldSize(Action<string, bool> check, EconomyDefinition definition)
        {
            var settings = new WorldGeneratorSettings { CitiesPerCountry = new[] { 3, 81, 5 }, FacilitiesPerCity = 2, Seed = 9 };
            WorldState world = WorldGenerator.Create(definition, settings);
            check("dünya: şehir listesi ülke sayısını belirler", world.BaseCountryCount == 3 && world.CityCount == 89);
            check("dünya: ülkelerin şehirleri listeye uyar", world.CountryCityStart[1] == 3 && world.CountryCityStart[2] == 84 && world.CountryCityStart[3] == 89);
            check("dünya: slotlar şehirlerle gelir", world.CountryFacilityStart[1] == world.CitySlotStart[3] && world.CountryFacilityStart[3] == world.FacilityCount);
            check("dünya: her şehre iki tesis", world.CountBuiltFacilities() == 178);
        }

        private static bool FitsFlag(string path, FlagData flag)
        {
            return SvgPathParser.Parse(path).All(command =>
                command.X >= -Tolerance && command.X <= flag.Width + Tolerance && command.Y >= -Tolerance && command.Y <= flag.Height + Tolerance);
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < Tolerance;
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            return (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }

        private static UiData Load()
        {
            string directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, UiFile);
                if (File.Exists(candidate))
                {
                    return JsonSerializer.Deserialize<UiData>(File.ReadAllText(candidate), new JsonSerializerOptions { IncludeFields = true });
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new FileNotFoundException(UiFile);
        }
    }
}
