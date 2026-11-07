using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Pazar ekranı: tüm kaynakların fiyatı ve pazardaki miktarı. Satıra dokununca teklif penceresi açılır.
    public sealed class MarketView
    {
        private const int ColumnChange = 0;
        private const int ColumnOffered = 1;
        private const int ColumnPrice = 2;
        private const int FullPercent = 100;

        // GeoWar.uss içindeki .gw-table-warning geçiş süresiyle aynıdır.
        private const long WarningPulseMs = 600;

        private static readonly Color Muted = UiColors.Hex("#B5AE92");

        private readonly GameSession _session;
        private readonly ResourceDetailView _detail;
        private readonly Action _closed;
        private readonly Action _changed;
        private readonly ScreenFrame _screen;
        private readonly MarketOfferView _offers;
        private readonly List<ResourceRow> _rows = new List<ResourceRow>();
        private readonly IVisualElementScheduledItem _warningPulse;

        private bool _warningDim;

        public bool IsOpen { get; private set; }

        public MarketView(VisualElement frame, GameSession session, ResourceDetailView detail, Action closed, Action changed)
        {
            _session = session;
            _detail = detail;
            _closed = closed;
            _changed = changed;
            _screen = new ScreenFrame(frame, session, "PAZAR", Close);
            _offers = new MarketOfferView(frame, session, detail, OnTraded);

            ResourceRow.AddHeader(_screen.Root, "Kaynak", "Değişim", "Pazarda", "Fiyat");

            ScrollView scroll = UiBuilder.VerticalScroll(_screen.Root, "gw-builder-scroll");
            foreach (ResourceGroupData group in session.Ui.ResourceGroups)
            {
                UiBuilder.Text(scroll.contentContainer, group.Name, "gw-resource-group", "gw-f-oswald-600");
                foreach (string key in group.Keys)
                {
                    var row = new ResourceRow(scroll.contentContainer, session, _offers.Open, ColumnPrice);
                    row.SetResource(session.Definition.ResourceIndex(key));
                    _rows.Add(row);
                }
            }

            // Uyarılar hep birlikte atar; ekran kapalıyken saat durur.
            _warningPulse = _screen.Root.schedule.Execute(PulseWarnings).Every(WarningPulseMs);
            _warningPulse.Pause();
        }

        public void Open()
        {
            IsOpen = true;
            _screen.SetVisible(true);
            _warningPulse.Resume();
            Refresh();
            Debug.Log("GeoWarUi market open");
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _screen.SetVisible(false);
            _warningPulse.Pause();
            _offers.Close();
            _detail.Close();
            _closed();
        }

        // Gün bitince ve her işlemden sonra çağrılır; her karede değil.
        public void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            int ownOffers = 0;
            foreach (ResourceRow row in _rows)
            {
                FillRow(row);
                if (_session.PlayerOfferMilli(row.Resource) > 0)
                {
                    ownOffers++;
                }
            }

            int shipments = Shipments.CountFor(_session.World, _session.Player);
            _screen.Subtitle.text = "Hazine " + NumberFormat.Compact(_session.PlayerMoney()) + " · " + ownOffers + " teklif · " + shipments + " sevkiyat yolda";
            _offers.Refresh();
        }

        private void OnTraded()
        {
            Refresh();
            _changed();
        }

        private void PulseWarnings()
        {
            _warningDim = !_warningDim;
            foreach (ResourceRow row in _rows)
            {
                row.SetWarningDim(_warningDim);
            }
        }

        private void FillRow(ResourceRow row)
        {
            WorldState world = _session.World;
            int resource = row.Resource;
            long price = world.PriceMilli[resource];
            long basePrice = _session.Definition.BasePriceMilli[resource];
            long percent = basePrice > 0 ? (price - basePrice) * FullPercent / basePrice : 0;

            row.SetValue(ColumnPrice, NumberFormat.Amount(price), UiColors.Text);

            // Pahalıysa turuncu, ucuzsa yeşil: satın alan gözüyle.
            if (percent == 0)
            {
                row.SetValue(ColumnChange, "%0", Muted);
            }
            else if (percent > 0)
            {
                row.SetValue(ColumnChange, "+%" + percent, UiColors.Warn);
            }
            else
            {
                row.SetValue(ColumnChange, "−%" + -percent, UiColors.Good);
            }

            long offered = MarketCommands.OthersOfferMilli(world, _session.Player, resource);
            row.SetValue(ColumnOffered, NumberFormat.Amount(offered), offered > 0 ? UiColors.Text : UiColors.Warn);

            row.SetWarning(StockReport.RunsOutWithin(world, _session.Player, resource, _session.Ui.MarketWarningDays));
        }
    }
}
