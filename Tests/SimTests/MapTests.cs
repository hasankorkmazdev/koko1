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

        public static void Run(Action<string, bool> check)
        {
            ConcavePolygonIsCoveredExactly(check);
            RegionFileFindsKnownPlaces(check);
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
