using System;

namespace GeoWar.Simulation
{
    // Ülkeler arası rota: hangi ulaşım türü kullanılabilir ve mal kaç günde gelir. Kurallar docs/ECONOMY.md 3'tedir.
    // Sonuç tabloda durur; yalnızca ulaşım tesisi veya savaş durumu değişince yeniden hesaplanır.
    public static class CountryRoutes
    {
        public const int NoRoute = -1;

        private const int Permille = EconomyDefinition.Permille;

        // Kullanılacak ulaşım türü; yol yoksa NoRoute.
        public static int Route(EconomyDefinition definition, WorldState world, int buyer, int seller)
        {
            if (world.RoutesDirty)
            {
                Recompute(definition, world);
            }

            return world.CountryRoute[buyer * world.CountryCount + seller];
        }

        // Süre başkentler arası mesafeden çıkar ve yukarı yuvarlanır: 777 km, günde 100 km ile 8 gün.
        public static int TransitDays(EconomyDefinition definition, WorldState world, int buyer, int seller, int transport)
        {
            long km = world.CountryDistanceKm[buyer * world.CountryCount + seller];
            long kmPerDayTimesThousand = (long)definition.Balance.RoadKmPerDay * definition.TransportSpeedPermille[transport];
            long days = (km * Permille + kmPerDayTimesThousand - 1) / kmPerDayTimesThousand;
            return (int)Math.Max(1, days);
        }

        public static void SetLandNeighbors(WorldState world, int a, int b)
        {
            world.CountryLandNeighbor[a * world.CountryCount + b] = true;
            world.CountryLandNeighbor[b * world.CountryCount + a] = true;
            world.RoutesDirty = true;
        }

        public static void SetWar(WorldState world, int a, int b, bool atWar)
        {
            world.CountryAtWar[a * world.CountryCount + b] = atWar;
            world.CountryAtWar[b * world.CountryCount + a] = atWar;
            world.RoutesDirty = true;
        }

        // İsyancı devlet ana ülkenin yerinde sayılır: hava rotaları ana ülkeninkiyle aynıdır.
        // Ana ülkenin kendi uçuşları kendi toprağından kalkar; bu yüzden isyancı devletin üstünden geçmiş sayılmaz.
        public static void CopyAirPaths(WorldState world, int parent, int rebel)
        {
            if (world.AirPathCrossings == null)
            {
                return;
            }

            bool[] paths = world.AirPathCrossings;
            int count = world.CountryCount;
            for (int a = 0; a < count; a++)
            {
                for (int b = 0; b < count; b++)
                {
                    paths[(a * count + b) * count + rebel] = paths[(a * count + b) * count + parent];
                    paths[(rebel * count + a) * count + b] = paths[(parent * count + a) * count + b];
                    paths[(a * count + rebel) * count + b] = paths[(a * count + parent) * count + b];
                }
            }

            for (int other = 0; other < count; other++)
            {
                paths[(parent * count + other) * count + rebel] = false;
                paths[(other * count + parent) * count + rebel] = false;
            }
        }

        public static void Recompute(EconomyDefinition definition, WorldState world)
        {
            world.RoutesDirty = false;
            MarkTransportBuildings(definition, world);

            int count = world.CountryCount;
            for (int buyer = 0; buyer < count; buyer++)
            {
                FindLandReach(world, buyer);
                for (int seller = 0; seller < count; seller++)
                {
                    bool blocked = buyer == seller || world.CountryAtWar[buyer * count + seller];
                    world.CountryRoute[buyer * count + seller] = blocked ? NoRoute : CheapestTransport(definition, world, buyer, seller);
                }
            }
        }

        private static int CheapestTransport(EconomyDefinition definition, WorldState world, int buyer, int seller)
        {
            int best = NoRoute;
            for (int transport = 0; transport < definition.TransportCount; transport++)
            {
                if (IsUsable(definition, world, buyer, seller, transport)
                    && (best == NoRoute || definition.TransportCostPermille[transport] < definition.TransportCostPermille[best]))
                {
                    best = transport;
                }
            }

            return best;
        }

        private static bool IsUsable(EconomyDefinition definition, WorldState world, int buyer, int seller, int transport)
        {
            switch (definition.TransportKinds[transport])
            {
                case TransportKind.Road:
                    return world.RouteSeen[seller];
                case TransportKind.Sea:
                    return HasBuilding(definition, world, buyer, transport) && HasBuilding(definition, world, seller, transport);
                case TransportKind.Air:
                    return HasBuilding(definition, world, buyer, transport)
                        && HasBuilding(definition, world, seller, transport)
                        && !AirPathCrossesEnemy(world, buyer, seller);
                default:
                    return false;
            }
        }

        private static bool HasBuilding(EconomyDefinition definition, WorldState world, int country, int transport)
        {
            return world.CountryHasTransport[country * definition.TransportCount + transport];
        }

        private static bool AirPathCrossesEnemy(WorldState world, int buyer, int seller)
        {
            if (world.AirPathCrossings == null)
            {
                return false;
            }

            int count = world.CountryCount;
            int pair = (buyer * count + seller) * count;
            for (int other = 0; other < count; other++)
            {
                if (world.AirPathCrossings[pair + other] && world.CountryAtWar[buyer * count + other])
                {
                    return true;
                }
            }

            return false;
        }

        // Alıcının düşmanı olan ülkelere girmeden karadan ulaşılan ülkeler.
        private static void FindLandReach(WorldState world, int buyer)
        {
            int count = world.CountryCount;
            Array.Clear(world.RouteSeen, 0, count);
            world.RouteSeen[buyer] = true;
            world.RouteQueue[0] = buyer;
            int head = 0;
            int tail = 1;
            while (head < tail)
            {
                int current = world.RouteQueue[head++];
                for (int next = 0; next < count; next++)
                {
                    if (!world.RouteSeen[next]
                        && world.CountryLandNeighbor[current * count + next]
                        && !world.CountryAtWar[buyer * count + next])
                    {
                        world.RouteSeen[next] = true;
                        world.RouteQueue[tail++] = next;
                    }
                }
            }
        }

        // İnşaatı biten liman ve havaalanı sayılır.
        private static void MarkTransportBuildings(EconomyDefinition definition, WorldState world)
        {
            int size = world.CountryCount * definition.TransportCount;
            if (world.CountryHasTransport == null || world.CountryHasTransport.Length != size)
            {
                world.CountryHasTransport = new bool[size];
            }

            Array.Clear(world.CountryHasTransport, 0, size);
            for (int country = 0; country < world.CountryCount; country++)
            {
                int end = world.CountryFacilityStart[country + 1];
                for (int i = world.CountryFacilityStart[country]; i < end; i++)
                {
                    int slot = world.CountrySlots[i];
                    int building = world.FacilityBuilding[slot];
                    if (building == WorldState.NoBuilding || !definition.BuildingIsTransport[building] || world.FacilityBuildDays[slot] > 0)
                    {
                        continue;
                    }

                    for (int transport = 0; transport < definition.TransportCount; transport++)
                    {
                        if (definition.TransportBuilding[transport] == building)
                        {
                            world.CountryHasTransport[country * definition.TransportCount + transport] = true;
                        }
                    }
                }
            }
        }
    }
}
