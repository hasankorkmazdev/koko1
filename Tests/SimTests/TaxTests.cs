using System;
using GeoWar.Simulation;

namespace GeoWar.Tests
{
    // Şehir bazında vergi ve ocakta yürürlüğe girme: docs/ECONOMY.md 1.2 ve docs/MECHANICS.md 10.2.
    public static class TaxTests
    {
        private const int Player = 0;
        private const int Ai = 1;
        private const int StartRate = 20;
        private const int LowRate = 10;
        private const int HighRate = 60;
        private const int DaysIn2027 = 365;
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

            BalanceAllowsSteps();
            CitiesStartAtSettingsRate();
            DecisionTouchesOnlyThatCity();
            BadRequestsAreRejected();
            SetAllCoversOnlyOwnCities();
            StepsStayInsideLimits();
            IncomeWaitsForJanuary();
            NewRateStartsOnFirstOfJanuary();
            DecisionCanChangeUntilJanuary();
            RateChangesAtMostOncePerYear();
            NextChangeYearFollowsTheDate();
            IncomeFollowsEachCity();
            ZeroTaxPaysNothing();
            HigherTaxLowersSatisfactionOnlyThere();
            ConcessionKeepsTheRate();
            RateStaysWithCityAfterHandover();
            ChecksumSeesTheDecision();
        }

        private static void BalanceAllowsSteps()
        {
            _check("vergi: adım sınırın içinde", _balance.TaxStepPercent > 0 && _balance.TaxStepPercent <= _balance.TaxMaxPercent);
            _check("vergi: başlangıç oranı adıma oturur", new WorldGeneratorSettings().TaxPercent % _balance.TaxStepPercent == 0);
        }

        private static void CitiesStartAtSettingsRate()
        {
            WorldState world = MakeWorld(3, 2);
            _check("vergi: kararlar başlangıç oranında", TaxCommands.AllCitiesAt(world, Player, StartRate) && TaxCommands.AllCitiesAt(world, Ai, StartRate));
            _check("vergi: yürürlükteki oran başlangıç oranında", world.CityTaxPercent[0] == StartRate && world.CityTaxPercent[2] == StartRate);
        }

        private static void DecisionTouchesOnlyThatCity()
        {
            WorldState world = MakeWorld(3, 1);
            _check("tek şehir: kabul edilir", TaxCommands.TrySetCity(_definition, world, Player, 1, HighRate));
            _check("tek şehir: karar yazılır", world.CityNextTaxPercent[1] == HighRate);
            _check("tek şehir: oran hemen değişmez", world.CityTaxPercent[1] == StartRate);
            _check("tek şehir: diğerleri değişmez", world.CityNextTaxPercent[0] == StartRate && world.CityNextTaxPercent[2] == StartRate);
            _check("tek şehir: artık hepsi aynı değil", !TaxCommands.AllCitiesAt(world, Player, HighRate));
        }

        private static void BadRequestsAreRejected()
        {
            WorldState world = MakeWorld(2, 1);
            int aiCity = world.CountryCities[world.CountryCityStart[Ai]];

            _check("red: başka ülkenin şehri", !TaxCommands.TrySetCity(_definition, world, Player, aiCity, LowRate));
            _check("red: sınırın üstü", !TaxCommands.TrySetCity(_definition, world, Player, 0, _balance.TaxMaxPercent + 1));
            _check("red: eksi oran", !TaxCommands.TrySetCity(_definition, world, Player, 0, -1));
            _check("red: toplu sınırın üstü", TaxCommands.SetAll(_definition, world, Player, _balance.TaxMaxPercent + 1) == 0);
            _check("red: karar değişmez", world.CityNextTaxPercent[aiCity] == StartRate && TaxCommands.AllCitiesAt(world, Player, StartRate));
        }

        private static void SetAllCoversOnlyOwnCities()
        {
            WorldState world = MakeWorld(4, 2);
            TaxCommands.TrySetCity(_definition, world, Player, 0, LowRate);

            _check("toplu: değişen şehir sayısı", TaxCommands.SetAll(_definition, world, Player, LowRate) == 3);
            _check("toplu: hepsi aynı kararda", TaxCommands.AllCitiesAt(world, Player, LowRate));
            _check("toplu: oran hemen değişmez", world.CityTaxPercent[0] == StartRate && world.CityTaxPercent[3] == StartRate);
            _check("toplu: AI şehirleri değişmez", TaxCommands.AllCitiesAt(world, Ai, StartRate));
            _check("toplu: tekrarında değişen yok", TaxCommands.SetAll(_definition, world, Player, LowRate) == 0);
        }

        private static void StepsStayInsideLimits()
        {
            int step = _balance.TaxStepPercent;
            _check("adım: yukarı", TaxCommands.Stepped(_balance, StartRate, true) == StartRate + step);
            _check("adım: aşağı", TaxCommands.Stepped(_balance, StartRate, false) == StartRate - step);
            _check("adım: sıfırın altına inmez", TaxCommands.Stepped(_balance, 0, false) == 0);
            _check("adım: sınırı aşmaz", TaxCommands.Stepped(_balance, _balance.TaxMaxPercent, true) == _balance.TaxMaxPercent);
        }

        private static void IncomeWaitsForJanuary()
        {
            WorldState world = MakeWorld(1, 1);
            TaxCommands.TrySetCity(_definition, world, Player, 0, HighRate);

            long expected = TaxCommands.DailyIncomeMilli(_balance, world.Population[0], StartRate);
            long money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);

            _check("ocak: o güne kadar gelir eski orandan", world.Money[Player] - money == expected);
        }

        private static void NewRateStartsOnFirstOfJanuary()
        {
            WorldState world = MakeWorld(2, 1);
            world.StartDate = new DateTime(2026, 12, 30);
            TaxCommands.TrySetCity(_definition, world, Player, 0, HighRate);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("ocak: 31 Aralık'ta oran eski", world.Date == new DateTime(2026, 12, 31) && world.CityTaxPercent[0] == StartRate);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("ocak: 1 Ocak'ta yeni oran", world.Date == new DateTime(2027, 1, 1) && world.CityTaxPercent[0] == HighRate);
            _check("ocak: kararı değişmeyen şehir aynı", world.CityTaxPercent[1] == StartRate);
        }

        private static void DecisionCanChangeUntilJanuary()
        {
            WorldState world = MakeWorld(1, 1);
            world.StartDate = new DateTime(2026, 12, 31);
            TaxCommands.TrySetCity(_definition, world, Player, 0, HighRate);
            TaxCommands.TrySetCity(_definition, world, Player, 0, LowRate);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("karar: son seçilen geçerli olur", world.CityTaxPercent[0] == LowRate);
        }

        private static void RateChangesAtMostOncePerYear()
        {
            WorldState world = MakeWorld(2, 1);
            world.StartDate = new DateTime(2026, 12, 31);
            TaxCommands.TrySetCity(_definition, world, Player, 0, LowRate);
            EconomySimulator.AdvanceDay(_definition, world);
            _check("yıl: ilk ocakta değişir", world.CityTaxPercent[0] == LowRate);

            // 1 Ocak'ta verilen karar bile tam 12 ay bekler.
            TaxCommands.TrySetCity(_definition, world, Player, 0, StartRate);
            for (int day = 0; day < DaysIn2027 - 1; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("yıl: 12 ay dolmadan değişmez", world.Date == new DateTime(2027, 12, 31) && world.CityTaxPercent[0] == LowRate);

            EconomySimulator.AdvanceDay(_definition, world);
            _check("yıl: sonraki ocakta değişir", world.Date == new DateTime(2028, 1, 1) && world.CityTaxPercent[0] == StartRate);
        }

        private static void NextChangeYearFollowsTheDate()
        {
            WorldState world = MakeWorld(1, 1);
            world.StartDate = new DateTime(2026, 5, 4);
            _check("yıl: yıl ortasında sonraki yıl", TaxCommands.NextChangeYear(world) == 2027);

            world.StartDate = new DateTime(2027, 1, 1);
            _check("yıl: 1 Ocak'ta da sonraki yıl", TaxCommands.NextChangeYear(world) == 2028);
        }

        private static void IncomeFollowsEachCity()
        {
            WorldState world = MakeWorld(2, 1);
            TaxCommands.TrySetCity(_definition, world, Player, 0, LowRate);
            TaxCommands.TrySetCity(_definition, world, Player, 1, HighRate);
            TaxCommands.ApplyDecisions(world);

            long expected = TaxCommands.DailyIncomeMilli(_balance, world.Population[0], LowRate)
                + TaxCommands.DailyIncomeMilli(_balance, world.Population[1], HighRate);
            long money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);

            _check("gelir: şehirlerin kendi oranından", expected > 0 && world.Money[Player] - money == expected);
        }

        private static void ZeroTaxPaysNothing()
        {
            WorldState world = MakeWorld(2, 1);
            TaxCommands.SetAll(_definition, world, Player, 0);
            TaxCommands.ApplyDecisions(world);

            long money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);

            _check("sıfır vergi: gelir yok", world.Money[Player] == money);
        }

        private static void HigherTaxLowersSatisfactionOnlyThere()
        {
            WorldState world = MakeWorld(4, 1);
            TaxCommands.SetAll(_definition, world, Player, LowRate);
            TaxCommands.TrySetCity(_definition, world, Player, 0, HighRate);
            TaxCommands.ApplyDecisions(world);
            for (int day = 0; day < 30; day++)
            {
                EconomySimulator.AdvanceDay(_definition, world);
            }

            _check("memnuniyet: yüksek vergili şehir düşer", world.Satisfaction[0] < world.Satisfaction[2]);
            _check("memnuniyet: aynı oranlı şehirler aynı", world.Satisfaction[2] == world.Satisfaction[3]);
        }

        private static void ConcessionKeepsTheRate()
        {
            WorldState world = MakeWorld(1, 1);
            TaxCommands.TrySetCity(_definition, world, Player, 0, HighRate);
            TaxCommands.ApplyDecisions(world);
            world.CityConcessionDays[0] = 1;

            _check("taviz: oran saklanır", world.CityTaxPercent[0] == HighRate);
            _check("taviz: uygulanan oran sıfır", TaxCommands.EffectivePercent(world, 0) == 0);

            long money = world.Money[Player];
            EconomySimulator.AdvanceCountry(_definition, world, Player);
            _check("taviz: gelir yok", world.Money[Player] == money);

            world.CityConcessionDays[0] = 0;
            _check("taviz: bitince oran geçerli", TaxCommands.EffectivePercent(world, 0) == HighRate);
        }

        private static void RateStaysWithCityAfterHandover()
        {
            WorldState world = MakeWorld(3, 1);
            int rebel = world.BaseCountryCount;
            TaxCommands.TrySetCity(_definition, world, Player, 1, HighRate);
            TaxCommands.ApplyDecisions(world);
            TaxCommands.TrySetCity(_definition, world, Player, 1, LowRate);

            Rebellions.Start(_definition, world, Player, 1);
            world.CityRebellionDays[1] = 1;
            Rebellions.AdvanceDay(_definition, world);

            _check("el değiştirme: şehir yeni sahibinde", world.CityOwner[1] == rebel);
            _check("el değiştirme: oran şehirle gider", world.CityTaxPercent[1] == HighRate);
            _check("el değiştirme: eski sahibin kararı düşer", world.CityNextTaxPercent[1] == HighRate);
            _check("el değiştirme: eski sahibi artık ayarlayamaz", !TaxCommands.TrySetCity(_definition, world, Player, 1, LowRate));
            _check("el değiştirme: toplu ayar kaybedilen şehre dokunmaz", TaxCommands.SetAll(_definition, world, Player, LowRate) == 2 && world.CityNextTaxPercent[1] == HighRate);
        }

        private static void ChecksumSeesTheDecision()
        {
            WorldState first = MakeWorld(2, 1);
            WorldState second = MakeWorld(2, 1);
            _check("özet: aynı dünya aynı", first.Checksum() == second.Checksum());

            TaxCommands.TrySetCity(_definition, second, Player, 0, HighRate);
            _check("özet: karar değişince değişir", first.Checksum() != second.Checksum());
        }

        private static WorldState MakeWorld(int playerCities, int aiCities)
        {
            WorldState world = WorldGenerator.Create(_definition, new WorldGeneratorSettings
            {
                CitiesPerCountry = new[] { playerCities, aiCities },
                FacilitiesPerCity = 2,
                Seed = 34,
                StartingStockUnits = LargeStockUnits,
                StartingMoney = LargeMoneyMilli,
                TaxPercent = StartRate
            });
            world.StartDate = new DateTime(2026, 5, 4);
            world.CountryPlayerControlled[Player] = true;
            world.CountryManualTrade[Player] = true;
            return world;
        }
    }
}
