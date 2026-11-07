namespace GeoWar.Simulation
{
    // Şehrin huzursuzluk düzeyi: memnuniyet, üretimin yavaşladığı eşiğin altına indikçe artar; isyandaki şehir en üst düzeydedir.
    // Harita oyuncunun şehirlerini bu düzeye göre kırmızıya boyar. Kademeli olduğu için renkler her gün gönderilmez.
    public static class UnrestLevel
    {
        public const int Max = 16;

        public static int Of(BalanceData balance, WorldState world, int city)
        {
            if (world.CityRebellionDays[city] > 0)
            {
                return Max;
            }

            int low = balance.LowSatisfactionPermille;
            int satisfaction = world.Satisfaction[city];
            return satisfaction >= low ? 0 : (low - satisfaction) * Max / low;
        }
    }
}
