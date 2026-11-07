using System;
using System.Collections.Generic;
using System.IO;
using GeoWar.Game;
using GeoWar.Map;

namespace GeoWar.Tests
{
    public static class MapTests
    {
        private const string RegionFile = "Assets/Data/Resources/map_region.bytes";
        private const string UiFile = "Assets/Data/Resources/ui.json";
        private const float LonScale = 30f;
        private const float LatScale = 38.6f;
        private const int AzerbaijanRegions = 14;

        public static void Run(Action<string, bool> check)
        {
            ConcavePolygonIsCoveredExactly(check);
            RingUnionKeepsOnlyTheOuterBorder(check);
            LandBeyondAnEdgeIsNotSea(check);
            RegionFileFindsKnownPlaces(check);
        }

        private static void RingUnionKeepsOnlyTheOuterBorder(Action<string, bool> check)
        {
            double[][] left = Square(0, 0, 2);
            double[][] right = Reversed(Square(2, 0, 2));
            double[][] island = Square(10, 10, 1);
            double[][] enclave = Square(0.5, 0.5, 1);

            List<double[][]> merged = RingUnion.Merge(new List<double[][]> { left, right });
            check("birleşme: bitişik iki kare tek halka olur", merged.Count == 1);
            check("birleşme: alan toplanır", merged.Count == 1 && Math.Abs(Area(merged[0]) - 8) < 1e-9);

            check("birleşme: uzaktaki ada ayrı kalır", RingUnion.Merge(new List<double[][]> { left, island }).Count == 2);

            List<double[][]> withEnclave = RingUnion.Merge(new List<double[][]> { left, enclave });
            check("birleşme: içerideki parça düşer", withEnclave.Count == 1 && Math.Abs(Area(withEnclave[0]) - 4) < 1e-9);

            // Ortası boş dört kare: dış sınır kalır, ortadaki delik atılır.
            var ring = new List<double[][]>
            {
                Square(0, 0, 1), Square(1, 0, 1), Square(2, 0, 1),
                Square(0, 1, 1), Square(2, 1, 1),
                Square(0, 2, 1), Square(1, 2, 1), Square(2, 2, 1)
            };
            List<double[][]> withHole = RingUnion.Merge(ring);
            check("birleşme: oluşan delik atılır", withHole.Count == 1 && Math.Abs(Area(withHole[0]) - 9) < 1e-9);
        }

        private static void LandBeyondAnEdgeIsNotSea(Action<string, bool> check)
        {
            // Halkalar: 0 il, 1 yanındaki arka plan ülkesi, 2 uzaktaki il.
            float[] x = { 0, 2, 2, 0, 2.5f, 4.5f, 4.5f, 2.5f, -2.5f, -0.5f, -0.5f, -2.5f };
            float[] y = { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2 };
            int[] ringStart = { 0, 4, 8, 12 };
            var probe = new LandProbe(x, y, ringStart, new[] { false, true, false });
            const float distance = 1f;

            check("kıyı: yandaki arka plan karası sınırdır", probe.HasLandBeyond(1, 2, 0, distance));
            check("kıyı: dışarıda kara yoksa denizdir", !probe.HasLandBeyond(2, 3, 0, distance));
            check("kıyı: seçilebilir ilin karşı kıyısı denizdir", !probe.HasLandBeyond(3, 0, 0, distance));
            check("kıyı: arka plan kenarı da ilin yanında sınırdır", probe.HasLandBeyond(7, 4, 1, distance));
        }

        private static double[][] Square(double x, double y, double size)
        {
            return new[]
            {
                new[] { x, y },
                new[] { x + size, y },
                new[] { x + size, y + size },
                new[] { x, y + size }
            };
        }

        private static double[][] Reversed(double[][] ring)
        {
            var reversed = (double[][])ring.Clone();
            Array.Reverse(reversed);
            return reversed;
        }

        private static double Area(double[][] ring)
        {
            double sum = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                double[] a = ring[i];
                double[] b = ring[(i + 1) % ring.Length];
                sum += a[0] * b[1] - b[0] * a[1];
            }

            return Math.Abs(sum) / 2;
        }

        private static void ConcavePolygonIsCoveredExactly(Action<string, bool> check)
        {
            // L biçimli çokgen: 3 birim alan, saat yönünde verilmiş.
            double[] x = { 0, 0, 2, 2, 1, 1 };
            double[] y = { 0, 2, 2, 1, 1, 0 };
            var triangles = new List<int>();

            bool clean = PolygonTriangulator.Triangulate(x, y, 0, x.Length, 0, triangles);

            double area = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                area += Math.Abs((x[b] - x[a]) * (y[c] - y[a]) - (y[b] - y[a]) * (x[c] - x[a])) / 2;
            }

            check("üçgenleme: temiz kesilir", clean);
            check("üçgenleme: en fazla 4 üçgen", triangles.Count > 0 && triangles.Count <= 12);
            check("üçgenleme: alan korunur", Math.Abs(area - 3) < 1e-6);
        }

        private static void RegionFileFindsKnownPlaces(Action<string, bool> check)
        {
            MapData map = Load();

            check("harita: 81 Türkiye ili", CountProvinces(map, "TR") == 81);
            check("harita: Eskişehir bulunur", NameAt(map, 30.5f, 39.78f) == "Eskişehir");
            check("harita: Ankara bulunur", NameAt(map, 32.85f, 39.93f) == "Ankara");
            check("harita: Trabzon bulunur", NameAt(map, 39.72f, 40.95f) == "Trabzon");
            check("harita: Tahran bulunur", NameAt(map, 51.4f, 35.7f).Length > 0);
            check("harita: Karadeniz boş", MapHitTest.FindProvince(map, 35f * LonScale, 43f * LatScale) == MapHitTest.NotFound);
            check("harita: arka plan seçilmez", MapHitTest.FindProvince(map, 10f * LonScale, 50f * LatScale) == MapHitTest.NotFound);
            CapitalsBelongToTheirCountries(check, map);
            CountryBordersComeFromTheMap(check, map);
            check("harita: kıyı kenarları var", map.CoastLines.Length > 0 && map.CoastLines.Length % 2 == 0);
            check("harita: kıyı il sınırlarından az", map.CoastLines.Length < (map.ProvinceBorderLines.Length + map.CountryBorderLines.Length) * 4);
            SlicesJoinTheirNeighbors(check, map);
            EveryCityTouchesANeighborOrTheSea(check, map);
            OnlyRealCoastsCount(check, map);
        }

        private static void SlicesJoinTheirNeighbors(Action<string, bool> check, MapData map)
        {
            check("harita: Azerbaycan 14 ekonomik bölge", CountProvinces(map, "AZ") == AzerbaijanRegions);

            foreach (string gone in new[] { "UNDOF", "Rif Şam", "Kuneytire", "Mingeçevir", "Hankendi", "Naftalan", "Şəki", "Hocalı", "Goranboy", "Lankaran", "Abşeron" })
            {
                check("harita: " + gone + " ayrı il değil", Array.IndexOf(map.ProvinceNames, gone) < 0);
            }

            check("harita: Şam kendi yerinde", NameAt(map, 36.29f, 33.51f) == "Şam");
            check("harita: Şam kırsalı Şam'da", NameAt(map, 37.2f, 33.5f) == "Şam");
            check("harita: Kuneytire bölgesi Dera'da", NameAt(map, 35.91f, 33.05f) == "Dera");
            check("harita: tampon bölge Dera'da", NameAt(map, 35.86f, 33.15f) == "Dera");
            check("harita: Hankendi Berde'de", NameAt(map, 46.77f, 39.83f) == "Berde");
            check("harita: Naftalan Gence'de", NameAt(map, 46.83f, 40.51f) == "Gence");
            check("harita: Mingeçevir Yevlah'ta", NameAt(map, 46.98f, 40.75f) == "Yevlah");
            check("harita: Astara Lenkeran'da", NameAt(map, 48.70f, 38.49f) == "Lenkeran");
            check("harita: Şerur Nahçıvan'da", NameAt(map, 45.02f, 39.59f) == "Nahçıvan");
            check("harita: Şeki tek il", Array.FindAll(map.ProvinceNames, name => name == "Şeki").Length == 1);
        }

        // Komşusuyla kenar paylaşmayan ve denize de değmeyen il, başka bir ilin içinde kalmış demektir.
        private static void EveryCityTouchesANeighborOrTheSea(Action<string, bool> check, MapData map)
        {
            var edgeOwners = new Dictionary<(float, float, float, float), int>();
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] == MapData.NoCountry)
                {
                    continue;
                }

                for (int ring = map.ProvinceRingStart[province]; ring < map.ProvinceRingStart[province + 1]; ring++)
                {
                    int start = map.RingStart[ring];
                    int end = map.RingStart[ring + 1];
                    for (int i = start, j = end - 1; i < end; j = i, i++)
                    {
                        (float, float, float, float) key = EdgeKey(map, i, j);
                        edgeOwners.TryGetValue(key, out int owners);
                        edgeOwners[key] = owners + 1;
                    }
                }
            }

            var enclosed = new List<string>();
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] == MapData.NoCountry || IsCoastal(map, province))
                {
                    continue;
                }

                bool touches = false;
                for (int ring = map.ProvinceRingStart[province]; ring < map.ProvinceRingStart[province + 1]; ring++)
                {
                    int start = map.RingStart[ring];
                    int end = map.RingStart[ring + 1];
                    for (int i = start, j = end - 1; i < end; j = i, i++)
                    {
                        touches |= edgeOwners[EdgeKey(map, i, j)] > 1;
                    }
                }

                if (!touches)
                {
                    enclosed.Add(map.ProvinceNames[province]);
                }
            }

            check("harita: başka ilin içinde kalan il yok (" + string.Join(",", enclosed) + ")", enclosed.Count == 0);
        }

        private static void OnlyRealCoastsCount(Action<string, bool> check, MapData map)
        {
            var expected = new Dictionary<string, string[]>
            {
                { "SY", new[] { "Lazkiye", "Tartus" } },
                { "IQ", new[] { "Basra" } },
                { "BG", new[] { "Burgaz", "Dobriç", "Varna" } },
                { "GE", new[] { "Abhazya", "Acara", "Guria", "Samegrelo-Zemo Svaneti" } },
                { "IR", new[] { "Buşehr", "Gülistan", "Mazenderan", "Huzistan", "Hürmüzgan", "Gilan", "Sistan ve Belucistan" } },
                { "AZ", new[] { "Bakü", "Sumgayıt", "Lenkeran", "Salyan", "Haçmaz" } },
                { "AM", new string[0] }
            };

            foreach (KeyValuePair<string, string[]> pair in expected)
            {
                List<string> found = CoastalCities(map, pair.Key);
                bool same = found.Count == pair.Value.Length && Array.TrueForAll(pair.Value, found.Contains);
                check("kıyı: " + pair.Key + " (" + string.Join(",", found) + ")", same);
            }

            check("kıyı: Türkiye'nin 28 kıyı ili", CoastalCities(map, "TR").Count == 28);
            check("kıyı: Batı Makedonya karada", !CoastalCities(map, "GR").Contains("Batı Makedonya"));
        }

        private static List<string> CoastalCities(MapData map, string countryKey)
        {
            int country = Array.IndexOf(map.Countries, countryKey);
            var names = new List<string>();
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] == country && IsCoastal(map, province))
                {
                    names.Add(map.ProvinceNames[province]);
                }
            }

            return names;
        }

        private static bool IsCoastal(MapData map, int province)
        {
            for (int i = 0; i < map.CoastLines.Length; i += 2)
            {
                if (map.CoastLines[i] >= map.VertexStart[province] && map.CoastLines[i] < map.VertexStart[province + 1])
                {
                    return true;
                }
            }

            return false;
        }

        private static (float, float, float, float) EdgeKey(MapData map, int a, int b)
        {
            float ax = map.VertexX[a];
            float ay = map.VertexY[a];
            float bx = map.VertexX[b];
            float by = map.VertexY[b];
            bool swap = ax > bx || (ax == bx && ay > by);
            return swap ? (bx, by, ax, ay) : (ax, ay, bx, by);
        }

        private static void CountryBordersComeFromTheMap(Action<string, bool> check, MapData map)
        {
            bool[] neighbors = MapRoutes.CountryNeighbors(map);
            var expected = new Dictionary<string, string[]>
            {
                { "TR", new[] { "GR", "BG", "GE", "AM", "AZ", "IR", "IQ", "SY" } },
                { "GR", new[] { "BG", "TR" } },
                { "BG", new[] { "GR", "TR" } },
                { "GE", new[] { "TR", "AM", "AZ" } },
                { "AM", new[] { "TR", "GE", "AZ", "IR" } },
                { "AZ", new[] { "TR", "GE", "AM", "IR" } },
                { "IR", new[] { "TR", "AM", "AZ", "IQ" } },
                { "IQ", new[] { "TR", "IR", "SY" } },
                { "SY", new[] { "TR", "IQ" } }
            };

            int count = map.Countries.Length;
            foreach (KeyValuePair<string, string[]> pair in expected)
            {
                int country = Array.IndexOf(map.Countries, pair.Key);
                var found = new List<string>();
                for (int other = 0; other < count; other++)
                {
                    if (neighbors[country * count + other])
                    {
                        found.Add(map.Countries[other]);
                    }
                }

                bool same = found.Count == pair.Value.Length && Array.TrueForAll(pair.Value, found.Contains);
                check("sınır: " + pair.Key + " komşuları (" + string.Join(",", found) + ")", same);
            }
        }

        // Haritadaki her ülkenin başkenti verilmiş olmalı ve o ülkenin bir ili olmalı.
        private static void CapitalsBelongToTheirCountries(Action<string, bool> check, MapData map)
        {
            string directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(directory, UiFile)))
            {
                directory = Path.GetDirectoryName(directory);
            }

            string json = File.ReadAllText(Path.Combine(directory, UiFile));
            UiData ui = System.Text.Json.JsonSerializer.Deserialize<UiData>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });

            foreach (string countryKey in map.Countries)
            {
                CountryViewData country = ui.Countries.Find(item => item.Key == countryKey);
                int province = country == null ? -1 : Array.IndexOf(map.ProvinceIds, country.Capital);
                bool valid = province >= 0 && map.ProvinceCountry[province] == Array.IndexOf(map.Countries, countryKey);
                check("başkent: " + countryKey + " kendi ilinde", valid);
            }
        }

        private static MapData Load()
        {
            string directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, RegionFile);
                if (File.Exists(candidate))
                {
                    using (FileStream stream = File.OpenRead(candidate))
                    {
                        return MapData.ReadFrom(stream);
                    }
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new FileNotFoundException(RegionFile);
        }

        private static int CountProvinces(MapData map, string country)
        {
            int index = Array.IndexOf(map.Countries, country);
            int count = 0;
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] == index)
                {
                    count++;
                }
            }

            return count;
        }

        private static string NameAt(MapData map, float lon, float lat)
        {
            int province = MapHitTest.FindProvince(map, lon * LonScale, lat * LatScale);
            return province == MapHitTest.NotFound ? "" : map.ProvinceNames[province];
        }
    }
}
