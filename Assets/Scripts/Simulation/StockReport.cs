using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Stok ekranının sorguları: stok kaç gün yeter, toplam değeri ne kadar, hangi kaynaklar azalıyor.
    // Ekran açıkken gün başına bir kez okunur; hesap döngüsünde çağrılmaz.
    public static class StockReport
    {
        public const long NeverEnds = -1;

        private const long OneUnit = EconomyDefinition.MilliPerUnit;

        public static long NetMilli(WorldState world, int country, int resource)
        {
            int index = country * world.ResourceCount + resource;
            return world.DailyProduced[index] - world.DailyConsumed[index];
        }

        // Bu hızla stok kaç günde biter; azalmıyorsa NeverEnds.
        public static long DaysLeft(WorldState world, int country, int resource)
        {
            long net = NetMilli(world, country, resource);
            return net >= 0 ? NeverEnds : world.Stock[country * world.ResourceCount + resource] / -net;
        }

        public static long TotalValueMilli(WorldState world, int country)
        {
            long total = 0;
            int stockBase = country * world.ResourceCount;
            for (int resource = 0; resource < world.ResourceCount; resource++)
            {
                total += world.Stock[stockBase + resource] / OneUnit * world.PriceMilli[resource];
            }

            return total;
        }

        // Azalan kaynaklar, en önce bitecek olan başta.
        public static void CollectShrinking(WorldState world, int country, List<int> resources)
        {
            resources.Clear();
            for (int resource = 0; resource < world.ResourceCount; resource++)
            {
                if (NetMilli(world, country, resource) < 0)
                {
                    resources.Add(resource);
                }
            }

            resources.Sort((a, b) =>
            {
                int byDays = DaysLeft(world, country, a).CompareTo(DaysLeft(world, country, b));
                return byDays != 0 ? byDays : a.CompareTo(b);
            });
        }
    }
}
