using System.Collections.Generic;

namespace GeoWar.Map
{
    // Kulak kesme yöntemiyle çokgen üçgenleme. Harita dosyası üretilirken çalışır.
    public static class PolygonTriangulator
    {
        private const double AreaEpsilon = 1e-9;

        // Köşeler output'a firstVertex'ten başlayan indekslerle yazılır.
        // Temiz kesilemeyen kısım varsa false döner; yine de en yakın kulak kesilerek sonuç üretilir.
        public static bool Triangulate(double[] x, double[] y, int start, int count, int firstVertex, List<int> output)
        {
            if (count < 3)
            {
                return false;
            }

            var remaining = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                remaining.Add(i);
            }

            if (SignedArea(x, y, start, count) < 0)
            {
                remaining.Reverse();
            }

            bool clean = true;
            while (remaining.Count > 3)
            {
                // Alansız köşeler üçgen üretmez, sadece çıkarılır.
                int flat = FindFlatCorner(x, y, start, remaining);
                if (flat >= 0)
                {
                    remaining.RemoveAt(flat);
                    continue;
                }

                int ear = FindEar(x, y, start, remaining);
                if (ear < 0)
                {
                    clean = false;
                    ear = FindSmallestCorner(x, y, start, remaining);
                }

                int size = remaining.Count;
                AddTriangle(x, y, start, remaining[(ear + size - 1) % size], remaining[ear], remaining[(ear + 1) % size], firstVertex, output);
                remaining.RemoveAt(ear);
            }

            AddTriangle(x, y, start, remaining[0], remaining[1], remaining[2], firstVertex, output);
            return clean;
        }

        private static void AddTriangle(double[] x, double[] y, int start, int a, int b, int c, int firstVertex, List<int> output)
        {
            if (Cross(x, y, start, a, b, c) > AreaEpsilon)
            {
                output.Add(firstVertex + a);
                output.Add(firstVertex + b);
                output.Add(firstVertex + c);
            }
        }

        private static int FindFlatCorner(double[] x, double[] y, int start, List<int> remaining)
        {
            int size = remaining.Count;
            for (int i = 0; i < size; i++)
            {
                double cross = Cross(x, y, start, remaining[(i + size - 1) % size], remaining[i], remaining[(i + 1) % size]);
                if (System.Math.Abs(cross) <= AreaEpsilon)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindEar(double[] x, double[] y, int start, List<int> remaining)
        {
            int size = remaining.Count;
            for (int i = 0; i < size; i++)
            {
                int a = remaining[(i + size - 1) % size];
                int b = remaining[i];
                int c = remaining[(i + 1) % size];
                if (Cross(x, y, start, a, b, c) <= AreaEpsilon)
                {
                    continue;
                }

                bool empty = true;
                for (int k = 0; k < size && empty; k++)
                {
                    int point = remaining[k];
                    if (point == a || point == b || point == c)
                    {
                        continue;
                    }

                    empty = !InTriangle(x, y, start, a, b, c, point);
                }

                if (empty)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindSmallestCorner(double[] x, double[] y, int start, List<int> remaining)
        {
            int size = remaining.Count;
            int best = 0;
            double smallest = double.MaxValue;
            for (int i = 0; i < size; i++)
            {
                double area = System.Math.Abs(Cross(x, y, start, remaining[(i + size - 1) % size], remaining[i], remaining[(i + 1) % size]));
                if (area < smallest)
                {
                    smallest = area;
                    best = i;
                }
            }

            return best;
        }

        // Kenar üstündeki noktalar içeride sayılmaz; yoksa çokgenin kendine değdiği köşeler kulak kesmeyi kilitler.
        private static bool InTriangle(double[] x, double[] y, int start, int a, int b, int c, int point)
        {
            return Cross(x, y, start, a, b, point) > AreaEpsilon
                && Cross(x, y, start, b, c, point) > AreaEpsilon
                && Cross(x, y, start, c, a, point) > AreaEpsilon;
        }

        private static double Cross(double[] x, double[] y, int start, int a, int b, int c)
        {
            double ax = x[start + a];
            double ay = y[start + a];
            return (x[start + b] - ax) * (y[start + c] - ay) - (y[start + b] - ay) * (x[start + c] - ax);
        }

        private static double SignedArea(double[] x, double[] y, int start, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                sum += x[start + i] * y[start + j] - x[start + j] * y[start + i];
            }

            return sum / 2;
        }
    }
}
