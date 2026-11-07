namespace GeoWar.Simulation
{
    // İsyan penceresinin kendiliğinden ne zaman açılacağını söyler. Kural docs/MECHANICS.md 11'dedir.
    // Yalnızca dalganın ilk isyanında açılır. Açıldıktan sonra bir süre yeniden açılmaz:
    // kıtlıkta cevap verilen isyanın yerine hemen yenisi çıkar, pencere arka arkaya açılmasın.
    public sealed class RebellionAlerts
    {
        private int _seenStarted;
        private int _lastAlertDay;
        private bool _hasAlerted;

        // Gün sonunda bir kez sorulur.
        public bool ShouldOpen(EconomyDefinition definition, WorldState world, int country)
        {
            int started = world.CountryRebellionsStarted[country];
            int newRebellions = started - _seenStarted;
            _seenStarted = started;
            if (newRebellions == 0)
            {
                return false;
            }

            bool isFirstOfWave = world.CountryRebellionCount[country] <= newRebellions;
            bool isQuiet = !_hasAlerted || world.Day - _lastAlertDay >= definition.Balance.RebellionAlertGapDays;
            if (!isFirstOfWave || !isQuiet)
            {
                return false;
            }

            _hasAlerted = true;
            _lastAlertDay = world.Day;
            return true;
        }
    }
}
