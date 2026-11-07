using System;
using System.Collections.Generic;

namespace GeoWar.Map
{
    // Birleşecek illerin halkalarını tek ilin halkalarına çevirir: ortak kenarlar silinir, kalanlar yeni dış sınırı verir.
    // Başka bir halkanın içinde kalan halka ve birleşince oluşan delik atılır; harita yalnızca dış halkaları çizer.
    public static class RingUnion
    {
        private const double KeyScale = 1e7;
        private const int MinRingPoints = 3;
        private const double Half = 0.5;

        private sealed class Edge
        {
            public double[] From;
            public (long, long) FromKey;
            public (long, long) ToKey;
            public bool Used;
        }

        public static List<double[][]> Merge(List<double[][]> rings)
        {
            List<Edge> edges = OuterEdges(rings);
            var outgoing = new Dictionary<(long, long), List<Edge>>();
            foreach (Edge edge in edges)
            {
                if (!outgoing.TryGetValue(edge.FromKey, out List<Edge> list))
                {
                    list = new List<Edge>(1);
                    outgoing.Add(edge.FromKey, list);
                }

                list.Add(edge);
            }

            var loops = new List<double[][]>();
            foreach (Edge edge in edges)
            {
                if (!edge.Used)
                {
                    Walk(edge, outgoing, loops);
                }
            }

            return WithoutHolesAndEnclosed(loops);
        }

        // Halkalar aynı yöne çevrilir: iki halkanın ortak kenarı birbirine ters yönde görünür ve ikisi de düşer.
        private static List<Edge> OuterEdges(List<double[][]> rings)
        {
            var all = new List<Edge>();
            var directed = new HashSet<((long, long), (long, long))>();
            foreach (double[][] ring in rings)
            {
                bool reverse = SignedArea(ring) < 0;
                int count = ring.Length;
                for (int i = 0; i < count; i++)
                {
                    double[] from = ring[reverse ? count - 1 - i : i];
                    double[] to = ring[reverse ? (2 * count - 2 - i) % count : (i + 1) % count];
                    var edge = new Edge { From = from, FromKey = Key(from), ToKey = Key(to) };
                    if (edge.FromKey != edge.ToKey)
                    {
                        all.Add(edge);
                        directed.Add((edge.FromKey, edge.ToKey));
                    }
                }
            }

            var outer = new List<Edge>(all.Count);
            foreach (Edge edge in all)
            {
                if (!directed.Contains((edge.ToKey, edge.FromKey)))
                {
                    outer.Add(edge);
                }
            }

            return outer;
        }

        // Yol, daha önce geçtiği bir köşeye dönünce aradaki parça ayrı halka olarak kesilir; tek noktada değen iller de ayrışır.
        private static void Walk(Edge first, Dictionary<(long, long), List<Edge>> outgoing, List<double[][]> loops)
        {
            var path = new List<Edge>();
            var positions = new Dictionary<(long, long), int>();
            Edge edge = first;
            while (edge != null)
            {
                edge.Used = true;
                positions[edge.FromKey] = path.Count;
                path.Add(edge);

                if (positions.TryGetValue(edge.ToKey, out int start))
                {
                    var loop = new double[path.Count - start][];
                    for (int i = start; i < path.Count; i++)
                    {
                        loop[i - start] = path[i].From;
                        positions.Remove(path[i].FromKey);
                    }

                    path.RemoveRange(start, path.Count - start);
                    loops.Add(loop);
                }

                edge = NextUnused(outgoing, edge.ToKey);
            }
        }

        private static Edge NextUnused(Dictionary<(long, long), List<Edge>> outgoing, (long, long) vertex)
        {
            if (outgoing.TryGetValue(vertex, out List<Edge> list))
            {
                foreach (Edge edge in list)
                {
                    if (!edge.Used)
                    {
                        return edge;
                    }
                }
            }

            return null;
        }

        private static List<double[][]> WithoutHolesAndEnclosed(List<double[][]> loops)
        {
            var outer = new List<double[][]>(loops.Count);
            foreach (double[][] loop in loops)
            {
                if (loop.Length >= MinRingPoints && SignedArea(loop) > 0)
                {
                    outer.Add(loop);
                }
            }

            var kept = new List<double[][]>(outer.Count);
            foreach (double[][] ring in outer)
            {
                // Kenarın ortası kullanılır: halkalar köşede değebilir ama artık kenar paylaşmaz.
                double x = (ring[0][0] + ring[1][0]) * Half;
                double y = (ring[0][1] + ring[1][1]) * Half;
                bool enclosed = false;
                foreach (double[][] other in outer)
                {
                    enclosed |= other != ring && SignedArea(other) > SignedArea(ring) && Contains(other, x, y);
                }

                if (!enclosed)
                {
                    kept.Add(ring);
                }
            }

            return kept;
        }

        private static bool Contains(double[][] ring, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i, i++)
            {
                double[] a = ring[i];
                double[] b = ring[j];
                if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0])
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static double SignedArea(double[][] ring)
        {
            double sum = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                double[] a = ring[i];
                double[] b = ring[(i + 1) % ring.Length];
                sum += a[0] * b[1] - b[0] * a[1];
            }

            return sum * Half;
        }

        private static (long, long) Key(double[] point)
        {
            return ((long)Math.Round(point[0] * KeyScale), (long)Math.Round(point[1] * KeyScale));
        }
    }
}
