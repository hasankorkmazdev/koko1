using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Oyuncunun ve başka ülkelerin şehir panellerinde ortak parçalar: ülkenin bayrağı, şehrin kısa bilgisi ve rezervleri.
    public sealed class CityInfoParts
    {
        private const float DepositIconStroke = 2f;
        private const float FlagHitOutsetX = 6f;
        private const float FlagHitOutsetY = 8f;

        private readonly GameSession _session;
        private readonly List<int> _depositBuildings = new List<int>();

        private FlagElement _flag;
        private Label _brief;
        private VisualElement _deposits;

        public CityInfoParts(GameSession session)
        {
            _session = session;
        }

        public void AddFlagButton(VisualElement parent, Action openCountry)
        {
            MetalPanel button = UiBuilder.MetalButton().WithHitOutset(FlagHitOutsetX, FlagHitOutsetY).WithClick(openCountry);
            button.AddToClassList("gw-flag-button");
            parent.Add(button);

            _flag = new FlagElement(null);
            _flag.AddToClassList("gw-flag-fill");
            button.Add(_flag);
        }

        public void AddBrief(VisualElement parent)
        {
            _brief = UiBuilder.Text(parent, "", "gw-doc", "gw-city-brief");
        }

        public void AddDeposits(VisualElement parent)
        {
            _deposits = UiBuilder.Passive(parent, "gw-chips");
        }

        // Şehir seçilince bir kez: bayrak ve rezervler gün içinde değişmez.
        public void Show(int city)
        {
            _flag.SetFlag(_session.CountryFlag(_session.Map.ProvinceCountry[city]));
            BuildDeposits(city);
            Refresh(city);
        }

        // Nüfus ve tesis sayısı değiştikçe kısa bilgi yenilenir.
        public void Refresh(int city)
        {
            WorldState world = _session.World;
            int country = _session.Map.ProvinceCountry[city];
            _brief.text = CityBrief.Text(
                _session.CountryTitle(country),
                CountryReport.CityCount(world, country),
                _session.IsCapital(city),
                world.Population[city],
                world.CityCoastal[city],
                FacilityCommands.UsedSlots(world, city));
        }

        private void BuildDeposits(int city)
        {
            _deposits.Clear();
            EconomyDefinition definition = _session.Definition;
            CountryReport.CollectDeposits(definition, _session.World, city, _depositBuildings);
            foreach (int building in _depositBuildings)
            {
                ResourceViewData resource = _session.ResourceViews[CountryReport.DepositResource(definition, building)];
                VisualElement chip = UiBuilder.Passive(_deposits, "gw-chip");
                var icon = new IconElement(_session.Icons.Get(resource.Icon), UiColors.Hex(resource.Tint), DepositIconStroke);
                icon.AddToClassList("gw-chip-icon");
                chip.Add(icon);
                UiBuilder.Text(chip, _session.BuildingViews[building].Deposit, "gw-f-600");
            }
        }
    }
}
