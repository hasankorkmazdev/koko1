using System;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class FacilityTests
    {
        private const long Unit = EconomyDefinition.MilliPerUnit;
        private const int Slots = 4;

        private static EconomyDefinition _definition;
        private static Action<string, bool> _check;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            _check = check;
            _definition = definition;

            SlotsFollowPopulation();
            RecipeResourcesAreListedOnce();
            BuildSpendsMoneyAndMaterials();
            BuildNeedsTimeBeforeWorking();
            BuildIsBlockedWithReasons();
            ExtractionNeedsDepositAndIsUniquePerCity();
            PortNeedsCoast();
            KombinaFollowsFinishedFactories();
            ExperienceFollowsWorkAndWaitsWhenFull();
            UpgradeCostsAndTakesTimeButKeepsWorking();
            BatchUpgradeStopsWhenMoneyRunsOut();
            ClosingFreesSlotAndLosesLevel();
            CompanyFacilityCannotBeClosedOrUpgraded();
            GeneratedWorldRespectsRules();
            EmptySlotsCostNothingAndDoNotAllocate();
        }

        private static void SlotsFollowPopulation()
        {
            int microchip = _definition.RecipeIndex("microchip");
            _check("reçete: 3 günde 1 yuvarlanmadan saklanır", _definition.OutputMilliPerCycle[_definition.OutputStart[microchip]] == Unit);
            _check("reçete: günlük miktar kesilir", _definition.OutputMilliPerDay[_definition.OutputStart[microchip]] == 333);
            _check("reçete: enerji miktarı saklanır", _definition.EnergyMilliPerCycle[_definition.RecipeIndex("nuclear_plant")] == 20 * Unit);

            BalanceData balance = _definition.Balance;
            _check("slot: küçük şehir en az sınırında", FacilityCommands.CitySlots(balance, 100000) == balance.MinCitySlots);
            _check("slot: nüfus artırır", FacilityCommands.CitySlots(balance, balance.PopulationPerSlot * 3) == balance.MinCitySlots + 3);
            _check("slot: en çok sınırı aşılmaz", FacilityCommands.CitySlots(balance, 500000000) == balance.MaxCitySlots);
        }

        private static void RecipeResourcesAreListedOnce()
        {
            int oil = _definition.ResourceIndex("oil");
            int refinery = _definition.BuildingIndex("refinery");
            var refineryInputs = _definition.BuildingInputResources(refinery);
            var refineryOutputs = _definition.BuildingOutputResources(refinery);
            _check("tesis kaynağı: iki reçetenin ortak girdisi bir kez yazılır", refineryInputs.Count == 1 && refineryInputs[0] == oil);
            _check("tesis kaynağı: her reçetenin ürünü yazılır", refineryOutputs.Count == 2
                && refineryOutputs[0] == _definition.ResourceIndex("fuel")
                && refineryOutputs[1] == _definition.ResourceIndex("rubber"));

            var steelInputs = _definition.BuildingInputResources(_definition.BuildingIndex("steel_factory"));
            _check("tesis kaynağı: girdiler reçete sırasında kalır", steelInputs.Count == 3
                && steelInputs[0] == _definition.ResourceIndex("iron")
                && steelInputs[1] == _definition.ResourceIndex("coal")
                && steelInputs[2] == _definition.ResourceIndex("chrome"));

            int coalPlant = _definition.BuildingIndex("coal_plant");
            _check("tesis kaynağı: santralin yakıtı girdidir", _definition.BuildingInputResources(coalPlant).Contains(_definition.ResourceIndex("coal")));
            _check("tesis kaynağı: enerji kaynak sayılmaz", _definition.BuildingOutputResources(coalPlant).Count == 0);

            int port = _definition.BuildingIndex("port");
            _check("tesis kaynağı: reçetesiz tesiste girdi yok", _definition.BuildingInputResources(port).Count == 0);
            _check("tesis kaynağı: reçetesiz tesiste ürün yok", _definition.BuildingOutputResources(port).Count == 0);
        }

        private static void BuildSpendsMoneyAndMaterials()
        {
            WorldState world = EmptyWorld();
            SetStock(world, "steel", 100);
            SetStock(world, "cement", 100);
            SetStock(world, "machine", 3);
            int building = _definition.BuildingIndex("steel_factory");
            long money = world.Money[0];

            int slot = FacilityCommands.TryBuild(_definition, world, 0, 0, building);

            _check("kurma: slot döner", slot == 0);
            _check("kurma: para düşer", world.Money[0] == money - _definition.BuildingMoneyMilli[building]);
            _check("kurma: çelik düşer", Stock(world, "steel") == 40 * Unit);
            _check("kurma: çimento düşer", Stock(world, "cement") == 20 * Unit);
            _check("kurma: makine düşer", Stock(world, "machine") == 2 * Unit);
            _check("kurma: seviye 1 ve sıfır deneyim", world.FacilityLevel[slot] == 1 && world.FacilityExperience[slot] == 0);
            _check("kurma: varsayılan ürün", world.FacilityRecipe[slot] == _definition.RecipeIndex("steel"));
            _check("kurma: slot dolu sayılır", FacilityCommands.UsedSlots(world, 0) == 1);
        }

        private static void BuildNeedsTimeBeforeWorking()
        {
            WorldState world = EmptyWorld();
            SetStock(world, "steel", 100);
            SetStock(world, "cement", 100);
            int building = _definition.BuildingIndex("iron_mine");
            world.CityDeposits[0] = 1L << building;

            int slot = FacilityCommands.TryBuild(_definition, world, 0, 0, building);
            int days = _definition.BuildingDays[building];

            for (int day = 0; day < days; day++)
            {
                EconomySimulator.AdvanceCountry(_definition, world, 0);
            }

            _check("inşaat: süre bitene kadar üretmez", Stock(world, "iron") == 0);
            _check("inşaat: süre biter", world.FacilityBuildDays[slot] == 0);

            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("inşaat: sonra üretir", Stock(world, "iron") == 3 * Unit);
            _check("inşaat: sonra deneyim kazanır", world.FacilityExperience[slot] > 0);
        }

        private static void BuildIsBlockedWithReasons()
        {
            WorldState world = EmptyWorld();
            int factory = _definition.BuildingIndex("steel_factory");

            _check("engel: kilitli tür", FacilityCommands.CheckBuild(_definition, world, 0, 0, _definition.BuildingIndex("radar")) == BuildBlock.Locked);
            _check("engel: malzeme yok", FacilityCommands.CheckBuild(_definition, world, 0, 0, factory) == BuildBlock.NoMaterials);

            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            SetStock(world, "machine", 10);
            world.Money[0] = 0;
            _check("engel: para yok", FacilityCommands.CheckBuild(_definition, world, 0, 0, factory) == BuildBlock.NoMoney);
            _check("engel: kurulamaz", FacilityCommands.TryBuild(_definition, world, 0, 0, factory) == -1);

            world.Money[0] = 1000000 * Unit;
            for (int i = 0; i < Slots; i++)
            {
                _check("engel: boş slota kurulur", FacilityCommands.TryBuild(_definition, world, 0, 0, factory) == i);
            }

            _check("engel: slot doldu", FacilityCommands.CheckBuild(_definition, world, 0, 0, factory) == BuildBlock.NoSlot);
        }

        private static void ExtractionNeedsDepositAndIsUniquePerCity()
        {
            WorldState world = EmptyWorld();
            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            int mine = _definition.BuildingIndex("iron_mine");

            _check("çıkarma: kaynak yoksa kurulmaz", FacilityCommands.CheckBuild(_definition, world, 0, 0, mine) == BuildBlock.NoDeposit);

            world.CityDeposits[0] = 1L << mine;
            _check("çıkarma: kaynak varsa kurulur", FacilityCommands.TryBuild(_definition, world, 0, 0, mine) >= 0);
            _check("çıkarma: her kaynağa tek tesis", FacilityCommands.CheckBuild(_definition, world, 0, 0, mine) == BuildBlock.AlreadyBuilt);
        }

        private static void PortNeedsCoast()
        {
            WorldState world = EmptyWorld();
            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            int port = _definition.BuildingIndex("port");

            _check("liman: kıyı ister", FacilityCommands.CheckBuild(_definition, world, 0, 0, port) == BuildBlock.NeedsCoast);
            world.CityCoastal[0] = true;
            _check("liman: kıyıda kurulur", FacilityCommands.CheckBuild(_definition, world, 0, 0, port) == BuildBlock.None);
            _check("liman: üretimsizdir", _definition.BuildingDefaultRecipe(port) == WorldState.NoRecipe);

            int slot = FacilityCommands.TryBuild(_definition, world, 0, 0, port);
            for (int day = 0; day < _definition.BuildingDays[port] + 2; day++)
            {
                EconomySimulator.AdvanceCountry(_definition, world, 0);
            }

            _check("liman: inşaatı biter, hata vermez", world.FacilityBuildDays[slot] == 0);
        }

        private static void KombinaFollowsFinishedFactories()
        {
            WorldState world = EmptyWorld();
            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            SetStock(world, "machine", 10);
            int steelFactory = _definition.BuildingIndex("steel_factory");
            int machineFactory = _definition.BuildingIndex("machine_factory");
            int cementFactory = _definition.BuildingIndex("cement_factory");
            int linkPermille = _definition.Balance.KombinaPermillePerLink;

            _check("kombina: tablo reçetelerden çıkar", _definition.BuildingLinked[steelFactory * _definition.BuildingCount + machineFactory]);
            _check("kombina: tablo iki yönlüdür", _definition.BuildingLinked[machineFactory * _definition.BuildingCount + steelFactory]);
            _check("kombina: ilgisiz fabrika bağlanmaz", !_definition.BuildingLinked[cementFactory * _definition.BuildingCount + machineFactory]);

            int a = FacilityCommands.TryBuild(_definition, world, 0, 0, steelFactory);
            int b = FacilityCommands.TryBuild(_definition, world, 0, 0, machineFactory);
            _check("kombina: inşaattayken bağlantı yok", world.FacilityKombinaPermille[a] == 0);

            for (int day = 0; day < _definition.BuildingDays[machineFactory]; day++)
            {
                EconomySimulator.AdvanceCountry(_definition, world, 0);
            }

            _check("kombina: inşaat bitince bağlanır", world.FacilityKombinaPermille[a] == linkPermille && world.FacilityKombinaPermille[b] == linkPermille);
            _check("kombina: önizleme sayar", FacilityCommands.LinkCount(_definition, world, 0, steelFactory) == 1);

            FacilityCommands.TryClose(_definition, world, b);
            _check("kombina: kapanınca bağlantı gider", world.FacilityKombinaPermille[a] == 0);
        }

        private static void ExperienceFollowsWorkAndWaitsWhenFull()
        {
            WorldState world = WorldWith("iron_mine");
            long full = FacilityCommands.ExperienceNeeded(_definition, 1);

            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("deneyim: tam hızda günde 1000", world.FacilityExperience[0] == EconomyDefinition.Permille);

            world.Satisfaction[0] = _definition.Balance.LowSatisfactionPermille * 6 / 10;
            world.FacilityExperience[0] = 0;
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("deneyim: %60 hızda %60 artar", world.FacilityExperience[0] == 600);

            world.Satisfaction[0] = _definition.Balance.BaseSatisfactionPermille;
            world.FacilityLevel[0] = 2;
            world.FacilityExperience[0] = 0;
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("deneyim: seviye hızı katlamaz", world.FacilityExperience[0] == EconomyDefinition.Permille);

            world.FacilityLevel[0] = 1;
            world.FacilityExperience[0] = full - 10;
            _check("deneyim: dolmadan hazır değil", !FacilityCommands.IsReadyToUpgrade(_definition, world, 0));
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("deneyim: dolunca durur", world.FacilityExperience[0] == full);
            _check("deneyim: dolunca hazır", FacilityCommands.IsReadyToUpgrade(_definition, world, 0));

            world.FacilityLevel[0] = _definition.Balance.MaxFacilityLevel;
            world.FacilityExperience[0] = 0;
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("deneyim: son seviyede kazanılmaz", world.FacilityExperience[0] == 0);
            _check("deneyim: son seviye hazır olmaz", !FacilityCommands.IsReadyToUpgrade(_definition, world, 0));
            _check("deneyim: seviye arttıkça uzar", FacilityCommands.ExperienceNeeded(_definition, 2) > FacilityCommands.ExperienceNeeded(_definition, 1));
        }

        private static void UpgradeCostsAndTakesTimeButKeepsWorking()
        {
            WorldState world = WorldWith("iron_mine");
            int building = world.FacilityBuilding[0];
            world.FacilityExperience[0] = FacilityCommands.ExperienceNeeded(_definition, 1);
            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            long money = world.Money[0];
            long cost = FacilityCommands.UpgradeMoneyMilli(_definition, building, 1);
            int days = FacilityCommands.UpgradeDays(_definition, building, 1);

            _check("yükseltme: bedeli seviyeyle artar", FacilityCommands.UpgradeMoneyMilli(_definition, building, 2) > cost);
            _check("yükseltme: başlar", FacilityCommands.TryUpgrade(_definition, world, 0, 0));
            _check("yükseltme: para düşer", world.Money[0] == money - cost);
            _check("yükseltme: ikinci kez başlamaz", !FacilityCommands.TryUpgrade(_definition, world, 0, 0));

            long ironBefore = Stock(world, "iron");
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            _check("yükseltme: sürerken çalışır", Stock(world, "iron") > ironBefore);
            _check("yükseltme: seviye hemen artmaz", world.FacilityLevel[0] == 1);

            for (int day = 1; day < days; day++)
            {
                EconomySimulator.AdvanceCountry(_definition, world, 0);
            }

            _check("yükseltme: süre sonunda seviye artar", world.FacilityLevel[0] == 2);
            // Tesis o gün de çalıştığı için yeni seviyeye bir günlük deneyimle başlar.
            _check("yükseltme: deneyim yeni seviyeden başlar", world.FacilityExperience[0] == EconomyDefinition.Permille);
        }

        private static void BatchUpgradeStopsWhenMoneyRunsOut()
        {
            WorldState world = WorldWith("iron_mine", "iron_mine", "iron_mine", "coal_mine");
            long full = FacilityCommands.ExperienceNeeded(_definition, 1);
            for (int slot = 0; slot < Slots; slot++)
            {
                world.FacilityExperience[slot] = full;
            }

            world.FacilityExperience[2] = 0;
            SetStock(world, "steel", 1000);
            SetStock(world, "cement", 1000);
            int building = world.FacilityBuilding[0];

            _check("toplu: sadece hazır ve aynı tür", FacilityCommands.UpgradeAllReady(_definition, world, 0, building) == 2);

            WorldState poor = WorldWith("iron_mine", "iron_mine", "iron_mine", "coal_mine");
            for (int slot = 0; slot < Slots; slot++)
            {
                poor.FacilityExperience[slot] = full;
            }

            SetStock(poor, "steel", 1000);
            SetStock(poor, "cement", 1000);
            poor.Money[0] = FacilityCommands.UpgradeMoneyMilli(_definition, building, 1);
            _check("toplu: para yettiği kadar", FacilityCommands.UpgradeAllReady(_definition, poor, 0, building) == 1);
            _check("toplu: para biter", poor.Money[0] == 0);
        }

        private static void ClosingFreesSlotAndLosesLevel()
        {
            WorldState world = WorldWith("iron_mine");
            world.FacilityLevel[0] = 3;
            world.FacilityExperience[0] = 500;

            _check("kapatma: olur", FacilityCommands.TryClose(_definition, world, 0));
            _check("kapatma: slot boşalır", world.FacilityBuilding[0] == WorldState.NoBuilding && FacilityCommands.UsedSlots(world, 0) == 0);
            _check("kapatma: seviye ve deneyim gider", world.FacilityLevel[0] == 0 && world.FacilityExperience[0] == 0);
            _check("kapatma: boş slot ikinci kez kapanmaz", !FacilityCommands.TryClose(_definition, world, 0));
        }

        private static void CompanyFacilityCannotBeClosedOrUpgraded()
        {
            WorldState world = WorldWith("iron_mine");
            world.FacilityPrivate[0] = true;
            world.FacilityExperience[0] = FacilityCommands.ExperienceNeeded(_definition, 1);

            _check("şirket: devlet kapatamaz", !FacilityCommands.TryClose(_definition, world, 0));
            _check("şirket: devlet yükseltemez", !FacilityCommands.IsReadyToUpgrade(_definition, world, 0));
        }

        private static void GeneratedWorldRespectsRules()
        {
            var settings = new WorldGeneratorSettings { CountryCount = 20, CityCount = 300, FacilitiesPerCity = 3, Seed = 11 };
            WorldState world = WorldGenerator.Create(_definition, settings);

            bool slotsContiguous = world.CitySlotStart[world.CityCount] == world.FacilityCount;
            bool slotCityMatches = true;
            bool extractionHasDeposit = true;
            bool extractionUnique = true;

            for (int city = 0; city < world.CityCount; city++)
            {
                long seen = 0;
                for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
                {
                    slotCityMatches &= world.FacilityCity[slot] == city;
                    int building = world.FacilityBuilding[slot];
                    if (building == WorldState.NoBuilding || !FacilityCommands.IsExtraction(_definition, building))
                    {
                        continue;
                    }

                    extractionHasDeposit &= FacilityCommands.HasDeposit(world, city, building);
                    extractionUnique &= (seen & (1L << building)) == 0;
                    seen |= 1L << building;
                }
            }

            _check("dünya: slotlar art arda", slotsContiguous);
            _check("dünya: slot şehri doğru", slotCityMatches);
            _check("dünya: çıkarma tesisi kaynağın üstünde", extractionHasDeposit);
            _check("dünya: her kaynağa tek tesis", extractionUnique);
            _check("dünya: kurulu tesis sayılır", world.CountBuiltFacilities() == 900);
            _check("dünya: boş slot da var", world.FacilityCount > world.CountBuiltFacilities());
        }

        private static void EmptySlotsCostNothingAndDoNotAllocate()
        {
            WorldState world = EmptyWorld();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int day = 0; day < 10; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("boş slot: bellek ayırmaz", GC.GetAllocatedBytesForCurrentThread() - before == 0);
            _check("boş slot: üretim yok", Stock(world, "iron") == 0);
        }

        private static WorldState EmptyWorld()
        {
            var world = new WorldState(1, 1, Slots, _definition.ResourceCount);
            world.CountryCityStart[1] = 1;
            world.CountryFacilityStart[1] = Slots;
            world.CitySlotStart[1] = Slots;
            world.Money[0] = 1000000 * Unit;
            world.Satisfaction[0] = _definition.Balance.BaseSatisfactionPermille;
            world.GridCapacityMilli[0] = 1000 * Unit;
            world.EnergyRatioPermille[0] = EconomyDefinition.Permille;
            world.RandomState[0] = 1;
            for (int resource = 0; resource < _definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = _definition.BasePriceMilli[resource];
            }

            return world;
        }

        private static WorldState WorldWith(params string[] buildings)
        {
            WorldState world = EmptyWorld();
            for (int slot = 0; slot < buildings.Length; slot++)
            {
                int building = _definition.BuildingIndex(buildings[slot]);
                world.FacilityBuilding[slot] = building;
                world.FacilityRecipe[slot] = _definition.BuildingDefaultRecipe(building);
                world.FacilityLevel[slot] = 1;
                world.FacilityMachineDays[slot] = _definition.Balance.MachineLifeDays;
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
    }
}
