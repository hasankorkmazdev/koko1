using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Assets/Data/Resources/population.json dosyasının karşılığı: şehirlerin gerçek nüfusu ve verinin kaynağı.
    [Serializable]
    public sealed class PopulationData
    {
        public List<PopulationSourceData> Sources;
        public List<CityPopulationData> Cities;
    }

    [Serializable]
    public sealed class PopulationSourceData
    {
        public string Country;
        public string Date;
        public string Source;
        public string Note;
    }

    [Serializable]
    public sealed class CityPopulationData
    {
        public string Id;
        public string Name;
        public long Population;
        public string Note;
    }

    public static class PopulationTable
    {
        // Nüfuslar şehir sırasıyla döner: haritadaki il sırası şehir sırasıdır.
        // Verisi eksik şehir hata verir; oyun sessizce yanlış sayıyla açılmaz.
        public static long[] ForCities(PopulationData data, string[] provinceIds, int cityCount)
        {
            var byId = new Dictionary<string, long>(data.Cities.Count);
            foreach (CityPopulationData city in data.Cities)
            {
                byId[city.Id] = city.Population;
            }

            var populations = new long[cityCount];
            for (int city = 0; city < cityCount; city++)
            {
                if (!byId.TryGetValue(provinceIds[city], out populations[city]))
                {
                    throw new InvalidOperationException("Nüfus verisi eksik: " + provinceIds[city]);
                }
            }

            return populations;
        }
    }
}
