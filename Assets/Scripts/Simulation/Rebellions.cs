using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    public enum RebellionResponse : byte
    {
        Concession,
        Aid
    }

    // İsyan: başlama, süre, cevaplar ve isyancı devlet. Kurallar docs/MECHANICS.md 10 ve 11'dedir.
    // Gün sonunda, tüm ülkeler hesaplandıktan sonra çalışır; şehir ancak burada el değiştirir.
    public static class Rebellions
    {
        private const int Permille = EconomyDefinition.Permille;
        private const long Million = EconomyDefinition.Million;

        public static void AdvanceDay(EconomyDefinition definition, WorldState world)
        {
            bool ownersChanged = false;
            for (int country = 0; country < world.CountryCount; country++)
            {
                int end = world.CountryCityStart[country + 1];
                for (int i = world.CountryCityStart[country]; i < end; i++)
                {
                    ownersChanged |= AdvanceCity(definition, world, country, world.CountryCities[i]);
                }
            }

            if (ownersChanged)
            {
                CityOwnership.RebuildCountryLists(world);
                CityOwnership.RebuildLandNeighbors(world);
                CityOwnership.RefreshCapitals(world);
                world.OwnersVersion++;
            }
        }

        // İsyandaki veya yeni el değiştirmiş şehirde tesisler üretim yapmaz.
        public static bool IsCityStopped(WorldState world, int city)
        {
            return world.CityRebellionDays[city] > 0 || world.CityStopDays[city] > 0;
        }

        public static bool IsRebelState(WorldState world, int country)
        {
            return world.RebelParent[country] != WorldState.NoCountry;
        }

        public static void Start(EconomyDefinition definition, WorldState world, int country, int city)
        {
            world.CityRebellionDays[city] = definition.Balance.RebellionDeadlineDays;
            world.CountryRebellionCount[country]++;
            world.CountryRebellionsStarted[country]++;
            ChangeRebelNeighbors(world, city, 1);

            if (!world.CountryPlayerControlled[country])
            {
                AnswerForAi(definition, world, city);
            }
        }

        // Yardım paketinin bedeli şehrin nüfusuyla büyür.
        public static long AidCostMilli(EconomyDefinition definition, WorldState world, int city)
        {
            return world.Population[city] * definition.Balance.AidCostMilliPerMillion / Million;
        }

        public static long TotalAidCostMilli(EconomyDefinition definition, WorldState world, int country)
        {
            long total = 0;
            int end = world.CountryCityStart[country + 1];
            for (int i = world.CountryCityStart[country]; i < end; i++)
            {
                int city = world.CountryCities[i];
                if (world.CityRebellionDays[city] > 0)
                {
                    total += AidCostMilli(definition, world, city);
                }
            }

            return total;
        }

        public static bool CanAnswer(EconomyDefinition definition, WorldState world, int city, RebellionResponse response)
        {
            return world.CityRebellionDays[city] > 0
                && (response == RebellionResponse.Concession || world.Money[world.CityOwner[city]] >= AidCostMilli(definition, world, city));
        }

        public static bool TryAnswer(EconomyDefinition definition, WorldState world, int city, RebellionResponse response)
        {
            if (!CanAnswer(definition, world, city, response))
            {
                return false;
            }

            BalanceData balance = definition.Balance;
            int owner = world.CityOwner[city];
            EndRebellion(world, owner, city);

            if (response == RebellionResponse.Concession)
            {
                world.CityConcessionDays[city] = balance.ConcessionDays;
                world.CityCalmDays[city] = balance.ConcessionDays + balance.RebellionCalmDays;
            }
            else
            {
                world.Money[owner] -= AidCostMilli(definition, world, city);
                world.Satisfaction[city] = Math.Min(Permille, world.Satisfaction[city] + balance.AidSatisfactionGainPermille);
                world.CityCalmDays[city] = balance.RebellionCalmDays;
            }

            return true;
        }

        // Tek seçimle tüm isyanlara cevap verir. Yardımın parası hepsine yetmiyorsa hiçbirine verilmez.
        public static int TryAnswerAll(EconomyDefinition definition, WorldState world, int country, RebellionResponse response)
        {
            if (response == RebellionResponse.Aid && world.Money[country] < TotalAidCostMilli(definition, world, country))
            {
                return 0;
            }

            int answered = 0;
            int end = world.CountryCityStart[country + 1];
            for (int i = world.CountryCityStart[country]; i < end; i++)
            {
                if (TryAnswer(definition, world, world.CountryCities[i], response))
                {
                    answered++;
                }
            }

            return answered;
        }

        // Ülkede isyan eden şehirler, süresi en az kalandan başlayarak.
        public static void CollectCities(WorldState world, int country, List<int> cities)
        {
            cities.Clear();
            int end = world.CountryCityStart[country + 1];
            for (int i = world.CountryCityStart[country]; i < end; i++)
            {
                int city = world.CountryCities[i];
                int days = world.CityRebellionDays[city];
                if (days <= 0)
                {
                    continue;
                }

                int place = cities.Count;
                while (place > 0 && world.CityRebellionDays[cities[place - 1]] > days)
                {
                    place--;
                }

                cities.Insert(place, city);
            }
        }

        // Şehrin bir günlük sayaçları. Şehir el değiştirdiyse true döner.
        private static bool AdvanceCity(EconomyDefinition definition, WorldState world, int country, int city)
        {
            CountDown(world.CityConcessionDays, city);
            CountDown(world.CityCalmDays, city);
            CountDown(world.CityStopDays, city);

            if (world.CityRebellionDays[city] > 0)
            {
                world.CityRebellionDays[city]--;
                return world.CityRebellionDays[city] == 0 && Secede(definition, world, country, city);
            }

            if (ShouldStart(definition.Balance, world, country, city))
            {
                Start(definition, world, country, city);
            }

            return false;
        }

        private static void CountDown(int[] days, int city)
        {
            if (days[city] > 0)
            {
                days[city]--;
            }
        }

        // Eşiğin altındaki şehirde her gün zar atılır; memnuniyet düştükçe ihtimal artar. Üstteki şehir zar tüketmez.
        private static bool ShouldStart(BalanceData balance, WorldState world, int country, int city)
        {
            int satisfaction = world.Satisfaction[city];
            if (satisfaction >= balance.RebellionThresholdPermille
                || world.CityCalmDays[city] > 0
                || world.CountryRebellionCount[country] >= balance.MaxRebellionsPerCountry)
            {
                return false;
            }

            long chance = (long)balance.RebellionChancePerMillion * (balance.RebellionThresholdPermille - satisfaction) / balance.RebellionThresholdPermille;
            return DeterministicRandom.Range(ref world.RandomState[country], (int)Million) < chance;
        }

        private static void AnswerForAi(EconomyDefinition definition, WorldState world, int city)
        {
            RebellionResponse response = CanAnswer(definition, world, city, RebellionResponse.Aid)
                ? RebellionResponse.Aid
                : RebellionResponse.Concession;
            TryAnswer(definition, world, city, response);
        }

        private static void EndRebellion(WorldState world, int country, int city)
        {
            world.CityRebellionDays[city] = 0;
            world.CountryRebellionCount[country]--;
            ChangeRebelNeighbors(world, city, -1);
        }

        private static void ChangeRebelNeighbors(WorldState world, int city, int change)
        {
            int end = world.CityNeighborStart[city + 1];
            for (int i = world.CityNeighborStart[city]; i < end; i++)
            {
                world.CityRebelNeighbors[world.CityNeighbors[i]] += change;
            }
        }

        // Süre doldu: şehir ülkenin isyancı devletine geçer. Boş devlet yeri kalmadıysa isyan söner, şehir ülkede kalır.
        private static bool Secede(EconomyDefinition definition, WorldState world, int parent, int city)
        {
            BalanceData balance = definition.Balance;
            int rebel = world.RebelStateOf[parent];
            if (rebel == WorldState.NoCountry)
            {
                rebel = FoundRebelState(world, parent, city);
            }

            EndRebellion(world, parent, city);
            world.CityCalmDays[city] = balance.RebellionCalmDays;
            if (rebel == WorldState.NoCountry)
            {
                return false;
            }

            MoveShareOfCountry(world, parent, rebel, city);
            world.CityOwner[city] = rebel;
            TaxCommands.CancelDecision(world, city);
            world.CityConcessionDays[city] = 0;
            world.CityStopDays[city] = balance.HandoverStopDays;
            world.Satisfaction[city] = Math.Min(world.Satisfaction[city], balance.HandoverSatisfactionPermille);
            world.CountryCitiesLost[parent]++;
            world.CountryLastLostCity[parent] = city;
            return true;
        }

        // Şehrin ülke nüfusundaki payı kadar stok ve şebeke kapasitesi şehirle gider. Nüfusun tamamı şehirle kalır.
        private static void MoveShareOfCountry(WorldState world, int parent, int rebel, int city)
        {
            long total = Math.Max(1, world.CountryPopulation[parent]);
            long people = world.Population[city];
            long share = Math.Min(Permille, people * Permille / total);

            int parentBase = parent * world.ResourceCount;
            int rebelBase = rebel * world.ResourceCount;
            for (int resource = 0; resource < world.ResourceCount; resource++)
            {
                long moved = world.Stock[parentBase + resource] * share / Permille;
                world.Stock[parentBase + resource] -= moved;
                world.Stock[rebelBase + resource] += moved;
            }

            long grid = world.GridCapacityMilli[parent] * share / Permille;
            world.GridCapacityMilli[parent] -= grid;
            world.GridCapacityMilli[rebel] += grid;

            world.CountryPopulation[parent] -= people;
            world.CountryPopulation[rebel] += people;
        }

        private static int FoundRebelState(WorldState world, int parent, int city)
        {
            for (int slot = world.BaseCountryCount; slot < world.CountryCount; slot++)
            {
                if (IsRebelState(world, slot))
                {
                    continue;
                }

                world.RebelParent[slot] = parent;
                world.RebelStateOf[parent] = slot;
                world.CountryCapital[slot] = city;
                world.GridLossPermille[slot] = world.GridLossPermille[parent];
                CountryDistances.CopyForRebelState(world, parent, slot);
                CountryRoutes.CopyAirPaths(world, parent, slot);
                CountryRoutes.SetWar(world, parent, slot, true);
                return slot;
            }

            return WorldState.NoCountry;
        }
    }
}
