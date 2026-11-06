using System;

namespace GeoWar.Simulation
{
    // Oyun durumu düz dizilerde tutulur; ülke, şehir ve tesisler indeksle okunur.
    // Bir ülkenin şehirleri, bir şehrin slotları dizide art arda durur.
    // Tesis dizileri slot başınadır: boş slotun tesisi yoktur, kurulan tesis slotun yerine yazılır, hiçbir şey kaymaz.
    public sealed class WorldState
    {
        public const byte AlarmNone = 0;
        public const byte AlarmActive = 1;
        public const int NoBuilding = -1;
        public const int NoRecipe = -1;

        private const int MinShipmentCapacity = 64;
        private const int ShipmentsPerStock = 4;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public readonly int CountryCount;
        public readonly int CityCount;
        public readonly int FacilityCount;
        public readonly int ResourceCount;

        public int Day;

        // Ülkeler
        public readonly int[] CountryCityStart;
        public readonly int[] CountryFacilityStart;
        public readonly long[] Money;
        public readonly long[] Stock;
        public readonly long[] DailyProduced;
        public readonly long[] DailyConsumed;

        // O gün stok yetmediği için karşılanamayan ihtiyaç. AI hedef stoğunu buna göre de hesaplar.
        public readonly long[] DailyShortage;
        public readonly long[] CountryPopulation;
        public readonly int[] EnergyRatioPermille;
        public readonly long[] GridCapacityMilli;
        public readonly int[] GridLossPermille;
        public readonly int[] TaxPercent;

        // Başkentler arası mesafe: [alıcı * ülke sayısı + satıcı].
        public readonly int[] CountryDistanceKm;

        // Her alıcı için ülkeler, en yakından uzağa: [alıcı * ülke sayısı + sıra]. AI bu sırayla alır.
        public readonly int[] NearestCountries;

        // Kara sınırı olan ülkeler ve savaş durumu: [a * ülke sayısı + b].
        public readonly bool[] CountryLandNeighbor;
        public readonly bool[] CountryAtWar;

        // Alıcı ile satıcı arasında kullanılan ulaşım türü: [alıcı * ülke sayısı + satıcı]. Yol yoksa NoRoute.
        public readonly int[] CountryRoute;

        // Ülkede çalışan ulaşım tesisi: [ülke * ulaşım türü sayısı + tür]. Rota hesabı doldurur.
        public bool[] CountryHasTransport;

        // Hava rotası hangi ülkelerin üstünden geçer: [(a * ülke sayısı + b) * ülke sayısı + c]. Harita yoksa boştur.
        public bool[] AirPathCrossings;

        // Tesis veya savaş durumu değişince rotalar yeniden hesaplanır.
        public bool RoutesDirty = true;
        public readonly bool[] RouteSeen;
        public readonly int[] RouteQueue;

        // Pazarda yalnızca elle alıp satan ülke: oyuncu. AI ülkeleri otomatik alıp satar.
        public readonly bool[] CountryManualTrade;
        public readonly int[] BestRecipe;
        public readonly ulong[] RandomState;

        // Şehirler
        public readonly long[] Population;
        public readonly int[] Satisfaction;
        public readonly int[] CitySlotStart;

        // Keşfedilmiş kaynaklar: tesis türü indeksine göre bit.
        public readonly long[] CityDeposits;
        public readonly bool[] CityCoastal;

        // Tesisler (slot başına)
        public readonly int[] FacilityCity;
        public readonly int[] FacilityBuilding;
        public readonly int[] FacilityRecipe;
        public readonly int[] FacilityBuildDays;
        public readonly int[] FacilityUpgradeDays;
        public readonly int[] FacilityLevel;
        public readonly long[] FacilityExperience;
        public readonly int[] FacilityMachineDays;
        public readonly int[] FacilityKombinaPermille;
        public readonly bool[] FacilityPrivate;
        public readonly int[] FacilityStateSharePermille;
        public readonly byte[] FacilityAlarm;

        // Son biten gündeki üretim hızı: seviye, memnuniyet, enerji ve kombina dahil. Çalışmadıysa 0.
        public readonly int[] FacilityOutputSpeedPermille;

        // Pazar
        public readonly long[] PriceMilli;
        public readonly long[] MarketSupply;
        public readonly long[] MarketDemand;

        // Pazardaki satılmamış mallar: [satıcı * kaynak sayısı + kaynak]. Teklif sahibinin stoğundan çıkmıştır.
        public readonly long[] OfferMilli;

        // Kaynak başına toplam teklif; pazar kapanırken hesaplanır, boş kaynakta satıcı aranmasın diye.
        public readonly long[] MarketOfferTotal;

        // Yoldaki mallar. Dizi nadiren büyür; günlük hesapta bellek ayrılmaz.
        public Shipment[] ShipmentList;
        public int ShipmentCount;

        // Yolda olan toplam miktar: [alıcı * kaynak sayısı + kaynak]. AI hedef stoğuna sayar.
        public readonly long[] InTransitMilli;

        public WorldState(int countryCount, int cityCount, int facilityCount, int resourceCount)
        {
            CountryCount = countryCount;
            CityCount = cityCount;
            FacilityCount = facilityCount;
            ResourceCount = resourceCount;

            CountryCityStart = new int[countryCount + 1];
            CountryFacilityStart = new int[countryCount + 1];
            Money = new long[countryCount];
            Stock = new long[countryCount * resourceCount];
            DailyProduced = new long[countryCount * resourceCount];
            DailyConsumed = new long[countryCount * resourceCount];
            DailyShortage = new long[countryCount * resourceCount];
            CountryPopulation = new long[countryCount];
            EnergyRatioPermille = new int[countryCount];
            GridCapacityMilli = new long[countryCount];
            GridLossPermille = new int[countryCount];
            TaxPercent = new int[countryCount];
            CountryDistanceKm = new int[countryCount * countryCount];
            NearestCountries = new int[countryCount * countryCount];
            CountryLandNeighbor = new bool[countryCount * countryCount];
            CountryAtWar = new bool[countryCount * countryCount];
            CountryRoute = new int[countryCount * countryCount];
            RouteSeen = new bool[countryCount];
            RouteQueue = new int[countryCount];
            CountryManualTrade = new bool[countryCount];
            BestRecipe = new int[countryCount];
            RandomState = new ulong[countryCount];

            Population = new long[cityCount];
            Satisfaction = new int[cityCount];
            CitySlotStart = new int[cityCount + 1];
            CityDeposits = new long[cityCount];
            CityCoastal = new bool[cityCount];

            FacilityCity = new int[facilityCount];
            FacilityBuilding = new int[facilityCount];
            FacilityRecipe = new int[facilityCount];
            FacilityBuildDays = new int[facilityCount];
            FacilityUpgradeDays = new int[facilityCount];
            FacilityLevel = new int[facilityCount];
            FacilityExperience = new long[facilityCount];
            FacilityMachineDays = new int[facilityCount];
            FacilityKombinaPermille = new int[facilityCount];
            FacilityPrivate = new bool[facilityCount];
            FacilityStateSharePermille = new int[facilityCount];
            FacilityAlarm = new byte[facilityCount];
            FacilityOutputSpeedPermille = new int[facilityCount];

            PriceMilli = new long[resourceCount];
            MarketSupply = new long[resourceCount];
            MarketDemand = new long[resourceCount];
            OfferMilli = new long[countryCount * resourceCount];
            MarketOfferTotal = new long[resourceCount];
            ShipmentList = new Shipment[Math.Max(MinShipmentCapacity, countryCount * resourceCount * ShipmentsPerStock)];
            InTransitMilli = new long[countryCount * resourceCount];

            for (int slot = 0; slot < facilityCount; slot++)
            {
                FacilityBuilding[slot] = NoBuilding;
                FacilityRecipe[slot] = NoRecipe;
            }

            // Mesafe verilene kadar sıra ülke sırasıdır.
            for (int buyer = 0; buyer < countryCount; buyer++)
            {
                for (int i = 0; i < countryCount; i++)
                {
                    NearestCountries[buyer * countryCount + i] = i;
                }
            }
        }

        public int CountBuiltFacilities()
        {
            int count = 0;
            for (int slot = 0; slot < FacilityCount; slot++)
            {
                if (FacilityBuilding[slot] != NoBuilding)
                {
                    count++;
                }
            }

            return count;
        }

        // Aynı girdinin aynı sonucu verdiğini doğrulamak için tüm durumun özeti.
        public ulong Checksum()
        {
            ulong hash = FnvOffset;
            hash = Mix(hash, Money);
            hash = Mix(hash, Stock);
            hash = Mix(hash, OfferMilli);
            hash = Mix(hash, InTransitMilli);
            hash = Mix(hash, Population);
            hash = Mix(hash, PriceMilli);
            hash = Mix(hash, FacilityExperience);
            for (int i = 0; i < Satisfaction.Length; i++)
            {
                hash = (hash ^ (ulong)Satisfaction[i]) * FnvPrime;
            }

            for (int slot = 0; slot < FacilityCount; slot++)
            {
                hash = (hash ^ (ulong)FacilityBuilding[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityLevel[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityBuildDays[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityUpgradeDays[slot]) * FnvPrime;
            }

            return hash;
        }

        private static ulong Mix(ulong hash, long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                hash = (hash ^ (ulong)values[i]) * FnvPrime;
            }

            return hash;
        }
    }
}
