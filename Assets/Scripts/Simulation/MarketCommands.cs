using System.Collections.Generic;

namespace GeoWar.Simulation
{
    public enum TradeBlock : byte
    {
        None,
        NoAmount,
        OwnOffer,
        NoRoute,
        NoOffer,
        NoMoney,
        NoStock
    }

    // Pazar: satış teklifleri ve alım. Kurallar docs/ECONOMY.md 2'dedir.
    // Satılan mal teklif olarak pazara çıkar, para alıcı çıkınca gelir. Alıcı piyasa fiyatını ve yol ücretini öder.
    // İşlem o günkü fiyattan yapılır; fiyat gün sonunda arz ve talebe göre değişir.
    public static class MarketCommands
    {
        private const int Permille = EconomyDefinition.Permille;
        private const long OneUnit = EconomyDefinition.MilliPerUnit;

        // Yol ücreti başkentler arası mesafeden ve kullanılan ulaşım türünün maliyet çarpanından çıkar. Yol yoksa 0.
        public static int RoadFeePermille(EconomyDefinition definition, WorldState world, int buyer, int seller)
        {
            int transport = CountryRoutes.Route(definition, world, buyer, seller);
            if (transport == CountryRoutes.NoRoute)
            {
                return 0;
            }

            BalanceData balance = definition.Balance;
            long km = world.CountryDistanceKm[buyer * world.CountryCount + seller];
            long fee = km * balance.RoadFeePermillePerStep / balance.RoadFeeStepKm;
            return (int)(fee * definition.TransportCostPermille[transport] / Permille);
        }

        // Bir birimin alıcıya maliyeti: piyasa fiyatı ve yol ücreti.
        public static long UnitCostMilli(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource)
        {
            long price = world.PriceMilli[resource];
            return price + price * RoadFeePermille(definition, world, buyer, seller) / Permille;
        }

        public static long BuyCostMilli(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource, long amountMilli)
        {
            return amountMilli * UnitCostMilli(definition, world, buyer, seller, resource) / OneUnit;
        }

        // Satıcının eline geçen: piyasa fiyatı. Yol ücreti yola gider.
        public static long SaleIncomeMilli(WorldState world, int resource, long amountMilli)
        {
            return amountMilli * world.PriceMilli[resource] / OneUnit;
        }

        public static long OfferMilli(WorldState world, int seller, int resource)
        {
            return world.OfferMilli[seller * world.ResourceCount + resource];
        }

        // Başka ülkelerin bu kaynaktaki toplam teklifi.
        public static long OthersOfferMilli(WorldState world, int buyer, int resource)
        {
            long total = 0;
            for (int seller = 0; seller < world.CountryCount; seller++)
            {
                if (seller != buyer)
                {
                    total += OfferMilli(world, seller, resource);
                }
            }

            return total;
        }

        // Bu kaynağı satan ülkeler, en yakından uzağa. Aynı fiyatta en yakın olan en ucuzdur. Kendi teklifi listelenmez.
        public static void CollectOffers(WorldState world, int buyer, int resource, List<int> sellers)
        {
            sellers.Clear();
            int count = world.CountryCount;
            for (int i = 0; i < count; i++)
            {
                int seller = world.NearestCountries[buyer * count + i];
                if (seller != buyer && OfferMilli(world, seller, resource) > 0)
                {
                    sellers.Add(seller);
                }
            }
        }

        public static TradeBlock CheckBuy(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource, long amountMilli)
        {
            if (amountMilli <= 0)
            {
                return TradeBlock.NoAmount;
            }

            if (buyer == seller)
            {
                return TradeBlock.OwnOffer;
            }

            if (CountryRoutes.Route(definition, world, buyer, seller) == CountryRoutes.NoRoute)
            {
                return TradeBlock.NoRoute;
            }

            if (amountMilli > OfferMilli(world, seller, resource))
            {
                return TradeBlock.NoOffer;
            }

            return BuyCostMilli(definition, world, buyer, seller, resource, amountMilli) > world.Money[buyer] ? TradeBlock.NoMoney : TradeBlock.None;
        }

        // Yalnızca seçilen tekliften alır. Teklif veya para yetmezse hiç almaz.
        public static bool TryBuy(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource, long amountMilli)
        {
            if (CheckBuy(definition, world, buyer, seller, resource, amountMilli) != TradeBlock.None)
            {
                return false;
            }

            Transfer(definition, world, buyer, seller, resource, amountMilli);
            return true;
        }

        // Para hemen el değiştirir, mal yola çıkar. Kontrol yapmaz: para, teklif ve rota çağıran tarafından doğrulanır.
        public static void Transfer(EconomyDefinition definition, WorldState world, int buyer, int seller, int resource, long amountMilli)
        {
            world.Money[buyer] -= BuyCostMilli(definition, world, buyer, seller, resource, amountMilli);
            world.Money[seller] += SaleIncomeMilli(world, resource, amountMilli);
            world.OfferMilli[seller * world.ResourceCount + resource] -= amountMilli;
            world.MarketDemand[resource] += amountMilli;
            Shipments.Start(definition, world, buyer, seller, resource, amountMilli);
        }

        public static TradeBlock CheckList(WorldState world, int country, int resource, long amountMilli)
        {
            if (amountMilli <= 0)
            {
                return TradeBlock.NoAmount;
            }

            return amountMilli > world.Stock[country * world.ResourceCount + resource] ? TradeBlock.NoStock : TradeBlock.None;
        }

        // Malı stoktan çıkarıp teklif yapar. Para gelmez; alıcı çıkınca gelir.
        public static bool TryList(WorldState world, int country, int resource, long amountMilli)
        {
            if (CheckList(world, country, resource, amountMilli) != TradeBlock.None)
            {
                return false;
            }

            int index = country * world.ResourceCount + resource;
            world.Stock[index] -= amountMilli;
            world.OfferMilli[index] += amountMilli;
            world.MarketSupply[resource] += amountMilli;
            return true;
        }

        // Satılmamış malın tamamını stoğa geri alır.
        public static bool TryWithdraw(WorldState world, int country, int resource)
        {
            int index = country * world.ResourceCount + resource;
            long amount = world.OfferMilli[index];
            if (amount <= 0)
            {
                return false;
            }

            world.OfferMilli[index] = 0;
            world.Stock[index] += amount;
            return true;
        }
    }
}
