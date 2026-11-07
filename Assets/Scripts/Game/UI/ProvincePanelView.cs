using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Oyuncunun iline dokununca açılan panel: bayrak, kısa bilgi, rezervler, slotlar ve seçili tesis.
    public sealed class ProvincePanelView
    {
        private const float StrokeBold = 2.4f;
        private const float StrokeTile = 1.8f;
        private const int LowSatisfactionPercent = 50;
        private const float StatGap = 4f;
        private const string EmptySlotIcon = "plus";

        private static readonly Color TileEmptyBorder = UiColors.Hex("#5F5C44");
        private static readonly Color TileSelectedBorder = UiColors.Hex("#F6EBC2");
        private static readonly Color TileSelectedFill = UiColors.Hex("#3D3B2A");
        private static readonly Color TilePlusTint = UiColors.Hex("#8A8468");
        private static readonly Color DisabledFill = UiColors.Hex("#2C2B20");
        private static readonly Color DisabledText = UiColors.Hex("#B5AE92");

        private readonly GameSession _session;
        private readonly FacilityText _text;
        private readonly Action<int> _openBuilder;
        private readonly Action _changed;
        private readonly CityInfoParts _info;

        private readonly MetalPanel _panel;
        private readonly Label _name;
        private readonly Label _sub;
        private readonly Label _population;
        private readonly Label _satisfaction;
        private readonly VisualElement _satisfactionFill;
        private readonly Label _tax;
        private readonly Label _slotCaption;
        private readonly ScrollView _tiles;
        private readonly MetalPanel _detailCard;
        private readonly VisualElement _emptyDetail;
        private readonly VisualElement _builtDetail;
        private readonly IconElement _detailIcon;
        private readonly Label _detailName;
        private readonly Label _detailLevel;
        private readonly Label _detailInfo;
        private readonly VisualElement _experienceFill;
        private readonly Label _status;
        private readonly MetalPanel _upgrade;
        private readonly Label _upgradeLabel;
        private readonly Label _upgradeCost;

        private readonly List<MetalPanel> _tileViews = new List<MetalPanel>();
        private readonly List<Label> _tileLevels = new List<Label>();
        private readonly List<int> _tileBuildings = new List<int>();

        private int _city = -1;
        private int _selectedSlot = -1;

        public ProvincePanelView(VisualElement frame, GameSession session, FacilityText text, Action close, Action openCountry, Action<int> openBuilder, Action changed)
        {
            _session = session;
            _text = text;
            _openBuilder = openBuilder;
            _changed = changed;
            _info = new CityInfoParts(session);

            _panel = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _panel.AddToClassList("gw-panel");
            _panel.AddToClassList("gw-province-panel");
            _panel.style.display = DisplayStyle.None;
            frame.Add(_panel);

            VisualElement header = UiBuilder.Passive(_panel, "gw-panel-header");
            VisualElement title = UiBuilder.Passive(header, "gw-panel-title");
            _info.AddFlagButton(title, openCountry);
            _name = UiBuilder.Text(title, "", "gw-tag");
            _sub = UiBuilder.Text(title, "", "gw-panel-sub");
            UiBuilder.CloseButton(header, session, close);

            _info.AddBrief(_panel);

            VisualElement stats = UiBuilder.Passive(_panel, "gw-stats");
            _population = AddStat(stats, "Nüfus", 0f);

            VisualElement satisfactionCell = UiBuilder.Passive(stats, "gw-stat");
            satisfactionCell.style.marginLeft = StatGap;
            UiBuilder.Text(satisfactionCell, "Memnuniyet", "gw-stat-caption");
            VisualElement satisfactionRow = UiBuilder.Passive(satisfactionCell, "gw-sat-row");
            _satisfaction = UiBuilder.Text(satisfactionRow, "", "gw-stat-value", "gw-f-700");
            VisualElement bar = UiBuilder.Passive(satisfactionRow, "gw-sat-bar");
            _satisfactionFill = UiBuilder.Passive(bar, "gw-bar-fill");

            _tax = AddStat(stats, "Vergi (ülke)", StatGap);

            VisualElement resources = UiBuilder.Passive(_panel, "gw-resources");
            _info.AddDeposits(resources);
            MetalPanel survey = UiBuilder.MetalButton().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(UiBuilder.NoAction);
            survey.AddToClassList("gw-text-button");
            UiBuilder.Text(survey, "Kaynak ara", "gw-f-600");
            resources.Add(survey);

            VisualElement slots = UiBuilder.Passive(_panel, "gw-slots");
            _slotCaption = UiBuilder.Text(slots, "", "gw-slots-caption");
            _tiles = UiBuilder.HorizontalScroll(slots, "gw-slot-scroll");

            _detailCard = new MetalPanel().WithGradient(UiColors.Well, UiColors.Well);
            _detailCard.pickingMode = PickingMode.Ignore;
            _detailCard.AddToClassList("gw-detail-card");
            _panel.Add(_detailCard);

            _emptyDetail = UiBuilder.Passive(_detailCard, "gw-detail");
            BuildEmptyDetail(_emptyDetail);

            _builtDetail = UiBuilder.Passive(_detailCard, "gw-detail");
            VisualElement detailHead = UiBuilder.Passive(_builtDetail, "gw-detail-head");
            _detailIcon = new IconElement(session.Icons.Get(EmptySlotIcon), UiColors.Text, StrokeTile);
            _detailIcon.AddToClassList("gw-detail-icon");
            detailHead.Add(_detailIcon);

            VisualElement detailText = UiBuilder.Passive(detailHead, "gw-detail-text");
            VisualElement detailRow = UiBuilder.Passive(detailText, "gw-detail-row");
            _detailName = UiBuilder.Text(detailRow, "", "gw-detail-name", "gw-f-oswald-700");
            _detailLevel = UiBuilder.Text(detailRow, "", "gw-detail-level", "gw-f-700");
            _detailInfo = UiBuilder.Text(detailText, "", "gw-doc");

            VisualElement experienceRow = UiBuilder.Passive(_builtDetail, "gw-xp-row");
            UiBuilder.Text(experienceRow, "Deneyim", "gw-xp-caption");
            VisualElement experienceBar = UiBuilder.Passive(experienceRow, "gw-xp-bar");
            _experienceFill = UiBuilder.Passive(experienceBar, "gw-bar-fill");
            _status = UiBuilder.Text(experienceRow, "", "gw-xp-status", "gw-f-600");

            _upgrade = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OnUpgrade);
            _upgrade.AddToClassList("gw-gold-button");
            _upgrade.style.marginLeft = StatGap * 2f;
            _upgradeLabel = UiBuilder.Text(_upgrade, "YÜKSELT", "gw-f-oswald-700");
            experienceRow.Add(_upgrade);

            _upgradeCost = UiBuilder.Text(_builtDetail, "", "gw-doc");
        }

        public void Show(int city, int preferredSlot)
        {
            _city = city;
            int first = _session.World.CitySlotStart[city];
            _selectedSlot = preferredSlot >= 0 ? preferredSlot : first;
            _panel.style.display = DisplayStyle.Flex;

            WorldState world = _session.World;
            _name.text = NumberFormat.Upper(_session.Map.ProvinceNames[city]);
            _sub.text = _session.Map.ProvinceIds[city];
            _tax.text = "%" + world.TaxPercent[_session.Player];

            _info.Show(city);
            BuildTiles();
            Refresh();
        }

        public void Hide()
        {
            _city = -1;
            _panel.style.display = DisplayStyle.None;
        }

        // Yeni tesis kurulunca paneli o tesise getirir.
        public void ShowSlot(int slot)
        {
            if (_city >= 0)
            {
                _selectedSlot = slot;
                BuildTiles();
                Refresh();
            }
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_city < 0)
            {
                return;
            }

            WorldState world = _session.World;
            _info.Refresh(_city);
            _population.text = NumberFormat.Compact(world.Population[_city]);

            int satisfaction = _session.CitySatisfactionPercent(_city);
            Color satisfactionColor = satisfaction < LowSatisfactionPercent ? UiColors.Warn : UiColors.Good;
            _satisfaction.text = satisfaction.ToString();
            _satisfaction.style.color = satisfactionColor;
            _satisfactionFill.style.width = Length.Percent(satisfaction);
            _satisfactionFill.style.backgroundColor = satisfactionColor;

            _slotCaption.text = "Slot " + FacilityCommands.UsedSlots(world, _city) + "/" + FacilityCommands.SlotCount(world, _city);
            RefreshTiles();
            RefreshDetail();
        }

        private static Label AddStat(VisualElement parent, string caption, float marginLeft)
        {
            VisualElement cell = UiBuilder.Passive(parent, "gw-stat");
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-stat-caption");
            return UiBuilder.Text(cell, "", "gw-stat-value", "gw-f-700");
        }

        private void BuildEmptyDetail(VisualElement parent)
        {
            VisualElement row = UiBuilder.Passive(parent, "gw-empty-row");
            VisualElement text = UiBuilder.Passive(row, "gw-empty-text");
            UiBuilder.Text(text, "Boş slot", "gw-detail-name", "gw-f-oswald-700");
            UiBuilder.Text(text, "Para ve malzeme harcayarak tesis kurabilirsin.", "gw-doc");

            MetalPanel build = UiBuilder.GoldButton().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(() => _openBuilder(_city));
            build.AddToClassList("gw-gold-button");
            Label label = UiBuilder.Text(build, "TESİS KUR", "gw-f-oswald-700");
            label.style.color = UiColors.DarkText;
            row.Add(build);
        }

        private void BuildTiles()
        {
            _tiles.Clear();
            _tileViews.Clear();
            _tileLevels.Clear();
            _tileBuildings.Clear();

            WorldState world = _session.World;
            for (int slot = world.CitySlotStart[_city]; slot < world.CitySlotStart[_city + 1]; slot++)
            {
                int current = slot;
                MetalPanel tile = new MetalPanel().WithClick(() => SelectSlot(current));
                tile.AddToClassList("gw-tile");
                _tiles.Add(tile);

                int building = world.FacilityBuilding[slot];
                bool isEmpty = building == WorldState.NoBuilding;
                string iconKey = isEmpty ? EmptySlotIcon : _session.BuildingViews[building].Icon;
                var icon = new IconElement(_session.Icons.Get(iconKey), isEmpty ? TilePlusTint : UiColors.Text, StrokeTile);
                icon.AddToClassList("gw-tile-icon");
                tile.Add(icon);

                Label level = UiBuilder.Text(tile, "", "gw-tile-level");
                level.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;

                _tileViews.Add(tile);
                _tileLevels.Add(level);
                _tileBuildings.Add(building);
            }
        }

        private void RefreshTiles()
        {
            WorldState world = _session.World;
            int first = world.CitySlotStart[_city];
            for (int i = 0; i < _tileViews.Count; i++)
            {
                int slot = first + i;
                bool selected = slot == _selectedSlot;
                bool isEmpty = _tileBuildings[i] == WorldState.NoBuilding;

                Color fill = selected ? TileSelectedFill : UiColors.Well;
                Color border = selected ? TileSelectedBorder : isEmpty ? TileEmptyBorder : _text.Status(slot).Accent;
                _tileViews[i].WithGradient(fill, fill);
                UiBuilder.SetBorder(_tileViews[i], border);

                if (!isEmpty)
                {
                    _tileLevels[i].text = world.FacilityLevel[slot].ToString();
                }
            }
        }

        private void RefreshDetail()
        {
            WorldState world = _session.World;
            int building = world.FacilityBuilding[_selectedSlot];
            bool isEmpty = building == WorldState.NoBuilding;
            _emptyDetail.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            _builtDetail.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            if (isEmpty)
            {
                UiBuilder.SetBorder(_detailCard, TileEmptyBorder);
                return;
            }

            int level = world.FacilityLevel[_selectedSlot];
            FacilityStatus status = _text.Status(_selectedSlot);
            UiBuilder.SetBorder(_detailCard, status.Accent);
            _detailIcon.SetPath(_session.Icons.Get(_session.BuildingViews[building].Icon));
            _detailName.text = _session.BuildingViews[building].Name;
            _detailLevel.text = "Seviye " + level;
            _detailInfo.text = _text.Rate(building, world.FacilityRecipe[_selectedSlot], level) + " · " + _text.Owner(_selectedSlot);
            _experienceFill.style.width = Length.Percent(_text.ExperiencePercent(_selectedSlot));
            _experienceFill.style.backgroundColor = status.BarColor;
            _status.text = status.Text;
            _status.style.color = status.TextColor;

            bool isReady = FacilityCommands.IsReadyToUpgrade(_session.Definition, world, _selectedSlot);
            _upgrade.style.display = isReady ? DisplayStyle.Flex : DisplayStyle.None;
            _upgradeCost.style.display = isReady ? DisplayStyle.Flex : DisplayStyle.None;
            if (isReady)
            {
                bool canPay = FacilityCommands.CanPayUpgrade(_session.Definition, world, _session.Player, _selectedSlot);
                UiBuilder.ApplyGold(_upgrade, canPay);
                _upgradeLabel.style.color = canPay ? UiColors.DarkText : DisabledText;
                _upgradeCost.text = _text.UpgradeCost(_selectedSlot);
                _upgradeCost.style.color = canPay ? DisabledText : UiColors.Warn;
            }
        }

        private void SelectSlot(int slot)
        {
            _selectedSlot = slot;
            RefreshTiles();
            RefreshDetail();
        }

        private void OnUpgrade()
        {
            if (_selectedSlot >= 0 && _session.Upgrade(_selectedSlot))
            {
                Debug.Log("GeoWarUi upgrade slot=" + _selectedSlot);
                Refresh();
                _changed();
            }
        }
    }

    // Başka ülkenin iline dokununca açılan panel: bayrak, kısa bilgi ve rezervler. İlişki ve savaş bayraktan açılan ülke panelindedir.
    public sealed class ForeignPanelView
    {
        private const int NoProvince = -1;

        // Bundan uzun şehir adı bayrak ve kapatma düğmesinin arasına küçük yazıyla sığar.
        private const int LongNameLength = 26;

        private readonly GameSession _session;
        private readonly CityInfoParts _info;
        private readonly MetalPanel _panel;
        private readonly Label _name;
        private readonly Label _sub;

        private int _province = NoProvince;

        public ForeignPanelView(VisualElement frame, GameSession session, Action close, Action openCountry)
        {
            _session = session;
            _info = new CityInfoParts(session);

            _panel = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _panel.AddToClassList("gw-panel");
            _panel.AddToClassList("gw-foreign-panel");
            _panel.style.display = DisplayStyle.None;
            frame.Add(_panel);

            VisualElement header = UiBuilder.Passive(_panel, "gw-panel-header");
            VisualElement title = UiBuilder.Passive(header, "gw-panel-title");
            _info.AddFlagButton(title, openCountry);
            VisualElement column = UiBuilder.Passive(title, "gw-panel-title-column");
            _name = UiBuilder.Text(column, "", "gw-tag", "gw-tag-small");
            _sub = UiBuilder.Text(column, "", "gw-panel-sub-below");
            UiBuilder.CloseButton(header, session, close);

            _info.AddBrief(_panel);

            VisualElement resources = UiBuilder.Passive(_panel, "gw-resources");
            _info.AddDeposits(resources);
        }

        public void Show(int province)
        {
            _province = province;
            int country = _session.Map.ProvinceCountry[province];
            _name.text = NumberFormat.Upper(_session.Map.ProvinceNames[province]);
            _name.EnableInClassList("gw-tag-long", _name.text.Length > LongNameLength);
            _sub.text = _session.CountryTitle(country) + " · " + CountryPanelView.RelationName(_session.Relations[country]);
            _info.Show(province);
            _panel.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _province = NoProvince;
            _panel.style.display = DisplayStyle.None;
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_province != NoProvince)
            {
                _info.Refresh(_province);
            }
        }
    }
}
