using System.Collections.Generic;

namespace GeoWar.Map
{
    // Rota hesabının haritadan aldığı bilgiler: kara sınırı olan ülkeler ve hava rotasının geçtiği ülkeler.
    // Oyun başında bir kez çalışır.
    public static class MapRoutes
    {
        private const int AirPathSamples = 64;

        // Köşesi ortak olan iki ülke komşudur. Sonuç: [a * ülke sayısı + b].
        public static bool[] CountryNeighbors(MapData map)
        {
            int count = map.Countries.Length;
            var neighbors = new bool[count * count];
            var owners = new Dictionary<(float, float), int>();

            for (int province = 0; province < map.ProvinceCount; province++)
            {
                int country = map.ProvinceCountry[province];
                if (country == MapData.NoCountry)
                {
                    continue;
                }

                for (int vertex = map.VertexStart[province]; vertex < map.VertexStart[province + 1]; vertex++)
                {
                    var key = (map.VertexX[vertex], map.VertexY[vertex]);
                    if (!owners.TryGetValue(key, out int owner))
                    {
                        owners.Add(key, country);
                    }
                    else if (owner != country)
                    {
                        neighbors[country * count + owner] = true;
                        neighbors[owner * count + country] = true;
                    }
                }
            }

            return neighbors;
        }

        // İki başkent arasındaki düz çizginin üstünden geçtiği ülkeler: [(a * ülke sayısı + b) * ülke sayısı + c].
        public static bool[] AirPathCrossings(MapData map, float[] capitalX, float[] capitalY)
        {
            int count = map.Countries.Length;
            var crossings = new bool[count * count * count];

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
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
