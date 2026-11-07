using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Şehir panelindeki bayrağa dokununca açılan panel: ilişki, savaş durumu, ittifaklar ve ülkenin genel bilgileri.
    // Diplomasi mekaniği geldiğinde düğmeler bağlanacak.
    public sealed class CountryPanelView
    {
        private const int NoCountry = -1;
        private const int ScoreRange = 100;
        private const float StrokeBack = 2.4f;
        private const float StrokeChip = 2.4f;
        private const float StatGap = 4f;
        private const float ActionGap = 6f;

        private static readonly Color DangerTop = UiColors.Hex("#9A4032");
        private static readonly Color DangerBottom = UiColors.Hex("#7E3226");
        private static readonly Color DangerBorder = UiColors.Hex("#2E0F09");
        private static readonly Color DangerInset = UiColors.Hex("#C07A64");
        private static readonly Color FriendlyTop = UiColors.Hex("#A3BA7C");
        private static readonly Color FriendlyBottom = UiColors.Hex("#829C60");
        private static readonly Color FriendlyBorder = UiColors.Hex("#3E5228");
        private static readonly Color FriendlyInset = UiColors.Hex("#C9D6A8");
        private static readonly Color FriendlyText = UiColors.Hex("#10241A");
        private static readonly Color PeaceChip = UiColors.Hex("#BDB69C");
        private static readonly Color PeaceText = UiColors.Hex("#1B2428");
        private static readonly Color WarChip = UiColors.Hex("#8E3B2E");
        private static readonly Color AllyChip = UiColors.Hex("#8FA86B");
        private static readonly Color Muted = UiColors.Hex("#B5AE92");

        private readonly GameSession _session;
        private readonly MetalPanel _panel;
        private readonly ScrollView _scroll;
        private readonly FlagElement _flag;
        private readonly Label _name;
        private readonly Label _sub;
        private readonly VisualElement _status;
        private readonly VisualElement _scoreCell;
        private readonly Label _score;
        private readonly VisualElement _scoreFill;
        private readonly Label _noEnemies;
        private readonly VisualElement _enemies;
        private readonly Label _noAllies;
        private readonly VisualElement _allies;
        private readonly Label _population;
        private readonly VisualElement _borderCell;
        private readonly Label _border;
        private readonly VisualElement _distanceCell;
        private readonly Label _distance;
        private readonly VisualElement _routeCell;
        private readonly Label _route;
        private readonly VisualElement _actions;
        private readonly List<int> _countries = new List<int>();

        private int _country = NoCountry;

        public CountryPanelView(VisualElement frame, GameSession session, Action close)
        {
            _session = session;

            _panel = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _panel.AddToClassList("gw-panel");
            _panel.AddToClassList("gw-country-panel");
            _panel.style.display = DisplayStyle.None;
            frame.Add(_panel);

            VisualElement header = UiBuilder.Passive(_panel, "gw-country-header");
            MetalPanel back = UiBuilder.MetalButton().WithHitOutset(UiBuilder.SmallButtonHitOutset, UiBuilder.SmallButtonHitOutset).WithClick(close);
            back.AddToClassList("gw-icon-button");
            var backIcon = new IconElement(session.Icons.Get("back"), UiColors.Text, StrokeBack);
            backIcon.AddToClassList("gw-back-icon");
            back.Add(backIcon);
            header.Add(back);

            _flag = new FlagElement(null);
            _flag.AddToClassList("gw-country-flag");
            header.Add(_flag);

            VisualElement title = UiBuilder.Passive(header, "gw-panel-title-column");
            _name = UiBuilder.Text(title, "", "gw-tag", "gw-tag-small");
            _sub = UiBuilder.Text(title, "", "gw-panel-sub-below");

            _scroll = UiBuilder.VerticalScroll(_panel, "gw-country-scroll");
            VisualElement content = _scroll.contentContainer;

            VisualElement relationRow = UiBuilder.Passive(content, "gw-stats");
            VisualElement statusCell = UiBuilder.Passive(relationRow, "gw-stat");
            UiBuilder.Text(statusCell, "İlişki durumu", "gw-stat-caption");
            _status = UiBuilder.Passive(statusCell, "gw-status-chip", "gw-country-status");

            _scoreCell = UiBuilder.Passive(relationRow, "gw-stat");
            _scoreCell.style.marginLeft = StatGap;
            UiBuilder.Text(_scoreCell, "İlişki puanı", "gw-stat-caption");
            VisualElement scoreRow = UiBuilder.Passive(_scoreCell, "gw-sat-row");
            _score = UiBuilder.Text(scoreRow, "", "gw-stat-value", "gw-f-700");
            VisualElement scoreBar = UiBuilder.Passive(scoreRow, "gw-sat-bar");
            _scoreFill = UiBuilder.Passive(scoreBar, "gw-bar-fill");

            UiBuilder.Text(content, "Savaş durumu", "gw-resource-group", "gw-f-oswald-600");
            _noEnemies = UiBuilder.Text(content, "Hiçbir ülkeyle savaşmıyor.", "gw-info-text", "gw-muted");
            _enemies = UiBuilder.Passive(content, "gw-country-list");

            UiBuilder.Text(content, "İttifaklar", "gw-resource-group", "gw-f-oswald-600");
            _noAllies = UiBuilder.Text(content, "Hiçbir ülkeyle ittifakı yok.", "gw-info-text", "gw-muted");
            _allies = UiBuilder.Passive(content, "gw-country-list");

            UiBuilder.Text(content, "Genel bilgi", "gw-resource-group", "gw-f-oswald-600");
            VisualElement infoRow = UiBuilder.Passive(content, "gw-stats", "gw-country-info");
            _population = AddStat(infoRow, "Nüfus", 0f);
            _border = AddStat(infoRow, "Kara sınırı", StatGap);
            _borderCell = _border.parent;
            _distance = AddStat(infoRow, "Mesafe", StatGap);
            _distanceCell = _distance.parent;
            _route = AddStat(infoRow, "Ticaret yolu", StatGap);
            _routeCell = _route.parent;

            _actions = UiBuilder.Passive(_panel, "gw-foreign-actions");
        }

        public static string RelationName(CountryRelation relation)
        {
            switch (relation)
            {
                case CountryRelation.Own: return "Kendi ülken";
                case CountryRelation.Ally: return "İttifak";
                case CountryRelation.War: return "Savaş";
                default: return "Barış";
            }
        }

        public void Open(int country)
        {
            _country = country;
            _panel.style.display = DisplayStyle.Flex;
            _scroll.scrollOffset = Vector2.zero;

            bool isOwn = country == _session.Player;
            CountryRelation relation = _session.Relations[country];
            WorldState world = _session.World;

            _flag.SetFlag(_session.CountryFlag(country));
            _name.text = NumberFormat.Upper(_session.CountryTitle(country));
            _sub.text = "Başkent: " + _session.CapitalName(country) + " · " + CountryReport.CityCount(world, country) + " şehir";

            BuildStatus(relation);
            ShowScore(country, isOwn);

            CountryReport.CollectEnemies(world, country, _countries);
            FillCountries(_enemies, _noEnemies);
            _session.CollectAllies(country, _countries);
            FillCountries(_allies, _noAllies);

            DisplayStyle foreignOnly = isOwn ? DisplayStyle.None : DisplayStyle.Flex;
            _borderCell.style.display = foreignOnly;
            _distanceCell.style.display = foreignOnly;
            _routeCell.style.display = foreignOnly;

            BuildActions(relation);
            Refresh();
            Debug.Log("GeoWarUi country=" + _session.CountryKey(country));
        }

        public void Close()
        {
            _country = NoCountry;
            _panel.style.display = DisplayStyle.None;
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_country == NoCountry)
            {
                return;
            }

            WorldState world = _session.World;
            _population.text = NumberFormat.Compact(world.CountryPopulation[_country]);
            if (_country == _session.Player)
            {
                return;
            }

            int pair = _session.Player * world.CountryCount + _country;
            _border.text = CountryReport.AreNeighbors(world, _session.Player, _country) ? "Var" : "Yok";
            _distance.text = NumberFormat.Compact(world.CountryDistanceKm[pair]) + " km";

            int route = CountryRoutes.Route(_session.Definition, world, _session.Player, _country);
            _route.text = route == CountryRoutes.NoRoute ? "Yok" : _session.TransportName(route);
        }

        private static Label AddStat(VisualElement parent, string caption, float marginLeft)
        {
            VisualElement cell = UiBuilder.Passive(parent, "gw-stat");
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-stat-caption");
            return UiBuilder.Text(cell, "", "gw-stat-value", "gw-f-700");
        }

        private void ShowScore(int country, bool isOwn)
        {
            _scoreCell.style.display = isOwn ? DisplayStyle.None : DisplayStyle.Flex;
            if (isOwn)
            {
                return;
            }

            CountryViewData view = _session.CountryViewAt(country);
            int score = view != null ? view.Score : 0;
            Color color = score > 0 ? UiColors.Good : score < 0 ? UiColors.Warn : Muted;
            _score.text = score > 0 ? "+" + score : score.ToString();
            _score.style.color = color;
            _scoreFill.style.width = Length.Percent((score + ScoreRange) * 100f / (2f * ScoreRange));
            _scoreFill.style.backgroundColor = color;
        }

        private void BuildStatus(CountryRelation relation)
        {
            _status.Clear();
            Color fill = relation == CountryRelation.Own ? UiColors.LegendOwn
                : relation == CountryRelation.Ally ? AllyChip
                : relation == CountryRelation.War ? WarChip
                : PeaceChip;
            Color text = relation == CountryRelation.Ally ? FriendlyText
                : relation == CountryRelation.Peace ? PeaceText
                : Color.white;
            string iconKey = relation == CountryRelation.Ally ? "shield" : relation == CountryRelation.War ? "swords" : null;

            _status.style.backgroundColor = fill;
            if (iconKey != null)
            {
                var icon = new IconElement(_session.Icons.Get(iconKey), text, StrokeChip);
                icon.AddToClassList("gw-status-chip-icon");
                _status.Add(icon);
            }

            Label name = UiBuilder.Text(_status, RelationName(relation), "gw-f-700");
            name.style.color = text;
        }

        // Liste boşsa açıklama yazısı görünür; doluysa her ülke bayrağı ve adıyla yazılır.
        private void FillCountries(VisualElement list, Label empty)
        {
            list.Clear();
            bool isEmpty = _countries.Count == 0;
            empty.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;

            foreach (int country in _countries)
            {
                VisualElement chip = UiBuilder.Passive(list, "gw-chip", "gw-country-chip");
                var flag = new FlagElement(_session.CountryFlag(country));
                flag.AddToClassList("gw-chip-flag");
                chip.Add(flag);
                UiBuilder.Text(chip, _session.CountryTitle(country), "gw-f-600");
            }
        }

        private void BuildActions(CountryRelation relation)
        {
            _actions.Clear();
            if (relation == CountryRelation.War)
            {
                AddAction("Barış teklif et", UiColors.ButtonTop, UiColors.ButtonBottom, MapPalette.CountryLine, UiColors.ButtonInset, UiColors.Text, 0f);
            }
            else if (relation == CountryRelation.Ally)
            {
                AddAction("İttifakı boz", UiColors.ButtonTop, UiColors.ButtonBottom, MapPalette.CountryLine, UiColors.ButtonInset, UiColors.Text, 0f);
            }
            else if (relation == CountryRelation.Peace)
            {
                AddAction("Savaş ilan et", DangerTop, DangerBottom, DangerBorder, DangerInset, Color.white, 0f);
                AddAction("İttifak teklif et", FriendlyTop, FriendlyBottom, FriendlyBorder, FriendlyInset, FriendlyText, ActionGap);
            }
        }

        private void AddAction(string text, Color top, Color bottom, Color border, Color inset, Color textColor, float marginLeft)
        {
            MetalPanel button = new MetalPanel().WithGradient(top, bottom).WithInsets(inset, UiColors.Clear).WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(UiBuilder.NoAction);
            button.AddToClassList("gw-foreign-action");
            button.style.marginLeft = marginLeft;
            UiBuilder.SetBorder(button, border);
            Label label = UiBuilder.Text(button, text, "gw-f-oswald-700");
            label.style.color = textColor;
            _actions.Add(button);
        }
    }
}
