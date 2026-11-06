using System;

namespace GeoWar.Simulation
{
    // Oyun hızını ülkelere dağıtır: bir gün tek karede değil, ülke ülke karelere yayılarak hesaplanır.
    // Sonucu kare süresi değil yalnızca ülke sırası belirler, bu yüzden hesap deterministik kalır.
    public sealed class SimulationClock
    {
        private readonly float _secondsPerDay;
        private float _countryBudget;
        private int _nextCountry;

        // 0 oyun durur; 1, 2 ve 3 oyun hızıdır.
        public int Speed;

        public SimulationClock(float secondsPerDay)
        {
            _secondsPerDay = secondsPerDay;
        }

        // Biten gün sayısını döndürür.
        public int Advance(EconomyDefinition definition, WorldState world, float seconds)
        {
            if (Speed <= 0)
            {
                return 0;
            }

            int finishedDays = 0;
            _countryBudget += world.CountryCount * Speed * seconds / _secondsPerDay;
            while (_countryBudget >= 1f)
            {
                _countryBudget -= 1f;
                EconomySimulator.AdvanceCountry(definition, world, _nextCountry);
                _nextCountry++;
                if (_nextCountry == world.CountryCount)
                {
                    EconomySimulator.FinishDay(definition, world);
                    _nextCountry = 0;
                    finishedDays++;
                }
            }

            return finishedDays;
        }

        // Günün ne kadarının geçtiği: yeni gün başında 0, gün biterken 1.
        public float DayProgress(WorldState world)
        {
            return Math.Min(1f, (_nextCountry + _countryBudget) / world.CountryCount);
        }
    }
}
