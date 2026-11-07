using System;
using System.Collections.Generic;

namespace GeoWar.Tools
{
    // Halkaların köşe sayısını düşürür, komşu halkaların ortak sınırını bozmaz.
    // Halkalar düğümlerde parçalara ayrılır: üç halkanın buluştuğu nokta, sınırın kıyıya çıktığı nokta, yerinde kalması istenen nokta.
    // Her parça tek yönde sadeleştirilir; böylece iki komşu aynı parçadan aynı köşeleri alır, araya boşluk girmez.
    public static class RingSimplifier
    {
        private sealed class PointInfo
        {
            public ((double, double), (double, double)) Neighbors;
            public int Count;
            public bool IsNode;
        }

        // Sonuç girdiyle aynı sıradadır. Sapma, enlem derecesi cinsinden en çok tolerance olur; xScale boylam derecesini enleme çevirir.
        public static double[][][] Simplify(IList<double[][]> rings, double tolerance, double xScale, HashSet<(double, double)> fixedPoints)
        {
            Dictionary<(double, double), PointInfo> points = FindNodes(rings, fixedPoints);
            var result = new double[rings.Count][][];
            for (int i = 0; i < rings.Count; i++)
            {
                result[i] = SimplifyRing(rings[i], points, tolerance, xScale);
            }

            return result;
        }

        private static Dictionary<(double, double), PointInfo> FindNodes(IList<double[][]> rings, HashSet<(double, double)> fixedPoints)
        {
            var points = new Dictionary<(double, double), PointInfo>();
            foreach (double[][] ring in rings)
            {
                int count = ring.Length;
                for (int i = 0; i < count; i++)
                {
                    (double, double) key = Key(ring[i]);
                    ((double, double), (double, double)) neighbors = Ordered(Key(ring[(i + count - 1) % count]), Key(ring[(i + 1) % count]));
                    if (!points.TryGetValue(key, out PointInfo info))
                    {
                        info = new PointInfo { Neighbors = neighbors, IsNode = fixedPoints != null && fixedPoints.Contains(key) };
                        points.Add(key, info);
                    }

                    info.Count++;
                    info.IsNode |= info.Count > 2 || info.Neighbors != neighbors;
                }
            }

            return points;
        }

        private static double[][] SimplifyRing(double[][] ring, Dictionary<(double, double), PointInfo> points, double tolerance, double xScale)
        {
            int count = ring.Length;
            var nodes = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (points[Key(ring[i])].IsNode)
                {
                    nodes.Add(i);
                }
            }

            // Düğümü olmayan halka adadır. Tek noktadan ayrılan parçanın ucu kendine döneceği için en uzak köşeden ikiye bölünür.
            if (nodes.Count == 0)
            {
                nodes.Add(0);
            }

            if (nodes.Count == 1)
            {
                nodes.Add(FarthestFrom(ring, nodes[0], xScale));
                nodes.Sort();
            }

            var kept = new List<double[]>();
            for (int n = 0; n < nodes.Count; n++)
            {
                int from = nodes[n];
                int to = nodes[(n + 1) % nodes.Count];
                int length = (to - from + count) % count + 1;
                var arc = new double[length][];
                for (int i = 0; i < length; i++)
                {
                    arc[i] = ring[(from + i) % count];
                }

                AppendSimplified(arc, kept, tolerance, xScale);
            }

            return kept.ToArray();
        }

        // Son köşe eklenmez: sonraki parçanın ilk köşesidir.
        private static void AppendSimplified(double[][] arc, List<double[]> kept, double tolerance, double xScale)
        {
            bool reverse = IsBackward(arc);
            int last = arc.Length - 1;
            var keep = new bool[arc.Length];
            keep[0] = true;
            keep[last] = true;

            var stack = new Stack<(int, int)>();
            stack.Push((0, last));
            while (stack.Count > 0)
            {
                (int a, int b) = stack.Pop();
                double worst = tolerance;
                int worstIndex = -1;
                for (int i = a + 1; i < b; i++)
                {
                    int pa = reverse ? last - a : a;
                    int pb = reverse ? last - b : b;
                    int pi = reverse ? last - i : i;
                    double distance = SegmentDistance(arc[pi], arc[pa], arc[pb], xScale);
                    if (distance > worst)
                    {
                        worst = distance;
                        worstIndex = i;
                    }
                }

                if (worstIndex >= 0)
                {
                    keep[reverse ? last - worstIndex : worstIndex] = true;
                    stack.Push((a, worstIndex));
                    stack.Push((worstIndex, b));
                }
            }

            for (int i = 0; i < last; i++)
            {
                if (keep[i])
                {
                    kept.Add(arc[i]);
                }
            }
        }

        // Ortak parça iki halkada ters yönde görünür; hangi yönden gelirse gelsin aynı sırayla işlenmesi için yön sabitlenir.
        private static bool IsBackward(double[][] arc)
        {
            for (int i = 0, j = arc.Length - 1; i < j; i++, j--)
            {
                int order = Compare(arc[i], arc[j]);
                if (order != 0)
                {
                    return order > 0;
                }
            }

            return false;
        }

        private static int Compare(double[] a, double[] b)
        {
            return a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]);
        }

        private static int FarthestFrom(double[][] ring, int origin, double xScale)
        {
            int best = origin;
            double bestDistance = -1;
            for (int i = 0; i < ring.Length; i++)
            {
                double dx = (ring[i][0] - ring[origin][0]) * xScale;
                double dy = ring[i][1] - ring[origin][1];
                double distance = dx * dx + dy * dy;
                if (distance > bestDistance && i != origin)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static double SegmentDistance(double[] point, double[] a, double[] b, double xScale)
        {
            double abx = (b[0] - a[0]) * xScale;
            double aby = b[1] - a[1];
            double apx = (point[0] - a[0]) * xScale;
            double apy = point[1] - a[1];
            double lengthSquared = abx * abx + aby * aby;
            double t = lengthSquared > 0 ? Math.Max(0, Math.Min(1, (apx * abx + apy * aby) / lengthSquared)) : 0;
            double dx = apx - abx * t;
            double dy = apy - aby * t;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static (double, double) Key(double[] point)
        {
            return (point[0], point[1]);
        }

        private static ((double, double), (double, double)) Ordered((double, double) a, (double, double) b)
        {
            return a.CompareTo(b) <= 0 ? (a, b) : (b, a);
        }
    }
}
