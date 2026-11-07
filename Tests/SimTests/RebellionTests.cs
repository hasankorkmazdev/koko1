using System;
using System.Collections.Generic;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    // İsyan ve şehir sahipliği: docs/MECHANICS.md 10 ve 11.
    public static class RebellionTests
    {
        private const int Player = 0;
        private const int Ai = 1;
        private const int Permille = EconomyDefinition.Permille;
        private const int MaxTax = 100;
        private const long LargeStockUnits = 1000000;
        private const long LargeMoneyMilli = 1000000000000;

        private static EconomyDefinition _definition;
        private static BalanceData _balance;
        private static Action<string, bool> _check;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            _check = check;
            _definition = definition;
            _balance = definition.Balance;

            ListsStartInOrder();
            HappyCitiesStayCalm();
            LowSatisfactionStartsLimitedRebellions();
            CalmCityDoesNotRebel();
            RebellionStopsProductionAndPressesNeighbors();
            ConcessionCutsTaxAndProduction();
            AidCostsMoneyAndRaisesSatisfaction();
            AnswerAllIsOneChoice();
            AiAnswersAtOnce();
            MissedDeadlineFoundsRebelState();
            SecondCityJoinsSameRebelState();
            RebelStateWorksAsCountry();
            RebelStateDoesNotBlockOwnFlights();
            NoEmptySlotKeepsCity();
            LosingEveryCityDoesNotBreakTheDay();
            ResultDoesNotDependOnSplitting();
            CityStopsHoldingDaysAfterHandover();
        }

        private static void ListsStartInOrder()
        {
            WorldState world = MakeWorld(4, 3);
            _check("sahiplik: isyancı yerleri ülke sayısına eklenir", world.BaseCountryCount == 2 && world.CountryCount == 2 + _balance.RebelStateSlots);
            _check("sahiplik: şehirler ülkelerine ait", world.CityOwner[3] == Player && world.CityOwner[4] == Ai);
            _check("sahiplik: boş yerin şehri yok", CountryReport.CityCount(world, 2) == 0);
            _check("sahiplik: başta liste sıralı", world.CountryCities[5] == 5 && world.CountrySlots[7] == 7);

            CityOwnership.RebuildCountryLists(world);
            bool same = world.CountryCityStart[1] == 4 && world.CountryCityStart[2] == 7 && world.CountryFacilityStart[2] == world.FacilityCount;
            _check("sahiplik: yeniden kurmak değiştirmez", same && world.CountryCities[5] == 5 && world.CountrySlots[7] == 7);
        }

        private static void HappyCitiesStayCalm()
        {
            WorldState world = MakeWorld(5, 5);
            AdvanceDays(world, 60);
            _check("tetik: memnun şehirde isyan çıkmaz", world.CountryRebellionsStarted[Player] == 0 && world.CountryRebellionsStarted[Ai] == 0);

            world = MakeWorld(5, 5);
            for (int city = 0; city < world.CityCount; city++)
            {
                world.Satisfaction[city] = _balance.RebellionThresholdPermille;
            }

            ulong random = world.RandomState[Player];
            Rebellions.AdvanceDay(_definition, world);
            _check("tetik: eşiğin üstünde zar atılmaz", world.CountryRebellionsStarted[Player] == 0 && world.RandomState[Player] == random);
        }

        private static void LowSatisfactionStartsLimitedRebellions()
        {
            WorldState world = MakeWorld(30, 3);
            world.TaxPercent[Player] = MaxTax;

            bool withinLimit = true;
            for (int day = 0; day < 40; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
                withinLimit &= world.CountryRebellionCount[Player] <= _balance.MaxRebellionsPerCountry;
            }

            _check("tetik: düşük memnuniyette isyan çıkar", world.CountryRebellionsStarted[Player] > 0);
            _check("tetik: aynı anda süren isyan sınırlı", withinLimit);
            _check("tetik: bütün ülke birden isyan etmez", world.CountryRebellionsStarted[Player] < 30);
        }

        private static void CalmCityDoesNotRebel()
        {
            WorldState world = MakeWorld(10, 2);
            world.TaxPercent[Player] = MaxTax;
            for (int city = 0; city < 10; city++)
            {
                world.CityCalmDays[city] = 200;
            }

            AdvanceDays(world, 100);
            _check("koruma: korunan şehir isyan etmez", world.CountryRebellionsStarted[Player] == 0);
            _check("koruma: süre işler", world.CityCalmDays[0] == 100);
        }

        private static void RebellionStopsProductionAndPressesNeighbors()
        {
            WorldState world = MakeWorld(5, 2);
            int rebelSlot = PlaceMine(world, 2);
            int otherSlot = PlaceMine(world, 3);

            Rebellions.Start(_definition, world, Player, 2);
            _check("isyan: süre başlar", world.CityRebellionDays[2] == _balance.RebellionDeadlineDays);
            _check("isyan: sayılır", world.CountryRebellionCount[Player] == 1 && world.CountryRebellionsStarted[Player] == 1);
            _check("isyan: komşular etkilenir", world.CityRebelNeighbors[1] == 1 && world.CityRebelNeighbors[3] == 1);
            _check("isyan: uzak şehir etkilenmez", world.CityRebelNeighbors[0] == 0 && world.CityRebelNeighbors[4] == 0);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("isyan: üretim durur", world.FacilityOutputSpeedPermille[rebelSlot] == 0 && world.FacilityAlarm[rebelSlot] == WorldState.AlarmStopped);
            _check("isyan: başka şehir çalışır", world.FacilityOutputSpeedPermille[otherSlot] == Permille);
            _check("isyan: süre işler", world.CityRebellionDays[2] == _balance.RebellionDeadlineDays - 1);

            AdvanceDays(world, 9);
            _check("isyan: komşunun memnuniyeti düşer", world.Satisfaction[1] < world.Satisfaction[0]);

            Rebellions.TryAnswer(_definition, world, 2, RebellionResponse.Aid);
            _check("isyan: bitince komşu baskısı kalkar", world.CityRebelNeighbors[1] == 0 && world.CityRebelNeighbors[3] == 0);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("isyan: bitince üretim döner", world.FacilityOutputSpeedPermille[rebelSlot] == Permille && world.FacilityAlarm[rebelSlot] == WorldState.AlarmNone);
        }

        private static void ConcessionCutsTaxAndProduction()
        {
            WorldState world = MakeWorld(1, 1);
            int slot = PlaceMine(world, 0);

            EconomySimulator.AdvanceCountry(_definition, world, Player);
            int normalSpeed = world.FacilityOutputSpeedPermille[slot];

            long money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);
            _check("taviz: öncesinde vergi gelir", world.Money[Player] > money);

            Rebellions.Start(_definition, world, Player, 0);
            _check("taviz: isyan yokken verilmez", !Rebellions.TryAnswer(_definition, world, 1, RebellionResponse.Concession));
            _check("taviz: verilir", Rebellions.TryAnswer(_definition, world, 0, RebellionResponse.Concession));
            _check("taviz: isyan biter", world.CityRebellionDays[0] == 0 && world.CountryRebellionCount[Player] == 0);
            _check("taviz: süresi işler", world.CityConcessionDays[0] == _balance.ConcessionDays);
            _check("taviz: sonrasında da korunur", world.CityCalmDays[0] == _balance.ConcessionDays + _balance.RebellionCalmDays);

            money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);
            _check("taviz: vergi sıfır", world.Money[Player] == money);
            _check("taviz: üretim yarıya iner", world.FacilityOutputSpeedPermille[slot] == normalSpeed * _balance.ConcessionProductionPermille / Permille);

            WorldState two = MakeWorld(4, 1);
            two.TaxPercent[Player] = MaxTax;
            two.CityConcessionDays[0] = _balance.ConcessionDays;
            AdvanceDays(two, 15);
            _check("taviz: vergi cezası kalkar", two.Satisfaction[0] > two.Satisfaction[3]);
        }

        private static void AidCostsMoneyAndRaisesSatisfaction()
        {
            WorldState world = MakeWorld(3, 1);
            world.Satisfaction[1] = 100;
            Rebellions.Start(_definition, world, Player, 1);

            long cost = Rebellions.AidCostMilli(_definition, world, 1);
            _check("yardım: bedel nüfusla büyür", cost == world.Population[1] * _balance.AidCostMilliPerMillion / EconomyDefinition.Million && cost > 0);

            world.Money[Player] = cost - 1;
            _check("yardım: para yetmezse olmaz", !Rebellions.TryAnswer(_definition, world, 1, RebellionResponse.Aid) && world.CityRebellionDays[1] > 0);

            world.Money[Player] = cost + 5;
            _check("yardım: yapılır", Rebellions.TryAnswer(_definition, world, 1, RebellionResponse.Aid));
            _check("yardım: para düşer", world.Money[Player] == 5);
            _check("yardım: memnuniyet yükselir", world.Satisfaction[1] == 100 + _balance.AidSatisfactionGainPermille);
            _check("yardım: isyan biter ve şehir korunur", world.CityRebellionDays[1] == 0 && world.CityCalmDays[1] == _balance.RebellionCalmDays);
            _check("yardım: taviz başlamaz", world.CityConcessionDays[1] == 0);
        }

        private static void AnswerAllIsOneChoice()
        {
            WorldState world = MakeWorld(6, 1);
            Rebellions.Start(_definition, world, Player, 0);
            Rebellions.Start(_definition, world, Player, 2);
            Rebellions.Start(_definition, world, Player, 4);
            world.CityRebellionDays[0] = 10;
            world.CityRebellionDays[2] = 5;
            world.CityRebellionDays[4] = 1;

            var cities = new List<int>();
            Rebellions.CollectCities(world, Player, cities);
            _check("toplu: süresi az kalan önce", cities.Count == 3 && cities[0] == 4 && cities[1] == 2 && cities[2] == 0);

            long total = Rebellions.TotalAidCostMilli(_definition, world, Player);
            _check("toplu: bedel toplanır", total == Rebellions.AidCostMilli(_definition, world, 0) + Rebellions.AidCostMilli(_definition, world, 2) + Rebellions.AidCostMilli(_definition, world, 4));

            world.Money[Player] = total - 1;
            _check("toplu: para hepsine yetmezse hiçbiri olmaz", Rebellions.TryAnswerAll(_definition, world, Player, RebellionResponse.Aid) == 0 && world.CountryRebellionCount[Player] == 3 && world.Money[Player] == total - 1);

            world.Money[Player] = total;
            _check("toplu: hepsine yardım", Rebellions.TryAnswerAll(_definition, world, Player, RebellionResponse.Aid) == 3 && world.Money[Player] == 0);
            _check("toplu: hepsi biter", world.CountryRebellionCount[Player] == 0 && world.CityRebelNeighbors[1] == 0 && world.CityRebelNeighbors[3] == 0);

            Rebellions.Start(_definition, world, Player, 1);
            Rebellions.Start(_definition, world, Player, 3);
            _check("toplu: hepsine taviz", Rebellions.TryAnswerAll(_definition, world, Player, RebellionResponse.Concession) == 2 && world.CityConcessionDays[1] > 0 && world.CityConcessionDays[3] > 0);
        }

        private static void AiAnswersAtOnce()
        {
            WorldState world = MakeWorld(2, 3);
            int city = 3;
            long cost = Rebellions.AidCostMilli(_definition, world, city);
            long money = world.Money[Ai];
            Rebellions.Start(_definition, world, Ai, city);
            _check("AI: parası varsa yardım", world.Money[Ai] == money - cost && world.CityConcessionDays[city] == 0);
            _check("AI: isyan hemen biter", world.CityRebellionDays[city] == 0 && world.CountryRebellionCount[Ai] == 0);

            world.Money[Ai] = 0;
            Rebellions.Start(_definition, world, Ai, 4);
            _check("AI: parası yoksa taviz", world.Money[Ai] == 0 && world.CityConcessionDays[4] == _balance.ConcessionDays);
            _check("AI: şehir kaybetmez", world.CityOwner[3] == Ai && world.CityOwner[4] == Ai);
        }

        private static void MissedDeadlineFoundsRebelState()
        {
            WorldState world = MakeWorld(5, 2);
            int city = 2;
            int rebel = world.BaseCountryCount;
            int iron = _definition.ResourceIndex("iron");
            long stock = world.Stock[Player * world.ResourceCount + iron];
            long countryPopulation = world.CountryPopulation[Player];
            long people = world.Population[city];
            long grid = world.GridCapacityMilli[Player];
            int slotsOfCity = FacilityCommands.SlotCount(world, city);
            int firstSlot = world.CitySlotStart[city];
            world.Satisfaction[city] = 500;

            Rebellions.Start(_definition, world, Player, city);
            world.CityRebellionDays[city] = 1;
            Rebellions.AdvanceDay(_definition, world);

            _check("devlet: şehir elden çıkar", world.CityOwner[city] == rebel);
            _check("devlet: boş yere kurulur", world.RebelParent[rebel] == Player && world.RebelStateOf[Player] == rebel && world.RebelCapital[rebel] == city);
            _check("devlet: isyan kapanır", world.CityRebellionDays[city] == 0 && world.CountryRebellionCount[Player] == 0 && world.CityRebelNeighbors[1] == 0);
            _check("devlet: kayıp sayılır", world.CountryCitiesLost[Player] == 1 && world.CountryLastLostCity[Player] == city);
            _check("devlet: şehir listeleri güncellenir", CountryReport.CityCount(world, Player) == 4 && CountryReport.CityCount(world, rebel) == 1 && world.CountryCities[world.CountryCityStart[rebel]] == city);
            _check("devlet: tesisler şehirle gider", world.CountryFacilityStart[rebel + 1] - world.CountryFacilityStart[rebel] == slotsOfCity && world.CountrySlots[world.CountryFacilityStart[rebel]] == firstSlot);
            _check("devlet: hiçbir şehir kaybolmaz", world.CountryCityStart[world.CountryCount] == world.CityCount && world.CountryFacilityStart[world.CountryCount] == world.FacilityCount);

            long share = people * Permille / countryPopulation;
            long moved = stock * share / Permille;
            _check("devlet: nüfus payı kadar stok geçer", moved > 0 && world.Stock[rebel * world.ResourceCount + iron] == moved && world.Stock[Player * world.ResourceCount + iron] == stock - moved);
            _check("devlet: şebeke payı geçer", world.GridCapacityMilli[rebel] == grid * share / Permille);
            _check("devlet: nüfusun tamamı geçer", world.Population[city] == people && world.CountryPopulation[rebel] == people && world.CountryPopulation[Player] == countryPopulation - people);
            _check("devlet: memnuniyet düşük başlar", world.Satisfaction[city] == _balance.HandoverSatisfactionPermille);
            _check("devlet: şehir korunur", world.CityCalmDays[city] == _balance.RebellionCalmDays);

            int count = world.CountryCount;
            _check("devlet: ana ülkeyle savaşır", world.CountryAtWar[Player * count + rebel] && world.CountryAtWar[rebel * count + Player]);
            _check("devlet: ticaret yolu kapanır", CountryRoutes.Route(_definition, world, Player, rebel) == CountryRoutes.NoRoute);
            _check("devlet: kara sınırı oluşur", world.CountryLandNeighbor[Player * count + rebel] && world.CountryLandNeighbor[rebel * count + Player]);
            _check("devlet: vergi ana ülkeden", world.TaxPercent[rebel] == world.TaxPercent[Player]);
            _check("devlet: mesafe ana ülkeden", world.CountryDistanceKm[Ai * count + rebel] == world.CountryDistanceKm[Ai * count + Player]);
            _check("devlet: en yakınlar sırasına girer", NearestPlace(world, Ai, rebel) < NearestPlace(world, Ai, rebel + 1));
            _check("devlet: AI yönetir", !world.CountryPlayerControlled[rebel] && !world.CountryManualTrade[rebel]);
        }

        private static void SecondCityJoinsSameRebelState()
        {
            WorldState world = MakeWorld(6, 2);
            int rebel = world.BaseCountryCount;
            ForceSecession(world, 1);
            ForceSecession(world, 4);

            _check("katılım: komşu olmayan şehir de aynı devlete", world.CityOwner[1] == rebel && world.CityOwner[4] == rebel);
            _check("katılım: ikinci devlet kurulmaz", !Rebellions.IsRebelState(world, rebel + 1));
            _check("katılım: ilk şehir başkent kalır", world.RebelCapital[rebel] == 1);
            _check("katılım: liste sıralı", CountryReport.CityCount(world, rebel) == 2 && world.CountryCities[world.CountryCityStart[rebel]] == 1 && world.CountryCities[world.CountryCityStart[rebel] + 1] == 4);
            _check("katılım: ana ülkede kalanlar", CountryReport.CityCount(world, Player) == 4);
        }

        private static void RebelStateWorksAsCountry()
        {
            WorldState world = MakeWorld(4, 2);
            int rebel = world.BaseCountryCount;
            int slot = PlaceMine(world, 1);
            int iron = _definition.ResourceIndex("iron");
            ForceSecession(world, 1);

            AdvanceDays(world, _balance.HandoverStopDays + 1);
            EconomySimulator.AdvanceDay(_definition, world);

            _check("devlet çalışır: üretim isyancı stoğuna girer", world.FacilityOutputSpeedPermille[slot] > 0 && world.DailyProduced[rebel * world.ResourceCount + iron] > 0);
            _check("devlet çalışır: nüfusu sayılır", world.CountryPopulation[rebel] > 0);
            _check("devlet çalışır: vergi toplar", world.Money[rebel] > 0);
        }

        private static void RebelStateDoesNotBlockOwnFlights()
        {
            WorldState world = MakeWorld(4, 2);
            int count = world.CountryCount;
            int rebel = world.BaseCountryCount;
            world.AirPathCrossings = new bool[count * count * count];
            world.AirPathCrossings[(Player * count + Ai) * count + Player] = true;
            world.AirPathCrossings[(Ai * count + Player) * count + Player] = true;

            ForceSecession(world, 1);

            _check("uçuş: ana ülkenin uçuşu isyancı devletten geçmez", !world.AirPathCrossings[(Player * count + Ai) * count + rebel]);
            _check("uçuş: isyancı devletin uçuşu ana ülkeden geçer", world.AirPathCrossings[(rebel * count + Ai) * count + Player]);
        }

        private static void NoEmptySlotKeepsCity()
        {
            WorldState world = MakeWorld(4, 2);
            for (int slot = world.BaseCountryCount; slot < world.CountryCount; slot++)
            {
                world.RebelParent[slot] = Ai;
            }

            ForceSecession(world, 1);
            _check("boş yer yok: şehir ülkede kalır", world.CityOwner[1] == Player && CountryReport.CityCount(world, Player) == 4);
            _check("boş yer yok: isyan söner", world.CityRebellionDays[1] == 0 && world.CountryRebellionCount[Player] == 0);
            _check("boş yer yok: şehir korunur", world.CityCalmDays[1] == _balance.RebellionCalmDays);
        }

        private static void LosingEveryCityDoesNotBreakTheDay()
        {
            WorldState world = MakeWorld(3, 2);
            for (int city = 0; city < 3; city++)
            {
                ForceSecession(world, city);
            }

            AdvanceDays(world, 10);
            _check("hepsi gider: oyuncunun şehri kalmaz", CountryReport.CityCount(world, Player) == 0);
            _check("hepsi gider: gün çalışmaya devam eder", world.Day == 10 && world.CountryPopulation[Player] == 0);
        }

        private static void ResultDoesNotDependOnSplitting()
        {
            WorldState whole = MakeWorld(20, 3);
            WorldState split = MakeWorld(20, 3);
            whole.TaxPercent[Player] = MaxTax;
            split.TaxPercent[Player] = MaxTax;

            AdvanceDays(whole, 120);
            var clock = new SimulationClock(_balance.SecondsPerDay) { Speed = 1 };
            while (split.Day < 120)
            {
                clock.Advance(_definition, split, 0.25f);
            }

            _check("belirlilik: isyan oldu", whole.CountryRebellionsStarted[Player] > 0 && whole.CountryCitiesLost[Player] > 0);
            _check("belirlilik: karelere dağıtmak sonucu değiştirmez", whole.Checksum() == split.Checksum());

            WorldState again = MakeWorld(20, 3);
            again.TaxPercent[Player] = MaxTax;
            AdvanceDays(again, 120);
            _check("belirlilik: aynı seed aynı isyanlar", whole.Checksum() == again.Checksum());
        }

        private static void CityStopsHoldingDaysAfterHandover()
        {
            WorldState world = MakeWorld(3, 2);
            ForceSecession(world, 1);
            _check("el değiştirme: üretim durur", Rebellions.IsCityStopped(world, 1) && world.CityStopDays[1] == _balance.HandoverStopDays);

            AdvanceDays(world, _balance.HandoverStopDays);
            _check("el değiştirme: sonra normale döner", !Rebellions.IsCityStopped(world, 1));
        }

        private static WorldState MakeWorld(int playerCities, int aiCities)
        {
            WorldState world = WorldGenerator.Create(_definition, new WorldGeneratorSettings
            {
                CitiesPerCountry = new[] { playerCities, aiCities },
                FacilitiesPerCity = 2,
                Seed = 21,
                StartingStockUnits = LargeStockUnits,
                StartingMoney = LargeMoneyMilli
            });
            world.CountryPlayerControlled[Player] = true;
            world.CountryManualTrade[Player] = true;
            return world;
        }

        private static void AdvanceDays(WorldState world, int days)
        {
            for (int day = 0; day < days; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }
        }

        private static void ForceSecession(WorldState world, int city)
        {
            Rebellions.Start(_definition, world, Player, city);
            world.CityRebellionDays[city] = 1;
            Rebellions.AdvanceDay(_definition, world);
        }

        // Şehrin ilk slotuna girdi istemeyen, enerji kullanmayan bir maden koyar.
        private static int PlaceMine(WorldState world, int city)
        {
            int building = _definition.BuildingIndex("iron_mine");
            int slot = world.CitySlotStart[city];
            world.FacilityBuilding[slot] = building;
            world.FacilityRecipe[slot] = _definition.BuildingDefaultRecipe(building);
            world.FacilityLevel[slot] = 1;
            world.FacilityBuildDays[slot] = 0;
            world.FacilityUpgradeDays[slot] = 0;
            world.FacilityPrivate[slot] = false;
            return slot;
        }

        private static int NearestPlace(WorldState world, int buyer, int country)
        {
            int count = world.CountryCount;
            return Array.IndexOf(world.NearestCountries, country, buyer * count, count) - buyer * count;
        }
    }
}
