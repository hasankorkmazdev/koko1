using System;

namespace GeoWar.Simulation
{
    // Bir oyun gününün ekonomi hesabı. Kurallar docs/ECONOMY.md, ENERGY.md ve RESOURCES.md dosyalarındadır.
    // Tam sayı kullanır ve her gün için bellek ayırmaz.
    public static class EconomySimulator
    {
        private const int Permille = EconomyDefinition.Permille;
        private const long Million = EconomyDefinition.Million;
        private const long OneUnit = EconomyDefinition.MilliPerUnit;

        public static void AdvanceDay(EconomyDefinition definition, WorldState world)
        {
            for (int country = 0; country < world.CountryCount; country++)
            {
                AdvanceCountry(definition, world, country);
            }

            FinishDay(definition, world);
        }

        // Tüm ülkeler hesaplandıktan sonra günü kapatır: pazar kapanır, tarih ilerler.
        public static void FinishDay(EconomyDefinition definition, WorldState world)
        {
            Shipments.DeliverArrivals(definition, world);
            SettleMarket(definition, world);
            Rebellions.AdvanceDay(definition, world);
            world.Day++;
        }

        // Ülkeleri karelere dağıtmak için tek başına çağrılabilir.
        public static void AdvanceCountry(EconomyDefinition definition, WorldState world, int country)
        {
            int stockBase = country * world.ResourceCount;
            Array.Clear(world.DailyProduced, stockBase, world.ResourceCount);
            Array.Clear(world.DailyConsumed, stockBase, world.ResourceCount);
            Array.Clear(world.DailyShortage, stockBase, world.ResourceCount);

            RunEnergy(definition, world, country, stockBase);
            RunFacilities(definition, world, country, stockBase);
            RunCities(definition, world, country, stockBase);
            RollEvent(definition, world, country);
            RunAiPlanning(definition, world, country);
        }

        public static int LevelSpeedPermille(BalanceData balance, int level)
        {
            return Permille + (level - 1) * balance.LevelBonusPermille;
        }

        private static void RunEnergy(EconomyDefinition definition, WorldState world, int country, int stockBase)
        {
            BalanceData balance = definition.Balance;
            long produced = 0;
            long demand = world.CountryPopulation[country] * balance.PopulationEnergyMilliPerMillion / Million;

            int end = world.CountryFacilityStart[country + 1];
            for (int i = world.CountryFacilityStart[country]; i < end; i++)
            {
                // Boş slot, inşaattaki tesis ve üretimsiz tesis enerji işine karışmaz.
                int facility = world.CountrySlots[i];
                int recipe = world.FacilityRecipe[facility];
                if (recipe == WorldState.NoRecipe || world.FacilityBuildDays[facility] > 0)
                {
                    continue;
                }

                // Üretimi duran şehrin tesisi enerji üretmez, fabrikası enerji istemez.
                RecipeKind kind = definition.Kinds[recipe];
                if (Rebellions.IsCityStopped(world, world.FacilityCity[facility]))
                {
                    if (kind == RecipeKind.Energy)
                    {
                        world.FacilityAlarm[facility] = WorldState.AlarmStopped;
                    }

                    continue;
                }

                if (kind == RecipeKind.Factory)
                {
                    demand += balance.FactoryEnergyNeedMilli;
                    continue;
                }

                if (kind != RecipeKind.Energy)
                {
                    continue;
                }

                int speed = LevelSpeedPermille(balance, world.FacilityLevel[facility]);
                if (!HasInputs(definition, world, stockBase, recipe, speed))
                {
                    world.FacilityAlarm[facility] = WorldState.AlarmActive;
                    NoteInputShortage(definition, world, stockBase, recipe, speed);
                    continue;
                }

                ConsumeInputs(definition, world, stockBase, recipe, speed);
                world.FacilityAlarm[facility] = WorldState.AlarmNone;
                AddExperience(definition, world, facility, Permille);
                produced += definition.EnergyMilliPerDay[recipe] * speed / Permille;
            }

            // Önce kapasitenin üstü boşa gider, sonra kalandan kayıp düşülür.
            long delivered = Math.Min(produced, world.GridCapacityMilli[country]);
            delivered -= delivered * world.GridLossPermille[country] / Permille;

            world.EnergyRatioPermille[country] = demand == 0
                ? Permille
                : (int)Math.Min(Permille, delivered * Permille / demand);
        }

        private static void RunFacilities(EconomyDefinition definition, WorldState world, int country, int stockBase)
        {
            BalanceData balance = definition.Balance;
            int energyRatio = world.EnergyRatioPermille[country];

            int end = world.CountryFacilityStart[country + 1];
            for (int i = world.CountryFacilityStart[country]; i < end; i++)
            {
                int facility = world.CountrySlots[i];
                world.FacilityOutputSpeedPermille[facility] = 0;

                if (world.FacilityBuilding[facility] == WorldState.NoBuilding || AdvanceTimers(definition, world, facility))
                {
                    continue;
                }

                int recipe = world.FacilityRecipe[facility];
                if (recipe == WorldState.NoRecipe)
                {
                    continue;
                }

                RecipeKind kind = definition.Kinds[recipe];
                if (kind == RecipeKind.Energy)
                {
                    continue;
                }

                if (Rebellions.IsCityStopped(world, world.FacilityCity[facility]))
                {
                    world.FacilityAlarm[facility] = WorldState.AlarmStopped;
                    continue;
                }

                int work = WorkPermille(balance, world, facility, kind, energyRatio);
                int speed = (int)((long)LevelSpeedPermille(balance, world.FacilityLevel[facility]) * work / Permille);
                bool isPrivate = world.FacilityPrivate[facility];

                // Girdisi eksik tesis çalışmaz; makine ömrü ve sarf malzemesi de harcamaz.
                if (!isPrivate && !HasInputs(definition, world, stockBase, recipe, speed))
                {
                    world.FacilityAlarm[facility] = WorldState.AlarmActive;
                    NoteInputShortage(definition, world, stockBase, recipe, speed);
                    continue;
                }

                if (kind == RecipeKind.Factory && !TryRunFactoryUpkeep(definition, world, facility, stockBase, isPrivate))
                {
                    world.FacilityAlarm[facility] = WorldState.AlarmActive;
                    continue;
                }

                if (isPrivate)
                {
                    // Şirket girdisini pazardan alır, ülke stoğuna dokunmaz.
                    BuyInputsFromMarket(definition, world, recipe, speed);
                }
                else
                {
                    ConsumeInputs(definition, world, stockBase, recipe, speed);
                }

                world.FacilityAlarm[facility] = WorldState.AlarmNone;
                AddExperience(definition, world, facility, work);

                // Kombina bonusu verimdir: aynı girdiyle daha çok ürün çıkar.
                int outputSpeed = kind == RecipeKind.Factory
                    ? speed * (Permille + world.FacilityKombinaPermille[facility]) / Permille
                    : speed;
                world.FacilityOutputSpeedPermille[facility] = outputSpeed;
                DeliverOutputs(definition, world, facility, recipe, outputSpeed, stockBase, isPrivate);
            }
        }

        // İnşaat ve yükseltme süresini işletir. Tesis inşaattaysa true döner; yükseltilen tesis çalışmaya devam eder.
        private static bool AdvanceTimers(EconomyDefinition definition, WorldState world, int facility)
        {
            int buildDays = world.FacilityBuildDays[facility];
            if (buildDays > 0)
            {
                world.FacilityBuildDays[facility] = buildDays - 1;
                if (buildDays == 1)
                {
                    FacilityCommands.RecomputeKombina(definition, world, world.FacilityCity[facility]);
                    world.RoutesDirty |= definition.BuildingIsTransport[world.FacilityBuilding[facility]];
                }

                return true;
            }

            int upgradeDays = world.FacilityUpgradeDays[facility];
            if (upgradeDays > 0)
            {
                world.FacilityUpgradeDays[facility] = upgradeDays - 1;
                if (upgradeDays == 1)
                {
                    world.FacilityLevel[facility]++;
                    world.FacilityExperience[facility] = 0;
                }
            }

            return false;
        }

        // Tesis ne kadar çalışırsa o kadar deneyim kazanır; seviye açılınca deneyim dolu bekler.
        private static void AddExperience(EconomyDefinition definition, WorldState world, int facility, int work)
        {
            long need = FacilityCommands.ExperienceNeeded(definition, world.FacilityLevel[facility]);
            long experience = world.FacilityExperience[facility];
            if (experience < need)
            {
                world.FacilityExperience[facility] = Math.Min(need, experience + work);
            }
        }

        // Tesisin seviyeden bağımsız çalışma oranı: memnuniyet, taviz ve enerji düşürür.
        private static int WorkPermille(BalanceData balance, WorldState world, int facility, RecipeKind kind, int energyRatio)
        {
            long work = Permille;

            int city = world.FacilityCity[facility];
            int satisfaction = world.Satisfaction[city];
            if (satisfaction < balance.LowSatisfactionPermille)
            {
                work = work * satisfaction / balance.LowSatisfactionPermille;
            }

            if (world.CityConcessionDays[city] > 0)
            {
                work = work * balance.ConcessionProductionPermille / Permille;
            }

            // Çıkarma tesisleri enerji kullanmaz.
            if (kind == RecipeKind.Factory)
            {
                work = work * energyRatio / Permille;
            }

            return (int)work;
        }

        // Fabrikalar makine ve sarf malzemesi ister; ikisi yoksa üretim yapmaz.
        private static bool TryRunFactoryUpkeep(EconomyDefinition definition, WorldState world, int facility, int stockBase, bool isPrivate)
        {
            BalanceData balance = definition.Balance;

            if (world.FacilityMachineDays[facility] <= 0)
            {
                if (isPrivate)
                {
                    world.MarketDemand[definition.Machine] += OneUnit;
                }
                else if (!TryTake(world, stockBase, definition.Machine, OneUnit))
                {
                    return false;
                }

                world.FacilityMachineDays[facility] = balance.MachineLifeDays;
            }

            if (isPrivate)
            {
                world.MarketDemand[definition.Consumable] += balance.FactoryConsumableMilli;
            }
            else if (!TryTake(world, stockBase, definition.Consumable, balance.FactoryConsumableMilli))
            {
                return false;
            }

            world.FacilityMachineDays[facility]--;
            return true;
        }

        private static bool TryTake(WorldState world, int stockBase, int resource, long amount)
        {
            int index = stockBase + resource;
            if (world.Stock[index] < amount)
            {
                world.DailyShortage[index] += amount - world.Stock[index];
                return false;
            }

            world.Stock[index] -= amount;
            world.DailyConsumed[index] += amount;
            return true;
        }

        // Girdisi yetmeyen tesisin eksiği: tesis çalışmadı ama ihtiyaç duruyor.
        private static void NoteInputShortage(EconomyDefinition definition, WorldState world, int stockBase, int recipe, int speed)
        {
            int end = definition.InputStart[recipe + 1];
            for (int i = definition.InputStart[recipe]; i < end; i++)
            {
                long need = definition.InputMilliPerDay[i] * speed / Permille;
                int index = stockBase + definition.InputResource[i];
                if (world.Stock[index] < need)
                {
                    world.DailyShortage[index] += need - world.Stock[index];
                }
            }
        }

        private static bool HasInputs(EconomyDefinition definition, WorldState world, int stockBase, int recipe, int speed)
        {
            int end = definition.InputStart[recipe + 1];
            for (int i = definition.InputStart[recipe]; i < end; i++)
            {
                long need = definition.InputMilliPerDay[i] * speed / Permille;
                if (world.Stock[stockBase + definition.InputResource[i]] < need)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConsumeInputs(EconomyDefinition definition, WorldState world, int stockBase, int recipe, int speed)
        {
            int end = definition.InputStart[recipe + 1];
            for (int i = definition.InputStart[recipe]; i < end; i++)
            {
                long need = definition.InputMilliPerDay[i] * speed / Permille;
                int index = stockBase + definition.InputResource[i];
                world.Stock[index] -= need;
                world.DailyConsumed[index] += need;
            }
        }

        private static void BuyInputsFromMarket(EconomyDefinition definition, WorldState world, int recipe, int speed)
        {
            int end = definition.InputStart[recipe + 1];
            for (int i = definition.InputStart[recipe]; i < end; i++)
            {
                world.MarketDemand[definition.InputResource[i]] += definition.InputMilliPerDay[i] * speed / Permille;
            }
        }

        // Üretimin genel stoğa giren kısmı: şirket tesisinde yalnızca devlet payı, kalanını şirket pazara satar.
        public static long StockShareMilli(WorldState world, int facility, long amount)
        {
            return world.FacilityPrivate[facility]
                ? amount * world.FacilityStateSharePermille[facility] / Permille
                : amount;
        }

        private static void DeliverOutputs(EconomyDefinition definition, WorldState world, int facility, int recipe, int speed, int stockBase, bool isPrivate)
        {
            int end = definition.OutputStart[recipe + 1];
            for (int i = definition.OutputStart[recipe]; i < end; i++)
            {
                long amount = definition.OutputMilliPerDay[i] * speed / Permille;
                int resource = definition.OutputResource[i];
                long toStock = StockShareMilli(world, facility, amount);

                if (isPrivate)
                {
                    world.MarketSupply[resource] += amount - toStock;
                }

                int index = stockBase + resource;
                world.Stock[index] += toStock;
                world.DailyProduced[index] += toStock;
            }
        }

        private static void RunCities(EconomyDefinition definition, WorldState world, int country, int stockBase)
        {
            BalanceData balance = definition.Balance;
            int cityStart = world.CountryCityStart[country];
            int cityEnd = world.CountryCityStart[country + 1];

            long population = 0;
            for (int i = cityStart; i < cityEnd; i++)
            {
                population += world.Population[world.CountryCities[i]];
            }

            world.CountryPopulation[country] = population;

            int foodRatio = FeedPeople(world, stockBase, definition.Food, population * balance.FoodMilliPerMillion / Million);
            long goodsNeed = population * balance.GoodsMilliPerMillion / Million;
            int goodsRatio = (FeedPeople(world, stockBase, definition.Medicine, goodsNeed)
                + FeedPeople(world, stockBase, definition.Fuel, goodsNeed)
                + FeedPeople(world, stockBase, definition.Electronics, goodsNeed)) / 3;

            int taxPercent = world.TaxPercent[country];
            int taxPenalty = taxPercent * balance.TaxSatisfactionPenaltyPerPercent;
            int countryTarget = balance.BaseSatisfactionPermille
                - (Permille - foodRatio) / 2
                - (Permille - goodsRatio) / 4
                - (Permille - world.EnergyRatioPermille[country]) / 4;

            long income = 0;
            for (int i = cityStart; i < cityEnd; i++)
            {
                // Tavizdeki şehir vergi ödemez ve vergiden memnuniyet kaybetmez; isyandaki komşu hedefi düşürür.
                int city = world.CountryCities[i];
                bool isConceded = world.CityConcessionDays[city] > 0;
                int target = countryTarget
                    - (isConceded ? 0 : taxPenalty)
                    - world.CityRebelNeighbors[city] * balance.RebellionNeighborPenaltyPermille;

                int satisfaction = world.Satisfaction[city];
                satisfaction += (target - satisfaction) / balance.SatisfactionStepDivisor;
                world.Satisfaction[city] = Math.Max(0, Math.Min(Permille, satisfaction));

                long people = world.Population[city];
                long growth = foodRatio >= Permille ? balance.GrowthPerMillionPerDay : -balance.StarvationPerMillionPerDay;
                world.Population[city] = people + people * growth / Million;
                if (!isConceded)
                {
                    income += people * taxPercent * balance.TaxIncomePerMillionPerPercent / Million;
                }
            }

            world.Money[country] += income;
        }

        private static int FeedPeople(WorldState world, int stockBase, int resource, long need)
        {
            if (need <= 0)
            {
                return Permille;
            }

            int index = stockBase + resource;
            long taken = Math.Min(world.Stock[index], need);
            world.Stock[index] -= taken;
            world.DailyConsumed[index] += taken;
            world.DailyShortage[index] += need - taken;
            return (int)(taken * Permille / need);
        }

        private static void RollEvent(EconomyDefinition definition, WorldState world, int country)
        {
            BalanceData balance = definition.Balance;
            if (DeterministicRandom.Range(ref world.RandomState[country], (int)Million) >= balance.EventChancePerMillion)
            {
                return;
            }

            int cityStart = world.CountryCityStart[country];
            int cityCount = world.CountryCityStart[country + 1] - cityStart;
            if (cityCount == 0)
            {
                return;
            }

            int city = world.CountryCities[cityStart + DeterministicRandom.Range(ref world.RandomState[country], cityCount)];
            world.Satisfaction[city] = Math.Max(0, world.Satisfaction[city] - balance.EventSatisfactionHitPermille);
        }

        // AI her ülke için sırayla, seyrek olarak en kârlı reçeteyi hesaplar.
        private static void RunAiPlanning(EconomyDefinition definition, WorldState world, int country)
        {
            int interval = definition.Balance.AiPlanningIntervalDays;
            if ((world.Day + country) % interval != 0)
            {
                return;
            }

            long bestProfit = long.MinValue;
            int bestRecipe = 0;
            for (int recipe = 0; recipe < definition.RecipeCount; recipe++)
            {
                long profit = 0;
                int outputEnd = definition.OutputStart[recipe + 1];
                for (int i = definition.OutputStart[recipe]; i < outputEnd; i++)
                {
                    profit += definition.OutputMilliPerDay[i] * world.PriceMilli[definition.OutputResource[i]] / OneUnit;
                }

                int inputEnd = definition.InputStart[recipe + 1];
                for (int i = definition.InputStart[recipe]; i < inputEnd; i++)
                {
                    profit -= definition.InputMilliPerDay[i] * world.PriceMilli[definition.InputResource[i]] / OneUnit;
                }

                if (profit > bestProfit)
                {
                    bestProfit = profit;
                    bestRecipe = recipe;
                }
            }

            world.BestRecipe[country] = bestRecipe;
        }

        private static void SettleMarket(EconomyDefinition definition, WorldState world)
        {
            BalanceData balance = definition.Balance;

            CountOffers(world);

            // Sıra her gün kayar: hep aynı ülke önce almasın.
            for (int i = 0; i < world.CountryCount; i++)
            {
                int country = (i + world.Day) % world.CountryCount;
                if (!world.CountryManualTrade[country])
                {
                    TradeForCountry(definition, world, country);
                }
            }

            for (int resource = 0; resource < world.ResourceCount; resource++)
            {
                long basePrice = definition.BasePriceMilli[resource];
                long price = world.PriceMilli[resource];
                long supply = world.MarketSupply[resource];
                long demand = world.MarketDemand[resource];
                long total = supply + demand;

                if (total > 0)
                {
                    long step = (demand - supply) * balance.PriceMaxStepPermille / total;
                    price += price * step / Permille;
                }

                // Fiyat zamanla yavaşça taban fiyata döner.
                price += (basePrice - price) * balance.PriceReversionPermille / Permille;
                long floor = basePrice / balance.PriceFloorDivisor;
                long ceiling = basePrice * balance.PriceCeilingMultiplier;
                world.PriceMilli[resource] = Math.Max(floor, Math.Min(ceiling, price));
                world.MarketSupply[resource] = 0;
                world.MarketDemand[resource] = 0;
            }
        }

        private static void CountOffers(WorldState world)
        {
            Array.Clear(world.MarketOfferTotal, 0, world.ResourceCount);
            for (int index = 0; index < world.OfferMilli.Length; index++)
            {
                world.MarketOfferTotal[index % world.ResourceCount] += world.OfferMilli[index];
            }
        }

        // AI ülkesi hedef stoğun altındaysa satın alır, çok üstündeyse fazlasını teklif olarak pazara koyar.
        private static void TradeForCountry(EconomyDefinition definition, WorldState world, int country)
        {
            BalanceData balance = definition.Balance;
            int stockBase = country * world.ResourceCount;

            for (int resource = 0; resource < world.ResourceCount; resource++)
            {
                int index = stockBase + resource;
                long stock = world.Stock[index];
                long target = (world.DailyConsumed[index] + world.DailyShortage[index]) * balance.MarketTargetDays;

                // Yoldaki mal da sayılır. Hedefin yarısının altına inince sipariş verir: her gün küçük sevkiyat açılmaz.
                long available = stock + world.InTransitMilli[index];
                if (available < target * balance.MarketReorderPermille / Permille)
                {
                    BuyForCountry(definition, world, country, resource, target - available);
                    continue;
                }

                long sellAbove = target * balance.MarketSellAboveTargetMultiplier;
                if (stock > sellAbove)
                {
                    long listed = (stock - sellAbove) / balance.MarketSellDivisor;
                    world.Stock[index] -= listed;
                    world.OfferMilli[index] += listed;
                    world.MarketSupply[resource] += listed;
                    world.MarketOfferTotal[resource] += listed;
                }
            }
        }

        // Önce kendi satılmamış malını geri alır, sonra en yakın tekliften başlayarak satın alır.
        private static void BuyForCountry(EconomyDefinition definition, WorldState world, int buyer, int resource, long wanted)
        {
            int index = buyer * world.ResourceCount + resource;
            long reclaimed = Math.Min(world.OfferMilli[index], wanted);
            if (reclaimed > 0)
            {
                world.OfferMilli[index] -= reclaimed;
                world.Stock[index] += reclaimed;
                world.MarketOfferTotal[resource] -= reclaimed;
                wanted -= reclaimed;
            }

            int count = world.CountryCount;
            int orderStart = buyer * count;
            for (int i = 0; i < count && wanted > 0 && world.MarketOfferTotal[resource] > 0; i++)
            {
                int seller = world.NearestCountries[orderStart + i];
                long offer = world.OfferMilli[seller * world.ResourceCount + resource];
                if (seller == buyer || offer <= 0 || CountryRoutes.Route(definition, world, buyer, seller) == CountryRoutes.NoRoute)
                {
                    continue;
                }

                long unitCost = MarketCommands.UnitCostMilli(definition, world, buyer, seller, resource);
                long affordable = unitCost == 0 ? 0 : Math.Max(0, world.Money[buyer]) * OneUnit / unitCost;
                long amount = Math.Min(Math.Min(wanted, offer), affordable);
                if (amount <= 0)
                {
                    // Parası bu teklife yetmedi.
                    continue;
                }

                MarketCommands.Transfer(definition, world, buyer, seller, resource, amount);
                world.MarketOfferTotal[resource] -= amount;
                wanted -= amount;
            }

            // Karşılanamayan talep de fiyatı yükseltir.
            world.MarketDemand[resource] += wanted;
        }
    }
}
