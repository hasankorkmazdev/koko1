using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Bir kaynağın bir şehirdeki üretimi. Miktar, son biten günde genel stoğa giren gerçek üretimdir.
    public struct CityProduction
    {
        public int City;
        public int Working;
        public int Stopped;
        public int Building;
        public long ProducedMilli;
    }

    // Kaynak penceresinin sorguları: kaynağı hangi reçete üretir, nerede üretilir, nerede kullanılır.
    // Pencere açıkken gün başına bir kez okunur; hesap döngüsünde çağrılmaz.
    public static class ProductionReport
    {
        public const int NotFound = -1;

        private const int Permille = EconomyDefinition.Permille;

        private static readonly Comparison<CityProduction> MostProducedFirst = CompareRows;

        // Kaynağı üreten tesisi olan şehirleri, en çok üretenden başlayarak listeler.
        public static void CollectCities(EconomyDefinition definition, WorldState world, int country, int resource, List<CityProduction> rows)
        {
            rows.Clear();
            int cityEnd = world.CountryCityStart[country + 1];
            for (int city = world.CountryCityStart[country]; city < cityEnd; city++)
            {
                var row = new CityProduction { City = city };
                for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
                {
                    AddFacility(definition, world, slot, resource, ref row);
                }

                if (row.Working + row.Stopped + row.Building > 0)
                {
                    rows.Add(row);
                }
            }

            rows.Sort(MostProducedFirst);
        }

        // Reçetenin bu kaynağı veren çıktısının indeksi; vermiyorsa NotFound.
        public static int FindOutput(EconomyDefinition definition, int recipe, int resource)
        {
            for (int output = definition.OutputStart[recipe]; output < definition.OutputStart[recipe + 1]; output++)
            {
                if (definition.OutputResource[output] == resource)
                {
                    return output;
                }
            }

            return NotFound;
        }

        // Kaynağı üreten reçeteler. Tesisi olmayan reçete oyunda çalışamaz, sayılmaz.
        public static void CollectMakers(EconomyDefinition definition, int resource, List<int> recipes)
        {
            recipes.Clear();
            for (int recipe = 0; recipe < definition.RecipeCount; recipe++)
            {
                if (definition.RecipeBuilding[recipe] != WorldState.NoBuilding && FindOutput(definition, recipe, resource) != NotFound)
                {
                    recipes.Add(recipe);
                }
            }
        }

        // Kaynağı girdi olarak kullanan reçeteler.
        public static void CollectUsers(EconomyDefinition definition, int resource, List<int> recipes)
        {
            recipes.Clear();
            for (int recipe = 0; recipe < definition.RecipeCount; recipe++)
            {
                if (definition.RecipeBuilding[recipe] != WorldState.NoBuilding && HasInput(definition, recipe, resource))
                {
                    recipes.Add(recipe);
                }
            }
        }

        public static bool IsPeopleNeed(EconomyDefinition definition, int resource)
        {
            return resource == definition.Food
                || resource == definition.Medicine
                || resource == definition.Fuel
                || resource == definition.Electronics;
        }

        public static bool IsFactoryUpkeep(EconomyDefinition definition, int resource)
        {
            return resource == definition.Machine || resource == definition.Consumable;
        }

        public static bool IsBuildMaterial(EconomyDefinition definition, int resource)
        {
            return Array.IndexOf(definition.BuildingMaterialResource, resource) >= 0;
        }

        private static void AddFacility(EconomyDefinition definition, WorldState world, int slot, int resource, ref CityProduction row)
        {
            int recipe = world.FacilityRecipe[slot];
            if (world.FacilityBuilding[slot] == WorldState.NoBuilding || recipe == WorldState.NoRecipe)
            {
                return;
            }

            int output = FindOutput(definition, recipe, resource);
            if (output == NotFound)
            {
                return;
            }

            if (world.FacilityBuildDays[slot] > 0)
            {
                row.Building++;
                return;
            }

            if (world.FacilityAlarm[slot] == WorldState.AlarmActive)
            {
                row.Stopped++;
            }
            else
            {
                row.Working++;
            }

            long amount = definition.OutputMilliPerDay[output] * world.FacilityOutputSpeedPermille[slot] / Permille;
            row.ProducedMilli += EconomySimulator.StockShareMilli(world, slot, amount);
        }

        private static bool HasInput(EconomyDefinition definition, int recipe, int resource)
        {
            for (int input = definition.InputStart[recipe]; input < definition.InputStart[recipe + 1]; input++)
            {
                if (definition.InputResource[input] == resource)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareRows(CityProduction a, CityProduction b)
        {
            int byAmount = b.ProducedMilli.CompareTo(a.ProducedMilli);
            return byAmount != 0 ? byAmount : a.City.CompareTo(b.City);
        }
    }
}
