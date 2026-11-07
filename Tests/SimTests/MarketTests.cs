using System;
using System.Collections.Generic;
using GeoWar.Game;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class MarketTests
    {
        private const long Unit = EconomyDefinition.MilliPerUnit;
        private const int Permille = EconomyDefinition.Permille;
        private const long StartMoney = 100000 * Unit;
        private const string UiFile = "Assets/Data/Resources/ui.json";

        // Üç başkent ekvatorda: 0-1 arası yaklaşık 1112 km, 0-2 arası yaklaşık 2224 km.
        private static readonly double[] CapitalLat = { 0, 0, 0 };
        private static readonly double[] CapitalLon = { 0, 10, 20 };

        private static EconomyDefinition _definition;
        private static Action<string, bool> _check;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            _check = check;
            _definition = definition;

            DistanceComesFromCapitals();
            RoadFeeFollowsDistance();
            ListingMovesStockToOfferWithoutMoney();
            ListingMoreThanStockDoesNothing();
            BuyingPaysSellerAndRoadFee();
            BuyingIsLimitedToTheChosenOffer();
            BuyingWithoutMoneyDoesNothing();
            OwnOfferCannotBeBought();
            ZeroAmountIsRejected();
            OffersAreListedNearestFirst();
            WithdrawReturnsTheOffer();
            TradeMovesPriceAtDayEnd();
            ManualCountryDoesNotTradeByItself();
            AiBuysFromNearestAndPaysSeller();
            AiListsSurplusAsOffer();
            AiTakesBackOwnOfferBeforeBuying();
            UnmetDemandRaisesPrice();
            StarvingAiBuysForRealNeed();
            FamineRaisesPriceOverTime();
            FactoryShortageCountsAsNeed();
            PlayerOfferIsSoldToAi();
            DaysLeftFollowsNet();
            RunsOutWithinWarnsBeforeStockEnds();
            ShrinkingListIsSortedBySoonestEnd();
            TotalValueUsesMarketPrice();
            MarketAmountsAreListedInUiData();
        }

        private static void DistanceComesFromCapitals()
        {
            WorldState world = MakeWorld(3);

            _check("mesafe: ekvatorda 10 derece", Math.Abs(world.CountryDistanceKm[1] - 1112) <= 1);
            _check("mesafe: simetrik", world.CountryDistanceKm[1] == world.CountryDistanceKm[3]);
            _check("mesafe: ülke kendine sıfır", world.CountryDistanceKm[0] == 0);
            _check("mesafe: en yakın önce", world.NearestCountries[0] == 0 && world.NearestCountries[1] == 1 && world.NearestCountries[2] == 2);
            _check("mesafe: ortadaki ülkede eşit mesafe ülke sırasıyla", world.NearestCountries[3] == 1 && world.NearestCountries[4] == 0 && world.NearestCountries[5] == 2);
        }

        private static void RoadFeeFollowsDistance()
        {
            WorldState world = MakeWorld(3);
            BalanceData balance = _definition.Balance;
            int steel = _definition.ResourceIndex("steel");
            int near = MarketCommands.RoadFeePermille(_definition, world, 0, 1);
            int far = MarketCommands.RoadFeePermille(_definition, world, 0, 2);

            _check("yol ücreti: mesafeyle orantılı", near == world.CountryDistanceKm[1] * balance.RoadFeePermillePerStep / balance.RoadFeeStepKm);
            _check("yol ücreti: uzak ülke pahalı", far > near);
            _check("yol ücreti: birim fiyata eklenir",
                MarketCommands.UnitCostMilli(_definition, world, 0, 1, steel) == world.PriceMilli[steel] + world.PriceMilli[steel] * near / Permille);
        }

        private static void ListingMovesStockToOfferWithoutMoney()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            SetStock(world, 0, steel, 30);

            bool listed = MarketCommands.TryList(world, 0, steel, 10 * Unit);

            _check("teklif: pazara çıkar", listed);
            _check("teklif: stoktan düşer", Stock(world, 0, steel) == 20 * Unit);
            _check("teklif: pazarda görünür", MarketCommands.OfferMilli(world, 0, steel) == 10 * Unit);
            _check("teklif: para hemen gelmez", world.Money[0] == StartMoney);
            _check("teklif: arza yazılır", world.MarketSupply[steel] == 10 * Unit);
        }

        private static void ListingMoreThanStockDoesNothing()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            SetStock(world, 0, steel, 5);

            _check("teklif: stok yetmezse çıkmaz", !MarketCommands.TryList(world, 0, steel, 10 * Unit));
            _check("teklif: stok yetmezse sebep yazılır", MarketCommands.CheckList(world, 0, steel, 10 * Unit) == TradeBlock.NoStock);
            _check("teklif: stok yetmezse değişmez", Stock(world, 0, steel) == 5 * Unit && MarketCommands.OfferMilli(world, 0, steel) == 0);
        }

        private static void BuyingPaysSellerAndRoadFee()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 30);
            long cost = MarketCommands.BuyCostMilli(_definition, world, 0, 1, steel, 10 * Unit);

            bool bought = MarketCommands.TryBuy(_definition, world, 0, 1, steel, 10 * Unit);

            _check("alım: yapılır", bought);
            _check("alım: yol ücreti fiyatın üstünde", cost > 10 * world.PriceMilli[steel]);
            _check("alım: alıcı yol ücretiyle öder", world.Money[0] == StartMoney - cost);
            _check("alım: satıcıya piyasa fiyatı gider", world.Money[1] == StartMoney + 10 * world.PriceMilli[steel]);
            _check("alım: yola çıkar", world.InTransitMilli[steel] == 10 * Unit);
            _check("alım: tekliften düşer", MarketCommands.OfferMilli(world, 1, steel) == 20 * Unit);
            _check("alım: talebe yazılır", world.MarketDemand[steel] == 10 * Unit);
        }

        private static void BuyingIsLimitedToTheChosenOffer()
        {
            WorldState world = MakeWorld(3);
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 1, steel, 5);
            SetOffer(world, 2, steel, 100);

            _check("alım: teklif yetmezse olmaz", !MarketCommands.TryBuy(_definition, world, 0, 1, steel, 10 * Unit));
            _check("alım: teklif yetmezse sebep yazılır", MarketCommands.CheckBuy(_definition, world, 0, 1, steel, 10 * Unit) == TradeBlock.NoOffer);
            _check("alım: başka teklifle tamamlanmaz", MarketCommands.OfferMilli(world, 2, steel) == 100 * Unit && world.InTransitMilli[steel] == 0);
            _check("alım: seçilen tekliften alınır", MarketCommands.TryBuy(_definition, world, 0, 2, steel, 10 * Unit) && MarketCommands.OfferMilli(world, 1, steel) == 5 * Unit);
        }

        private static void BuyingWithoutMoneyDoesNothing()
        {
            WorldState world = MakeWorld(2);
            int machine = _definition.ResourceIndex("machine");
            SetOffer(world, 1, machine, 5);
            world.Money[0] = 1 * Unit;

            _check("alım: para yetmezse olmaz", !MarketCommands.TryBuy(_definition, world, 0, 1, machine, 1 * Unit));
            _check("alım: para yetmezse sebep yazılır", MarketCommands.CheckBuy(_definition, world, 0, 1, machine, 1 * Unit) == TradeBlock.NoMoney);
            _check("alım: para yetmezse değişmez", world.Money[0] == 1 * Unit && Stock(world, 0, machine) == 0 && MarketCommands.OfferMilli(world, 1, machine) == 5 * Unit);
        }

        private static void OwnOfferCannotBeBought()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            SetOffer(world, 0, steel, 5);

            _check("alım: kendi teklifinden olmaz", MarketCommands.CheckBuy(_definition, world, 0, 0, steel, 1 * Unit) == TradeBlock.OwnOffer);
        }

        private static void ZeroAmountIsRejected()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");

            _check("pazar: sıfır alınmaz", MarketCommands.CheckBuy(_definition, world, 0, 1, steel, 0) == TradeBlock.NoAmount);
            _check("pazar: sıfır satılmaz", !MarketCommands.TryList(world, 0, steel, 0));
        }

        private static void OffersAreListedNearestFirst()
        {
            WorldState world = MakeWorld(3);
            int steel = _definition.ResourceIndex("steel");
            int gold = _definition.ResourceIndex("gold");
            SetOffer(world, 2, steel, 5);
            SetOffer(world, 1, steel, 5);
            SetOffer(world, 0, steel, 5);
            var sellers = new List<int>();

            MarketCommands.CollectOffers(world, 0, steel, sellers);
            _check("teklif listesi: en yakın başta, kendi teklifi yok", sellers.Count == 2 && sellers[0] == 1 && sellers[1] == 2);
            _check("teklif listesi: toplama kendi teklifi girmez", MarketCommands.OthersOfferMilli(world, 0, steel) == 10 * Unit);

            MarketCommands.CollectOffers(world, 0, gold, sellers);
            _check("teklif listesi: teklif yoksa boş", sellers.Count == 0);
        }

        private static void WithdrawReturnsTheOffer()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            SetStock(world, 0, steel, 30);
            MarketCommands.TryList(world, 0, steel, 10 * Unit);

            _check("geri çekme: yapılır", MarketCommands.TryWithdraw(world, 0, steel));
            _check("geri çekme: stok döner", Stock(world, 0, steel) == 30 * Unit);
            _check("geri çekme: teklif kalmaz", MarketCommands.OfferMilli(world, 0, steel) == 0);
            _check("geri çekme: teklif yoksa olmaz", !MarketCommands.TryWithdraw(world, 0, steel));
        }

        private static void TradeMovesPriceAtDayEnd()
        {
            WorldState world = MakeWorld(2);
            int steel = _definition.ResourceIndex("steel");
            int cement = _definition.ResourceIndex("cement");
            world.CountryManualTrade[0] = true;
            world.CountryManualTrade[1] = true;
            SetOffer(world, 1, steel, 100);
            SetStock(world, 0, cement, 100);
            long steelPrice = world.PriceMilli[steel];
            long cementPrice = world.PriceMilli[cement];

            MarketCommands.TryBuy(_definition, world, 0, 1, steel, 10 * Unit);
            MarketCommands.TryList(world, 0, cement, 10 * Unit);

            _check("pazar: fiyat gün içinde değişmez", world.PriceMilli[steel] == steelPrice && world.PriceMilli[cement] == cementPrice);

            EconomySimulator.AdvanceDay(_definition, world);

            _check("pazar: alınan kaynak pahalanır", world.PriceMilli[steel] > steelPrice);
            _check("pazar: pazara çıkan kaynak ucuzlar", world.PriceMilli[cement] < cementPrice);
        }

        private static void ManualCountryDoesNotTradeByItself()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[0] = true;
            world.Population[0] = 10 * EconomyDefinition.Million;
            SetStock(world, 0, food, 1000);
            SetOffer(world, 1, food, 100000);

            EconomySimulator.AdvanceDay(_definition, world);

            long dailyFood = 10 * _definition.Balance.FoodMilliPerMillion;
            _check("pazar: oyuncu kendiliğinden almaz", world.Money[0] == StartMoney);
            _check("pazar: oyuncunun stoğu sadece tüketimle düşer", Stock(world, 0, food) == 1000 * Unit - dailyFood);

            // Hedefin çok üstündeki stok da oyuncuda kendiliğinden pazara çıkmaz.
            SetStock(world, 0, food, 1000000);
            EconomySimulator.AdvanceDay(_definition, world);
            _check("pazar: oyuncu kendiliğinden satmaz", MarketCommands.OfferMilli(world, 0, food) == 0);
        }

        private static void AiBuysFromNearestAndPaysSeller()
        {
            WorldState world = MakeWorld(3);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[1] = true;
            world.CountryManualTrade[2] = true;
            world.Population[0] = 10 * EconomyDefinition.Million;
            SetStock(world, 0, food, 100);
            SetOffer(world, 1, food, 50);
            SetOffer(world, 2, food, 100000);
            long price = world.PriceMilli[food];

            EconomySimulator.AdvanceDay(_definition, world);

            long dailyFood = 10 * _definition.Balance.FoodMilliPerMillion;
            long target = dailyFood * _definition.Balance.MarketTargetDays;
            long boughtFromFar = target - (100 * Unit - dailyFood) - 50 * Unit;

            _check("AI: en yakın teklifi bitirir", MarketCommands.OfferMilli(world, 1, food) == 0);
            _check("AI: sonra uzak tekliften alır", MarketCommands.OfferMilli(world, 2, food) == 100000 * Unit - boughtFromFar);
            _check("AI: yoldakiyle hedefe ulaşır", Stock(world, 0, food) + world.InTransitMilli[food] == target);
            _check("AI: yakın satıcıya para gider", world.Money[1] == StartMoney + 50 * price);
            _check("AI: uzak satıcıya para gider", world.Money[2] == StartMoney + boughtFromFar * price / Unit);
            _check("AI: yol ücreti öder", StartMoney - world.Money[0] > 50 * price + boughtFromFar * price / Unit);
        }

        private static void AiListsSurplusAsOffer()
        {
            WorldState world = MakeWorld(2);
            int boron = _definition.ResourceIndex("boron");
            SetStock(world, 0, boron, 400);

            EconomySimulator.AdvanceDay(_definition, world);

            // Hiç tüketilmeyen kaynağın fazlası da satılır: bor gibi.
            long listed = 400 * Unit / _definition.Balance.MarketSellDivisor;
            _check("AI: fazlasını pazara koyar", MarketCommands.OfferMilli(world, 0, boron) == listed);
            _check("AI: stoğundan düşer", Stock(world, 0, boron) == 400 * Unit - listed);
            _check("AI: para hemen gelmez", world.Money[0] == StartMoney);
        }

        private static void AiTakesBackOwnOfferBeforeBuying()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[1] = true;
            world.Population[0] = 10 * EconomyDefinition.Million;
            SetStock(world, 0, food, 100);
            SetOffer(world, 0, food, 100000);
            SetOffer(world, 1, food, 100000);

            EconomySimulator.AdvanceDay(_definition, world);

            _check("AI: önce kendi teklifini geri alır", world.Money[0] == StartMoney && MarketCommands.OfferMilli(world, 1, food) == 100000 * Unit);
            _check("AI: geri alınca hedefe ulaşır", Stock(world, 0, food) == 10 * _definition.Balance.FoodMilliPerMillion * _definition.Balance.MarketTargetDays);
        }

        private static void UnmetDemandRaisesPrice()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[1] = true;
            world.Population[0] = 10 * EconomyDefinition.Million;
            SetStock(world, 0, food, 100);
            long price = world.PriceMilli[food];

            EconomySimulator.AdvanceDay(_definition, world);

            _check("AI: teklif yoksa alamaz", Stock(world, 0, food) < 100 * Unit && world.Money[0] == StartMoney);
            _check("pazar: karşılanamayan talep fiyatı yükseltir", world.PriceMilli[food] > price);
        }

        // Yiyemediği gıda da ihtiyaçtır: yalnızca yiyebildiği kadar hedef koyarsa kıtlıkta talep kaybolur.
        private static void StarvingAiBuysForRealNeed()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[1] = true;
            world.Population[0] = 10 * EconomyDefinition.Million;
            SetStock(world, 0, food, 1);
            SetOffer(world, 1, food, 100000);

            EconomySimulator.AdvanceDay(_definition, world);

            long need = 10 * _definition.Balance.FoodMilliPerMillion;
            _check("kıtlık: karşılanamayan ihtiyaç yazılır", world.DailyShortage[food] == need - 1 * Unit);
            _check("kıtlık: AI gerçek ihtiyacına göre alır", world.InTransitMilli[food] == need * _definition.Balance.MarketTargetDays);
        }

        private static void FamineRaisesPriceOverTime()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.Population[0] = 10 * EconomyDefinition.Million;
            world.Population[1] = 10 * EconomyDefinition.Million;
            long price = world.PriceMilli[food];

            for (int day = 0; day < 30; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("kıtlık: hiç gıda yokken fiyat yükselir", world.PriceMilli[food] > price);
        }

        private static void FactoryShortageCountsAsNeed()
        {
            WorldState world = new WorldState(1, 1, 1, _definition.ResourceCount);
            world.CountryCityStart[1] = 1;
            world.CountryFacilityStart[1] = 1;
            world.CitySlotStart[1] = 1;
            world.Satisfaction[0] = _definition.Balance.BaseSatisfactionPermille;
            world.GridCapacityMilli[0] = 1000 * Unit;
            world.EnergyRatioPermille[0] = Permille;
            world.RandomState[0] = 1;

            // Güneşsiz kurulumda enerji sıfır olur; girdi eksiğini yalnızca çıkarma dışı bir tesisle ölçmek için santral kullanılır.
            int recipe = _definition.RecipeIndex("coal_plant");
            int coal = _definition.ResourceIndex("coal");
            world.FacilityRecipe[0] = recipe;
            world.FacilityBuilding[0] = _definition.RecipeBuilding[recipe];
            world.FacilityLevel[0] = 1;

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            _check("kıtlık: girdisi eksik tesis ihtiyaç yazar", world.DailyShortage[coal] == _definition.InputMilliPerDay[_definition.InputStart[recipe]]);
            _check("kıtlık: yeni gün eksiği sıfırlar", ShortageResets(world, coal));
        }

        private static bool ShortageResets(WorldState world, int coal)
        {
            world.Stock[coal] = 100 * Unit;
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            return world.DailyShortage[coal] == 0;
        }

        private static void PlayerOfferIsSoldToAi()
        {
            WorldState world = MakeWorld(2);
            int food = _definition.ResourceIndex("food");
            world.CountryManualTrade[0] = true;
            world.Population[1] = 10 * EconomyDefinition.Million;
            SetStock(world, 1, food, 100);
            SetStock(world, 0, food, 100000);
            MarketCommands.TryList(world, 0, food, 50000 * Unit);
            long moneyBeforeSale = world.Money[0];

            EconomySimulator.AdvanceDay(_definition, world);

            _check("teklif: alıcı çıkınca para gelir", world.Money[0] > moneyBeforeSale);
            _check("teklif: alıcı çıkınca azalır", MarketCommands.OfferMilli(world, 0, food) < 50000 * Unit);
        }

        private static void DaysLeftFollowsNet()
        {
            WorldState world = MakeWorld(1);
            int steel = _definition.ResourceIndex("steel");
            SetStock(world, 0, steel, 100);

            world.DailyConsumed[steel] = 10 * Unit;
            _check("stok: azalıyorsa kaç gün yeter", StockReport.DaysLeft(world, 0, steel) == 10);
            _check("stok: net değişim", StockReport.NetMilli(world, 0, steel) == -10 * Unit);

            world.DailyProduced[steel] = 10 * Unit;
            _check("stok: dengedeyse bitmez", StockReport.DaysLeft(world, 0, steel) == StockReport.NeverEnds);

            world.DailyProduced[steel] = 20 * Unit;
            _check("stok: artıyorsa bitmez", StockReport.DaysLeft(world, 0, steel) == StockReport.NeverEnds);
        }

        private static void RunsOutWithinWarnsBeforeStockEnds()
        {
            const int warningDays = 90;
            WorldState world = MakeWorld(1);
            int steel = _definition.ResourceIndex("steel");

            _check("uyarı: stok da tüketim de yoksa çıkmaz", !StockReport.RunsOutWithin(world, 0, steel, warningDays));

            SetStock(world, 0, steel, 10);
            _check("uyarı: tüketim yoksa az stokta da çıkmaz", !StockReport.RunsOutWithin(world, 0, steel, warningDays));

            SetStock(world, 0, steel, 90);
            world.DailyConsumed[steel] = 1 * Unit;
            _check("uyarı: süre içinde bitecekse çıkar", StockReport.RunsOutWithin(world, 0, steel, warningDays));

            SetStock(world, 0, steel, 91);
            _check("uyarı: süreden uzun yetiyorsa çıkmaz", !StockReport.RunsOutWithin(world, 0, steel, warningDays));

            SetStock(world, 0, steel, 10);
            world.DailyProduced[steel] = 1 * Unit;
            _check("uyarı: stok azalmıyorsa çıkmaz", !StockReport.RunsOutWithin(world, 0, steel, warningDays));

            SetStock(world, 0, steel, 0);
            world.DailyProduced[steel] = 0;
            world.DailyConsumed[steel] = 0;
            world.DailyShortage[steel] = 1 * Unit;
            _check("uyarı: stok bitip tüketim durduysa çıkmaz", !StockReport.RunsOutWithin(world, 0, steel, warningDays));
        }

        private static void ShrinkingListIsSortedBySoonestEnd()
        {
            WorldState world = MakeWorld(1);
            int steel = _definition.ResourceIndex("steel");
            int food = _definition.ResourceIndex("food");
            int gold = _definition.ResourceIndex("gold");

            SetStock(world, 0, steel, 100);
            world.DailyConsumed[steel] = 10 * Unit;
            SetStock(world, 0, food, 100);
            world.DailyConsumed[food] = 50 * Unit;
            SetStock(world, 0, gold, 100);
            world.DailyProduced[gold] = 1 * Unit;

            var list = new List<int>();
            StockReport.CollectShrinking(world, 0, list);

            _check("stok: yalnızca azalanlar listelenir", list.Count == 2 && !list.Contains(gold));
            _check("stok: en önce biten başta", list[0] == food && list[1] == steel);
        }

        private static void TotalValueUsesMarketPrice()
        {
            WorldState world = MakeWorld(1);
            int steel = _definition.ResourceIndex("steel");
            int gold = _definition.ResourceIndex("gold");
            SetStock(world, 0, steel, 10);
            SetStock(world, 0, gold, 2);

            long expected = 10 * world.PriceMilli[steel] + 2 * world.PriceMilli[gold];
            _check("stok: toplam değer piyasa fiyatıyla", StockReport.TotalValueMilli(world, 0) == expected);
        }

        private static void MarketAmountsAreListedInUiData()
        {
            string directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Combine(directory, UiFile)))
            {
                directory = System.IO.Path.GetDirectoryName(directory);
            }

            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(directory, UiFile));
            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            UiData ui = System.Text.Json.JsonSerializer.Deserialize<UiData>(json, options);

            bool ascending = ui.MarketAmounts != null && ui.MarketAmounts.Count > 0 && ui.MarketAmounts[0] > 0;
            for (int i = 1; ascending && i < ui.MarketAmounts.Count; i++)
            {
                ascending = ui.MarketAmounts[i] > ui.MarketAmounts[i - 1];
            }

            _check("pazar: miktar düğmeleri veride ve artan sırada", ascending);
            _check("pazar: uyarı süresi veride", ui.MarketWarningDays > 0);
        }

        private static WorldState MakeWorld(int countries)
        {
            var world = new WorldState(countries, countries, 0, _definition.ResourceCount);
            for (int country = 0; country < countries; country++)
            {
                world.CountryCityStart[country + 1] = country + 1;
                world.Money[country] = StartMoney;
                world.EnergyRatioPermille[country] = Permille;
                world.RandomState[country] = 1;
                world.Satisfaction[country] = _definition.Balance.BaseSatisfactionPermille;
            }

            for (int resource = 0; resource < _definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = _definition.BasePriceMilli[resource];
            }

            CountryDistances.SetFromCapitals(world, CapitalLat, CapitalLon);
            for (int country = 0; country + 1 < countries; country++)
            {
                CountryRoutes.SetLandNeighbors(world, country, country + 1);
            }

            return world;
        }

        private static void SetStock(WorldState world, int country, int resource, long units)
        {
            world.Stock[country * world.ResourceCount + resource] = units * Unit;
        }

        private static long Stock(WorldState world, int country, int resource)
        {
            return world.Stock[country * world.ResourceCount + resource];
        }

        private static void SetOffer(WorldState world, int country, int resource, long units)
        {
            world.OfferMilli[country * world.ResourceCount + resource] = units * Unit;
        }
    }
}
