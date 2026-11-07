using System;

namespace GeoWar.Simulation
{
    public sealed class WorldGeneratorSettings
    {
        public int CountryCount = 200;
        public int CityCount = 5000;

        // Doluysa ülke ve şehir sayısını bu liste belirler: haritadaki iller şehirlerle bire bir eşleşir.
        public int[] CitiesPerCountry;

        // Doluysa şehirler bu nüfuslarla başlar. Boşsa örnek dünya için aralıktan rastgele seçilir.
        public long[] CityPopulations;
        public int FacilitiesPerCity = 4;
        public ulong Seed = 1;
        public int ExtractionPercent = 35;
        public int EnergyPercent = 10;
        public int PrivatePercent = 30;
        public int ReadyPercent = 15;
        public int MinPopulation = 100000;
        public int MaxPopulation = 2000000;
        public long StartingStockUnits = 2000000;
        public long StartingMoney = 500000000;
        public int MaxStartingLevel = 3;
        public int MinDeposits = 1;
        public int MaxDeposits = 3;
        public int GridCapacityFactorPercent = 150;
        public int GridLossPermille = 100;
        public int TaxPercent = 20;
        public int StartingSatisfactionPermille = 700;
    }

    // Ölçüm ve testler için örnek dünya üretir. Gerçek ülke verisi geldiğinde onun yerini veri yükleyici alır.
    public static class WorldGenerator
    {
        private const int Percent = 100;
        private const int LatitudeSpan = 120;
        private const int LatitudeSouth = -50;
        private const int LongitudeSpan = 360;
        private const int LongitudeWest = -180;

        private static readonly int[] StateShares = { 0, 250, 500 };

        public static WorldState Create(EconomyDefinition definition, WorldGeneratorSettings settings)
        {
            ApplyCityList(settings);
            ulong random = settings.Seed;

            // Slot sayısı nüfusa bağlı olduğu için önce nüfuslar üretilir, dizi boyutu buradan çıkar.
            var populations = new long[settings.CityCount];
            var slots = new int[settings.CityCount];
            int slotCount = 0;
            for (int city = 0; city < settings.CityCount; city++)
            {
                populations[city] = settings.CityPopulations != null
                    ? settings.CityPopulations[city]
                    : settings.MinPopulation + DeterministicRandom.Range(ref random, settings.MaxPopulation - settings.MinPopulation);
                slots[city] = Math.Max(FacilityCommands.CitySlots(definition.Balance, populations[city]), settings.FacilitiesPerCity);
                slotCount += slots[city];
            }

            var world = new WorldState(settings.CountryCount, settings.CityCount, slotCount, definition.ResourceCount, definition.Balance.RebelStateSlots);
            int[] extraction = BuildingsOf(definition, RecipeKind.Extraction);
            int[] factories = BuildingsOf(definition, RecipeKind.Factory);
            int[] energy = BuildingsOf(definition, RecipeKind.Energy);

            for (int resource = 0; resource < definition.ResourceCount; resource++)
            {
                world.PriceMilli[resource] = definition.BasePriceMilli[resource];
            }

            var capitalLat = new double[settings.CountryCount];
            var capitalLon = new double[settings.CountryCount];

            int nextCity = 0;
            int nextSlot = 0;
            for (int country = 0; country < settings.CountryCount; country++)
            {
                world.CountryCityStart[country] = nextCity;
                world.CountryFacilityStart[country] = nextSlot;
                world.RandomState[country] = DeterministicRandom.Next(ref random);
                world.Money[country] = settings.StartingMoney;
                world.TaxPercent[country] = settings.TaxPercent;
                world.GridLossPermille[country] = settings.GridLossPermille;

                // Örnek başkent konumu. Gerçek haritada oyun bunu il merkeziyle değiştirir.
                ulong place = DeterministicRandom.Next(ref random);
                capitalLat[country] = LatitudeSouth + DeterministicRandom.Range(ref place, LatitudeSpan);
                capitalLon[country] = LongitudeWest + DeterministicRandom.Range(ref place, LongitudeSpan);

                world.EnergyRatioPermille[country] = EconomyDefinition.Permille;

                for (int resource = 0; resource < definition.ResourceCount; resource++)
                {
                    world.Stock[country * definition.ResourceCount + resource] = settings.StartingStockUnits * EconomyDefinition.MilliPerUnit;
                }

                // Liste yoksa şehirler ülkelere eşit dağıtılır, kalan son ülkelere geçer.
                int cityEnd = settings.CitiesPerCountry != null
                    ? nextCity + settings.CitiesPerCountry[country]
                    : (int)((long)settings.CityCount * (country + 1) / settings.CountryCount);
                long population = 0;
                long energyDemand = 0;
                for (; nextCity < cityEnd; nextCity++)
                {
                    world.Population[nextCity] = populations[nextCity];
                    world.CityOwner[nextCity] = country;
                    world.Satisfaction[nextCity] = settings.StartingSatisfactionPermille;
                    world.CitySlotStart[nextCity] = nextSlot;
                    population += populations[nextCity];
                    PickDeposits(world, settings, ref random, nextCity, extraction);

                    for (int i = 0; i < slots[nextCity]; i++)
                    {
                        world.FacilityCity[nextSlot + i] = nextCity;
                    }

                    for (int i = 0; i < settings.FacilitiesPerCity; i++)
                    {
                        int building = PickBuilding(definition, world, settings, ref random, nextCity, nextSlot, i, extraction, factories, energy);
                        PlaceFacility(definition, world, settings, ref random, nextSlot + i, building);
                        if (definition.IsFactory(building))
                        {
                            energyDemand += definition.Balance.FactoryEnergyNeedMilli;
                        }
                    }

                    nextSlot += slots[nextCity];
                    world.CitySlotStart[nextCity + 1] = nextSlot;
                    FacilityCommands.RecomputeKombina(definition, world, nextCity);
                }

                world.CountryPopulation[country] = population;
                energyDemand += population * definition.Balance.PopulationEnergyMilliPerMillion / EconomyDefinition.Million;
                world.GridCapacityMilli[country] = energyDemand * settings.GridCapacityFactorPercent / Percent;
            }

            // Boş isyancı devlet yerlerinin şehri ve parası yoktur; zar dizileri yine de seed'den gelir.
            for (int country = settings.CountryCount; country <= world.CountryCount; country++)
            {
                world.CountryCityStart[country] = nextCity;
                world.CountryFacilityStart[country] = nextSlot;
                if (country < world.CountryCount)
                {
                    world.RandomState[country] = DeterministicRandom.Next(ref random);
                    world.EnergyRatioPermille[country] = EconomyDefinition.Permille;
                }
            }

            CountryDistances.SetFromCapitals(world, capitalLat, capitalLon);

            // Örnek komşuluk: ülkeler halka biçiminde bağlıdır. Gerçek haritada oyun bunu harita sınırlarıyla değiştirir.
            for (int country = 0; country < settings.CountryCount; country++)
            {
                CountryRoutes.SetLandNeighbors(world, country, (country + 1) % settings.CountryCount);
            }

            SetSampleCityNeighbors(world);
            return world;
        }

        // Örnek komşuluk: şehirler sırayla zincir gibi bağlıdır.
        private static void SetSampleCityNeighbors(WorldState world)
        {
            int cityCount = world.CityCount;
            var start = new int[cityCount + 1];
            var neighbors = new int[Math.Max(0, (cityCount - 1) * 2)];
            int next = 0;
            for (int city = 0; city < cityCount; city++)
            {
                start[city] = next;
                if (city > 0)
                {
                    neighbors[next++] = city - 1;
                }

                if (city < cityCount - 1)
                {
                    neighbors[next++] = city + 1;
                }
            }

            start[cityCount] = next;
            CityOwnership.SetCityNeighbors(world, start, neighbors);
        }

        private static void ApplyCityList(WorldGeneratorSettings settings)
        {
            if (settings.CitiesPerCountry == null)
            {
                return;
            }

            settings.CountryCount = settings.CitiesPerCountry.Length;
            settings.CityCount = 0;
            foreach (int cities in settings.CitiesPerCountry)
            {
                settings.CityCount += cities;
            }
        }

        private static void PickDeposits(WorldState world, WorldGeneratorSettings settings, ref ulong random, int city, int[] extraction)
        {
            int count = settings.MinDeposits + DeterministicRandom.Range(ref random, settings.MaxDeposits - settings.MinDeposits + 1);
            for (int i = 0; i < count; i++)
            {
                world.CityDeposits[city] |= 1L << extraction[DeterministicRandom.Range(ref random, extraction.Length)];
            }
        }

        private static void PlaceFacility(EconomyDefinition definition, WorldState world, WorldGeneratorSettings settings, ref ulong random, int slot, int building)
        {
            int recipeStart = definition.BuildingRecipeStart[building];
            int recipeCount = definition.BuildingRecipeStart[building + 1] - recipeStart;
            int recipe = definition.BuildingRecipes[recipeStart + DeterministicRandom.Range(ref random, recipeCount)];

            world.FacilityBuilding[slot] = building;
            world.FacilityRecipe[slot] = recipe;
            world.FacilityLevel[slot] = 1 + DeterministicRandom.Range(ref random, settings.MaxStartingLevel);
            world.FacilityMachineDays[slot] = 1 + DeterministicRandom.Range(ref random, definition.Balance.MachineLifeDays);

            long need = FacilityCommands.ExperienceNeeded(definition, world.FacilityLevel[slot]);
            bool isReady = DeterministicRandom.Range(ref random, Percent) < settings.ReadyPercent;
            world.FacilityExperience[slot] = isReady ? need : need * DeterministicRandom.Range(ref random, Percent) / Percent;

            if (definition.Kinds[recipe] != RecipeKind.Energy && DeterministicRandom.Range(ref random, Percent) < settings.PrivatePercent)
            {
                world.FacilityPrivate[slot] = true;
                world.FacilityStateSharePermille[slot] = StateShares[DeterministicRandom.Range(ref random, StateShares.Length)];
            }
        }

        // Çıkarma tesisi yalnızca şehirde bulunan ve henüz çıkarılmayan kaynağa kurulur; kalmadıysa fabrika seçilir.
        private static int PickBuilding(
            EconomyDefinition definition,
            WorldState world,
            WorldGeneratorSettings settings,
            ref ulong random,
            int city,
            int firstSlot,
            int placed,
            int[] extraction,
            int[] factories,
            int[] energy)
        {
            int roll = DeterministicRandom.Range(ref random, Percent);
            if (roll < settings.ExtractionPercent)
            {
                foreach (int building in extraction)
                {
                    if (FacilityCommands.HasDeposit(world, city, building) && !IsPlaced(world, firstSlot, placed, building))
                    {
                        return building;
                    }
                }
            }
            else if (roll < settings.ExtractionPercent + settings.EnergyPercent)
            {
                return energy[DeterministicRandom.Range(ref random, energy.Length)];
            }

            return factories[DeterministicRandom.Range(ref random, factories.Length)];
        }

        private static bool IsPlaced(WorldState world, int firstSlot, int placed, int building)
        {
            for (int i = 0; i < placed; i++)
            {
                if (world.FacilityBuilding[firstSlot + i] == building)
                {
                    return true;
                }
            }

            return false;
        }

        private static int[] BuildingsOf(EconomyDefinition definition, RecipeKind kind)
        {
            int count = 0;
            for (int building = 0; building < definition.BuildingCount; building++)
            {
                int recipe = definition.BuildingDefaultRecipe(building);
                if (recipe != WorldState.NoRecipe && definition.Kinds[recipe] == kind)
                {
                    count++;
                }
            }

            var buildings = new int[count];
            int next = 0;
            for (int building = 0; building < definition.BuildingCount; building++)
            {
                int recipe = definition.BuildingDefaultRecipe(building);
                if (recipe != WorldState.NoRecipe && definition.Kinds[recipe] == kind)
                {
                    buildings[next++] = building;
                }
            }

            return buildings;
        }
    }
}
