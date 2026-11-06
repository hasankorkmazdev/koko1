namespace GeoWar.Map
{
    // Dokunulan noktanın hangi ilde olduğunu bulur.
    public static class MapHitTest
    {
        public const int NotFound = -1;

        public static int FindProvince(MapData map, float x, float y)
        {
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                if (map.ProvinceCountry[province] == MapData.NoCountry)
                {
                    continue;
                }

                if (x < map.MinX[province] || x > map.MaxX[province] || y < map.MinY[province] || y > map.MaxY[province])
                {
                    continue;
                }

                int ringEnd = map.ProvinceRingStart[province + 1];
                for (int ring = map.ProvinceRingStart[province]; ring < ringEnd; ring++)
                {
                    if (RingContains(map, ring, x, y))
                    {
                        return province;
                    }
                }
            }

            return NotFound;
        }

        private static bool RingContains(MapData map, int ring, float x, float y)
        {
            int start = map.RingStart[ring];
            int end = map.RingStart[ring + 1];
            bool inside = false;
            for (int i = start, j = end - 1; i < end; j = i, i++)
            {
                float xi = map.VertexX[i];
                float yi = map.VertexY[i];
                float xj = map.VertexX[j];
                float yj = map.VertexY[j];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
