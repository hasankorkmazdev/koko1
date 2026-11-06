using System;
using System.Collections.Generic;
using GeoWar.Game;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    public static class CountryReportTests
    {
        public static void Run(Action<string, bool> check, EconomyDefinition definition)
        {
            CountsCitiesOfEachCountry(check, definition);
            FindsNeighbors(check, definition);
            ListsWarsOfACountry(check, definition);
            ListsDepositsOfACity(check, definition);
            DepositsPointToTheirResource(check, definition);
            GenitiveFollowsVowelHarmony(check);
            BriefDescribesTheCity(check);
        }

        private static WorldState MakeWorld(EconomyDefinition definition)
        {
            return WorldGenerator.Create(definition, new WorldGeneratorSettings { CitiesPerCountry = new[] { 3, 5, 2, 4 }, FacilitiesPerCity = 0, Seed = 3 });
        }

        private static void CountsCitiesOfEachCountry(Action<string, bool> check, EconomyDefinition definition)
        {
            WorldState world = MakeWorld(definition);
            check("ülke: şehir sayısı", CountryReport.CityCount(world, 0) == 3 && CountryReport.CityCount(world, 1) == 5);
            check("ülke: son ülkenin şehir sayısı", CountryReport.CityCount(world, 3) == 4);
        }

        private static void FindsNeighbors(Action<string, bool> check, EconomyDefinition definition)
        {
            WorldState world = MakeWorld(definition);
            Array.Clear(world.CountryLandNeighbor, 0, world.CountryLandNeighbor.Length);
            CountryRoutes.SetLandNeighbors(world, 0, 2);

            check("ülke: komşuluk iki yönlüdür", CountryReport.AreNeighbors(world, 0, 2) && CountryReport.AreNeighbors(world, 2, 0));
            check("ülke: sınırı olmayan komşu değildir", !CountryReport.AreNeighbors(world, 0, 1));
        }

        private static void ListsWarsOfACountry(Action<string, bool> check, EconomyDefinition definition)
        {
            WorldState world = MakeWorld(definition);
            var enemies = new List<int> { 99 };

            CountryReport.CollectEnemies(world, 0, enemies);
            check("savaş: barışta liste boştur", enemies.Count == 0);

            CountryRoutes.SetWar(world, 0, 3, true);
            CountryRoutes.SetWar(world, 0, 1, true);
            CountryReport.CollectEnemies(world, 0, enemies);
            check("savaş: düşmanlar ülke sırasıyla gelir", enemies.Count == 2 && enemies[0] == 1 && enemies[1] == 3);

            CountryReport.CollectEnemies(world, 3, enemies);
            check("savaş: karşı taraf da savaştadır", enemies.Count == 1 && enemies[0] == 0);

            CountryReport.CollectEnemies(world, 2, enemies);
            check("savaş: başkasının savaşı karışmaz", enemies.Count == 0);

            CountryRoutes.SetWar(world, 0, 3, false);
            CountryReport.CollectEnemies(world, 3, enemies);
            check("savaş: barış listeden çıkarır", enemies.Count == 0);
        }

        private static void ListsDepositsOfACity(Action<string, bool> check, EconomyDefinition definition)
        {
            WorldState world = MakeWorld(definition);
            int iron = definition.BuildingIndex("iron_mine");
            int farm = definition.BuildingIndex("farm");
            int steel = definition.BuildingIndex("steel_factory");
            var deposits = new List<int> { 99 };

            world.CityDeposits[0] = 0;
            CountryReport.CollectDeposits(definition, world, 0, deposits);
            check("rezerv: yoksa liste boştur", deposits.Count == 0);

            world.CityDeposits[0] = (1L << farm) | (1L << iron) | (1L << steel);
            CountryReport.CollectDeposits(definition, world, 0, deposits);
            check("rezerv: yalnızca çıkarma tesisleri gelir", deposits.Count == 2 && deposits.Contains(iron) && deposits.Contains(farm));
        }

        private static void DepositsPointToTheirResource(Action<string, bool> check, EconomyDefinition definition)
        {
            check("rezerv: demir madeni demir verir", CountryReport.DepositResource(definition, definition.BuildingIndex("iron_mine")) == definition.ResourceIndex("iron"));
            check("rezerv: tarla gıda verir", CountryReport.DepositResource(definition, definition.BuildingIndex("farm")) == definition.ResourceIndex("food"));
            check("rezerv: kuyu petrol verir", CountryReport.DepositResource(definition, definition.BuildingIndex("oil_well")) == definition.ResourceIndex("oil"));

            bool allHaveResource = true;
            for (int building = 0; building < definition.BuildingCount; building++)
            {
                if (FacilityCommands.IsExtraction(definition, building))
                {
                    int resource = CountryReport.DepositResource(definition, building);
                    allHaveResource &= resource >= 0 && resource < definition.ResourceCount;
                }
            }

            check("rezerv: her çıkarma tesisi bir kaynak verir", allHaveResource);
        }

        private static void GenitiveFollowsVowelHarmony(Action<string, bool> check)
        {
            check("ek: ünlüyle biten ad", NumberFormat.Genitive("Türkiye") == "Türkiye'nin");
            check("ek: ünsüzle biten kalın ad", NumberFormat.Genitive("Irak") == "Irak'ın");
            check("ek: büyük İ küçülünce ünlü bozulmaz", NumberFormat.Genitive("İran") == "İran'ın");
            check("ek: uzun ad", NumberFormat.Genitive("Bulgaristan") == "Bulgaristan'ın");
            check("ek: ince ad", NumberFormat.Genitive("Suriye") == "Suriye'nin");
            check("ek: yuvarlak kalın ad", NumberFormat.Genitive("Peru") == "Peru'nun");
            check("ek: yuvarlak ince ad", NumberFormat.Genitive("Ürdün") == "Ürdün'ün");
            check("ek: a ile biten ad", NumberFormat.Genitive("Rusya") == "Rusya'nın");
        }

        private static void BriefDescribesTheCity(Action<string, bool> check)
        {
            check("bilgi: başkent", CityBrief.Text("Türkiye", 81, true, 5800000, false, 2) == "Türkiye'nin başkenti. Nüfusu 5,8 Mn, denize kıyısı yok. 2 tesis kurulu.");
            check("bilgi: kıyı şehri", CityBrief.Text("Irak", 18, false, 950000, true, 1) == "Irak'ın 18 şehrinden biri. Nüfusu 950 B, denize kıyısı var. 1 tesis kurulu.");
            check("bilgi: tesissiz şehir", CityBrief.Text("Suriye", 15, false, 999, false, 0) == "Suriye'nin 15 şehrinden biri. Nüfusu 999, denize kıyısı yok. Tesis yok.");
        }
    }
}
