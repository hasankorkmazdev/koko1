namespace GeoWar.Simulation
{
    // Oyundaki tüm rastgelelik buradan gelir; aynı seed her cihazda aynı sayıları verir.
    public static class DeterministicRandom
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;
        private const ulong MixA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixB = 0x94D049BB133111EBUL;

        public static ulong Next(ref ulong state)
        {
            state += Increment;
            ulong z = state;
            z = (z ^ (z >> 30)) * MixA;
            z = (z ^ (z >> 27)) * MixB;
            return z ^ (z >> 31);
        }

        // Üst sınır hariç, [0, exclusiveMax) aralığında sayı.
        public static int Range(ref ulong state, int exclusiveMax)
        {
            return (int)(Next(ref state) % (ulong)exclusiveMax);
        }
    }
}
