using System;

namespace GeoWar.Simulation
{
    public enum BuildBlock : byte
    {
        None,
        Locked,
        NeedsCoast,
        NoDeposit,
        AlreadyBuilt,
        NoSlot,
        NoMoney,
        NoMaterials
    }

    // Tesis kurma, yükseltme ve kapatma komutları. Kurallar docs/MECHANICS.md 4, 5, 13.6 ve 18'dedir.
    // Arayüz durumu doğrudan değiştirmez, bu komutları çağırır.
    public static class FacilityCommands
    {
        private const int Permille = EconomyDefinition.Permille;

        public static int CitySlots(BalanceData balance, long population)
        {
            long slots = balance.MinCitySlots + population / balance.PopulationPerSlot;
            return (int)Math.Min(balance.MaxCitySlots, slots);
        }

        public static int SlotCount(WorldState world, int city)
        {
            return world.CitySlotStart[city + 1] - world.CitySlotStart[city];
        }

        public static int UsedSlots(WorldState world, int city)
        {
            int used = 0;
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                if (world.FacilityBuilding[slot] != WorldState.NoBuilding)
                {
                    used++;
                }
            }

            return used;
        }

        public static bool HasDeposit(WorldState world, int city, int building)
        {
            return (world.CityDeposits[city] & (1L << building)) != 0;
        }

        // Para ve malzeme dışındaki engeller: seçim ekranı bunları kilit olarak gösterir.
        public static BuildBlock CheckPlacement(EconomyDefinition definition, WorldState world, int city, int building)
        {
            if (definition.BuildingLocked[building])
            {
                return BuildBlock.Locked;
            }

            if (definition.BuildingCoastal[building] && !world.CityCoastal[city])
            {
                return BuildBlock.NeedsCoast;
            }

            if (IsExtraction(definition, building))
            {
                if (!HasDeposit(world, city, building))
                {
                    return BuildBlock.NoDeposit;
                }

                if (CityHasBuilding(world, city, building))
                {
                    return BuildBlock.AlreadyBuilt;
                }
            }

            return BuildBlock.None;
        }

        public static BuildBlock CheckBuild(EconomyDefinition definition, WorldState world, int country, int city, int building)
        {
            BuildBlock placement = CheckPlacement(definition, world, city, building);
            if (placement != BuildBlock.None)
            {
                return placement;
            }

            if (FindEmptySlot(world, city) < 0)
            {
                return BuildBlock.NoSlot;
            }

            if (world.Money[country] < definition.BuildingMoneyMilli[building])
            {
                return BuildBlock.NoMoney;
            }

            return HasMaterials(definition, world, country, building, Permille) ? BuildBlock.None : BuildBlock.NoMaterials;
        }

        // Kurulan tesisin slotunu döndürür; kurulamazsa -1.
        public static int TryBuild(EconomyDefinition definition, WorldState world, int country, int city, int building)
        {
            if (CheckBuild(definition, world, country, city, building) != BuildBlock.None)
            {
                return -1;
            }

            world.Money[country] -= definition.BuildingMoneyMilli[building];
            TakeMaterials(definition, world, country, building, Permille);

            int slot = FindEmptySlot(world, city);
            world.FacilityBuilding[slot] = building;
            world.FacilityRecipe[slot] = definition.BuildingDefaultRecipe(building);
            world.FacilityLevel[slot] = 1;
            world.FacilityExperience[slot] = 0;
            world.FacilityBuildDays[slot] = Math.Max(1, definition.BuildingDays[building]);
            world.FacilityUpgradeDays[slot] = 0;
            world.FacilityMachineDays[slot] = definition.Balance.MachineLifeDays;
            world.FacilityKombinaPermille[slot] = 0;
            world.FacilityPrivate[slot] = false;
            world.FacilityStateSharePermille[slot] = 0;
            world.FacilityAlarm[slot] = WorldState.AlarmNone;
            return slot;
        }

        public static long ExperienceNeeded(EconomyDefinition definition, int level)
        {
            return level < definition.ExperienceToLevelUp.Length ? definition.ExperienceToLevelUp[level] : 0;
        }

        public static bool IsReadyToUpgrade(EconomyDefinition definition, WorldState world, int slot)
        {
            if (world.FacilityBuilding[slot] == WorldState.NoBuilding
                || world.FacilityBuildDays[slot] > 0
                || world.FacilityUpgradeDays[slot] > 0
                || world.FacilityPrivate[slot])
            {
                return false;
            }

            long need = ExperienceNeeded(definition, world.FacilityLevel[slot]);
            return need > 0 && world.FacilityExperience[slot] >= need;
        }

        // Yükseltme maliyeti kurulum maliyetinin seviyeyle büyüyen payıdır.
        public static int UpgradeScalePermille(BalanceData balance, int level)
        {
            return level * balance.UpgradeCostPermillePerLevel;
        }

        public static int UpgradeDays(EconomyDefinition definition, int building, int level)
        {
            long days = definition.BuildingDays[building] * (long)level * definition.Balance.UpgradeDaysPermillePerLevel / Permille;
            return (int)Math.Max(1, days);
        }

        public static long UpgradeMoneyMilli(EconomyDefinition definition, int building, int level)
        {
            return definition.BuildingMoneyMilli[building] * UpgradeScalePermille(definition.Balance, level) / Permille;
        }

        public static bool CanPayUpgrade(EconomyDefinition definition, WorldState world, int country, int slot)
        {
            int building = world.FacilityBuilding[slot];
            int level = world.FacilityLevel[slot];
            return world.Money[country] >= UpgradeMoneyMilli(definition, building, level)
                && HasMaterials(definition, world, country, building, UpgradeScalePermille(definition.Balance, level));
        }

        public static bool TryUpgrade(EconomyDefinition definition, WorldState world, int country, int slot)
        {
            if (!IsReadyToUpgrade(definition, world, slot) || !CanPayUpgrade(definition, world, country, slot))
            {
                return false;
            }

            int building = world.FacilityBuilding[slot];
            int level = world.FacilityLevel[slot];
            world.Money[country] -= UpgradeMoneyMilli(definition, building, level);
            TakeMaterials(definition, world, country, building, UpgradeScalePermille(definition.Balance, level));
            world.FacilityUpgradeDays[slot] = UpgradeDays(definition, building, level);
            return true;
        }

        // Toplu yükseltme: ülkede hazır olan bu türdeki tesisler, para ve malzeme yettiği kadar yükselir.
        public static int UpgradeAllReady(EconomyDefinition definition, WorldState world, int country, int building)
        {
            int upgraded = 0;
            int end = world.CountryFacilityStart[country + 1];
            for (int slot = world.CountryFacilityStart[country]; slot < end; slot++)
            {
                if (world.FacilityBuilding[slot] == building && TryUpgrade(definition, world, country, slot))
                {
                    upgraded++;
                }
            }

            return upgraded;
        }

        // Tesis kapanınca seviyesi ve deneyimi de gider, slot boşalır. Şirket tesisini devlet kapatamaz.
        public static bool TryClose(EconomyDefinition definition, WorldState world, int slot)
        {
            if (world.FacilityBuilding[slot] == WorldState.NoBuilding || world.FacilityPrivate[slot])
            {
                return false;
            }

            world.RoutesDirty |= definition.BuildingIsTransport[world.FacilityBuilding[slot]];
            world.FacilityBuilding[slot] = WorldState.NoBuilding;
            world.FacilityRecipe[slot] = WorldState.NoRecipe;
            world.FacilityLevel[slot] = 0;
            world.FacilityExperience[slot] = 0;
            world.FacilityBuildDays[slot] = 0;
            world.FacilityUpgradeDays[slot] = 0;
            world.FacilityMachineDays[slot] = 0;
            world.FacilityKombinaPermille[slot] = 0;
            world.FacilityAlarm[slot] = WorldState.AlarmNone;
            RecomputeKombina(definition, world, world.FacilityCity[slot]);
            return true;
        }

        // Bu tür bu şehre kurulsa kaç kombina bağlantısı olur.
        public static int LinkCount(EconomyDefinition definition, WorldState world, int city, int building)
        {
            int links = 0;
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                int other = world.FacilityBuilding[slot];
                if (other != WorldState.NoBuilding
                    && world.FacilityBuildDays[slot] == 0
                    && definition.BuildingLinked[building * definition.BuildingCount + other])
                {
                    links++;
                }
            }

            return links;
        }

        // Şehirdeki fabrikaların verimini bağlantı sayısına göre yeniden yazar; sadece kurulum ve kapanışta çalışır.
        public static void RecomputeKombina(EconomyDefinition definition, WorldState world, int city)
        {
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                int building = world.FacilityBuilding[slot];
                world.FacilityKombinaPermille[slot] = building == WorldState.NoBuilding || world.FacilityBuildDays[slot] > 0
                    ? 0
                    : LinkCount(definition, world, city, building) * definition.Balance.KombinaPermillePerLink;
            }
        }

        public static bool IsExtraction(EconomyDefinition definition, int building)
        {
            int recipe = definition.BuildingDefaultRecipe(building);
            return recipe != WorldState.NoRecipe && definition.Kinds[recipe] == RecipeKind.Extraction;
        }

        private static bool CityHasBuilding(WorldState world, int city, int building)
        {
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                if (world.FacilityBuilding[slot] == building)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindEmptySlot(WorldState world, int city)
        {
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                if (world.FacilityBuilding[slot] == WorldState.NoBuilding)
                {
                    return slot;
                }
            }

            return -1;
        }

        private static bool HasMaterials(EconomyDefinition definition, WorldState world, int country, int building, int scalePermille)
        {
            int end = definition.BuildingMaterialStart[building + 1];
            for (int i = definition.BuildingMaterialStart[building]; i < end; i++)
            {
                if (Missing(definition, world, country, i, scalePermille) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void TakeMaterials(EconomyDefinition definition, WorldState world, int country, int building, int scalePermille)
        {
            int stockBase = country * world.ResourceCount;
            int end = definition.BuildingMaterialStart[building + 1];
            for (int i = definition.BuildingMaterialStart[building]; i < end; i++)
            {
                world.Stock[stockBase + definition.BuildingMaterialResource[i]] -= definition.BuildingMaterialMilli[i] * scalePermille / Permille;
            }
        }

        private static long Missing(EconomyDefinition definition, WorldState world, int country, int material, int scalePermille)
        {
            long need = definition.BuildingMaterialMilli[material] * scalePermille / Permille;
            long have = world.Stock[country * world.ResourceCount + definition.BuildingMaterialResource[material]];
            return Math.Max(0, need - have);
        }
    }
}
