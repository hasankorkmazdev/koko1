using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    public enum RecipeKind : byte
    {
        Extraction,
        Factory,
        Energy
    }

    public enum TransportKind : byte
    {
        Road,
        Sea,
        Air
    }

    // Reçeteler hesap sırasında hızlı okunsun diye düz dizilere çevrilir.
    // Miktarlar binde birim (milli) ve gün başınadır: 5 günde 1 altın, günde 200 millidir.
    public sealed class EconomyDefinition
    {
        public const int MilliPerUnit = 1000;
        public const int Permille = 1000;
        public const long Million = 1000000;

        public int ResourceCount;
        public int RecipeCount;
        public string[] ResourceKeys;
        public string[] RecipeKeys;
        public long[] BasePriceMilli;

        public RecipeKind[] Kinds;
        public int[] InputStart;
        public int[] InputResource;
        public long[] InputMilliPerDay;
        public int[] OutputStart;
        public int[] OutputResource;
        public long[] OutputMilliPerDay;
        public long[] EnergyMilliPerDay;

        // Reçetede yazan miktar: 3 günde 1 makine için 1000. Günlük miktar yuvarlandığı için ekran bunu kullanır.
        public long[] InputMilliPerCycle;
        public long[] OutputMilliPerCycle;
        public long[] EnergyMilliPerCycle;
        public int[] RecipeDays;
        public int[] RecipeBuilding;

        // Tesis türleri
        public int BuildingCount;
        public string[] BuildingKeys;
        public string[] BuildingTabs;
        public bool[] BuildingLocked;
        public bool[] BuildingCoastal;
        public long[] BuildingMoneyMilli;
        public int[] BuildingDays;
        public int[] BuildingRecipeStart;
        public int[] BuildingRecipes;
        public int[] BuildingMaterialStart;
        public int[] BuildingMaterialResource;
        public long[] BuildingMaterialMilli;

        // Biri diğerine girdi veren iki fabrika türü: [a * BuildingCount + b]
        public bool[] BuildingLinked;

        // Sonraki seviyenin açılması için gereken deneyim; son seviyede 0.
        public long[] ExperienceToLevelUp;

        // Ulaşım türleri
        public int TransportCount;
        public string[] TransportKeys;
        public TransportKind[] TransportKinds;
        public int[] TransportSpeedPermille;
        public int[] TransportCostPermille;
        public long[] TransportTripMilli;
        public int[] TransportBuilding;
        public bool[] BuildingIsTransport;

        public int Food;
        public int Medicine;
        public int Fuel;
        public int Electronics;
        public int Consumable;
        public int Machine;

        public BalanceData Balance;

        private Dictionary<string, int> _resourceIndex;
        private Dictionary<string, int> _recipeIndex;
        private Dictionary<string, int> _buildingIndex;

        public int BuildingIndex(string key)
        {
            return _buildingIndex[key];
        }

        public int BuildingDefaultRecipe(int building)
        {
            return BuildingRecipeStart[building + 1] > BuildingRecipeStart[building]
                ? BuildingRecipes[BuildingRecipeStart[building]]
                : WorldState.NoRecipe;
        }

        public int ResourceIndex(string key)
        {
            return _resourceIndex[key];
        }

        public int RecipeIndex(string key)
        {
            return _recipeIndex[key];
        }

        public static EconomyDefinition Build(EconomyData data)
        {
            var definition = new EconomyDefinition
            {
                Balance = data.Balance,
                ResourceCount = data.Resources.Count,
                RecipeCount = data.Recipes.Count,
                ResourceKeys = new string[data.Resources.Count],
                RecipeKeys = new string[data.Recipes.Count],
                BasePriceMilli = new long[data.Resources.Count],
                Kinds = new RecipeKind[data.Recipes.Count],
                InputStart = new int[data.Recipes.Count + 1],
                OutputStart = new int[data.Recipes.Count + 1],
                EnergyMilliPerDay = new long[data.Recipes.Count],
                EnergyMilliPerCycle = new long[data.Recipes.Count],
                RecipeDays = new int[data.Recipes.Count],
                RecipeBuilding = new int[data.Recipes.Count],
                _resourceIndex = new Dictionary<string, int>(StringComparer.Ordinal),
                _recipeIndex = new Dictionary<string, int>(StringComparer.Ordinal)
            };

            for (int i = 0; i < data.Resources.Count; i++)
            {
                definition.ResourceKeys[i] = data.Resources[i].Key;
                definition.BasePriceMilli[i] = data.Resources[i].BasePrice * MilliPerUnit;
                definition._resourceIndex.Add(data.Resources[i].Key, i);
            }

            var inputResources = new List<int>();
            var inputAmounts = new List<long>();
            var inputCycles = new List<long>();
            var outputResources = new List<int>();
            var outputAmounts = new List<long>();
            var outputCycles = new List<long>();

            for (int i = 0; i < data.Recipes.Count; i++)
            {
                RecipeData recipe = data.Recipes[i];
                definition.RecipeKeys[i] = recipe.Key;
                definition._recipeIndex.Add(recipe.Key, i);
                definition.Kinds[i] = ParseKind(recipe.Kind);
                definition.EnergyMilliPerDay[i] = PerDay(recipe.EnergyOutput, recipe.Days);
                definition.EnergyMilliPerCycle[i] = recipe.EnergyOutput * (long)MilliPerUnit;
                definition.RecipeDays[i] = recipe.Days;

                definition.InputStart[i] = inputResources.Count;
                AddAmounts(definition, recipe.Inputs, recipe.Days, inputResources, inputAmounts);
                if (recipe.Inputs != null)
                {
                    foreach (RecipeAmount input in recipe.Inputs)
                    {
                        inputCycles.Add(input.Amount * (long)MilliPerUnit);
                    }
                }

                definition.OutputStart[i] = outputResources.Count;
                AddAmounts(definition, recipe.Outputs, recipe.Days, outputResources, outputAmounts);
                if (recipe.Outputs != null)
                {
                    foreach (RecipeAmount output in recipe.Outputs)
                    {
                        outputCycles.Add(output.Amount * (long)MilliPerUnit);
                    }
                }
            }

            definition.InputStart[data.Recipes.Count] = inputResources.Count;
            definition.OutputStart[data.Recipes.Count] = outputResources.Count;
            definition.InputResource = inputResources.ToArray();
            definition.InputMilliPerDay = inputAmounts.ToArray();
            definition.InputMilliPerCycle = inputCycles.ToArray();
            definition.OutputResource = outputResources.ToArray();
            definition.OutputMilliPerDay = outputAmounts.ToArray();
            definition.OutputMilliPerCycle = outputCycles.ToArray();

            definition.Food = definition.ResourceIndex("food");
            definition.Medicine = definition.ResourceIndex("medicine");
            definition.Fuel = definition.ResourceIndex("fuel");
            definition.Electronics = definition.ResourceIndex("electronics");
            definition.Consumable = definition.ResourceIndex("consumable");
            definition.Machine = definition.ResourceIndex("machine");

            BuildBuildings(definition, data);
            BuildBuildingLinks(definition);
            BuildExperienceTable(definition);
            BuildTransports(definition, data);
            return definition;
        }

        private static void BuildTransports(EconomyDefinition definition, EconomyData data)
        {
            int count = data.Transport.Count;
            definition.TransportCount = count;
            definition.TransportKeys = new string[count];
            definition.TransportKinds = new TransportKind[count];
            definition.TransportSpeedPermille = new int[count];
            definition.TransportCostPermille = new int[count];
            definition.TransportTripMilli = new long[count];
            definition.TransportBuilding = new int[count];
            definition.BuildingIsTransport = new bool[definition.BuildingCount];

            for (int i = 0; i < count; i++)
            {
                TransportData transport = data.Transport[i];
                definition.TransportKeys[i] = transport.Key;
                definition.TransportKinds[i] = ParseTransport(transport.Key);
                definition.TransportSpeedPermille[i] = transport.SpeedPermille;
                definition.TransportCostPermille[i] = transport.CostPermille;
                definition.TransportTripMilli[i] = definition.Balance.RoadCapacityUnits * MilliPerUnit * transport.CapacityPermille / Permille;
                definition.TransportBuilding[i] = WorldState.NoBuilding;
                if (!string.IsNullOrEmpty(transport.Building))
                {
                    int building = definition.BuildingIndex(transport.Building);
                    definition.TransportBuilding[i] = building;
                    definition.BuildingIsTransport[building] = true;
                }
            }
        }

        private static TransportKind ParseTransport(string key)
        {
            switch (key)
            {
                case "road": return TransportKind.Road;
                case "sea": return TransportKind.Sea;
                case "air": return TransportKind.Air;
                default: throw new ArgumentException("Bilinmeyen ulaşım türü: " + key);
            }
        }

        private static void BuildBuildings(EconomyDefinition definition, EconomyData data)
        {
            int count = data.Buildings.Count;
            definition.BuildingCount = count;
            definition.BuildingKeys = new string[count];
            definition.BuildingTabs = new string[count];
            definition.BuildingLocked = new bool[count];
            definition.BuildingCoastal = new bool[count];
            definition.BuildingMoneyMilli = new long[count];
            definition.BuildingDays = new int[count];
            definition.BuildingRecipeStart = new int[count + 1];
            definition.BuildingMaterialStart = new int[count + 1];
            definition._buildingIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            var recipes = new List<int>();
            var materialResources = new List<int>();
            var materialAmounts = new List<long>();

            for (int recipe = 0; recipe < definition.RecipeCount; recipe++)
            {
                definition.RecipeBuilding[recipe] = WorldState.NoBuilding;
            }

            for (int i = 0; i < count; i++)
            {
                BuildingData building = data.Buildings[i];
                definition.BuildingKeys[i] = building.Key;
                definition.BuildingTabs[i] = building.Tab;
                definition.BuildingLocked[i] = building.Locked;
                definition.BuildingCoastal[i] = building.Coastal;
                definition.BuildingMoneyMilli[i] = building.Money * MilliPerUnit;
                definition.BuildingDays[i] = building.Days;
                definition._buildingIndex.Add(building.Key, i);

                definition.BuildingRecipeStart[i] = recipes.Count;
                if (building.Recipes != null)
                {
                    foreach (string recipeKey in building.Recipes)
                    {
                        int recipe = definition.RecipeIndex(recipeKey);
                        recipes.Add(recipe);
                        definition.RecipeBuilding[recipe] = i;
                    }
                }

                definition.BuildingMaterialStart[i] = materialResources.Count;
                if (building.Materials != null)
                {
                    foreach (RecipeAmount material in building.Materials)
                    {
                        materialResources.Add(definition.ResourceIndex(material.Key));
                        materialAmounts.Add(material.Amount * (long)MilliPerUnit);
                    }
                }
            }

            definition.BuildingRecipeStart[count] = recipes.Count;
            definition.BuildingMaterialStart[count] = materialResources.Count;
            definition.BuildingRecipes = recipes.ToArray();
            definition.BuildingMaterialResource = materialResources.ToArray();
            definition.BuildingMaterialMilli = materialAmounts.ToArray();
        }

        // Bağlantılar reçetelerden otomatik çıkar: fabrikanın ürünü diğerinin girdisiyse kombina olur.
        private static void BuildBuildingLinks(EconomyDefinition definition)
        {
            int count = definition.BuildingCount;
            definition.BuildingLinked = new bool[count * count];
            for (int a = 0; a < count; a++)
            {
                for (int b = 0; b < count; b++)
                {
                    if (a != b && definition.IsFactory(a) && definition.IsFactory(b) && (definition.Feeds(a, b) || definition.Feeds(b, a)))
                    {
                        definition.BuildingLinked[a * count + b] = true;
                    }
                }
            }
        }

        public bool IsFactory(int building)
        {
            int recipe = BuildingDefaultRecipe(building);
            return recipe != WorldState.NoRecipe && Kinds[recipe] == RecipeKind.Factory;
        }

        // Tesisin tüm reçetelerindeki girdiler, her kaynak bir kez. Ekran içindir; günlük hesapta çağrılmaz.
        public List<int> BuildingInputResources(int building)
        {
            return CollectResources(building, InputStart, InputResource);
        }

        // Tesisin tüm reçetelerindeki ürünler, her kaynak bir kez. Ekran içindir; günlük hesapta çağrılmaz.
        public List<int> BuildingOutputResources(int building)
        {
            return CollectResources(building, OutputStart, OutputResource);
        }

        private List<int> CollectResources(int building, int[] starts, int[] resources)
        {
            var found = new List<int>();
            for (int i = BuildingRecipeStart[building]; i < BuildingRecipeStart[building + 1]; i++)
            {
                int recipe = BuildingRecipes[i];
                for (int entry = starts[recipe]; entry < starts[recipe + 1]; entry++)
                {
                    if (!found.Contains(resources[entry]))
                    {
                        found.Add(resources[entry]);
                    }
                }
            }

            return found;
        }

        private bool Feeds(int from, int to)
        {
            for (int i = BuildingRecipeStart[from]; i < BuildingRecipeStart[from + 1]; i++)
            {
                int fromRecipe = BuildingRecipes[i];
                for (int output = OutputStart[fromRecipe]; output < OutputStart[fromRecipe + 1]; output++)
                {
                    for (int j = BuildingRecipeStart[to]; j < BuildingRecipeStart[to + 1]; j++)
                    {
                        int toRecipe = BuildingRecipes[j];
                        for (int input = InputStart[toRecipe]; input < InputStart[toRecipe + 1]; input++)
                        {
                            if (InputResource[input] == OutputResource[output])
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static void BuildExperienceTable(EconomyDefinition definition)
        {
            BalanceData balance = definition.Balance;
            definition.ExperienceToLevelUp = new long[balance.MaxFacilityLevel + 1];
            long need = (long)balance.ExperienceBaseDays * Permille;
            for (int level = 1; level < balance.MaxFacilityLevel; level++)
            {
                definition.ExperienceToLevelUp[level] = need;
                need = need * balance.ExperienceGrowthPermille / Permille;
            }
        }

        private static void AddAmounts(
            EconomyDefinition definition,
            List<RecipeAmount> amounts,
            int days,
            List<int> resources,
            List<long> perDay)
        {
            if (amounts == null)
            {
                return;
            }

            foreach (RecipeAmount amount in amounts)
            {
                resources.Add(definition.ResourceIndex(amount.Key));
                perDay.Add(PerDay(amount.Amount, days));
            }
        }

        private static long PerDay(int amount, int days)
        {
            return amount * (long)MilliPerUnit / days;
        }

        private static RecipeKind ParseKind(string kind)
        {
            switch (kind)
            {
                case "extraction": return RecipeKind.Extraction;
                case "factory": return RecipeKind.Factory;
                case "energy": return RecipeKind.Energy;
                default: throw new ArgumentException("Bilinmeyen reçete türü: " + kind);
            }
        }
    }
}
