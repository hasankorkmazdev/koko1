using System;

namespace GeoWar.Simulation
{
    // Vergi oranı şehir başınadır; tek şehre veya ülkenin tüm şehirlerine yazılır: docs/ECONOMY.md 1.2.
    // Seçilen oran hemen geçerli olmaz, 1 Ocak'ta yürürlüğe girer. Karar o güne kadar değiştirilebilir;
    // yürürlükteki oran böylece yılda en fazla bir kez değişir.
    // Tavizdeki şehirde vergi alınmaz; süre bitince yürürlükteki oran yeniden uygulanır.
    public static class TaxCommands
    {
        private const long Million = 1000000;
        private const int January = 1;
        private const int FirstDay = 1;

        public static bool IsValidRate(BalanceData balance, int percent)
        {
            return percent >= 0 && percent <= balance.TaxMaxPercent;
        }

        // Bir adım yukarı veya aşağı; sınırları aşmaz.
        public static int Stepped(BalanceData balance, int percent, bool isUp)
        {
            int next = isUp ? percent + balance.TaxStepPercent : percent - balance.TaxStepPercent;
            return Math.Max(0, Math.Min(balance.TaxMaxPercent, next));
        }

        public static bool TrySetCity(EconomyDefinition definition, WorldState world, int country, int city, int percent)
        {
            if (world.CityOwner[city] != country || !IsValidRate(definition.Balance, percent))
            {
                return false;
            }

            world.CityNextTaxPercent[city] = percent;
            return true;
        }

        // Kararı değişen şehir sayısını döndürür.
        public static int SetAll(EconomyDefinition definition, WorldState world, int country, int percent)
        {
            if (!IsValidRate(definition.Balance, percent))
            {
                return 0;
            }

            int changed = 0;
            int end = world.CountryCityStart[country + 1];
            for (int i = world.CountryCityStart[country]; i < end; i++)
            {
                int city = world.CountryCities[i];
                if (world.CityNextTaxPercent[city] != percent)
                {
                    world.CityNextTaxPercent[city] = percent;
                    changed++;
                }
            }

            return changed;
        }

        // Ülkenin tüm şehirlerinde karar bu oran mı?
        public static bool AllCitiesAt(WorldState world, int country, int percent)
        {
            int end = world.CountryCityStart[country + 1];
            for (int i = world.CountryCityStart[country]; i < end; i++)
            {
                if (world.CityNextTaxPercent[world.CountryCities[i]] != percent)
                {
                    return false;
                }
            }

            return true;
        }

        // Tarih ilerleyince çağrılır: 1 Ocak'ta kararlar yürürlüğe girer.
        public static void AdvanceDate(WorldState world)
        {
            DateTime date = world.Date;
            if (date.Month == January && date.Day == FirstDay)
            {
                ApplyDecisions(world);
            }
        }

        public static void ApplyDecisions(WorldState world)
        {
            Array.Copy(world.CityNextTaxPercent, world.CityTaxPercent, world.CityCount);
        }

        // Kararın yürürlüğe gireceği yıl: bugün 1 Ocak olsa bile o günün kararları uygulanmıştır, yeni karar sonraki yılı bekler.
        public static int NextChangeYear(WorldState world)
        {
            return world.Date.Year + 1;
        }

        // Şehir el değiştirince eski sahibinin kararı düşer; yeni sahibi kendi kararını verir.
        public static void CancelDecision(WorldState world, int city)
        {
            world.CityNextTaxPercent[city] = world.CityTaxPercent[city];
        }

        // Şehirde o gün uygulanan oran.
        public static int EffectivePercent(WorldState world, int city)
        {
            return world.CityConcessionDays[city] > 0 ? 0 : world.CityTaxPercent[city];
        }

        public static int SatisfactionPenaltyPermille(BalanceData balance, int percent)
        {
            return percent * balance.TaxSatisfactionPenaltyPerPercent;
        }

        public static long DailyIncomeMilli(BalanceData balance, long people, int percent)
        {
            return people * percent * balance.TaxIncomePerMillionPerPercent / Million;
        }
    }
}
