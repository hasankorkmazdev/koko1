using System;

namespace GeoWar.Simulation
{
    // Başkentler arası mesafe tablosu. Oyun başında bir kez hesaplanır, sonra yalnızca tam sayılar okunur.
    public static class CountryDistances
    {
        private const double EarthRadiusKm = 6371.0;
        private const double DegreesToRadians = Math.PI / 180.0;

        public static int Km(double latA, double lonA, double latB, double lonB)
        {
            double phiA = latA * DegreesToRadians;
            double phiB = latB * DegreesToRadians;
            double halfLat = (latB - latA) * DegreesToRadians / 2.0;
            double halfLon = (lonB - lonA) * DegreesToRadians / 2.0;
            double a = Math.Sin(halfLat) * Math.Sin(halfLat)
                + Math.Cos(phiA) * Math.Cos(phiB) * Math.Sin(halfLon) * Math.Sin(halfLon);
            return (int)Math.Round(2.0 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a))));
        }

        // Başkentlerin enlem ve boylamından mesafeleri ve her ülkenin en yakından uzağa sırasını yazar.
        // Oyuna başlayan ülkeler içindir; isyancı devletin yeri kurulana kadar sıranın sonunda bekler.
        public static void SetFromCapitals(WorldState world, double[] lat, double[] lon)
        {
            int count = world.CountryCount;
            for (int buyer = 0; buyer < world.BaseCountryCount; buyer++)
            {
                for (int other = 0; other < world.BaseCountryCount; other++)
                {
                    world.CountryDistanceKm[buyer * count + other] = buyer == other ? 0 : Km(lat[buyer], lon[buyer], lat[other], lon[other]);
                }
            }

            RebuildNearest(world);
        }

        // İsyancı devlet ana ülkenin yerinde sayılır: başkaları için mesafesi ana ülkeninkiyle aynıdır, ana ülkeye mesafesi sıfırdır.
        public static void CopyForRebelState(WorldState world, int parent, int rebel)
        {
            int count = world.CountryCount;
            for (int other = 0; other < count; other++)
            {
                int km = world.CountryDistanceKm[parent * count + other];
                world.CountryDistanceKm[rebel * count + other] = km;
                world.CountryDistanceKm[other * count + rebel] = km;
            }

            world.CountryDistanceKm[rebel * count + parent] = 0;
            world.CountryDistanceKm[parent * count + rebel] = 0;
            world.CountryDistanceKm[rebel * count + rebel] = 0;
            RebuildNearest(world);
        }

        private static void RebuildNearest(WorldState world)
        {
            int count = world.CountryCount;
            var keys = new long[count];
            var order = new int[count];

            for (int buyer = 0; buyer < count; buyer++)
            {
                for (int other = 0; other < count; other++)
                {
                    bool isEmptySlot = other >= world.BaseCountryCount && !Rebellions.IsRebelState(world, other);
                    long km = isEmptySlot ? int.MaxValue : world.CountryDistanceKm[buyer * count + other];

                    // Eşit mesafede ülke sırası belirler: sonuç her cihazda aynı kalır.
                    keys[other] = km * count + other;
                    order[other] = other;
                }

                Array.Sort(keys, order);
                Array.Copy(order, 0, world.NearestCountries, buyer * count, count);
            }
        }
    }
}
