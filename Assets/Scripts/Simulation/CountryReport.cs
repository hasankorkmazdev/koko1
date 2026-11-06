using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Ülke ve şehir panellerinin sorguları: şehir sayısı, komşuluk, savaşlar ve rezervler.
    // Panel açılınca veya gün başına bir kez okunur; hesap döngüsünde çağrılmaz.
    public static class CountryReport
    {
        public static int CityCount(WorldState world, int country)
        {
            return world.CountryCityStart[country + 1] - world.CountryCityStart[country];
        }

        public static bool AreNeighbors(WorldState world, int a, int b)
        {
            return world.CountryLandNeighbor[a * world.CountryCount + b];
        }

        // Ülkenin savaştığı ülkeler, ülke sırasıyla.
        public static void CollectEnemies(WorldState world, int country, List<int> enemies)
        {
            enemies.Clear();
            for (int other = 0; other < world.CountryCount; other++)
            {
                if (world.CountryAtWar[country * world.CountryCount + other])
                {
                    enemies.Add(other);
                }
            }
        }

        // Şehirde keşfedilmiş rezervlerin çıkarma tesisleri, tesis sırasıyla.
        public static void CollectDeposits(EconomyDefinition definition, WorldState world, int city, List<int> buildings)
        {
            buildings.Clear();
            for (int building = 0; building < definition.BuildingCount; building++)
            {
                if (FacilityCommands.IsExtraction(definition, building) && FacilityCommands.HasDeposit(world, city, building))
                {
                    buildings.Add(building);
                }
            }
        }

        // Çıkarma tesisinin verdiği kaynak: rezervin ikonu ve rengi buradan gelir.
        public static int DepositResource(EconomyDefinition definition, int building)
        {
            int recipe = definition.BuildingDefaultRecipe(building);
            return definition.OutputResource[definition.OutputStart[recipe]];
        }
    }
}
