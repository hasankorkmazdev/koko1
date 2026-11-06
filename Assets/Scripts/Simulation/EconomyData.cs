using System;
using System.Collections.Generic;

namespace GeoWar.Simulation
{
    // Assets/Data/Resources/economy.json dosyasının karşılığı. Denge değerleri koda değil bu veriye yazılır.
    // Unity'nin JsonUtility okuyucusuyla uyumlu olması için sözlük kullanılmaz.
    [Serializable]
    public sealed class EconomyData
    {
        public List<ResourceData> Resources;
        public List<RecipeData> Recipes;
        public List<BuildingData> Buildings;
        public List<TransportData> Transport;
        public BalanceData Balance;
    }

    // Ulaşım türü: hız, kapasite ve maliyet karayoluna göre binde oranla yazılır. Tesis gerekiyorsa adı burada yazar.
    [Serializable]
    public sealed class TransportData
    {
        public string Key;
        public int SpeedPermille;
        public int CapacityPermille;
        public int CostPermille;
        public string Building;
    }

    // Şehre kurulan tesis türü. Birden fazla ürün üretebilir; maliyeti ve kurulum süresi burada yazar.
    [Serializable]
    public sealed class BuildingData
    {
        public string Key;
        public string Tab;
        public List<string> Recipes;
        public long Money;
        public int Days;
        public List<RecipeAmount> Materials;
        public bool Coastal;
        public bool Locked;
    }

    [Serializable]
    public sealed class ResourceData
    {
        public string Key;
        public long BasePrice;
    }

    [Serializable]
    public sealed class RecipeAmount
    {
        public string Key;
        public int Amount;
    }

    [Serializable]
    public sealed class RecipeData
    {
        public string Key;
        public string Kind;
        public List<RecipeAmount> Inputs;
        public List<RecipeAmount> Outputs;
        public int EnergyOutput;
        public int Days;
    }

    [Serializable]
    public sealed class BalanceData
    {
        public int SecondsPerDay;
        public int MinCitySlots;
        public int MaxCitySlots;
        public long PopulationPerSlot;
        public int KombinaPermillePerLink;
        public int MaxFacilityLevel;
        public int ExperienceBaseDays;
        public int ExperienceGrowthPermille;
        public int UpgradeCostPermillePerLevel;
        public int UpgradeDaysPermillePerLevel;
        public int LevelBonusPermille;
        public long FactoryEnergyNeedMilli;
        public long FactoryConsumableMilli;
        public int MachineLifeDays;
        public long PopulationEnergyMilliPerMillion;
        public long FoodMilliPerMillion;
        public long GoodsMilliPerMillion;
        public int LowSatisfactionPermille;
        public int BaseSatisfactionPermille;
        public int SatisfactionStepDivisor;
        public int TaxSatisfactionPenaltyPerPercent;
        public long TaxIncomePerMillionPerPercent;
        public long GrowthPerMillionPerDay;
        public long StarvationPerMillionPerDay;
        public int MarketTargetDays;
        public int MarketSellAboveTargetMultiplier;
        public int MarketSellDivisor;
        public int RoadFeeStepKm;
        public int RoadFeePermillePerStep;
        public int RoadKmPerDay;
        public long RoadCapacityUnits;
        public int MarketReorderPermille;
        public int PriceMaxStepPermille;
        public int PriceReversionPermille;
        public int PriceFloorDivisor;
        public int PriceCeilingMultiplier;
        public int AiPlanningIntervalDays;
        public int EventChancePerMillion;
        public int EventSatisfactionHitPermille;
    }
}
