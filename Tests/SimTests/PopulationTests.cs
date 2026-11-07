using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GeoWar.Map;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class PopulationTests
    {
        private const string PopulationFile = "Assets/Data/Resources/population.json";
        private const string RegionFile = "Assets/Data/Resources/map_region.bytes";
        private const int MapCityCount = 221;
        private const long GreeceCensusTotal = 10482487;
        private const long GreeceSourceRounding = 10;
        private const long IranProjectionTotal = 86736000;
        private const long AzerbaijanTotal = 10424377;

        // Kaynak 31 ili bine yuvarlar: toplam en çok 31 * 500 kişi sapabilir.
        private const long IranSourceRounding = 15500;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            PopulationData data = LoadData();
            MapData map = LoadMap();
            EveryMapCityHasOneRecord(check, data, map);
            KnownCitiesAreExact(check, data);
            CountryTotalsMatchTheirSources(check, data, map);
            EverySourceIsNamed(check, data, map);
            MissingCityIsAnError(check, data);
            GeneratorUsesTheList(check, definition);
            GeneratorStaysRandomWithoutAList(check, definition);
        }

        private static void EveryMapCityHasOneRecord(Action<string, bool> check, PopulationData data, MapData map)
        {
            check("nüfus: haritada " + MapCityCount + " şehir", CountCities(map) == MapCityCount);
            check("nüfus: şehir başına bir kayıt", data.Cities.Count == MapCityCount);

            var ids = new HashSet<string>();
            bool allPositive = true;
            bool allOnMap = true;
            foreach (CityPopulationData city in data.Cities)
            {
                ids.Add(city.Id);
                allPositive &= city.Population > 0;
                allOnMap &= Array.IndexOf(map.ProvinceIds, city.Id) >= 0;
            }

            check("nüfus: kimlikler tekrar etmez", ids.Count == data.Cities.Count);
            check("nüfus: sıfır veya eksi yok", allPositive);
            check("nüfus: her kayıt haritadadır", allOnMap);

            long[] populations = PopulationTable.ForCities(data, map.ProvinceIds, MapCityCount);
            check("nüfus: şehir sırası il sırasıdır", populations[Array.IndexOf(map.ProvinceIds, "TR-34")] == 15754053);
        }

        private static void KnownCitiesAreExact(Action<string, bool> check, PopulationData data)
        {
            check("nüfus: İstanbul 2025", PopulationOf(data, "TR-34") == 15754053);
            check("nüfus: Ankara 2025", PopulationOf(data, "TR-06") == 5910320);
            check("nüfus: İzmir 2025", PopulationOf(data, "TR-35") == 4504185);
            check("nüfus: Bursa 2025", PopulationOf(data, "TR-16") == 3263011);
            check("nüfus: Antalya 2025", PopulationOf(data, "TR-07") == 2777677);
            check("nüfus: Bayburt 2025", PopulationOf(data, "TR-69") == 82836);
            check("nüfus: Tunceli 2025", PopulationOf(data, "TR-62") == 85083);
            check("nüfus: Bağdat 2024", PopulationOf(data, "IRQ-3063") == 9780429);
            check("nüfus: Tahran 2023", PopulationOf(data, "IRN-3216") == 14437000);
            check("nüfus: Halep 2026", PopulationOf(data, "SYR-137") == 5702889);

            long largest = 0;
            string largestId = "";
            foreach (CityPopulationData city in data.Cities)
            {
                if (city.Population > largest)
                {
                    largest = city.Population;
                    largestId = city.Id;
                }
            }

            check("nüfus: haritanın en kalabalığı İstanbul", largestId == "TR-34");
        }

        private static void CountryTotalsMatchTheirSources(Action<string, bool> check, PopulationData data, MapData map)
        {
            check("toplam: Türkiye TÜİK ile aynı", TotalOf(data, map, "TR") == 86092168);
            check("toplam: Bulgaristan", TotalOf(data, map, "BG") == 6423207);
            check("toplam: Ermenistan", TotalOf(data, map, "AM") == 3076200);
            check("toplam: Irak sayımı", TotalOf(data, map, "IQ") == 46118793);
            check("toplam: İran projeksiyonu", Math.Abs(TotalOf(data, map, "IR") - IranProjectionTotal) <= IranSourceRounding);
            check("toplam: Yunanistan sayımı", Math.Abs(TotalOf(data, map, "GR") - GreeceCensusTotal) <= GreeceSourceRounding);
            check("toplam: Gürcistan sayım + Abhazya + Güney Osetya", TotalOf(data, map, "GE") == 3929581 + 244236 + 56520);
            check("toplam: Suriye IOM", TotalOf(data, map, "SY") == 27897517);
            check("toplam: Azerbaycan birleşmeden etkilenmez", TotalOf(data, map, "AZ") == AzerbaijanTotal);
        }

        private static void EverySourceIsNamed(Action<string, bool> check, PopulationData data, MapData map)
        {
            foreach (string country in map.Countries)
            {
                PopulationSourceData source = data.Sources.Find(item => item.Country == country);
                check("kaynak: " + country + " yazılı", source != null && !string.IsNullOrEmpty(source.Source) && !string.IsNullOrEmpty(source.Date));
            }
        }

        private static void MissingCityIsAnError(Action<string, bool> check, PopulationData data)
        {
            bool failed = false;
            try
            {
                PopulationTable.ForCities(data, new[] { "TR-34", "XX-99" }, 2);
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            check("nüfus: eksik şehir hata verir", failed);
        }

        private static void GeneratorUsesTheList(Action<string, bool> check, EconomyDefinition definition)
        {
            var settings = new WorldGeneratorSettings
            {
                CitiesPerCountry = new[] { 2, 1 },
                CityPopulations = new long[] { 15754053, 82836, 1500000 },
                FacilitiesPerCity = 1,
                Seed = 4
            };

            WorldState world = WorldGenerator.Create(definition, settings);

            check("dünya: şehir verilen nüfusla başlar", world.Population[0] == 15754053 && world.Population[1] == 82836 && world.Population[2] == 1500000);
            check("dünya: ülke nüfusu şehirlerin toplamı", world.CountryPopulation[0] == 15754053 + 82836 && world.CountryPopulation[1] == 1500000);
            check("dünya: büyük şehirde çok slot", FacilityCommands.SlotCount(world, 0) == 13);
            check("dünya: küçük şehirde en az slot", FacilityCommands.SlotCount(world, 1) == definition.Balance.MinCitySlots);
            check("dünya: 1,5 milyonda bir slot artar", FacilityCommands.SlotCount(world, 2) == definition.Balance.MinCitySlots + 1);
        }

        private static void GeneratorStaysRandomWithoutAList(Action<string, bool> check, EconomyDefinition definition)
        {
            var settings = new WorldGeneratorSettings { CountryCount = 2, CityCount = 20, FacilitiesPerCity = 1, Seed = 4 };
            WorldState world = WorldGenerator.Create(definition, settings);

            bool inRange = true;
            bool varied = false;
            for (int city = 0; city < world.CityCount; city++)
            {
                inRange &= world.Population[city] >= settings.MinPopulation && world.Population[city] < settings.MaxPopulation;
                varied |= world.Population[city] != world.Population[0];
            }

            check("dünya: liste yoksa nüfus aralıkta rastgele", inRange && varied);
        }

        private static long PopulationOf(PopulationData data, string id)
        {
            CityPopulationData city = data.Cities.Find(item => item.Id == id);
            return city != null ? city.Population : -1;
        }

        private static long TotalOf(PopulationData data, MapData map, string countryKey)
        {
            int country = Array.IndexOf(map.Countries, countryKey);
            long total = 0;
            foreach (CityPopulationData city in data.Cities)
            {
                int province = Array.IndexOf(map.ProvinceIds, city.Id);
                if (province >= 0 && map.ProvinceCountry[province] == country)
                {
                    total += city.Population;
                }
            }

            return total;
        }

        private static int CountCities(MapData map)
        {
            int count = 0;
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] != MapData.NoCountry)
                {
                    count++;
                }
            }

            return count;
        }

        private static PopulationData LoadData()
        {
            string json = File.ReadAllText(FindFile(PopulationFile));
            return JsonSerializer.Deserialize<PopulationData>(json, new JsonSerializerOptions { IncludeFields = true });
        }

        private static MapData LoadMap()
        {
            using (FileStream stream = File.OpenRead(FindFile(RegionFile)))
            {
                return MapData.ReadFrom(stream);
            }
        }

        private static string FindFile(string relativePath)
        {
            string directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new FileNotFoundException(relativePath);
        }
    }
}
