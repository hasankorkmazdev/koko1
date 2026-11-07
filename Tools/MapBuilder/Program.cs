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

        // Arka plan karasının sadeleşince oynadığı mesafeden uzağa bakılır.
        private const double CoastProbeGrids = 2;

        // Harita birimi. Ölçümde en kısa gerçek kıyı 6,5 (Astara), sınırdaki en uzun boşluk 0,42 birim.
        private const double MinCityCoastLength = 1.5;
        private const double MinIslandArea = 0.004;
        private const double MinBackgroundArea = 0.03;
        private const int MinRingPoints = 4;
        private const double CoverageTolerance = 0.005;
        private const int TurkeyTones = 4;
        private const int NeighborTones = 2;
        private const double RegionMinLon = 15;

        // İran'ın en doğu ucu 63,3. Daha azı, sınır komşularını haritadan düşürür ve sınırı deniz gibi gösterir.
        private const double RegionMaxLon = 64;
        private const double RegionMinLat = 20;
        private const double RegionMaxLat = 56;

        private static readonly string[] RegionCountries = { "TR", "GR", "BG", "GE", "AM", "AZ", "IR", "IQ", "SY" };
        private static readonly Dictionary<string, string> NameFixes = new Dictionary<string, string>
        {
            { "Hakkâri", "Hakkari" },
            { "Elâzığ", "Elazığ" },
            { "Şəki", "Şeki" }
        };

        // Kaynakta ayrı görünen ama oyunda il olmaması gereken parçalar: anahtardaki il, listedekileri içine alır.
        // Hedefin kimliği, adı ve merkezi kalır. Yalnızca ülkenin iç bölünmesi değişir, ülke sınırı aynı kalır.
        // Azerbaycan'ın 73 birimi resmî 14 ekonomik bölgeye iner; hedef, bölgenin bilinen en büyük şehridir.
        private static readonly Dictionary<string, string[]> ProvinceMerges = new Dictionary<string, string[]>
        {
            { "SYR-4843", new[] { "SYR-144" } }, // Şam: Rif Şam
            { "SYR-146", new[] { "SYR-145", "SYR-5261" } }, // Dera: Kuneytire, UNDOF
            { "AZE-1717", new[] { "AZE-1690", "AZE-1719" } }, // Abşeron-Hızı -> Sumgayıt
            { "AZE-1716", new[] { "AZE-1693", "AZE-1705", "AZE-1711" } }, // Dağlık Şirvan -> Şamahı
            { "AZE-1679", new[] { "AZE-1677", "AZE-1680", "AZE-1681", "AZE-1686", "AZE-5561" } }, // Gence-Daşkesen -> Gence
            { "AZE-1696", new[] { "AZE-1691", "AZE-1702", "AZE-1722", "AZE-1734", "AZE-1737", "AZE-1738", "AZE-1739", "AZE-4838", "AZE-5565" } }, // Karabağ -> Berde
            { "AZE-1685", new[] { "AZE-1676", "AZE-1678", "AZE-1684", "AZE-1687" } }, // Gazah-Tovuz -> Şemkir
            { "AZE-1730", new[] { "AZE-1701", "AZE-1712", "AZE-1728", "AZE-1729" } }, // Guba-Haçmaz -> Haçmaz
            { "AZE-1688", new[] { "AZE-1683", "AZE-1692", "AZE-1703", "AZE-1706", "AZE-1718", "AZE-1721", "AZE-5566" } }, // Merkezi Aran -> Yevlah
            { "AZE-1714", new[] { "AZE-1697", "AZE-1704", "AZE-1713" } }, // Mil-Muğan -> Sabirabad
            { "AZE-5562", new[] { "AZE-1695", "AZE-1700", "AZE-1707", "AZE-1708", "AZE-1709", "AZE-1720" } }, // Lenkeran-Astara -> Lenkeran
            { "AZE-2422", new[] { "AZE-2415", "AZE-2418", "AZE-2419", "AZE-2420", "AZE-2421", "AZE-2423", "AZE-5567" } }, // Nahçıvan
            { "AZE-1727", new[] { "AZE-1723", "AZE-1724", "AZE-1725", "AZE-1726", "AZE-1731", "AZE-5564" } }, // Şeki-Zagatala -> Şeki
            { "AZE-1699", new[] { "AZE-1682", "AZE-1735", "AZE-1736", "AZE-1740" } }, // Doğu Zengezur -> Cebrayıl
            { "AZE-1715", new[] { "AZE-1694", "AZE-1698", "AZE-1710", "AZE-5563" } } // Şirvan-Salyan -> Salyan
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
            var backgroundRings = new Dictionary<string, List<double[][]>>();

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
                        AddBackgroundRings(feature.GetProperty("geometry"), backgroundRings, admin);
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

            MergeProvinces(provinces);
            DropPiecesInsideNeighbors(provinces);
            provinces.Sort((a, b) => string.CompareOrdinal(a.Country + a.Id, b.Country + b.Id));
            Province background = BuildBackground(backgroundRings);
            if (background.Rings.Count > 0)
            {
                provinces.Add(background);
            }

            return provinces;
        }

        // Bir ilin aynı ülkedeki başka ilin içinde kalan parçası haritada delik gibi görünür; çevreleyen il orayı zaten kaplar.
        // Başka ülkenin içindeki toprak parçasına dokunulmaz: ülke sınırı değişmemeli.
        private static void DropPiecesInsideNeighbors(List<Province> provinces)
        {
            foreach (Province province in provinces)
            {
                if (province.Rings.Count < 2)
                {
                    continue;
                }

                int dropped = province.Rings.RemoveAll(ring => IsInsideSameCountry(provinces, province, ring));
                if (dropped > 0)
                {
                    Console.WriteLine("Komşusunun içinde kalan parça: " + province.Name + " (" + dropped + ")");
                }
            }
        }

        private static bool IsInsideSameCountry(List<Province> provinces, Province owner, double[][] ring)
        {
            foreach (Province other in provinces)
            {
                if (other == owner || other.Country != owner.Country)
                {
                    continue;
                }

                foreach (double[][] outer in other.Rings)
                {
                    if (RingUnion.IsInside(ring, outer))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Arka plan ülkeleri sadeleşmeden önce tek parça olur: küçük iller tek tek kaybolup karada deniz gibi delik bırakmaz.
        private static Province BuildBackground(Dictionary<string, List<double[][]>> ringsByCountry)
        {
            var background = new Province { Id = "", Name = "", Country = "" };
            foreach (List<double[][]> rings in ringsByCountry.Values)
            {
                foreach (double[][] ring in RingUnion.Merge(rings))
                {
                    double[][] snapped = Snap(ring, FarGrid);
                    if (snapped.Length >= MinRingPoints && Math.Abs(Area(snapped)) >= MinBackgroundArea)
                    {
                        background.Rings.Add(snapped);
                    }
                }
            }

            return background;
        }

        private static void MergeProvinces(List<Province> provinces)
        {
            var byId = new Dictionary<string, Province>(provinces.Count);
            foreach (Province province in provinces)
            {
                byId[province.Id] = province;
            }

            foreach (KeyValuePair<string, string[]> merge in ProvinceMerges)
            {
                if (!byId.TryGetValue(merge.Key, out Province target))
                {
                    continue;
                }

                foreach (string sourceId in merge.Value)
                {
                    Province source = byId[sourceId];
                    target.Rings.AddRange(source.Rings);
                    provinces.Remove(source);
                }

                target.Rings = RingUnion.Merge(target.Rings);
                Console.WriteLine("Birleşen il: " + target.Name + " (" + target.Rings.Count + " halka)");
            }
        }

        private static void AddBackgroundRings(JsonElement geometry, Dictionary<string, List<double[][]>> ringsByCountry, string admin)
        {
            if (!ringsByCountry.TryGetValue(admin, out List<double[][]> rings))
            {
                rings = new List<double[][]>();
                ringsByCountry.Add(admin, rings);
            }

            foreach (double[][] ring in OuterRings(geometry))
            {
                if (TouchesRegion(ring))
                {
                    rings.Add(ring);
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
            var ringIsBackground = new bool[totalRings];
            var ringProvince = new int[totalRings];

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
                    int ringIndex = nextRing;
                    ringIsBackground[ringIndex] = isBackground;
                    ringProvince[ringIndex] = index;
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

                    AddCoastEdges(map, ringStart, ring.Length, ringIndex, coastEdges);
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

            map.ProvinceBorderLines = provinceLines.ToArray();
            map.CountryBorderLines = countryLines.ToArray();
            map.CoastLines = BuildCoastLines(map, coastEdges, ringIsBackground, ringProvince);
            return map;
        }

        // Karayla sınır olan kenarlar elenir. Bir ilde birkaç kilometrelik kıyı kalıyorsa o da sınırdaki boşluktur, deniz değil.
        private static int[] BuildCoastLines(
            MapData map,
            Dictionary<(float, float, float, float), OpenEdge> coastEdges,
            bool[] ringIsBackground,
            int[] ringProvince)
        {
            var landProbe = new LandProbe(map.VertexX, map.VertexY, map.RingStart, ringIsBackground);
            float probeDistance = (float)(CoastProbeGrids * FarGrid * LatScale);
            var candidates = new List<OpenEdge>();
            var coastLengths = new double[map.ProvinceCount];
            foreach (OpenEdge edge in coastEdges.Values)
            {
                if (!edge.Matched && !landProbe.HasLandBeyond(edge.A, edge.B, edge.Ring, probeDistance))
                {
                    candidates.Add(edge);
                    double dx = map.VertexX[edge.B] - map.VertexX[edge.A];
                    double dy = map.VertexY[edge.B] - map.VertexY[edge.A];
                    coastLengths[ringProvince[edge.Ring]] += Math.Sqrt(dx * dx + dy * dy);
                }
            }

            var coastLines = new List<int>(candidates.Count * 2);
            foreach (OpenEdge edge in candidates)
            {
                if (ringIsBackground[edge.Ring] || coastLengths[ringProvince[edge.Ring]] >= MinCityCoastLength)
                {
                    coastLines.Add(edge.A);
                    coastLines.Add(edge.B);
                }
            }

            return coastLines.ToArray();
        }

        // Tek halkaya ait kenar kıyı adayıdır; iki halkanın paylaştığı kenar karanın içinde kalır.
        private static void AddCoastEdges(
            MapData map,
            int start,
            int count,
            int ring,
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
                    coastEdges.Add(key, new OpenEdge { A = a, B = b, Ring = ring });
                }
            }
        }

        private sealed class OpenEdge
        {
            public int A;
            public int B;
            public byte Country;
            public int Ring;
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
