namespace GeoWar.Game
{
    // Şehir panelindeki kısa bilgi: ülke içindeki yeri, nüfusu, kıyısı ve tesisleri.
    public static class CityBrief
    {
        public static string Text(string countryTitle, int countryCityCount, bool isCapital, long population, bool isCoastal, int facilityCount)
        {
            string place = NumberFormat.Genitive(countryTitle) + (isCapital ? " başkenti" : " " + countryCityCount + " şehrinden biri");
            string coast = isCoastal ? "denize kıyısı var" : "denize kıyısı yok";
            string facilities = facilityCount > 0 ? facilityCount + " tesis kurulu" : "Tesis yok";
            return place + ". Nüfusu " + NumberFormat.Compact(population) + ", " + coast + ". " + facilities + ".";
        }
    }
}
