using System;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // İl panelindeki vergi kutusundan açılan pencere: docs/ECONOMY.md 1.2.
    // Seçilen oran 1 Ocak'ta yürürlüğe girer; o güne kadar değiştirilebilir. Tek düğme aynı oranı tüm şehirlere yazar,
    // büyük ülkede şehirler tek tek ayarlanmaz.
    public sealed class TaxView
    {
        private const int PercentDivisor = 10;
        private const string MinusSign = "−";
        private const string PlusSign = "+";
        private const string ChangeArrow = " → ";

        private static readonly Color Muted = UiColors.Hex("#B5AE92");

        private readonly GameSession _session;
        private readonly Action _changed;
        private readonly VisualElement _scrim;
        private readonly MetalPanel _window;
        private readonly Label _cityName;
        private readonly Label _schedule;
        private readonly Label _rate;
        private readonly Label _minusLabel;
        private readonly Label _plusLabel;
        private readonly Label _income;
        private readonly Label _penalty;
        private readonly Label _note;
        private readonly MetalPanel _allButton;
        private readonly Label _allLabel;

        private int _city = WorldState.NoCity;

        public TaxView(VisualElement frame, GameSession session, Action changed)
        {
            _session = session;
            _changed = changed;

            _scrim = new MetalPanel().WithGradient(UiColors.Clear, UiColors.Clear).WithClick(Close);
            _scrim.AddToClassList("gw-scrim");
            _scrim.style.display = DisplayStyle.None;
            frame.Add(_scrim);

            _window = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _window.AddToClassList("gw-tax");
            _window.style.display = DisplayStyle.None;
            frame.Add(_window);

            VisualElement header = UiBuilder.Passive(_window, "gw-info-header");
            VisualElement title = UiBuilder.Passive(header, "gw-panel-title");
            UiBuilder.Text(title, "VERGİ", "gw-tag", "gw-tag-small");
            _cityName = UiBuilder.Text(title, "", "gw-panel-sub");
            UiBuilder.CloseButton(header, session, Close);

            VisualElement body = UiBuilder.Passive(_window, "gw-tax-body");
            _schedule = UiBuilder.Text(body, "", "gw-info-text", "gw-muted", "gw-tax-schedule");

            VisualElement stepper = UiBuilder.Passive(body, "gw-tax-stepper");
            _minusLabel = AddStepButton(stepper, MinusSign, false);
            _rate = UiBuilder.Text(stepper, "", "gw-tax-rate", "gw-f-oswald-700");
            _plusLabel = AddStepButton(stepper, PlusSign, true);

            _income = AddEffectRow(body, "Günlük gelir", true);
            _penalty = AddEffectRow(body, "Memnuniyet etkisi", false);
            _note = UiBuilder.Text(body, "", "gw-info-text", "gw-tax-note");

            _allButton = UiBuilder.GoldButton().WithClick(OnApplyAll);
            _allButton.AddToClassList("gw-tax-all");
            body.Add(_allButton);
            _allLabel = UiBuilder.Text(_allButton, "", "gw-f-oswald-700");
        }

        private Label AddStepButton(VisualElement parent, string text, bool isUp)
        {
            MetalPanel button = UiBuilder.MetalButton().WithClick(() => OnStep(isUp));
            button.AddToClassList("gw-tax-step");
            parent.Add(button);
            return UiBuilder.Text(button, text, "gw-f-oswald-700");
        }

        private Label AddEffectRow(VisualElement parent, string caption, bool isMoney)
        {
            VisualElement row = UiBuilder.Passive(parent, "gw-tax-row");
            UiBuilder.Text(row, caption, "gw-tax-row-caption", "gw-muted");
            if (isMoney)
            {
                VisualElement icon = _session.Icons.CreateMoneyIcon();
                icon.AddToClassList("gw-market-price-icon");
                row.Add(icon);
            }

            return UiBuilder.Text(row, "", "gw-f-700");
        }

        public void Open(int city)
        {
            _city = city;
            _cityName.text = _session.Map.ProvinceNames[city];
            _scrim.BringToFront();
            _window.BringToFront();
            _scrim.style.display = DisplayStyle.Flex;
            _window.style.display = DisplayStyle.Flex;
            Refresh();
            Debug.Log("GeoWarUi tax open city=" + _session.Map.ProvinceIds[city]);
        }

        public void Close()
        {
            if (_city == WorldState.NoCity)
            {
                return;
            }

            _city = WorldState.NoCity;
            _scrim.style.display = DisplayStyle.None;
            _window.style.display = DisplayStyle.None;
        }

        // Gün bitince ve oran değişince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_city == WorldState.NoCity)
            {
                return;
            }

            WorldState world = _session.World;
            BalanceData balance = _session.Definition.Balance;
            int percent = world.CityNextTaxPercent[_city];
            int effective = TaxCommands.EffectivePercent(world, _city);

            _schedule.text = "Şu an %" + effective + " · Yeni oranın başlangıcı: 1 " + _session.Ui.Months[0] + " " + TaxCommands.NextChangeYear(world);
            _rate.text = "%" + percent;
            _minusLabel.style.color = percent > 0 ? UiColors.Text : Muted;
            _plusLabel.style.color = percent < balance.TaxMaxPercent ? UiColors.Text : Muted;

            long people = world.Population[_city];
            _income.text = Change(
                NumberFormat.Amount(TaxCommands.DailyIncomeMilli(balance, people, effective)),
                NumberFormat.Amount(TaxCommands.DailyIncomeMilli(balance, people, percent)),
                effective != percent);
            _penalty.text = Change(PenaltyText(balance, effective), PenaltyText(balance, percent), effective != percent);
            _penalty.style.color = percent > 0 ? UiColors.Warn : UiColors.Text;

            int concessionDays = world.CityConcessionDays[_city];
            _note.style.display = concessionDays > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _note.text = "Taviz sürüyor: " + concessionDays + " gün bu şehirden vergi alınmaz.";

            bool isApplied = TaxCommands.AllCitiesAt(world, _session.Player, percent);
            UiBuilder.ApplyGold(_allButton, !isApplied);
            _allLabel.style.color = isApplied ? Muted : UiColors.DarkText;
            _allLabel.text = (isApplied ? "TÜM ŞEHİRLER %" : "TÜM ŞEHİRLERE UYGULA · %") + percent;
        }

        private static string Change(string now, string next, bool isChanging)
        {
            return isChanging ? now + ChangeArrow + next : now;
        }

        private static string PenaltyText(BalanceData balance, int percent)
        {
            int penalty = TaxCommands.SatisfactionPenaltyPermille(balance, percent) / PercentDivisor;
            return penalty > 0 ? MinusSign + "%" + penalty : "%0";
        }

        private void OnStep(bool isUp)
        {
            int current = _session.World.CityNextTaxPercent[_city];
            int next = TaxCommands.Stepped(_session.Definition.Balance, current, isUp);
            if (next != current && _session.SetCityTax(_city, next))
            {
                Debug.Log("GeoWarUi tax city=" + _session.Map.ProvinceIds[_city] + " percent=" + next);
                Refresh();
                _changed();
            }
        }

        private void OnApplyAll()
        {
            int percent = _session.World.CityNextTaxPercent[_city];
            int changed = _session.SetAllTaxes(percent);
            if (changed > 0)
            {
                Debug.Log("GeoWarUi tax all percent=" + percent + " cities=" + changed);
                Refresh();
                _changed();
            }
        }
    }
}
