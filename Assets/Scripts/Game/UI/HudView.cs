using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Sabit ekran parçaları: üst bar, tarih ve hız kutusu, uyarı şeridi, renk açıklaması, yakınlaştırma ve alt şerit.
    public sealed class HudView
    {
        private const float StrokeThin = 1.8f;
        private const float StrokeMedium = 2f;
        private const float StrokeBold = 2.4f;
        private const float StrokeFit = 2.2f;
        private const float HeaderShadow = 8f;
        private const float StockHitOutsetY = 8f;
        private const float SpeedHitOutsetX = 1f;
        private const float SpeedHitOutsetY = 9f;
        private const float AlertHitOutsetY = 8f;
        private const float ZoomHitOutsetX = 6f;
        private const float ZoomHitOutsetY = 2f;
        private const float StockGap = 2f;
        private const int StockRows = 2;
        private const float CellGap = 3f;
        private const float DockGap = 4f;
        private const float OpenCellBorder = 2f;
        private const int LowSatisfactionPercent = 50;

        public const int NoDock = -1;
        public const int DockStock = 0;
        public const int DockMarket = 1;

        private static readonly string[] SpeedLabels = { "II", "1x", "2x", "3x" };

        private readonly GameSession _session;
        private readonly Action _jumpToAlarm;
        private readonly Action _jumpToReady;
        private readonly Action _openRebellions;
        private readonly Action<int> _openResource;
        private readonly Action _openStock;
        private readonly Action _openMarket;
        private readonly List<MetalPanel> _dockButtons = new List<MetalPanel>();
        private readonly List<Label> _dockLabels = new List<Label>();
        private readonly List<IconElement> _dockIcons = new List<IconElement>();
        private readonly List<MetalPanel> _barCells = new List<MetalPanel>();
        private readonly List<Label> _barValues = new List<Label>();
        private readonly MetalPanel[] _speedButtons = new MetalPanel[SpeedLabels.Length];
        private readonly Label[] _speedLabels = new Label[SpeedLabels.Length];

        private Label _population;
        private Label _satisfaction;
        private VisualElement _satisfactionIcon;
        private Label _money;
        private Label _income;
        private Label _energy;
        private Label _date;
        private VisualElement _dayFill;
        private float _dayProgress = -1f;
        private Label _alarmCount;
        private Label _readyCount;
        private Label _offerCount;
        private Label _rebellionCount;
        private VisualElement _hint;

        public HudView(
            VisualElement frame,
            GameSession session,
            Action zoomIn,
            Action zoomOut,
            Action fitCountry,
            Action jumpToAlarm,
            Action jumpToReady,
            Action openRebellions,
            Action<int> openResource,
            Action openStock,
            Action openMarket)
        {
            _session = session;
            _openResource = openResource;
            _openStock = openStock;
            _openMarket = openMarket;
            _jumpToAlarm = jumpToAlarm;
            _jumpToReady = jumpToReady;
            _openRebellions = openRebellions;
            BuildHint(frame);
            BuildLegend(frame);
            BuildAlerts(frame);
            BuildZoom(frame, zoomIn, zoomOut, fitCountry);
            BuildDock(frame);
            BuildSpeedBox(frame);
            BuildHeader(frame);
            Refresh();
            RefreshSpeed();
        }

        // Her karede çağrılır; yerleşimi bozmadan sadece çubuğu ölçekler, oyun duruyorsa hiçbir şey yapmaz.
        public void SetDayProgress(float progress)
        {
            if (progress != _dayProgress)
            {
                _dayProgress = progress;
                _dayFill.style.scale = new Scale(new Vector2(progress, 1f));
            }
        }

        public void SetHintVisible(bool visible)
        {
            _hint.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            _population.text = NumberFormat.Compact(_session.World.CountryPopulation[_session.Player]);

            int satisfaction = _session.PlayerSatisfactionPercent();
            _satisfaction.text = satisfaction.ToString();
            bool isLowSatisfaction = satisfaction < LowSatisfactionPercent;
            _satisfaction.style.color = isLowSatisfaction ? UiColors.Warn : UiColors.Good;
            _session.Icons.ShowSatisfaction(_satisfactionIcon, isLowSatisfaction);

            _money.text = NumberFormat.Compact(_session.PlayerMoney());
            long income = _session.MoneyChangePerDay / EconomyDefinition.MilliPerUnit;
            _income.text = NumberFormat.Signed(income);
            _income.style.color = income < 0 ? UiColors.Warn : UiColors.Good;

            _energy.text = "%" + _session.PlayerEnergyPercent();
            _date.text = _session.DateText();

            RefreshBar();

            _alarmCount.text = _session.AlarmProvinceCount.ToString();
            _readyCount.text = _session.ReadyProvinceCount.ToString();
            _offerCount.text = _session.OfferCount.ToString();

            // İsyan düğmesi yalnızca isyan varken görünür.
            int rebellions = _session.RebellionCities.Count;
            _rebellionCount.text = rebellions.ToString();
            _rebellionCount.parent.style.display = rebellions > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildHeader(VisualElement frame)
        {
            MetalPanel header = new MetalPanel()
                .WithGradient(UiColors.HeaderTop, UiColors.HeaderBottom)
                .WithInsets(UiColors.Clear, UiColors.HeaderInset)
                .WithRivets()
                .WithShadow(HeaderShadow);
            header.AddToClassList("gw-header");
            frame.Add(header);

            VisualElement top = UiBuilder.Box(header, "gw-header-top");
            top.pickingMode = PickingMode.Ignore;
            AddFlag(top);

            VisualElement cells = UiBuilder.Box(top, "gw-header-cells");
            cells.pickingMode = PickingMode.Ignore;
            _population = AddIconCell(cells, "Nüfus", 0f, _session.Icons.CreatePopulationIcon());
            _satisfactionIcon = _session.Icons.CreateSatisfactionIcon();
            _satisfaction = AddIconCell(cells, "Memnuniyet", CellGap, _satisfactionIcon);
            AddMoneyCell(cells);
            _energy = AddCell(cells, "Enerji", CellGap);
            _energy.parent.AddToClassList("gw-cell-narrow");

            AddStockCells(header);
        }

        private void AddFlag(VisualElement parent)
        {
            foreach (FlagData flag in _session.Ui.Flags)
            {
                if (flag.Key == _session.Ui.PlayerCountry)
                {
                    var element = new FlagElement(flag);
                    element.AddToClassList("gw-flag");
                    parent.Add(element);
                    return;
                }
            }
        }

        private static Label AddCell(VisualElement parent, string caption, float marginLeft)
        {
            VisualElement cell = UiBuilder.Box(parent, "gw-cell");
            cell.pickingMode = PickingMode.Ignore;
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-cell-caption");
            return UiBuilder.Text(cell, "", "gw-cell-value", "gw-f-700");
        }

        private static Label AddIconCell(VisualElement parent, string caption, float marginLeft, VisualElement icon)
        {
            VisualElement cell = UiBuilder.Box(parent, "gw-cell");
            cell.pickingMode = PickingMode.Ignore;
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-cell-caption");
            return AddIconValue(cell, icon);
        }

        private static Label AddIconValue(VisualElement cell, VisualElement icon)
        {
            VisualElement valueRow = UiBuilder.Passive(cell, "gw-value-row");
            icon.AddToClassList("gw-cell-icon");
            valueRow.Add(icon);
            return UiBuilder.Text(valueRow, "", "gw-cell-value", "gw-f-700");
        }

        private void AddMoneyCell(VisualElement parent)
        {
            VisualElement cell = UiBuilder.Box(parent, "gw-cell", "gw-cell-wide");
            cell.pickingMode = PickingMode.Ignore;
            cell.style.marginLeft = CellGap;

            VisualElement captionRow = UiBuilder.Box(cell, "gw-cell-caption-row");
            captionRow.pickingMode = PickingMode.Ignore;
            UiBuilder.Text(captionRow, "Hazine", "gw-cell-caption");
            _income = UiBuilder.Text(captionRow, "", "gw-cell-income", "gw-f-600");

            _money = AddIconValue(cell, _session.Icons.CreateTreasuryIcon());
            _money.style.color = UiColors.Gold;
        }

        // Kutular satırlara eşit bölünür; dokunma alanı dış kenarlarda taşar, kutu araları ortadan ayrılır.
        private void AddStockCells(VisualElement parent)
        {
            int count = _session.Bar.Items.Length;
            int perRow = (count + StockRows - 1) / StockRows;
            float halfGap = StockGap * 0.5f;
            VisualElement row = null;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                int rowIndex = i / perRow;
                bool startsRow = i % perRow == 0;
                if (startsRow)
                {
                    row = UiBuilder.Passive(parent, "gw-stock-row");
                }

                MetalPanel cell = new MetalPanel()
                    .WithGradient(UiColors.Well, UiColors.Well)
                    .WithHitOutset(
                        halfGap,
                        rowIndex == 0 ? StockHitOutsetY : halfGap,
                        rowIndex == StockRows - 1 ? StockHitOutsetY : halfGap)
                    .WithClick(() => _openResource(index));
                cell.AddToClassList("gw-stock");
                cell.style.marginLeft = startsRow ? 0f : StockGap;
                row.Add(cell);
                _barCells.Add(cell);
                _barValues.Add(null);
            }

            RebuildBar();
        }

        // Raptiyeyle kutunun kaynağı değişince kutular yeniden çizilir.
        public void RebuildBar()
        {
            for (int i = 0; i < _barCells.Count; i++)
            {
                MetalPanel cell = _barCells[i];
                cell.Clear();

                ResourceViewData view = _session.ResourceViews[_session.Bar.Items[i]];
                VisualElement icon = _session.Icons.CreateResourceIcon(view, StrokeMedium);
                icon.AddToClassList("gw-stock-icon");
                cell.Add(icon);

                _barValues[i] = UiBuilder.Text(cell, "", "gw-stock-value", "gw-f-600");
            }

            RefreshBar();
        }

        // Açık kaynak penceresinin kutusu altın çerçeveyle işaretlenir.
        public void SetOpenCell(int cellIndex)
        {
            for (int i = 0; i < _barCells.Count; i++)
            {
                if (i == cellIndex)
                {
                    UiBuilder.SetBorderWidth(_barCells[i], OpenCellBorder);
                    UiBuilder.SetBorder(_barCells[i], UiColors.SelectedBorder);
                }
                else
                {
                    ClearCellBorder(_barCells[i]);
                }
            }
        }

        private static void ClearCellBorder(VisualElement cell)
        {
            cell.style.borderTopWidth = StyleKeyword.Null;
            cell.style.borderBottomWidth = StyleKeyword.Null;
            cell.style.borderLeftWidth = StyleKeyword.Null;
            cell.style.borderRightWidth = StyleKeyword.Null;
            cell.style.borderTopColor = StyleKeyword.Null;
            cell.style.borderBottomColor = StyleKeyword.Null;
            cell.style.borderLeftColor = StyleKeyword.Null;
            cell.style.borderRightColor = StyleKeyword.Null;
        }

        private void RefreshBar()
        {
            for (int i = 0; i < _barValues.Count; i++)
            {
                int resource = _session.Bar.Items[i];
                long stock = _session.PlayerStock(resource);
                _barValues[i].text = NumberFormat.Compact(stock);
                _barValues[i].style.color = stock < _session.ResourceViews[resource].LowWarning ? UiColors.Warn : UiColors.Text;
            }
        }

        private void BuildSpeedBox(VisualElement frame)
        {
            MetalPanel box = new MetalPanel()
                .WithGradient(UiColors.HeaderTop, UiColors.HeaderBottom)
                .WithRivets()
                .WithShadow(HeaderShadow);
            box.AddToClassList("gw-speed-box");
            frame.Add(box);

            VisualElement dateColumn = UiBuilder.Passive(box, "gw-date-column");
            _date = UiBuilder.Text(dateColumn, "", "gw-date", "gw-f-courier-700");
            VisualElement dayBar = UiBuilder.Passive(dateColumn, "gw-day-bar");
            _dayFill = UiBuilder.Passive(dayBar, "gw-day-fill");
            _dayFill.usageHints = UsageHints.DynamicTransform;
            SetDayProgress(_session.DayProgress);

            for (int i = 0; i < SpeedLabels.Length; i++)
            {
                int level = i;
                MetalPanel button = new MetalPanel()
                    .WithHitOutset(SpeedHitOutsetX, SpeedHitOutsetY)
                    .WithClick(() => SelectSpeed(level));
                button.AddToClassList("gw-speed");
                box.Add(button);
                _speedButtons[i] = button;
                _speedLabels[i] = UiBuilder.Text(button, SpeedLabels[i], "gw-speed-label", "gw-f-oswald-700");
            }
        }

        private void SelectSpeed(int level)
        {
            _session.SetSpeed(level);
            RefreshSpeed();
            Debug.Log("GeoWarUi speed=" + level);
        }

        // Oyun başka yerden durdurulunca da hız düğmeleri güncellenir.
        public void RefreshSpeed()
        {
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = _session.Speed == i;
                _speedButtons[i]
                    .WithGradient(active ? UiColors.ActiveTop : UiColors.ButtonTop, active ? UiColors.ActiveBottom : UiColors.ButtonBottom)
                    .WithInsets(active ? UiColors.ActiveInset : UiColors.ButtonInset, UiColors.Clear);
                _speedLabels[i].style.color = active ? UiColors.DarkText : UiColors.Text;
            }
        }

        private void BuildAlerts(VisualElement frame)
        {
            VisualElement alerts = UiBuilder.Box(frame, "gw-alerts");
            alerts.pickingMode = PickingMode.Ignore;
            _alarmCount = AddAlert(alerts, "warning", UiColors.Alarm, _jumpToAlarm);
            _readyCount = AddAlert(alerts, "upgrade", UiColors.Gold, _jumpToReady);
            _offerCount = AddAlert(alerts, "offices", UiColors.Text, UiBuilder.NoAction);
            _rebellionCount = AddAlert(alerts, "rebel", UiColors.Warn, _openRebellions);
        }

        private Label AddAlert(VisualElement parent, string iconKey, Color tint, Action action)
        {
            MetalPanel button = UiBuilder.MetalButton().WithHitOutset(0f, AlertHitOutsetY).WithClick(action);
            button.AddToClassList("gw-alert");
            parent.Add(button);

            var icon = new IconElement(_session.Icons.Get(iconKey), tint, 0f, true);
            icon.AddToClassList("gw-alert-icon");
            button.Add(icon);
            return UiBuilder.Text(button, "", "gw-alert-count", "gw-f-700");
        }

        private static void BuildLegend(VisualElement frame)
        {
            VisualElement legend = UiBuilder.Box(frame, "gw-legend");
            legend.pickingMode = PickingMode.Ignore;
            AddLegendRow(legend, UiColors.LegendOwn, "Sen", true);
            AddLegendRow(legend, UiColors.LegendAlly, "Müttefik", true);
            AddLegendRow(legend, UiColors.LegendPeace, "Tarafsız", true);
            AddLegendRow(legend, UiColors.LegendWar, "Düşman", false);
        }

        private static void AddLegendRow(VisualElement parent, Color color, string text, bool hasGap)
        {
            VisualElement row = UiBuilder.Box(parent, "gw-legend-row");
            row.pickingMode = PickingMode.Ignore;
            if (!hasGap)
            {
                row.style.marginBottom = 0f;
            }

            VisualElement swatch = UiBuilder.Box(row, "gw-legend-swatch");
            swatch.pickingMode = PickingMode.Ignore;
            swatch.style.backgroundColor = color;
            UiBuilder.Text(row, text, "gw-f-600");
        }

        private void BuildZoom(VisualElement frame, Action zoomIn, Action zoomOut, Action fitCountry)
        {
            VisualElement zoom = UiBuilder.Box(frame, "gw-zoom");
            zoom.pickingMode = PickingMode.Ignore;
            AddZoomButton(zoom, "plus", StrokeBold, zoomIn);
            AddZoomButton(zoom, "minus", StrokeBold, zoomOut);
            AddZoomButton(zoom, "fit", StrokeFit, fitCountry);
        }

        private void AddZoomButton(VisualElement parent, string iconKey, float stroke, Action action)
        {
            MetalPanel button = UiBuilder.MetalButton().WithHitOutset(ZoomHitOutsetX, ZoomHitOutsetY).WithClick(action);
            button.AddToClassList("gw-zoom-button");
            parent.Add(button);

            var icon = new IconElement(_session.Icons.Get(iconKey), UiColors.Text, stroke);
            icon.AddToClassList("gw-zoom-icon");
            button.Add(icon);
        }

        private void BuildHint(VisualElement frame)
        {
            _hint = UiBuilder.Box(frame, "gw-hint");
            _hint.pickingMode = PickingMode.Ignore;
            UiBuilder.Text(_hint, "İl seçmek için haritaya dokun");
        }

        private void BuildDock(VisualElement frame)
        {
            MetalPanel dock = new MetalPanel().WithGradient(UiColors.Well, UiColors.Well).WithRivets();
            dock.AddToClassList("gw-dock");
            frame.Add(dock);

            AddDockButton(dock, "box", "Stok", 0f, _openStock);
            AddDockButton(dock, "chart", "Pazar", DockGap, _openMarket);
            AddDockButton(dock, "bolt", "Enerji", DockGap, UiBuilder.NoAction);
            AddDockButton(dock, "building", "Şirketler", DockGap, UiBuilder.NoAction);
            AddDockButton(dock, "flag", "Diplomasi", DockGap, UiBuilder.NoAction);
        }

        private void AddDockButton(VisualElement parent, string iconKey, string text, float marginLeft, Action action)
        {
            MetalPanel button = UiBuilder.MetalButton().WithClick(action);
            button.WithInsets(UiColors.ButtonInset, UiColors.ButtonInsetBottom);
            button.AddToClassList("gw-dock-button");
            button.style.marginLeft = marginLeft;
            parent.Add(button);

            var icon = new IconElement(_session.Icons.Get(iconKey), UiColors.Text, StrokeThin);
            icon.AddToClassList("gw-dock-icon");
            button.Add(icon);

            _dockButtons.Add(button);
            _dockIcons.Add(icon);
            _dockLabels.Add(UiBuilder.Text(button, text, "gw-dock-label", "gw-f-600"));
        }

        // Açık ekranın düğmesi altın görünür.
        public void SetActiveDock(int dock)
        {
            for (int i = 0; i < _dockButtons.Count; i++)
            {
                bool active = i == dock;
                _dockButtons[i]
                    .WithGradient(active ? UiColors.ActiveTop : UiColors.ButtonTop, active ? UiColors.ActiveBottom : UiColors.ButtonBottom)
                    .WithInsets(active ? UiColors.ActiveInset : UiColors.ButtonInset, active ? UiColors.Clear : UiColors.ButtonInsetBottom);
                _dockLabels[i].style.color = active ? UiColors.DarkText : UiColors.Text;
                _dockIcons[i].SetColor(active ? UiColors.DarkText : UiColors.Text);
            }
        }

    }

    public static class UiBuilder
    {
        private const float CloseStroke = 2.4f;

        // Küçük düğmelerin dokunma alanı görünenden geniştir.
        public const float SmallButtonHitOutset = 6f;

        public static readonly Color PanelTop = UiColors.Hex("#2D2C20");
        public static readonly Color PanelBottom = UiColors.Hex("#1C1B13");

        private static readonly Color GoldDisabled = UiColors.Hex("#2C2B20");

        public static void NoAction()
        {
        }

        public static MetalPanel MetalButton()
        {
            return new MetalPanel()
                .WithGradient(UiColors.ButtonTop, UiColors.ButtonBottom)
                .WithInsets(UiColors.ButtonInset, UiColors.Clear);
        }

        public static MetalPanel GoldButton()
        {
            return new MetalPanel()
                .WithGradient(UiColors.ActiveTop, UiColors.ActiveBottom)
                .WithInsets(UiColors.ActiveInset, UiColors.Clear);
        }

        public static void ApplyGold(MetalPanel button, bool enabled)
        {
            if (enabled)
            {
                button.WithGradient(UiColors.ActiveTop, UiColors.ActiveBottom).WithInsets(UiColors.ActiveInset, UiColors.Clear);
            }
            else
            {
                button.WithGradient(GoldDisabled, GoldDisabled).WithInsets(UiColors.Clear, UiColors.Clear);
            }
        }

        public static void SetBorderWidth(VisualElement element, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
        }

        public static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        // Dokunuşu tutmayan, yalnızca düzen kuran kutu.
        public static VisualElement Passive(VisualElement parent, params string[] classes)
        {
            VisualElement element = Box(parent, classes);
            element.pickingMode = PickingMode.Ignore;
            return element;
        }

        public static void CloseButton(VisualElement parent, GameSession session, Action close)
        {
            MetalPanel button = MetalButton().WithHitOutset(SmallButtonHitOutset, SmallButtonHitOutset).WithClick(close);
            button.AddToClassList("gw-icon-button");
            parent.Add(button);
            var icon = new IconElement(session.Icons.Get("close"), UiColors.Text, CloseStroke);
            icon.AddToClassList("gw-close-icon");
            button.Add(icon);
        }

        public static ScrollView HorizontalScroll(VisualElement parent, string className)
        {
            return MakeScroll(parent, ScrollViewMode.Horizontal, className);
        }

        public static ScrollView VerticalScroll(VisualElement parent, string className)
        {
            return MakeScroll(parent, ScrollViewMode.Vertical, className);
        }

        private static ScrollView MakeScroll(VisualElement parent, ScrollViewMode mode, string className)
        {
            var scroll = new ScrollView(mode)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped
            };
            scroll.AddToClassList(className);
            parent.Add(scroll);
            return scroll;
        }

        public static VisualElement Box(VisualElement parent, params string[] classes)
        {
            var element = new VisualElement();
            foreach (string className in classes)
            {
                element.AddToClassList(className);
            }

            parent.Add(element);
            return element;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string className in classes)
            {
                label.AddToClassList(className);
            }

            parent.Add(label);
            return label;
        }
    }
}
