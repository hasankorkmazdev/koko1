using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using GeoWar.Map;

namespace GeoWar.Tools
{
    // Natural Earth admin-1 GeoJSON dosyasından çizime hazır harita dosyası üretir.
    // Kullanım: dotnet run --project Tools/MapBuilder -c Release -- <geojson> <çıktı.bytes> region|world
    public static class Program
    {
        private const float LonScale = 30f;
        private const float LatScale = 38.6f;
        private const double FullDetail = 0;
        private const double CountryGrid = 0.02;
        private const double FarGrid = 0.04;
        private const double MinIslandArea = 0.004;
        private const double MinBackgroundArea = 0.03;
        private const int MinRingPoints = 4;
        private const double CoverageTolerance = 0.005;
        private const int TurkeyTones = 4;
        private const int NeighborTones = 2;
        private const double RegionMinLon = 15;
        private const double RegionMaxLon = 60;
        private const double RegionMinLat = 20;
        private const double RegionMaxLat = 56;

        private static readonly string[] RegionCountries = { "TR", "GR", "BG", "GE", "AM", "AZ", "IR", "IQ", "SY" };
        private static readonly Dictionary<string, string> NameFixes = new Dictionary<string, string>
        {
            { "Hakkâri", "Hakkari" },
            { "Elâzığ", "Elazığ" }
        };

        private static readonly string[] NameSuffixes = { " ili", " İli", " Rayonu", " Eyaleti", " İdari Bölgesi", " Özerk Cumhuriyeti" };

        private sealed class Province
        {
            public string Id;
            public string Name;
            public string Country;
            public double CenterLon;
            public double CenterLat;
            public List<double[][]> Rings = new List<double[][]>();
            public int Tone;
        }

        public static int Main(string[] args)
        {
            if (args.Length != 3 || (args[2] != "region" && args[2] != "world"))
            {
                Console.WriteLine("Kullanım: <geojson> <çıktı.bytes> region|world");
                return 2;
            }

            var clock = Stopwatch.StartNew();
            bool isRegion = args[2] == "region";
            List<Province> provinces = LoadProvinces(args[0], isRegion);
            Console.WriteLine("Okunan il: " + provinces.Count + " (" + clock.ElapsedMilliseconds + " ms)");

            AssignTones(provinces);
            clock.Restart();
            MapData map = Build(provinces, out int failedRings);
            Console.WriteLine("Üçgenleme: " + clock.ElapsedMilliseconds + " ms, temiz kesilemeyen halka: " + failedRings);
            ReportCoverage(map);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
            using (FileStream stream = File.Create(args[1]))
            {
                map.WriteTo(stream);
            }

            Console.WriteLine("İl: " + map.ProvinceCount);
            Console.WriteLine("Köşe: " + map.VertexX.Length);
            Console.WriteLine("Üçgen: " + map.Triangles.Length / 3);
            Console.WriteLine("İl sınırı kenarı: " + map.ProvinceBorderLines.Length / 2);
            Console.WriteLine("Ülke sınırı kenarı: " + map.CountryBorderLines.Length / 2);
            Console.WriteLine("Kıyı kenarı: " + map.CoastLines.Length / 2);
            Console.WriteLine("Dosya: " + new FileInfo(args[1]).Length / 1024 + " KB");
            return 0;
        }

        // Üçgenlerin toplam alanı halkaların alanıyla eşleşmeli; fark varsa harita delikli veya taşkın çizilir.
        private static void ReportCoverage(MapData map)
        {
            double worstError = 0;
            string worstName = "";
            int badProvinces = 0;

            for (int province = 0; province < map.ProvinceCount; province++)
            {
                double ringArea = 0;
                for (int ring = map.ProvinceRingStart[province]; ring < map.ProvinceRingStart[province + 1]; ring++)
                {
                    double sum = 0;
                    int start = map.RingStart[ring];
                    int end = map.RingStart[ring + 1];
                    for (int i = start, j = end - 1; i < end; j = i, i++)
                    {
                        sum += (double)map.VertexX[j] * map.VertexY[i] - (double)map.VertexX[i] * map.VertexY[j];
                    }

                    ringArea += Math.Abs(sum) / 2;
                }

                double triangleArea = 0;
                for (int t = map.TriangleStart[province]; t < map.TriangleStart[province + 1]; t += 3)
                {
                    int a = map.Triangles[t];
                    int b = map.Triangles[t + 1];
                    int c = map.Triangles[t + 2];
                    triangleArea += Math.Abs(
                        ((double)map.VertexX[b] - map.VertexX[a]) * ((double)map.VertexY[c] - map.VertexY[a])
                        - ((double)map.VertexY[b] - map.VertexY[a]) * ((double)map.VertexX[c] - map.VertexX[a])) / 2;
                }

                double error = ringArea > 0 ? Math.Abs(triangleArea - ringArea) / ringArea : 0;
                if (map.ProvinceCountry[province] != MapData.NoCountry && error > CoverageTolerance)
                {
                    badProvinces++;
                }

                // Arka plan karası sınırsız çizilir, üst üste binmesi görünmez; sadece seçilebilir iller sayılır.
                if (map.ProvinceCountry[province] != MapData.NoCountry && error > worstError)
                {
                    worstError = error;
                    worstName = map.ProvinceNames[province];
                }
            }

            Console.WriteLine("Alan kontrolü: sınırı aşan " + badProvinces + " il, en kötü %" + (worstError * 100).ToString("F3") + " (" + worstName + ")");
        }

        private static List<Province> LoadProvinces(string path, bool isRegion)
        {
            var countries = new HashSet<string>(RegionCountries);
            var provinces = new List<Province>();
            var background = new Province { Id = "", Name = "", Country = "" };

            using (FileStream stream = File.OpenRead(path))
            using (JsonDocument document = JsonDocument.Parse(stream))
            {
                foreach (JsonElement feature in document.RootElement.GetProperty("features").EnumerateArray())
                {
                    JsonElement properties = feature.GetProperty("properties");
                    string iso = Text(properties, "iso_a2");
                    string admin = Text(properties, "admin");
                    string countryKey = iso == "-1" || iso == "-99" || iso.Length == 0 ? admin : iso;
                    bool isSelectable = !isRegion || countries.Contains(iso);

                    if (!isSelectable)
                    {
                        AddBackgroundRings(feature.GetProperty("geometry"), background, admin);
                        continue;
                    }

                    var province = new Province
                    {
                        Id = iso == "TR" ? Text(properties, "iso_3166_2") : Text(properties, "adm1_code"),
                        Name = CleanName(FirstText(properties, "name_tr", "name")),
                        Country = countryKey,
                        CenterLon = properties.GetProperty("longitude").GetDouble(),
                        CenterLat = properties.GetProperty("latitude").GetDouble()
                    };

                    // Bölge haritası zaten küçük, sınırlar bozulmasın diye ham detayla kalır.
                    double grid = isRegion ? FullDetail : CountryGrid;
                    foreach (double[][] ring in OuterRings(feature.GetProperty("geometry")))
                    {
                        double[][] snapped = Snap(ring, grid);
                        if (snapped.Length >= MinRingPoints)
                        {
                            province.Rings.Add(snapped);
                        }
                    }

                    DropTinyIslands(province, iso == "TR");
                    if (province.Rings.Count > 0)
                    {
                        provinces.Add(province);
                    }
                }
            }

            provinces.Sort((a, b) => string.CompareOrdinal(a.Country + a.Id, b.Country + b.Id));
            if (background.Rings.Count > 0)
            {
                provinces.Add(background);
            }

            return provinces;
        }

        private static void AddBackgroundRings(JsonElement geometry, Province background, string admin)
        {
            foreach (double[][] ring in OuterRings(geometry))
            {
                if (!TouchesRegion(ring))
                {
                    continue;
                }

                double[][] snapped = Snap(ring, FarGrid);
                if (snapped.Length >= MinRingPoints && Math.Abs(Area(snapped)) >= MinBackgroundArea)
                {
                    background.Rings.Add(snapped);
                }
            }
        }

        private static void DropTinyIslands(Province province, bool keepAll)
        {
            if (keepAll || province.Rings.Count < 2)
            {
                return;
            }

            double largest = 0;
            foreach (double[][] ring in province.Rings)
            {
                largest = Math.Max(largest, Math.Abs(Area(ring)));
            }

            double limit = Math.Min(MinIslandArea, largest);
            province.Rings.RemoveAll(ring => Math.Abs(Area(ring)) < limit);
        }

        private static bool TouchesRegion(double[][] ring)
        {
            foreach (double[] point in ring)
            {
                if (point[0] >= RegionMinLon && point[0] <= RegionMaxLon && point[1] >= RegionMinLat && point[1] <= RegionMaxLat)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<double[][]> OuterRings(JsonElement geometry)
        {
            string type = geometry.GetProperty("type").GetString();
            JsonElement coordinates = geometry.GetProperty("coordinates");
            if (type == "Polygon")
            {
                yield return ReadRing(coordinates[0]);
            }
            else if (type == "MultiPolygon")
            {
                foreach (JsonElement polygon in coordinates.EnumerateArray())
                {
                    yield return ReadRing(polygon[0]);
                }
            }
        }

        private static double[][] ReadRing(JsonElement ring)
        {
            var points = new List<double[]>();
            foreach (JsonElement point in ring.EnumerateArray())
            {
                points.Add(new[] { point[0].GetDouble(), point[1].GetDouble() });
            }

            // GeoJSON halkası ilk noktayı sonda tekrar eder.
            int last = points.Count - 1;
            if (last > 0 && points[0][0] == points[last][0] && points[0][1] == points[last][1])
            {
                points.RemoveAt(last);
            }

            return points.ToArray();
        }

        private static double[][] Snap(double[][] ring, double grid)
        {
            if (grid <= 0)
            {
                return ring;
            }

            var points = new List<double[]>(ring.Length);
            foreach (double[] point in ring)
            {
                double x = Math.Round(point[0] / grid) * grid;
                double y = Math.Round(point[1] / grid) * grid;
                int count = points.Count;
                if (count > 0 && Same(points[count - 1], x, y))
                {
                    continue;
                }

                // Yuvarlama A-B-A sivriliği üretirse B atılır.
                if (count > 1 && Same(points[count - 2], x, y))
                {
                    points.RemoveAt(count - 1);
                    continue;
                }

                points.Add(new[] { x, y });
            }

            while (points.Count > 2)
            {
                int last = points.Count - 1;
                if (Same(points[0], points[last][0], points[last][1]))
                {
                    points.RemoveAt(last);
                }
                else if (Same(points[1], points[last][0], points[last][1]))
                {
                    points.RemoveAt(last);
                    points.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            return points.ToArray();
        }

        private static bool Same(double[] point, double x, double y)
        {
            return point[0] == x && point[1] == y;
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

            return sum / 2;
        }

        // Komşu iller farklı tonda olsun diye paylaşılan köşelerden komşuluk çıkarılır.
        private static void AssignTones(List<Province> provinces)
        {
            var byCountry = new Dictionary<string, List<Province>>();
            foreach (Province province in provinces)
            {
                if (!byCountry.TryGetValue(province.Country, out List<Province> list))
                {
                    list = new List<Province>();
                    byCountry.Add(province.Country, list);
                }

                list.Add(province);
            }

            foreach (KeyValuePair<string, List<Province>> pair in byCountry)
            {
                AssignCountryTones(pair.Value, pair.Key == "TR" ? TurkeyTones : NeighborTones);
            }
        }

        private static void AssignCountryTones(List<Province> provinces, int toneCount)
        {
            var owners = new Dictionary<(double, double), List<int>>();
            for (int i = 0; i < provinces.Count; i++)
            {
                foreach (double[][] ring in provinces[i].Rings)
                {
                    foreach (double[] point in ring)
                    {
                        var key = (point[0], point[1]);
                        if (!owners.TryGetValue(key, out List<int> list))
                        {
                            list = new List<int>(2);
                            owners.Add(key, list);
                        }

                        if (!list.Contains(i))
                        {
                            list.Add(i);
                        }
                    }
                }
            }

            var neighbors = new HashSet<int>[provinces.Count];
            for (int i = 0; i < provinces.Count; i++)
            {
                neighbors[i] = new HashSet<int>();
            }

            foreach (List<int> list in owners.Values)
            {
                foreach (int a in list)
                {
                    foreach (int b in list)
                    {
                        if (a != b)
                        {
                            neighbors[a].Add(b);
                        }
                    }
                }
            }

            var order = new List<int>();
            for (int i = 0; i < provinces.Count; i++)
            {
                order.Add(i);
                provinces[i].Tone = -1;
            }

            order.Sort((a, b) => neighbors[b].Count != neighbors[a].Count ? neighbors[b].Count - neighbors[a].Count : a - b);
            var counts = new int[toneCount];
            foreach (int index in order)
            {
                Array.Clear(counts, 0, toneCount);
                foreach (int other in neighbors[index])
                {
                    if (provinces[other].Tone >= 0)
                    {
                        counts[provinces[other].Tone]++;
                    }
                }

                int best = 0;
                for (int tone = 1; tone < toneCount; tone++)
                {
                    if (counts[tone] < counts[best])
                    {
                        best = tone;
                    }
                }

                provinces[index].Tone = best;
            }
        }

        private static MapData Build(List<Province> provinces, out int failedRings)
        {
            failedRings = 0;
            var countries = new List<string>();
            var map = new MapData
            {
                ProvinceCount = provinces.Count,
                ProvinceIds = new string[provinces.Count],
                ProvinceNames = new string[provinces.Count],
                ProvinceCountry = new byte[provinces.Count],
                ProvinceTone = new byte[provinces.Count],
                CenterX = new float[provinces.Count],
                CenterY = new float[provinces.Count],
                MinX = new float[provinces.Count],
                MinY = new float[provinces.Count],
                MaxX = new float[provinces.Count],
                MaxY = new float[provinces.Count],
                VertexStart = new int[provinces.Count + 1],
                TriangleStart = new int[provinces.Count + 1],
                ProvinceRingStart = new int[provinces.Count + 1]
            };

            int totalPoints = 0;
            int totalRings = 0;
            foreach (Province province in provinces)
            {
                totalRings += province.Rings.Count;
                foreach (double[][] ring in province.Rings)
                {
                    totalPoints += ring.Length;
                }
            }

            map.VertexX = new float[totalPoints];
            map.VertexY = new float[totalPoints];
            map.RingStart = new int[totalRings + 1];
            var triangles = new List<int>(totalPoints * 3);
            var provinceLines = new List<int>();
            var countryLines = new List<int>();
            var seenEdges = new Dictionary<(float, float, float, float), OpenEdge>();
            var coastEdges = new Dictionary<(float, float, float, float), OpenEdge>();

            int nextVertex = 0;
            int nextRing = 0;
            for (int index = 0; index < provinces.Count; index++)
            {
                Province province = provinces[index];
                bool isBackground = province.Id.Length == 0;
                map.ProvinceIds[index] = province.Id;
                map.ProvinceNames[index] = province.Name;
                map.ProvinceTone[index] = (byte)Math.Max(0, province.Tone);
                map.ProvinceCountry[index] = isBackground ? MapData.NoCountry : CountryIndex(countries, province.Country);
                map.CenterX[index] = (float)province.CenterLon * LonScale;
                map.CenterY[index] = (float)province.CenterLat * LatScale;
                map.VertexStart[index] = nextVertex;
                map.TriangleStart[index] = triangles.Count;
                map.ProvinceRingStart[index] = nextRing;

                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                foreach (double[][] ring in province.Rings)
                {
                    map.RingStart[nextRing++] = nextVertex;
                    int ringStart = nextVertex;
                    var ringX = new double[ring.Length];
                    var ringY = new double[ring.Length];
                    for (int i = 0; i < ring.Length; i++)
                    {
                        ringX[i] = ring[i][0] * LonScale;
                        ringY[i] = ring[i][1] * LatScale;
                        float x = (float)ringX[i];
                        float y = (float)ringY[i];
                        map.VertexX[nextVertex] = x;
                        map.VertexY[nextVertex] = y;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                        nextVertex++;
                    }

                    if (!PolygonTriangulator.Triangulate(ringX, ringY, 0, ring.Length, ringStart, triangles))
                    {
                        failedRings++;
                    }

                    if (!isBackground)
                    {
                        AddEdges(map, ringStart, ring.Length, index, seenEdges, provinceLines, countryLines);
                    }

                    AddCoastEdges(map, ringStart, ring.Length, coastEdges);
                }

                map.MinX[index] = minX;
                map.MinY[index] = minY;
                map.MaxX[index] = maxX;
                map.MaxY[index] = maxY;
            }

            map.VertexStart[provinces.Count] = nextVertex;
            map.TriangleStart[provinces.Count] = triangles.Count;
            map.ProvinceRingStart[provinces.Count] = nextRing;
            map.RingStart[nextRing] = nextVertex;
            map.Triangles = triangles.ToArray();
            map.Countries = countries.ToArray();

            // Tek ile ait kenar kıyı veya ülke sınırıdır.
            foreach (OpenEdge edge in seenEdges.Values)
            {
                if (!edge.Matched)
                {
                    countryLines.Add(edge.A);
                    countryLines.Add(edge.B);
                }
            }

            var coastLines = new List<int>();
            foreach (OpenEdge edge in coastEdges.Values)
            {
                if (!edge.Matched)
                {
                    coastLines.Add(edge.A);
                    coastLines.Add(edge.B);
                }
            }

            map.ProvinceBorderLines = provinceLines.ToArray();
            map.CountryBorderLines = countryLines.ToArray();
            map.CoastLines = coastLines.ToArray();
            return map;
        }

        // Tek halkaya ait kenar kıyıdır; iki halkanın paylaştığı kenar karanın içinde kalır.
        private static void AddCoastEdges(
            MapData map,
            int start,
            int count,
            Dictionary<(float, float, float, float), OpenEdge> coastEdges)
        {
            for (int i = 0; i < count; i++)
            {
                int a = start + i;
                int b = start + (i + 1) % count;
                (float, float, float, float) key = EdgeKey(map.VertexX[a], map.VertexY[a], map.VertexX[b], map.VertexY[b]);
                if (coastEdges.TryGetValue(key, out OpenEdge edge))
                {
                    edge.Matched = true;
                }
                else
                {
                    coastEdges.Add(key, new OpenEdge { A = a, B = b });
                }
            }
        }

        private sealed class OpenEdge
        {
            public int A;
            public int B;
            public byte Country;
            public bool Matched;
        }

        // İki ilin ortak kenarı bir kez çizilir; ülkeleri aynıysa il sınırı, farklıysa ülke sınırıdır.
        private static void AddEdges(
            MapData map,
            int start,
            int count,
            int province,
            Dictionary<(float, float, float, float), OpenEdge> seenEdges,
            List<int> provinceLines,
            List<int> countryLines)
        {
            byte country = map.ProvinceCountry[province];
            for (int i = 0; i < count; i++)
            {
                int a = start + i;
                int b = start + (i + 1) % count;
                (float, float, float, float) key = EdgeKey(map.VertexX[a], map.VertexY[a], map.VertexX[b], map.VertexY[b]);

                if (seenEdges.TryGetValue(key, out OpenEdge edge))
                {
                    if (!edge.Matched)
                    {
                        edge.Matched = true;
                        List<int> target = edge.Country == country ? provinceLines : countryLines;
                        target.Add(a);
                        target.Add(b);
                    }
                }
                else
                {
                    seenEdges.Add(key, new OpenEdge { A = a, B = b, Country = country });
                }
            }
        }

        private static (float, float, float, float) EdgeKey(float ax, float ay, float bx, float by)
        {
            bool swap = ax > bx || (ax == bx && ay > by);
            return swap ? (bx, by, ax, ay) : (ax, ay, bx, by);
        }

        private static byte CountryIndex(List<string> countries, string country)
        {
            int index = countries.IndexOf(country);
            if (index < 0)
            {
                countries.Add(country);
                index = countries.Count - 1;
            }

            return (byte)index;
        }

        private static string CleanName(string name)
        {
            if (NameFixes.TryGetValue(name, out string fixedName))
            {
                return fixedName;
            }

            foreach (string suffix in NameSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return name.Substring(0, name.Length - suffix.Length);
                }
            }

            return name;
        }

        private static string Text(JsonElement properties, string name)
        {
            return properties.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "";
        }

        private static string FirstText(JsonElement properties, string first, string second)
        {
            string value = Text(properties, first);
            return value.Length > 0 ? value : Text(properties, second);
        }
    }
}
