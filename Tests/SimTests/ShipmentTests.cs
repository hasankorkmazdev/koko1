using System;
using System.Collections.Generic;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class ShipmentTests
    {
        private const long Unit = EconomyDefinition.MilliPerUnit;
        private const int Permille = EconomyDefinition.Permille;
        private const long StartMoney = 1000000 * Unit;

        // Dört başkent ekvatorda, onar derece arayla: komşular yaklaşık 1112 km.
        private static readonly double[] CapitalLat = { 0, 0, 0, 0 };
        private static readonly double[] CapitalLon = { 0, 10, 20, 30 };

        private static EconomyDefinition _definition;
        private static Action<string, bool> _check;
        private static int _road;
        private static int _sea;
        private static int _air;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            _check = check;
            _definition = definition;
            _road = Array.IndexOf(definition.TransportKeys, "road");
            _sea = Array.IndexOf(definition.TransportKeys, "sea");
            _air = Array.IndexOf(definition.TransportKeys, "air");

            RoadNeedsLandConnection();
            RoadPassesThroughNeighbors();
            RoadDoesNotPassEnemy();
            EnemyCannotBeTradedWith();
            SeaNeedsPortAtBothEnds();
            CheapestTransportIsChosen();
            PortUnderConstructionDoesNotCount();
            ClosingPortRemovesRoute();
            AirNeedsAirportsAndAvoidsEnemy();
            FeeFollowsTransportCost();
            TransitDaysFollowDistanceAndSpeed();
            BuyingWithoutRouteIsBlocked();
            MoneyIsPaidAtOnceAndGoodsTravel();
            GoodsArriveExactlyOnTime();
            LargeOrderArrivesTripByTrip();
            SeaCarriesMorePerTrip();
            AiCountsGoodsOnTheWay();
            ShipmentsAreListedSoonestFirst();
            DeliveryDoesNotAllocate();
        }

        private static void RoadNeedsLandConnection()
        {
            WorldState world = MakeWorld();

            _check("rota: sınır yoksa yol yok", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);

            CountryRoutes.SetLandNeighbors(world, 0, 1);
            _check("rota: sınır varsa karayolu", CountryRoutes.Route(_definition, world, 0, 1) == _road);
            _check("rota: kendine yol yok", CountryRoutes.Route(_definition, world, 0, 0) == CountryRoutes.NoRoute);
        }

        private static void RoadPassesThroughNeighbors()
        {
            WorldState world = MakeWorld();
            CountryRoutes.SetLandNeighbors(world, 0, 1);
            CountryRoutes.SetLandNeighbors(world, 1, 2);

            _check("rota: tarafsız ülkeden geçer", CountryRoutes.Route(_definition, world, 0, 2) == _road);
            _check("rota: bağlantısız ülkeye karayolu yok", CountryRoutes.Route(_definition, world, 0, 3) == CountryRoutes.NoRoute);
        }

        private static void RoadDoesNotPassEnemy()
        {
            WorldState world = MakeWorld();
            CountryRoutes.SetLandNeighbors(world, 0, 1);
            CountryRoutes.SetLandNeighbors(world, 1, 2);
            CountryRoutes.SetWar(world, 0, 1, true);

            _check("rota: düşman ülkeden geçilmez", CountryRoutes.Route(_definition, world, 0, 2) == CountryRoutes.NoRoute);

            CountryRoutes.SetWar(world, 0, 1, false);
            _check("rota: barış olunca yol açılır", CountryRoutes.Route(_definition, world, 0, 2) == _road);
        }

        private static void EnemyCannotBeTradedWith()
        {
            WorldState world = MakeWorld();
            CountryRoutes.SetLandNeighbors(world, 0, 1);
            AddTransportBuilding(world, 0, "port");
            AddTransportBuilding(world, 1, "port");
            CountryRoutes.SetWar(world, 0, 1, true);

            _check("rota: düşmandan alınmaz, limanı da olsa", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);
        }

        private static void SeaNeedsPortAtBothEnds()
        {
            WorldState world = MakeWorld();
            AddTransportBuilding(world, 0, "port");

            _check("rota: tek limanla deniz yok", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);

            AddTransportBuilding(world, 1, "port");
            _check("rota: iki limanla deniz", CountryRoutes.Route(_definition, world, 0, 1) == _sea);
        }

        private static void CheapestTransportIsChosen()
        {
            WorldState world = MakeWorld();
            CountryRoutes.SetLandNeighbors(world, 0, 1);
            AddTransportBuilding(world, 0, "airport");
            AddTransportBuilding(world, 1, "airport");

            _check("rota: karayolu havadan ucuz", CountryRoutes.Route(_definition, world, 0, 1) == _road);

            AddTransportBuilding(world, 0, "port");
            AddTransportBuilding(world, 1, "port");
            _check("rota: deniz karayolundan ucuz", CountryRoutes.Route(_definition, world, 0, 1) == _sea);
        }

        private static void PortUnderConstructionDoesNotCount()
        {
            WorldState world = MakeWorld();
            int slot = AddTransportBuilding(world, 0, "port");
            AddTransportBuilding(world, 1, "port");
            world.FacilityBuildDays[slot] = 2;
            world.RoutesDirty = true;

            _check("rota: inşaattaki liman sayılmaz", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("rota: inşaat bitmeden açılmaz", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("rota: inşaat bitince açılır", CountryRoutes.Route(_definition, world, 0, 1) == _sea);
        }

        private static void ClosingPortRemovesRoute()
        {
            WorldState world = MakeWorld();
            int slot = AddTransportBuilding(world, 0, "port");
            AddTransportBuilding(world, 1, "port");
            CountryRoutes.Route(_definition, world, 0, 1);

            FacilityCommands.TryClose(_definition, world, slot);

            _check("rota: liman kapanınca deniz kapanır", CountryRoutes.Route(_definition, world, 0, 1) == CountryRoutes.NoRoute);
        }

        private static void AirNeedsAirportsAndAvoidsEnemy()
        {
            WorldState world = MakeWorld();
            AddTransportBuilding(world, 0, "airport");
            AddTransportBuilding(world, 2, "airport");
            int count = world.CountryCount;
            world.AirPathCrossings = new bool[count * count * count];
            world.AirPathCrossings[(0 * count + 2) * count + 1] = true;
            world.RoutesDirty = true;

            _check("rota: iki havaalanıyla hava", CountryRoutes.Route(_definition, world, 0, 2) == _air);

            CountryRoutes.SetWar(world, 0, 1, true);
            _check("rota: düşman üstünden uçulmaz", CountryRoutes.Route(_definition, world, 0, 2) == CountryRoutes.NoRoute);

            CountryRoutes.SetWar(world, 0, 1, false);
            CountryRoutes.SetWar(world, 0, 3, true);
            _check("rota: rota dışındaki düşman engellemez", CountryRoutes.Route(_definition, world, 0, 2) == _air);
        }

        private static void FeeFollowsTransportCost()
        {
            WorldState road = MakeWorld();
            CountryRoutes.SetLandNeighbors(road, 0, 1);
            int roadFee = MarketCommands.RoadFeePermille(_definition, road, 0, 1);

            WorldState sea = MakeWorld();
            AddTransportBuilding(sea, 0, "port");
            AddTransportBuilding(sea, 1, "port");
            int seaFee = MarketCommands.RoadFeePermille(_definition, sea, 0, 1);

            WorldState air = MakeWorld();
            AddTransportBuilding(air, 0, "airport");
            AddTransportBuilding(air, 1, "airport");
            int airFee = MarketCommands.RoadFeePermille(_definition, air, 0, 1);

            _check("ücret: deniz karayolunun dörtte biri", seaFee == roadFee * _definition.TransportCostPermille[_sea] / Permille);
            _check("ücret: hava karayolunun beş katı", airFee == roadFee * _definition.TransportCostPermille[_air] / Permille);
            _check("ücret: yol yoksa sıfır", MarketCommands.RoadFeePermille(_definition, MakeWorld(), 0, 1) == 0);
        }

        private static void TransitDaysFollowDistanceAndSpeed()
        {
            WorldState world = MakeWorld();
            int count = world.CountryCount;
            world.CountryDistanceKm[0 * count + 1] = 777;
            int kmPerDay = _definition.Balance.RoadKmPerDay;
            int roadDays = (777 + kmPerDay - 1) / kmPerDay;

            _check("süre: karayolu mesafeden, yukarı yuvarlanır", CountryRoutes.TransitDays(_definition, world, 0, 1, _road) == roadDays);
            _check("süre: deniz iki kat yavaş", CountryRoutes.TransitDays(_definition, world, 0, 1, _sea) == (777 * 2 + kmPerDay - 1) / kmPerDay);
            _check("süre: hava beş kat hızlı", CountryRoutes.TransitDays(_definition, world, 0, 1, _air) == (777 + kmPerDay * 5 - 1) / (kmPerDay * 5));

            world.CountryDistanceKm[0 * count + 1] = 0;
            _check("süre: en az bir gün", CountryRoutes.TransitDays(_definition, world, 0, 1, _road) == 1);
        }

        private static void BuyingWithoutRouteIsBlocked()
        {
            WorldState world = MakeWorld();
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 100);

            _check("alım: yol yoksa olmaz", MarketCommands.CheckBuy(_definition, world, 0, 1, steel, 1 * Unit) == TradeBlock.NoRoute);
            _check("alım: yol yoksa para düşmez", !MarketCommands.TryBuy(_definition, world, 0, 1, steel, 1 * Unit) && world.Money[0] == StartMoney);
        }

        private static void MoneyIsPaidAtOnceAndGoodsTravel()
        {
            WorldState world = LinkedWorld();
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 100);
            long cost = MarketCommands.BuyCostMilli(_definition, world, 0, 1, steel, 10 * Unit);

            bool bought = MarketCommands.TryBuy(_definition, world, 0, 1, steel, 10 * Unit);

            _check("sevkiyat: alım yapılır", bought);
            _check("sevkiyat: para baştan ödenir", world.Money[0] == StartMoney - cost);
            _check("sevkiyat: satıcı parasını hemen alır", world.Money[1] == StartMoney + 10 * world.PriceMilli[steel]);
            _check("sevkiyat: mal hemen stoğa girmez", world.Stock[steel] == 0);
            _check("sevkiyat: mal yolda görünür", world.InTransitMilli[steel] == 10 * Unit);
            _check("sevkiyat: kayıt açılır", world.ShipmentCount == 1 && world.ShipmentList[0].Transport == _road);
        }

        private static void GoodsArriveExactlyOnTime()
        {
            WorldState world = LinkedWorld();
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 100);
            MarketCommands.TryBuy(_definition, world, 0, 1, steel, 10 * Unit);
            int days = CountryRoutes.TransitDays(_definition, world, 0, 1, _road);

            for (int day = 0; day < days; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("sevkiyat: süre dolmadan gelmez", world.Stock[steel] == 0 && world.InTransitMilli[steel] == 10 * Unit);

            EconomySimulator.AdvanceDay(_definition, world);

            _check("sevkiyat: süre dolunca stoğa girer", world.Stock[steel] == 10 * Unit);
            _check("sevkiyat: yolda kalmaz", world.InTransitMilli[steel] == 0 && world.ShipmentCount == 0);
        }

        private static void LargeOrderArrivesTripByTrip()
        {
            WorldState world = LinkedWorld();
            int steel = _definition.ResourceIndex("steel");
            long trip = _definition.TransportTripMilli[_road];
            long order = trip * 2 + trip / 2;
            world.OfferMilli[world.ResourceCount + steel] = order;
            MarketCommands.TryBuy(_definition, world, 0, 1, steel, order);
            int days = CountryRoutes.TransitDays(_definition, world, 0, 1, _road);

            for (int day = 0; day <= days; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("kapasite: ilk sefer kadar gelir", world.Stock[steel] == trip);
            _check("kapasite: kalanı yolda", world.InTransitMilli[steel] == order - trip);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("kapasite: ertesi gün bir sefer daha", world.Stock[steel] == trip * 2);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("kapasite: son sefer kalanı getirir", world.Stock[steel] == order && world.ShipmentCount == 0);
        }

        private static void SeaCarriesMorePerTrip()
        {
            _check("kapasite: deniz karayolunun on katı", _definition.TransportTripMilli[_sea] == _definition.TransportTripMilli[_road] * 10);
            _check("kapasite: hava karayolunun dörtte biri", _definition.TransportTripMilli[_air] == _definition.TransportTripMilli[_road] / 4);
        }

        private static void AiCountsGoodsOnTheWay()
        {
            WorldState world = LinkedWorld();
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[0] = false;
            world.Population[0] = 10 * EconomyDefinition.Million;
            world.Stock[food] = 100 * Unit;
            SetOffer(world, 1, food, 100000);

            EconomySimulator.AdvanceDay(_definition, world);

            long dailyFood = 10 * _definition.Balance.FoodMilliPerMillion;
            long target = dailyFood * _definition.Balance.MarketTargetDays;
            _check("AI: mal yola çıkar, hemen gelmez", world.Stock[food] == 100 * Unit - dailyFood);
            _check("AI: yoldaki hedefe tamamlar", world.Stock[food] + world.InTransitMilli[food] == target);

            int shipments = world.ShipmentCount;
            long moneyAfterFirstOrder = world.Money[0];
            EconomySimulator.AdvanceDay(_definition, world);
            _check("AI: yoldaki malı sayar, yeniden sipariş vermez", world.ShipmentCount == shipments && world.Money[0] == moneyAfterFirstOrder);
        }

        private static void ShipmentsAreListedSoonestFirst()
        {
            WorldState world = LinkedWorld();
            int steel = _definition.ResourceIndex("steel");
            int gold = _definition.ResourceIndex("gold");
            SetOffer(world, 2, steel, 100);
            SetOffer(world, 1, steel, 100);
            SetOffer(world, 1, gold, 100);

            MarketCommands.TryBuy(_definition, world, 0, 2, steel, 1 * Unit);
            MarketCommands.TryBuy(_definition, world, 0, 1, steel, 1 * Unit);
            MarketCommands.TryBuy(_definition, world, 0, 1, gold, 1 * Unit);

            var list = new List<Shipment>();
            Shipments.CollectFor(world, 0, steel, list);

            _check("yolda: yalnızca bu kaynak", list.Count == 2);
            _check("yolda: en önce gelen başta", list[0].Seller == 1 && list[1].Seller == 2);
            _check("yolda: ülkenin sevkiyat sayısı", Shipments.CountFor(world, 0) == 3 && Shipments.CountFor(world, 1) == 0);
        }

        private static void DeliveryDoesNotAllocate()
        {
            WorldState world = LinkedWorld();
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 1000);
            for (int i = 0; i < 20; i++)
            {
                MarketCommands.TryBuy(_definition, world, 0, 1, steel, 1 * Unit);
            }

            EconomySimulator.AdvanceDay(_definition, world);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int day = 0; day < 30; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("sevkiyat: teslimat bellek ayırmaz", GC.GetAllocatedBytesForCurrentThread() - before == 0);
            _check("sevkiyat: hepsi teslim edilir", world.ShipmentCount == 0 && world.Stock[steel] == 20 * Unit);
        }

        private static WorldState LinkedWorld()
        {
            WorldState world = MakeWorld();
            for (int country = 0; country + 1 < world.CountryCount; country++)
            {
                CountryRoutes.SetLandNeighbors(world, country, country + 1);
            }

            return world;
        }

        private static WorldState MakeWorld()
        {
            const int countries = 4;
            const int slotsPerCountry = 2;
            var world = new WorldState(countries, countries, countries * slotsPerCountry, _definition.ResourceCount);
            for (int country = 0; country < countries; country++)
            {
                world.CountryCityStart[country + 1] = country + 1;
                world.CountryFacilityStart[country + 1] = (country + 1) * slotsPerCountry;
                world.CitySlotStart[country + 1] = (country + 1) * slotsPerCountry;
                world.Money[country] = StartMoney;
                world.CountryManualTrade[country] = true;
                world.EnergyRatioPermille[country] = Permille;
                world.RandomState[country] = 1;
                world.Satisfaction[country] = _definition.Balance.BaseSatisfactionPermille;
                world.FacilityCity[country * slotsPerCountry] = country;
                world.FacilityCity[country * slotsPerCountry + 1] = country;
            }

            for (int resource = 0; resource < _definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = _definition.BasePriceMilli[resource];
            }

            CountryDistances.SetFromCapitals(world, CapitalLat, CapitalLon);
            return world;
        }

        private static int AddTransportBuilding(WorldState world, int country, string key)
        {
            int slot = world.CountryFacilityStart[country];
            if (world.FacilityBuilding[slot] != WorldState.NoBuilding)
            {
                slot++;
            }

            int building = _definition.BuildingIndex(key);
            world.FacilityBuilding[slot] = building;
            world.FacilityRecipe[slot] = WorldState.NoRecipe;
            world.FacilityLevel[slot] = 1;
            world.RoutesDirty = true;
            return slot;
        }

        private static void SetOffer(WorldState world, int country, int resource, long units)
        {
            world.OfferMilli[country * world.ResourceCount + resource] = units * Unit;
        }
    }
}
