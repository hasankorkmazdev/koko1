using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // İsyan penceresi: süren isyanlar tek yerde toplanır, seçim hepsine veya dokunulan şehre uygulanır.
    // Kurallar docs/MECHANICS.md 11'dedir. Ordu kodda olmadığı için askeri bastırma kapalıdır.
    public sealed class RebellionView
    {
        private const int AllCities = WorldState.NoCity;
        private const int PercentDivisor = 10;
        private const int FullPercent = 100;
        private const string ApplyText = "UYGULA";

        private static readonly Color Muted = UiColors.Hex("#B5AE92");

        private sealed class CityRow
        {
            public MetalPanel Root;
            public Label Name;
            public Label Info;
            public Label Days;
            public int City;
        }

        private sealed class Option
        {
            public MetalPanel Button;
            public Label ButtonLabel;
            public Label Description;
        }

        private readonly GameSession _session;
        private readonly Action _closed;
        private readonly Action _answered;
        private readonly ScreenFrame _screen;
        private readonly Label _notice;
        private readonly Label _noRebellion;
        private readonly VisualElement _cityList;
        private readonly CityRow _allRow;
        private readonly List<CityRow> _rows = new List<CityRow>();
        private readonly VisualElement _options;
        private readonly Option _concession;
        private readonly Option _aid;

        private int _selected = AllCities;

        public bool IsOpen { get; private set; }

        public RebellionView(VisualElement frame, GameSession session, Action closed, Action answered)
        {
            _session = session;
            _closed = closed;
            _answered = answered;
            _screen = new ScreenFrame(frame, session, "İSYAN", Close);

            ScrollView scroll = UiBuilder.VerticalScroll(_screen.Root, "gw-builder-scroll");
            VisualElement content = scroll.contentContainer;

            _notice = UiBuilder.Text(content, "", "gw-rebel-notice", "gw-f-600");
            _noRebellion = UiBuilder.Text(content, "Süren isyan yok.", "gw-info-text", "gw-muted");

            _cityList = UiBuilder.Passive(content, "gw-rebel-cities");
            UiBuilder.Text(_cityList, "Şehirler", "gw-resource-group", "gw-f-oswald-600");
            _allRow = AddRow(AllCities);
            _allRow.Name.text = "Tüm şehirler";

            _options = UiBuilder.Passive(content, "gw-rebel-options");
            UiBuilder.Text(_options, "Seçenekler", "gw-resource-group", "gw-f-oswald-600");

            BalanceData balance = session.Definition.Balance;
            _concession = AddOption("Taviz", () => Answer(RebellionResponse.Concession));
            _concession.Description.text = balance.ConcessionDays + " gün vergi %0, üretim %"
                + (FullPercent - balance.ConcessionProductionPermille / PercentDivisor) + " düşük.";
            _aid = AddOption("Yardım paketi", () => Answer(RebellionResponse.Aid));

            Option military = AddOption("Askeri bastırma", UiBuilder.NoAction);
            military.Description.text = "Ordu kurulunca açılır.";
            ShowEnabled(military, false);
        }

        // Pencere açıkken yeni kayıp olursa eski uyarının yerine geçer. Kayıp yoksa uyarı kapanır.
        public void Open(int lostCities)
        {
            IsOpen = true;
            _screen.SetVisible(true);
            _notice.text = lostCities > 0 ? LossText(lostCities) : "";
            _notice.style.display = lostCities > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            Debug.Log("GeoWarUi rebellion open cities=" + _session.RebellionCities.Count + " lost=" + lostCities);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _screen.SetVisible(false);
            _closed();
        }

        // Gün bitince ve cevap verilince çağrılır; her karede değil.
        public void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            List<int> cities = _session.RebellionCities;
            if (!cities.Contains(_selected))
            {
                _selected = AllCities;
            }

            bool hasRebellion = cities.Count > 0;
            _noRebellion.style.display = hasRebellion ? DisplayStyle.None : DisplayStyle.Flex;
            _cityList.style.display = hasRebellion ? DisplayStyle.Flex : DisplayStyle.None;
            _options.style.display = hasRebellion ? DisplayStyle.Flex : DisplayStyle.None;
            _screen.Subtitle.text = hasRebellion
                ? cities.Count + " şehirde isyan · süre dolarsa şehir elden çıkar"
                : "Ülke sakin";

            RefreshRows(cities);
            RefreshAid();
        }

        private void RefreshRows(List<int> cities)
        {
            // Tek isyan varsa "tüm şehirler" satırı gereksizdir.
            _allRow.Root.style.display = cities.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            StyleRow(_allRow, _selected == AllCities);
            _allRow.Info.text = "Seçim hepsine uygulanır";

            while (_rows.Count < cities.Count)
            {
                _rows.Add(AddRow(AllCities));
            }

            WorldState world = _session.World;
            for (int i = 0; i < _rows.Count; i++)
            {
                CityRow row = _rows[i];
                bool isUsed = i < cities.Count;
                row.Root.style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isUsed)
                {
                    continue;
                }

                int city = cities[i];
                row.City = city;
                row.Name.text = _session.Map.ProvinceNames[city];
                row.Info.text = NumberFormat.Compact(world.Population[city]) + " kişi · memnuniyet %" + _session.CitySatisfactionPercent(city);
                row.Days.text = world.CityRebellionDays[city] + " gün";
                StyleRow(row, cities.Count == 1 || _selected == city);
            }
        }

        private void RefreshAid()
        {
            WorldState world = _session.World;
            BalanceData balance = _session.Definition.Balance;
            long cost = SelectedAidCostMilli();
            bool canPay = world.Money[_session.Player] >= cost;

            _aid.Description.text = "Memnuniyet +%" + balance.AidSatisfactionGainPermille / PercentDivisor
                + ", bedel " + NumberFormat.Compact(cost / EconomyDefinition.MilliPerUnit) + (canPay ? "." : ". Hazine yetmiyor.");
            _aid.Description.style.color = canPay ? Muted : UiColors.Warn;
            ShowEnabled(_aid, canPay);
        }

        private long SelectedAidCostMilli()
        {
            return _selected == AllCities
                ? Rebellions.TotalAidCostMilli(_session.Definition, _session.World, _session.Player)
                : Rebellions.AidCostMilli(_session.Definition, _session.World, _selected);
        }

        private void Answer(RebellionResponse response)
        {
            int answered = _selected == AllCities
                ? _session.AnswerAllRebellions(response)
                : _session.AnswerRebellion(_selected, response);
            if (answered == 0)
            {
                return;
            }

            Debug.Log("GeoWarUi rebellion answer=" + response + " cities=" + answered);
            _answered();

            // Uyarı okunmadıysa pencere açık kalır; başka isyan da yoksa kapanır.
            if (_session.RebellionCities.Count == 0 && _notice.style.display == DisplayStyle.None)
            {
                Close();
            }
            else
            {
                Refresh();
            }
        }

        private string LossText(int lostCities)
        {
            WorldState world = _session.World;
            int city = world.CountryLastLostCity[_session.Player];
            string name = _session.Map.ProvinceNames[city];
            string state = _session.CountryTitle(world.RebelStateOf[_session.Player]);
            return lostCities == 1
                ? name + " elden çıktı ve " + state + " tarafına geçti."
                : name + " ve " + (lostCities - 1) + " şehir daha elden çıktı, " + state + " tarafına geçti.";
        }

        private CityRow AddRow(int city)
        {
            var row = new CityRow { City = city };
            row.Root = new MetalPanel().WithClick(() => SelectRow(row));
            row.Root.AddToClassList("gw-rebel-row");
            _cityList.Add(row.Root);

            VisualElement text = UiBuilder.Passive(row.Root, "gw-info-row-text");
            row.Name = UiBuilder.Text(text, "", "gw-rebel-row-name", "gw-f-700");
            row.Info = UiBuilder.Text(text, "", "gw-rebel-row-info");
            row.Days = UiBuilder.Text(row.Root, "", "gw-rebel-row-days", "gw-f-700");
            return row;
        }

        private void SelectRow(CityRow row)
        {
            _selected = row.City;
            Refresh();
        }

        private static void StyleRow(CityRow row, bool isActive)
        {
            ScreenFrame.StyleChoice(row.Root, row.Name, isActive);
            row.Info.style.color = isActive ? UiColors.DarkText : Muted;
            row.Days.style.color = isActive ? UiColors.DarkText : UiColors.Warn;
        }

        private Option AddOption(string title, Action action)
        {
            MetalPanel card = new MetalPanel().WithGradient(UiColors.Well, UiColors.Well);
            card.pickingMode = PickingMode.Ignore;
            card.AddToClassList("gw-rebel-option");
            _options.Add(card);

            VisualElement text = UiBuilder.Passive(card, "gw-info-row-text");
            UiBuilder.Text(text, title, "gw-detail-name", "gw-f-oswald-700");
            var option = new Option { Description = UiBuilder.Text(text, "", "gw-info-text", "gw-muted") };

            option.Button = UiBuilder.GoldButton().WithClick(action);
            option.Button.AddToClassList("gw-gold-button");
            option.Button.AddToClassList("gw-rebel-apply");
            option.ButtonLabel = UiBuilder.Text(option.Button, ApplyText, "gw-f-oswald-700");
            option.ButtonLabel.style.color = UiColors.DarkText;
            card.Add(option.Button);
            return option;
        }

        private static void ShowEnabled(Option option, bool isEnabled)
        {
            UiBuilder.ApplyGold(option.Button, isEnabled);
            option.ButtonLabel.style.color = isEnabled ? UiColors.DarkText : Muted;
        }
    }
}
