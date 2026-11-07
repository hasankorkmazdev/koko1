using System;

namespace GeoWar.Simulation
{
    // Oyun durumu düz dizilerde tutulur; ülke, şehir ve tesisler indeksle okunur.
    // Bir şehrin slotları dizide art arda durur. Bir ülkenin şehir ve slotları listelerden okunur: şehir el değiştirebilir.
    // Tesis dizileri slot başınadır: boş slotun tesisi yoktur, kurulan tesis slotun yerine yazılır, hiçbir şey kaymaz.
    public sealed class WorldState
    {
        public const byte AlarmNone = 0;
        public const byte AlarmActive = 1;
        public const byte AlarmStopped = 2;
        public const int NoBuilding = -1;
        public const int NoRecipe = -1;
        public const int NoCountry = -1;
        public const int NoCity = -1;

        private const int MinShipmentCapacity = 64;
        private const int ShipmentsPerStock = 4;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        // Ülke dizilerinin boyutu: oyuna başlayan ülkeler ve sonda duran boş isyancı devlet yerleri.
        public readonly int CountryCount;
        public readonly int BaseCountryCount;
        public readonly int CityCount;
        public readonly int FacilityCount;
        public readonly int ResourceCount;

        public int Day;

        // Ülkelerin şehirleri: CountryCities[CountryCityStart[ülke] .. CountryCityStart[ülke + 1]]. Slotlar da aynı biçimde.
        // Oyun başında listeler sıralıdır; şehir el değiştirince yeniden kurulur.
        public readonly int[] CountryCityStart;
        public readonly int[] CountryFacilityStart;
        public readonly int[] CountryCities;
        public readonly int[] CountrySlots;

        // Şehir el değiştirince artar; görünüm haritayı buna bakarak yeniler.
        public int OwnersVersion;

        // Ülkeler
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

        // İsyana kendisi cevap veren ülke: oyuncu. AI ülkeleri hemen cevap verir.
        public readonly bool[] CountryPlayerControlled;

        // İsyancı devletin çıktığı ülke, ülkeden çıkan isyancı devlet ve isyancı devletin ilk şehri. Yoksa NoCountry, NoCity.
        public readonly int[] RebelParent;
        public readonly int[] RebelStateOf;
        public readonly int[] RebelCapital;

        // Ülkede süren isyan sayısı, başlayan isyan toplamı, isyanla kaybedilen şehir toplamı ve kaybedilen son şehir.
        public readonly int[] CountryRebellionCount;
        public readonly int[] CountryRebellionsStarted;
        public readonly int[] CountryCitiesLost;
        public readonly int[] CountryLastLostCity;

        // Şehirler
        public readonly long[] Population;
        public readonly int[] Satisfaction;
        public readonly int[] CitySlotStart;
        public readonly int[] CityOwner;

        // Keşfedilmiş kaynaklar: tesis türü indeksine göre bit.
        public readonly long[] CityDeposits;
        public readonly bool[] CityCoastal;

        // Komşu şehirler: CityNeighbors[CityNeighborStart[şehir] .. CityNeighborStart[şehir + 1]]. Harita yoksa boştur.
        public int[] CityNeighborStart;
        public int[] CityNeighbors;

        // Sayaçlar gün olarak tutulur: isyanın kalan süresi, taviz süresi, yeniden isyan etmeyeceği süre, el değiştirince üretimsiz süre.
        public readonly int[] CityRebellionDays;
        public readonly int[] CityConcessionDays;
        public readonly int[] CityCalmDays;
        public readonly int[] CityStopDays;

        // Şehrin isyandaki komşu sayısı; memnuniyet hedefini düşürür.
        public readonly int[] CityRebelNeighbors;

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

        public WorldState(int baseCountryCount, int cityCount, int facilityCount, int resourceCount, int rebelStateSlots = 0)
        {
            int countryCount = baseCountryCount + rebelStateSlots;
            CountryCount = countryCount;
            BaseCountryCount = baseCountryCount;
            CityCount = cityCount;
            FacilityCount = facilityCount;
            ResourceCount = resourceCount;

            CountryCityStart = new int[countryCount + 1];
            CountryFacilityStart = new int[countryCount + 1];
            CountryCities = new int[cityCount];
            CountrySlots = new int[facilityCount];
            CountryPlayerControlled = new bool[countryCount];
            RebelParent = new int[countryCount];
            RebelStateOf = new int[countryCount];
            RebelCapital = new int[countryCount];
            CountryRebellionCount = new int[countryCount];
            CountryRebellionsStarted = new int[countryCount];
            CountryCitiesLost = new int[countryCount];
            CountryLastLostCity = new int[countryCount];
            CityOwner = new int[cityCount];
            CityNeighborStart = new int[cityCount + 1];
            CityNeighbors = new int[0];
            CityRebellionDays = new int[cityCount];
            CityConcessionDays = new int[cityCount];
            CityCalmDays = new int[cityCount];
            CityStopDays = new int[cityCount];
            CityRebelNeighbors = new int[cityCount];
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
                CountrySlots[slot] = slot;
            }

            for (int city = 0; city < cityCount; city++)
            {
                CountryCities[city] = city;
            }

            for (int country = 0; country < countryCount; country++)
            {
                RebelParent[country] = NoCountry;
                RebelStateOf[country] = NoCountry;
                RebelCapital[country] = NoCity;
                CountryLastLostCity[country] = NoCity;
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
            hash = Mix(hash, Satisfaction);
            hash = Mix(hash, CityOwner);
            hash = Mix(hash, CityRebellionDays);
            hash = Mix(hash, CityConcessionDays);
            hash = Mix(hash, CityCalmDays);
            hash = Mix(hash, CityStopDays);

            for (int slot = 0; slot < FacilityCount; slot++)
            {
                hash = (hash ^ (ulong)FacilityBuilding[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityLevel[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityBuildDays[slot]) * FnvPrime;
                hash = (hash ^ (ulong)FacilityUpgradeDays[slot]) * FnvPrime;
            }

            return hash;
        }

        private static ulong Mix(ulong hash, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                hash = (hash ^ (ulong)values[i]) * FnvPrime;
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
