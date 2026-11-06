using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Stok ekranı: ülkenin genel stoğu, günlük değişimi ve stoğun kaç gün yeteceği. Satıra dokununca bilgi penceresi açılır.
    public sealed class StockView
    {
        private const int TabAll = 0;
        private const int TabShrinking = 1;
        private const float TabGap = 3f;
        private const int ColumnStock = 0;
        private const int ColumnNet = 1;
        private const int ColumnDays = 2;
        private const string NoEnd = "—";

        private static readonly Color Muted = UiColors.Hex("#B5AE92");
        private static readonly string[] TabNames = { "Tümü", "Azalan" };

        private readonly GameSession _session;
        private readonly ResourceDetailView _detail;
        private readonly Action _closed;
        private readonly ScreenFrame _screen;
        private readonly MetalPanel[] _tabs = new MetalPanel[TabNames.Length];
        private readonly Label[] _tabLabels = new Label[TabNames.Length];
        private readonly ScrollView _allScroll;
        private readonly ScrollView _shrinkingScroll;
        private readonly Label _noneShrinking;
        private readonly List<ResourceRow> _allRows = new List<ResourceRow>();
        private readonly List<ResourceRow> _shrinkingRows = new List<ResourceRow>();
        private readonly List<int> _shrinking = new List<int>();

        private int _tab = TabAll;

        public bool IsOpen { get; private set; }

        public StockView(VisualElement frame, GameSession session, ResourceDetailView detail, Action closed)
        {
            _session = session;
            _detail = detail;
            _closed = closed;
            _screen = new ScreenFrame(frame, session, "GENEL STOK", Close);

            VisualElement tabs = UiBuilder.Passive(_screen.Root, "gw-builder-tabs");
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                MetalPanel tab = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(() => SelectTab(index));
                tab.AddToClassList("gw-builder-tab");
                tab.style.marginLeft = i == 0 ? 0f : TabGap;
                tabs.Add(tab);
                _tabs[i] = tab;
                _tabLabels[i] = UiBuilder.Text(tab, TabNames[i], "gw-f-700");
            }

            ResourceRow.AddHeader(_screen.Root, "Kaynak", "Stok", "Net/gün", "Yeter");

            _allScroll = UiBuilder.VerticalScroll(_screen.Root, "gw-builder-scroll");
            foreach (ResourceGroupData group in session.Ui.ResourceGroups)
            {
                UiBuilder.Text(_allScroll.contentContainer, group.Name, "gw-resource-group", "gw-f-oswald-600");
                foreach (string key in group.Keys)
                {
                    var row = new ResourceRow(_allScroll.contentContainer, session, OpenDetail);
                    row.SetResource(session.Definition.ResourceIndex(key));
                    _allRows.Add(row);
                }
            }

            _shrinkingScroll = UiBuilder.VerticalScroll(_screen.Root, "gw-builder-scroll");
            _noneShrinking = UiBuilder.Text(_shrinkingScroll.contentContainer, "Azalan kaynak yok.", "gw-info-text", "gw-muted");
        }

        public void Open()
        {
            IsOpen = true;
            _screen.SetVisible(true);
            Refresh();
            Debug.Log("GeoWarUi stock open");
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _screen.SetVisible(false);
            _detail.Close();
            _closed();
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            WorldState world = _session.World;
            StockReport.CollectShrinking(world, _session.Player, _shrinking);
            long value = StockReport.TotalValueMilli(world, _session.Player) / EconomyDefinition.MilliPerUnit;
            _screen.Subtitle.text = "Stok değeri " + NumberFormat.Compact(value) + " · " + _shrinking.Count + " kaynak azalıyor";

            for (int i = 0; i < _tabs.Length; i++)
            {
                ScreenFrame.StyleChoice(_tabs[i], _tabLabels[i], i == _tab);
            }

            _allScroll.style.display = _tab == TabAll ? DisplayStyle.Flex : DisplayStyle.None;
            _shrinkingScroll.style.display = _tab == TabShrinking ? DisplayStyle.Flex : DisplayStyle.None;

            if (_tab == TabAll)
            {
                foreach (ResourceRow row in _allRows)
                {
                    FillRow(row);
                }
            }
            else
            {
                RefreshShrinking();
            }
        }

        private void SelectTab(int tab)
        {
            _tab = tab;
            Refresh();
        }

        private void OpenDetail(int resource)
        {
            _detail.Open(resource);
        }

        private void RefreshShrinking()
        {
            _noneShrinking.style.display = _shrinking.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            while (_shrinkingRows.Count < _shrinking.Count)
            {
                _shrinkingRows.Add(new ResourceRow(_shrinkingScroll.contentContainer, _session, OpenDetail));
            }

            for (int i = 0; i < _shrinkingRows.Count; i++)
            {
                bool used = i < _shrinking.Count;
                _shrinkingRows[i].SetVisible(used);
                if (used)
                {
                    _shrinkingRows[i].SetResource(_shrinking[i]);
                    FillRow(_shrinkingRows[i]);
                }
            }
        }

        private void FillRow(ResourceRow row)
        {
            WorldState world = _session.World;
            int resource = row.Resource;
            long stock = _session.PlayerStockMilli(resource);
            long net = StockReport.NetMilli(world, _session.Player, resource);

            row.SetValue(ColumnStock, NumberFormat.Amount(stock), stock == 0 ? UiColors.Warn : UiColors.Text);
            row.SetValue(ColumnNet, NumberFormat.SignedAmount(net), net == 0 ? Muted : ResourceStats.NetColor(net));

            if (stock == 0)
            {
                row.SetValue(ColumnDays, "Yok", UiColors.Warn);
                return;
            }

            long days = StockReport.DaysLeft(world, _session.Player, resource);
            if (days == StockReport.NeverEnds)
            {
                row.SetValue(ColumnDays, NoEnd, Muted);
                return;
            }

            bool soon = days < _session.Definition.Balance.MarketTargetDays;
            row.SetValue(ColumnDays, days == 0 ? "<1 gün" : NumberFormat.Compact(days) + " gün", soon ? UiColors.Warn : UiColors.Text);
        }
    }
}
