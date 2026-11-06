using System;
using System.Collections.Generic;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class ProductionReportTests
    {
        private const long Unit = EconomyDefinition.MilliPerUnit;
        private const int Permille = EconomyDefinition.Permille;
        private const int SlotsPerCity = 3;
        private const int SimulatedDays = 20;

        private static EconomyDefinition _definition;
        private static Action<string, bool> _check;

        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            _check = check;
            _definition = definition;

            CitiesAreListedByAmount();
            ResourceWithoutFacilityHasNoCities();
            NewFacilityWorksBeforeFirstDay();
            ConstructionIsCountedButProducesNothing();
            MissingInputsStopTheFacility();
            KombinaIsIncluded();
            CompanyShowsOnlyTheStateShare();
            OtherResourcesDoNotLeak();
            ListMatchesCountryTotalInGeneratedWorld();
            MakersAndUsersComeFromRecipes();
            SpecialUsesAreKnown();
            CycleAmountsAreNotRounded();
        }

        private static void CitiesAreListedByAmount()
        {
            WorldState world = MakeWorld(3);
            Place(world, 0, 0, "iron_mine", 1);
            Place(world, 2, 0, "iron_mine", 2);
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "iron");
            long levelTwo = 3 * Unit * EconomySimulator.LevelSpeedPermille(_definition.Balance, 2) / Permille;

            _check("üretim listesi: üreten şehirler", rows.Count == 2);
            _check("üretim listesi: en çok üreten başta", rows[0].City == 2 && rows[1].City == 0);
            _check("üretim listesi: seviye miktara girer", rows[0].ProducedMilli == levelTwo);
            _check("üretim listesi: seviye 1 miktarı", rows[1].ProducedMilli == 3 * Unit);
            _check("üretim listesi: tesis sayılır", rows[0].Working == 1 && rows[0].Stopped == 0 && rows[0].Building == 0);
            _check("üretim listesi: toplam ülkeyle aynı", Sum(rows) == Produced(world, "iron"));
        }

        private static void ResourceWithoutFacilityHasNoCities()
        {
            WorldState world = MakeWorld(2);
            Place(world, 0, 0, "iron_mine", 1);
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            _check("üretim listesi: tesis yoksa boş", Cities(world, "gold").Count == 0);
        }

        private static void NewFacilityWorksBeforeFirstDay()
        {
            WorldState world = MakeWorld(1);
            Place(world, 0, 0, "iron_mine", 1);

            List<CityProduction> rows = Cities(world, "iron");

            _check("üretim listesi: ilk gün bitmeden durmuş sayılmaz", rows[0].Working == 1 && rows[0].Stopped == 0);
            _check("üretim listesi: ilk gün bitmeden miktar sıfır", rows[0].ProducedMilli == 0);
        }

        private static void ConstructionIsCountedButProducesNothing()
        {
            WorldState world = MakeWorld(1);
            int slot = Place(world, 0, 0, "iron_mine", 1);
            world.FacilityBuildDays[slot] = 5;
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "iron");

            _check("üretim listesi: inşaattaki tesis yazılır", rows.Count == 1 && rows[0].Building == 1 && rows[0].Working == 0);
            _check("üretim listesi: inşaattaki tesis üretmez", rows[0].ProducedMilli == 0);
        }

        private static void MissingInputsStopTheFacility()
        {
            WorldState world = MakeWorld(1);
            Place(world, 0, 0, "steel_factory", 1);
            Place(world, 0, 1, "solar_plant", 1);
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "steel");

            _check("üretim listesi: girdisiz tesis durmuş sayılır", rows[0].Stopped == 1 && rows[0].Working == 0);
            _check("üretim listesi: durmuş tesis üretmez", rows[0].ProducedMilli == 0);

            // Girdi gelince aynı tesis yeniden sayılır, durmuş kalmaz.
            SetStock(world, "iron", 100);
            SetStock(world, "coal", 100);
            SetStock(world, "chrome", 100);
            SetStock(world, "consumable", 100);
            EconomySimulator.AdvanceCountry(_definition, world, 0);
            rows = Cities(world, "steel");

            _check("üretim listesi: girdi gelince çalışır", rows[0].Working == 1 && rows[0].Stopped == 0);
            _check("üretim listesi: girdi gelince üretir", rows[0].ProducedMilli == 3 * Unit);
        }

        private static void KombinaIsIncluded()
        {
            WorldState world = MakeWorld(1);
            int slot = Place(world, 0, 0, "steel_factory", 1);
            Place(world, 0, 1, "solar_plant", 1);
            int kombina = _definition.Balance.KombinaPermillePerLink;
            world.FacilityKombinaPermille[slot] = kombina;
            SetStock(world, "iron", 100);
            SetStock(world, "coal", 100);
            SetStock(world, "chrome", 100);
            SetStock(world, "consumable", 100);
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "steel");
            long expected = 3 * Unit * (Permille + kombina) / Permille;

            _check("üretim listesi: kombina miktara girer", rows[0].ProducedMilli == expected);
            _check("üretim listesi: kombina toplamı ülkeyle aynı", rows[0].ProducedMilli == Produced(world, "steel"));
        }

        private static void CompanyShowsOnlyTheStateShare()
        {
            const int share = 250;
            WorldState world = MakeWorld(1);
            int slot = Place(world, 0, 0, "iron_mine", 1);
            world.FacilityPrivate[slot] = true;
            world.FacilityStateSharePermille[slot] = share;
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "iron");

            _check("üretim listesi: şirketin devlet payı yazılır", rows[0].ProducedMilli == 3 * Unit * share / Permille);
            _check("üretim listesi: şirket toplamı ülkeyle aynı", rows[0].ProducedMilli == Produced(world, "iron"));
        }

        private static void OtherResourcesDoNotLeak()
        {
            WorldState world = MakeWorld(2);
            Place(world, 0, 0, "iron_mine", 1);
            Place(world, 1, 0, "coal_mine", 1);
            EconomySimulator.AdvanceCountry(_definition, world, 0);

            List<CityProduction> rows = Cities(world, "coal");

            _check("üretim listesi: başka kaynağın tesisi girmez", rows.Count == 1 && rows[0].City == 1);
        }

        private static void ListMatchesCountryTotalInGeneratedWorld()
        {
            var settings = new WorldGeneratorSettings
            {
                CitiesPerCountry = new[] { 12, 8, 20 },
                Seed = 7,
                FacilitiesPerCity = 3,
                MaxStartingLevel = 3,
                StartingStockUnits = 5000,
                StartingMoney = 500000 * Unit
            };
            WorldState world = WorldGenerator.Create(_definition, settings);
            for (int day = 0; day < SimulatedDays; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            bool totalsMatch = true;
            bool citiesInCountry = true;
            bool sorted = true;
            bool anyProduction = false;
            var rows = new List<CityProduction>();
            for (int country = 0; country < world.CountryCount; country++)
            {
                for (int resource = 0; resource < world.ResourceCount; resource++)
                {
                    ProductionReport.CollectCities(_definition, world, country, resource, rows);
                    long produced = world.DailyProduced[country * world.ResourceCount + resource];
                    totalsMatch &= Sum(rows) == produced;
                    anyProduction |= produced > 0;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        citiesInCountry &= rows[i].City >= world.CountryCityStart[country] && rows[i].City < world.CountryCityStart[country + 1];
                        sorted &= i == 0 || rows[i - 1].ProducedMilli >= rows[i].ProducedMilli;
                    }
                }
            }

            _check("üretim listesi: dünyada üretim var", anyProduction);
            _check("üretim listesi: her kaynak için toplam tutar", totalsMatch);
            _check("üretim listesi: yalnızca o ülkenin şehirleri", citiesInCountry);
            _check("üretim listesi: sıralı", sorted);
        }

        private static void MakersAndUsersComeFromRecipes()
        {
            var recipes = new List<int>();

            ProductionReport.CollectMakers(_definition, _definition.ResourceIndex("steel"), recipes);
            _check("kaynak bilgisi: çeliği çelik reçetesi üretir", recipes.Count == 1 && recipes[0] == _definition.RecipeIndex("steel"));

            ProductionReport.CollectMakers(_definition, _definition.ResourceIndex("iron"), recipes);
            _check("kaynak bilgisi: demiri maden çıkarır", recipes.Count == 1 && recipes[0] == _definition.RecipeIndex("iron_mine"));

            ProductionReport.CollectMakers(_definition, _definition.ResourceIndex("aluminum"), recipes);
            _check("kaynak bilgisi: alüminyumu kimse üretmez", recipes.Count == 0);

            ProductionReport.CollectUsers(_definition, _definition.ResourceIndex("coal"), recipes);
            _check("kaynak bilgisi: kömür çelik ve santralde kullanılır",
                recipes.Count == 2 && recipes.Contains(_definition.RecipeIndex("steel")) && recipes.Contains(_definition.RecipeIndex("coal_plant")));

            ProductionReport.CollectUsers(_definition, _definition.ResourceIndex("microchip"), recipes);
            _check("kaynak bilgisi: mikroçip reçetelerde yok", recipes.Count == 0);
        }

        private static void SpecialUsesAreKnown()
        {
            int food = _definition.ResourceIndex("food");
            int machine = _definition.ResourceIndex("machine");
            int cement = _definition.ResourceIndex("cement");
            int boron = _definition.ResourceIndex("boron");

            _check("kaynak bilgisi: gıda halk tüketir", ProductionReport.IsPeopleNeed(_definition, food));
            _check("kaynak bilgisi: makine fabrika bakımı", ProductionReport.IsFactoryUpkeep(_definition, machine));
            _check("kaynak bilgisi: çimento inşaat malzemesi", ProductionReport.IsBuildMaterial(_definition, cement));
            _check("kaynak bilgisi: bor özel kullanım yok",
                !ProductionReport.IsPeopleNeed(_definition, boron)
                && !ProductionReport.IsFactoryUpkeep(_definition, boron)
                && !ProductionReport.IsBuildMaterial(_definition, boron));
        }

        private static void CycleAmountsAreNotRounded()
        {
            int microchip = _definition.RecipeIndex("microchip");
            int input = _definition.InputStart[microchip];

            _check("reçete: girdi miktarı yuvarlanmadan saklanır", _definition.InputMilliPerCycle[input] == 2 * Unit);
            _check("reçete: girdi dizileri aynı uzunlukta", _definition.InputMilliPerCycle.Length == _definition.InputMilliPerDay.Length);
        }

        private static WorldState MakeWorld(int cities)
        {
            int slots = cities * SlotsPerCity;
            var world = new WorldState(1, cities, slots, _definition.ResourceCount);
            world.CountryCityStart[1] = cities;
            world.CountryFacilityStart[1] = slots;
            world.Money[0] = 1000000 * Unit;
            world.GridCapacityMilli[0] = 1000 * Unit;
            world.EnergyRatioPermille[0] = Permille;
            world.RandomState[0] = 1;

            for (int city = 0; city < cities; city++)
            {
                world.CitySlotStart[city] = city * SlotsPerCity;
                world.Satisfaction[city] = _definition.Balance.BaseSatisfactionPermille;
                for (int slot = 0; slot < SlotsPerCity; slot++)
                {
                    world.FacilityCity[city * SlotsPerCity + slot] = city;
                }
            }

            world.CitySlotStart[cities] = slots;
            for (int resource = 0; resource < _definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = _definition.BasePriceMilli[resource];
            }

            return world;
        }

        private static int Place(WorldState world, int city, int index, string building, int level)
        {
            int slot = world.CitySlotStart[city] + index;
            int buildingIndex = _definition.BuildingIndex(building);
            world.FacilityBuilding[slot] = buildingIndex;
            world.FacilityRecipe[slot] = _definition.BuildingDefaultRecipe(buildingIndex);
            world.FacilityLevel[slot] = level;
            world.FacilityMachineDays[slot] = _definition.Balance.MachineLifeDays;
            return slot;
        }

        private static List<CityProduction> Cities(WorldState world, string resource)
        {
            var rows = new List<CityProduction>();
            ProductionReport.CollectCities(_definition, world, 0, _definition.ResourceIndex(resource), rows);
            return rows;
        }

        private static long Sum(List<CityProduction> rows)
        {
            long total = 0;
            foreach (CityProduction row in rows)
            {
                total += row.ProducedMilli;
            }

            return total;
        }

        private static long Produced(WorldState world, string resource)
        {
            return world.DailyProduced[_definition.ResourceIndex(resource)];
        }

        private static void SetStock(WorldState world, string resource, long units)
        {
            world.Stock[_definition.ResourceIndex(resource)] = units * Unit;
        }
    }
}
