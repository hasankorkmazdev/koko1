using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Yoldaki bir alım. Kapasiteyi aşan miktar parça parça gelir: ilk sefer ArrivalDay'de, sonra her gün bir sefer.
    public struct Shipment
    {
        public int Buyer;
        public int Seller;
        public int Resource;
        public int Transport;
        public int ArrivalDay;
        public long RemainingMilli;
    }

    public static class Shipments
    {
        private const int GrowthFactor = 2;

        // Malı yola çıkarır. Rotanın var olduğunu çağıran doğrular.
        public static void Start(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource, long amountMilli)
        {
            int transport = CountryRoutes.Route(definition, world, buyer, seller);
            if (world.ShipmentCount == world.ShipmentList.Length)
            {
                Array.Resize(ref world.ShipmentList, world.ShipmentList.Length * GrowthFactor);
            }

            world.ShipmentList[world.ShipmentCount++] = new Shipment
            {
                Buyer = buyer,
                Seller = seller,
                Resource = resource,
                Transport = transport,
                ArrivalDay = world.Day + CountryRoutes.TransitDays(definition, world, buyer, seller, transport),
                RemainingMilli = amountMilli
            };
            world.InTransitMilli[buyer * world.ResourceCount + resource] += amountMilli;
        }

        // Gün kapanırken vakti gelen seferleri alıcının stoğuna koyar. Biten kayıt sondakiyle yer değiştirir.
        public static void DeliverArrivals(EconomyDefinition definition, WorldState world)
        {
            for (int i = world.ShipmentCount - 1; i >= 0; i--)
            {
                ref Shipment shipment = ref world.ShipmentList[i];
                if (shipment.ArrivalDay > world.Day)
                {
                    continue;
                }

                long trip = Math.Min(shipment.RemainingMilli, definition.TransportTripMilli[shipment.Transport]);
                int index = shipment.Buyer * world.ResourceCount + shipment.Resource;
                world.Stock[index] += trip;
                world.InTransitMilli[index] -= trip;
                shipment.RemainingMilli -= trip;
                shipment.ArrivalDay = world.Day + 1;

                if (shipment.RemainingMilli == 0)
                {
                    world.ShipmentList[i] = world.ShipmentList[--world.ShipmentCount];
                }
            }
        }

        // Alıcının bu kaynaktaki sevkiyatları, en önce gelen başta.
        public static void CollectFor(WorldState world, int buyer, int resource, List<Shipment> shipments)
        {
            shipments.Clear();
            for (int i = 0; i < world.ShipmentCount; i++)
            {
                Shipment shipment = world.ShipmentList[i];
                if (shipment.Buyer == buyer && shipment.Resource == resource)
                {
                    shipments.Add(shipment);
                }
            }

            shipments.Sort((a, b) => a.ArrivalDay != b.ArrivalDay ? a.ArrivalDay.CompareTo(b.ArrivalDay) : a.Seller.CompareTo(b.Seller));
        }

        public static int CountFor(WorldState world, int buyer)
        {
            int count = 0;
            for (int i = 0; i < world.ShipmentCount; i++)
            {
                if (world.ShipmentList[i].Buyer == buyer)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
