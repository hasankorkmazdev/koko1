using System.Text;
using GeoWar.Simulation;
using UnityEngine;

namespace GeoWar.Game
{
    public struct FacilityStatus
    {
        public string Text;
        public Color TextColor;
        public Color BarColor;
        public Color Accent;
    }

    // Tesis bilgisinin ekrandaki yazıları ve renkleri.
    public sealed class FacilityText
    {
        private const int FullPercent = 100;
        private const int OneDay = 1;

        private static readonly Color Muted = UiColors.Hex("#B5AE92");
        private static readonly Color Steel = UiColors.Hex("#9FBCC7");
        private static readonly Color WorkingBar = UiColors.Hex("#6FA8C7");
        private static readonly Color BuildingBar = UiColors.Hex("#5F5C44");

        private readonly GameSession _session;

        public FacilityText(GameSession session)
        {
            _session = session;
        }

        public string Rate(int building, int recipe, int level)
        {
            EconomyDefinition definition = _session.Definition;
            if (recipe == WorldState.NoRecipe)
            {
                return _session.BuildingViews[building].Produces;
            }

            int speed = EconomySimulator.LevelSpeedPermille(definition.Balance, level);
            int days = definition.RecipeDays[recipe];
            long amountMilli;
            string name;
            if (definition.Kinds[recipe] == RecipeKind.Energy)
            {
                amountMilli = definition.EnergyMilliPerCycle[recipe];
                name = "enerji";
            }
            else
            {
                int output = definition.OutputStart[recipe];
                amountMilli = definition.OutputMilliPerCycle[output];
                name = NumberFormat.Lower(_session.ResourceViews[definition.OutputResource[output]].Name);
            }

            string amount = NumberFormat.Milli(amountMilli * speed / EconomyDefinition.Permille);
            return days == OneDay ? "Günde " + amount + " " + name : days + " günde " + amount + " " + name;
        }

        public string Owner(int slot)
        {
            WorldState world = _session.World;
            if (!world.FacilityPrivate[slot])
            {
                return "Devlet";
            }

            return "Şirket · devlet payı %" + world.FacilityStateSharePermille[slot] / 10;
        }

        public int ExperiencePercent(int slot)
        {
            WorldState world = _session.World;
            long need = FacilityCommands.ExperienceNeeded(_session.Definition, world.FacilityLevel[slot]);
            return need > 0 ? (int)(world.FacilityExperience[slot] * FullPercent / need) : FullPercent;
        }

        public FacilityStatus Status(int slot)
        {
            WorldState world = _session.World;
            if (world.FacilityBuildDays[slot] > 0)
            {
                return new FacilityStatus { Text = "İnşaat: " + world.FacilityBuildDays[slot] + " gün", TextColor = Steel, BarColor = BuildingBar, Accent = Steel };
            }

            if (world.FacilityUpgradeDays[slot] > 0)
            {
                return new FacilityStatus { Text = "AR-GE: " + world.FacilityUpgradeDays[slot] + " gün", TextColor = Steel, BarColor = Steel, Accent = BuildingBar };
            }

            if (world.FacilityAlarm[slot] == WorldState.AlarmStopped)
            {
                return new FacilityStatus { Text = "Üretim durdu", TextColor = UiColors.Warn, BarColor = UiColors.Warn, Accent = UiColors.Alarm };
            }

            if (world.FacilityAlarm[slot] == WorldState.AlarmActive)
            {
                return new FacilityStatus { Text = "Alarm: " + MissingInput(slot) + " yok", TextColor = UiColors.Warn, BarColor = UiColors.Warn, Accent = UiColors.Alarm };
            }

            if (FacilityCommands.IsReadyToUpgrade(_session.Definition, world, slot))
            {
                return new FacilityStatus { Text = "Hazır", TextColor = UiColors.Gold, BarColor = UiColors.Gold, Accent = UiColors.Gold };
            }

            return new FacilityStatus { Text = "Çalışıyor", TextColor = Muted, BarColor = WorkingBar, Accent = BuildingBar };
        }

        // Yükseltmenin bedeli: para, malzemeler ve süre.
        public string UpgradeCost(int slot)
        {
            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;
            int building = world.FacilityBuilding[slot];
            int level = world.FacilityLevel[slot];
            int scale = FacilityCommands.UpgradeScalePermille(definition.Balance, level);

            var text = new StringBuilder("Bedel: ");
            text.Append(NumberFormat.Compact(FacilityCommands.UpgradeMoneyMilli(definition, building, level) / EconomyDefinition.MilliPerUnit));
            for (int i = definition.BuildingMaterialStart[building]; i < definition.BuildingMaterialStart[building + 1]; i++)
            {
                long amount = definition.BuildingMaterialMilli[i] * scale / EconomyDefinition.Permille / EconomyDefinition.MilliPerUnit;
                text.Append(" · ").Append(NumberFormat.Compact(amount)).Append(' ');
                text.Append(NumberFormat.Lower(_session.ResourceViews[definition.BuildingMaterialResource[i]].Name));
            }

            text.Append(" · ").Append(FacilityCommands.UpgradeDays(definition, building, level)).Append(" gün");
            return text.ToString();
        }

        // Alarmdaki tesisin eksik girdisini bulur.
        private string MissingInput(int slot)
        {
            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;
            int recipe = world.FacilityRecipe[slot];
            int stockBase = _session.Player * world.ResourceCount;
            for (int input = definition.InputStart[recipe]; input < definition.InputStart[recipe + 1]; input++)
            {
                int resource = definition.InputResource[input];
                if (world.Stock[stockBase + resource] < definition.InputMilliPerDay[input])
                {
                    return NumberFormat.Lower(_session.ResourceViews[resource].Name);
                }
            }

            int upkeep = world.FacilityMachineDays[slot] <= 0 ? definition.Machine : definition.Consumable;
            return NumberFormat.Lower(_session.ResourceViews[upkeep].Name);
        }
    }
}
