using System.Collections.Generic;

namespace GeoWar.Map
{
    // Rota hesabının haritadan aldığı bilgiler: kara sınırı olan ülkeler ve hava rotasının geçtiği ülkeler.
    // Oyun başında bir kez çalışır.
    public static class MapRoutes
    {
        private const int AirPathSamples = 64;
        private const int NoVertex = -1;

        // Köşesi ortak olan iki il komşudur. Sonuç şehirler içindir: il ve şehir indeksi aynıdır, ülkesi olmayan il sayılmaz.
        public static void CityNeighbors(MapData map, int cityCount, out int[] start, out int[] neighbors)
        {
            int vertexCount = map.VertexStart[cityCount];
            var lastVertexAtPoint = new Dictionary<(float, float), int>(vertexCount);
            var previousAtPoint = new int[vertexCount];
            var cityOfVertex = new int[vertexCount];
            var pairs = new List<HashSet<int>>(cityCount);
            for (int city = 0; city < cityCount; city++)
            {
                pairs.Add(new HashSet<int>());
            }

            for (int city = 0; city < cityCount; city++)
            {
                for (int vertex = map.VertexStart[city]; vertex < map.VertexStart[city + 1]; vertex++)
                {
                    var key = (map.VertexX[vertex], map.VertexY[vertex]);
                    if (!lastVertexAtPoint.TryGetValue(key, out int last))
                    {
                        last = NoVertex;
                    }

                    for (int other = last; other != NoVertex; other = previousAtPoint[other])
                    {
                        if (cityOfVertex[other] != city)
                        {
                            pairs[city].Add(cityOfVertex[other]);
                            pairs[cityOfVertex[other]].Add(city);
                        }
                    }

                    cityOfVertex[vertex] = city;
                    previousAtPoint[vertex] = last;
                    lastVertexAtPoint[key] = vertex;
                }
            }

            start = new int[cityCount + 1];
            var flat = new List<int>();
            for (int city = 0; city < cityCount; city++)
            {
                start[city] = flat.Count;
                var sorted = new List<int>(pairs[city]);
                sorted.Sort();
                flat.AddRange(sorted);
            }

            start[cityCount] = flat.Count;
            neighbors = flat.ToArray();
        }

        // İki başkent arasındaki düz çizginin üstünden geçtiği ülkeler: [(a * dizi ülke sayısı + b) * dizi ülke sayısı + c].
        // Dizi ülke sayısına isyancı devlet yerleri de dahildir; onların satırları devlet kurulunca dolar.
        public static bool[] AirPathCrossings(MapData map, float[] capitalX, float[] capitalY, int worldCountryCount)
        {
            int count = worldCountryCount;
            int founded = map.Countries.Length;
            var crossings = new bool[count * count * count];

            for (int a = 0; a < founded; a++)
            {
                for (int b = a + 1; b < founded; b++)
                {
                    for (int sample = 1; sample < AirPathSamples; sample++)
                    {
                        float t = (float)sample / AirPathSamples;
                        float x = capitalX[a] + (capitalX[b] - capitalX[a]) * t;
                        float y = capitalY[a] + (capitalY[b] - capitalY[a]) * t;
                        int province = MapHitTest.FindProvince(map, x, y);
                        if (province == MapHitTest.NotFound || map.ProvinceCountry[province] == MapData.NoCountry)
                        {
                            continue;
                        }

                        int crossed = map.ProvinceCountry[province];
                        crossings[(a * count + b) * count + crossed] = true;
                        crossings[(b * count + a) * count + crossed] = true;
                    }
                }
            }

            return crossings;
        }
    }
}
