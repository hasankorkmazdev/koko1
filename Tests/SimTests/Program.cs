using System;
using GeoWar.Simulation;
using GeoWar.Tools;

namespace GeoWar.Tests
{
    // Oyun mantığı testleri. Unity'yi açmadan çalışır: dotnet run --project Tests/SimTests -c Release
    public static class Program
    {
        private const long Unit = EconomyDefinition.MilliPerUnit;
        private static int _failures;
        private static int _checks;
        private static EconomyDefinition _definition;

        public static int Main()
        {
            _definition = EconomyJsonLoader.Load();

            FactoryFollowsRecipe();
            MissingInputRaisesAlarmAndSpendsNothing();
            LevelRaisesSpeedAndInputs();
            KombinaAddsOutputWithoutMoreInput();
            GridCapacityAppliesBeforeLoss();
            ExtractionNeedsNoInputs();
            SlowResourcesFollowTheirDuration();
            LowSatisfactionSlowsProduction();
            PrivateFacilityUsesMarketAndSharesOutput();
            ExpiredMachineStopsFactoryUntilReplaced();
            FoodShortageShrinksPopulationAndSatisfaction();
            PriceRisesWithDemandAndReturnsToBase();
            SameSeedGivesSameWorld();
            DaysDoNotAllocate();
            MapTests.Run((name, passed) => Check(name, passed, true));
            UiTests.Run((name, passed) => Check(name, passed, true), _definition);
            FacilityTests.Run((name, passed) => Check(name, passed, true), _definition);
            ProductionReportTests.Run((name, passed) => Check(name, passed, true), _definition);
            MarketTests.Run((name, passed) => Check(name, passed, true), _definition);
            ShipmentTests.Run((name, passed) => Check(name, passed, true), _definition);
            CountryReportTests.Run((name, passed) => Check(name, passed, true), _definition);

            Console.WriteLine(_checks + " kontrol, " + _failures + " hata");
            return _failures == 0 ? 0 : 1;
        }

        private static void FactoryFollowsRecipe()
        {
            WorldState world = OneCity("solar_plant", "steel");
            SetStock(world, "iron", 10);
            SetStock(world, "coal", 10);
            SetStock(world, "chrome", 10);
            SetStock(world, "consumable", 10);

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("çelik: demir düşer", Stock(world, "iron"), 7 * Unit);
            Check("çelik: kömür düşer", Stock(world, "coal"), 8 * Unit);
            Check("çelik: krom düşer", Stock(world, "chrome"), 9 * Unit);
            Check("çelik: ürün gelir", Stock(world, "steel"), 3 * Unit);
            Check("çelik: sarf harcanır", Stock(world, "consumable"), 10 * Unit - _definition.Balance.FactoryConsumableMilli);
        }

        private static void MissingInputRaisesAlarmAndSpendsNothing()
        {
            WorldState world = OneCity("solar_plant", "steel");
            SetStock(world, "iron", 10);
            SetStock(world, "coal", 10);
            SetStock(world, "consumable", 10);
            int machineDays = world.FacilityMachineDays[1];

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("eksik girdi: alarm", world.FacilityAlarm[1], WorldState.AlarmActive);
            Check("eksik girdi: ürün yok", Stock(world, "steel"), 0);
            Check("eksik girdi: demir harcanmaz", Stock(world, "iron"), 10 * Unit);
            Check("eksik girdi: sarf harcanmaz", Stock(world, "consumable"), 10 * Unit);
            Check("eksik girdi: makine ömrü azalmaz", world.FacilityMachineDays[1], machineDays);
        }

        private static void LevelRaisesSpeedAndInputs()
        {
            WorldState world = OneCity("solar_plant", "steel");
            world.FacilityLevel[1] = 2;
            SetStock(world, "iron", 10);
            SetStock(world, "coal", 10);
            SetStock(world, "chrome", 10);
            SetStock(world, "consumable", 10);

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("seviye 2: ürün +%25", Stock(world, "steel"), 3750);
            Check("seviye 2: girdi +%25", Stock(world, "iron"), 10 * Unit - 3750);
        }

        private static void KombinaAddsOutputWithoutMoreInput()
        {
            WorldState world = OneCity("solar_plant", "steel");
            world.FacilityKombinaPermille[1] = 25;
            SetStock(world, "iron", 10);
            SetStock(world, "coal", 10);
            SetStock(world, "chrome", 10);
            SetStock(world, "consumable", 10);

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("kombina: ürün +%2,5", Stock(world, "steel"), 3075);
            Check("kombina: girdi aynı", Stock(world, "iron"), 7 * Unit);
        }

        private static void GridCapacityAppliesBeforeLoss()
        {
            // 2 enerji üretilir, kapasite 1'dir, %20 kayıpla 0,8 ulaşır. Talep 1, oran %80 olur.
            WorldState world = OneCity("solar_plant", "steel", "steel");
            world.GridCapacityMilli[0] = Unit;
            world.GridLossPermille[0] = 200;
            SetStock(world, "iron", 100);
            SetStock(world, "coal", 100);
            SetStock(world, "chrome", 100);
            SetStock(world, "consumable", 100);

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("şebeke: enerji oranı", world.EnergyRatioPermille[0], 800);
            Check("şebeke: üretim enerjiye göre", Stock(world, "steel"), 4800);
        }

        private static void ExtractionNeedsNoInputs()
        {
            WorldState world = OneCity("iron_mine");

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("maden: girdisiz üretir", Stock(world, "iron"), 3 * Unit);
            Check("maden: alarm yok", world.FacilityAlarm[0], WorldState.AlarmNone);
        }

        private static void SlowResourcesFollowTheirDuration()
        {
            WorldState world = OneCity("gold_mine", "farm");

            for (int day = 0; day < 5; day++)
            {
                EconomySimulator.AdvanceCountry(_definition, world, 0);
            }

            Check("altın: 5 günde 1", Stock(world, "gold"), Unit);
            Check("tarla: günde 5 gıda", Stock(world, "food"), 25 * Unit);
        }

        private static void LowSatisfactionSlowsProduction()
        {
            WorldState world = OneCity("iron_mine");
            world.Satisfaction[0] = _definition.Balance.LowSatisfactionPermille / 2;

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("düşük memnuniyet: üretim yarıya iner", Stock(world, "iron"), 1500);
        }

        private static void PrivateFacilityUsesMarketAndSharesOutput()
        {
            WorldState world = OneCity("solar_plant", "steel");
            world.FacilityPrivate[1] = true;
            world.FacilityStateSharePermille[1] = 250;

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            int steel = _definition.ResourceIndex("steel");
            int iron = _definition.ResourceIndex("iron");
            Check("şirket: devlet payı stoğa", Stock(world, "steel"), 750);
            Check("şirket: kalan pazara", world.MarketSupply[steel], 2250);
            Check("şirket: girdi pazardan", world.MarketDemand[iron], 3 * Unit);
            Check("şirket: alarm yok", world.FacilityAlarm[1], WorldState.AlarmNone);
        }

        private static void ExpiredMachineStopsFactoryUntilReplaced()
        {
            WorldState world = OneCity("solar_plant", "cement");
            world.FacilityMachineDays[1] = 0;
            SetStock(world, "limestone", 10);
            SetStock(world, "consumable", 10);

            EconomySimulator.AdvanceCountry(_definition, world, 0);
            Check("makine yok: alarm", world.FacilityAlarm[1], WorldState.AlarmActive);
            Check("makine yok: ürün yok", Stock(world, "cement"), 0);

            SetStock(world, "machine", 1);
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            Check("yeni makine: alarm biter", world.FacilityAlarm[1], WorldState.AlarmNone);
            Check("yeni makine: stoktan düşer", Stock(world, "machine"), 0);
            Check("yeni makine: ömür başlar", world.FacilityMachineDays[1], _definition.Balance.MachineLifeDays - 1);
            Check("yeni makine: üretim başlar", Stock(world, "cement"), 2 * Unit);
        }

        private static void FoodShortageShrinksPopulationAndSatisfaction()
        {
            WorldState world = OneCity("solar_plant");
            world.Population[0] = 1000000;
            world.Satisfaction[0] = 700;

            EconomySimulator.AdvanceCountry(_definition, world, 0);

            Check("kıtlık: nüfus azalır", world.Population[0] < 1000000, true);
            Check("kıtlık: memnuniyet düşer", world.Satisfaction[0] < 700, true);
        }

        private static void PriceRisesWithDemandAndReturnsToBase()
        {
            WorldState world = OneCity("solar_plant");
            int steel = _definition.ResourceIndex("steel");
            long basePrice = _definition.BasePriceMilli[steel];

            world.MarketDemand[steel] = 100 * Unit;
            EconomySimulator.AdvanceDay(_definition, world);
            Check("pazar: talep fiyatı yükseltir", world.PriceMilli[steel] > basePrice, true);

            long raised = world.PriceMilli[steel];
            EconomySimulator.AdvanceDay(_definition, world);
            Check("pazar: fiyat tabana döner", world.PriceMilli[steel] < raised, true);
        }

        private static void SameSeedGivesSameWorld()
        {
            ulong first = RunGeneratedWorld(7);
            ulong second = RunGeneratedWorld(7);
            ulong other = RunGeneratedWorld(8);

            Check("aynı seed aynı sonuç", first, second);
            Check("farklı seed farklı sonuç", first != other, true);
        }

        private static void DaysDoNotAllocate()
        {
            var settings = new WorldGeneratorSettings { CountryCount = 20, CityCount = 200, Seed = 3 };
            WorldState world = WorldGenerator.Create(_definition, settings);
            EconomySimulator.AdvanceDay(_definition, world);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int day = 0; day < 30; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            Check("günlük hesap bellek ayırmaz", GC.GetAllocatedBytesForCurrentThread() - before, 0L);
        }

        private static ulong RunGeneratedWorld(ulong seed)
        {
            var settings = new WorldGeneratorSettings { CountryCount = 20, CityCount = 200, Seed = seed };
            WorldState world = WorldGenerator.Create(_definition, settings);
            for (int day = 0; day < 120; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            return world.Checksum();
        }

        private static WorldState OneCity(params string[] recipes)
        {
            var world = new WorldState(1, 1, recipes.Length, _definition.ResourceCount);
            world.CountryCityStart[1] = 1;
            world.CountryFacilityStart[1] = recipes.Length;
            world.CitySlotStart[1] = recipes.Length;
            world.Satisfaction[0] = _definition.Balance.BaseSatisfactionPermille;
            world.GridCapacityMilli[0] = 1000 * Unit;
            world.EnergyRatioPermille[0] = EconomyDefinition.Permille;
            world.RandomState[0] = 1;

            for (int facility = 0; facility < recipes.Length; facility++)
            {
                int recipe = _definition.RecipeIndex(recipes[facility]);
                world.FacilityRecipe[facility] = recipe;
                world.FacilityBuilding[facility] = _definition.RecipeBuilding[recipe];
                world.FacilityLevel[facility] = 1;
                world.FacilityMachineDays[facility] = _definition.Balance.MachineLifeDays;
            }

            for (int resource = 0; resource < _definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = _definition.BasePriceMilli[resource];
            }

            return world;
        }

        private static void SetStock(WorldState world, string resource, long units)
        {
            world.Stock[_definition.ResourceIndex(resource)] = units * Unit;
        }

        private static long Stock(WorldState world, string resource)
        {
            return world.Stock[_definition.ResourceIndex(resource)];
        }

        private static void Check<T>(string name, T actual, T expected)
        {
            _checks++;
            if (Equals(actual, expected))
            {
                return;
            }

            _failures++;
            Console.WriteLine("HATA: " + name + ": beklenen " + expected + ", gelen " + actual);
        }
    }
}
