using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Üst bardaki bir kaynağa dokununca açılan pencere: seçili kaynağın stok akışı ve tüm kaynakların listesi.
    // Karta dokunmak bilgi penceresini açar; raptiye kaynağı açılan kutuya taşır.
    public sealed class ResourceWindowView
    {
        private const int CardsPerRow = 3;
        private const float CardGap = 3f;
        private const float StrokeTitle = 2f;
        private const float StrokeCard = 2f;

        private static readonly Color CardFill = UiColors.Hex("#14130C");
        private static readonly Color CardFillSelected = UiColors.Hex("#3D3B2A");
        private static readonly Color CardBorder = UiColors.Hex("#3A392A");
        private static readonly Color CardBorderSelected = UiColors.Hex("#CFAB52");
        private static readonly Color PinIdle = UiColors.Hex("#B5AE92");

        private sealed class Card
        {
            public int Resource;
            public VisualElement Root;
            public Label Note;
            public Label Rate;
            public IconElement Pin;
        }

        private readonly GameSession _session;
        private readonly Action _closed;
        private readonly Action _barChanged;

        private readonly VisualElement _scrim;
        private readonly MetalPanel _window;
        private readonly VisualElement _titleIconHolder;
        private readonly Label _title;
        private readonly ResourceStats _stats;
        private readonly Label _outlook;
        private readonly ResourceDetailView _detail;
        private readonly List<Card> _cards = new List<Card>();

        private int _cell = -1;
        private int _focus = -1;

        public ResourceWindowView(VisualElement frame, GameSession session, ResourceDetailView detail, Action closed, Action barChanged)
        {
            _session = session;
            _detail = detail;
            _closed = closed;
            _barChanged = barChanged;

            _scrim = new MetalPanel().WithGradient(UiColors.Clear, UiColors.Clear).WithClick(Close);
            _scrim.AddToClassList("gw-scrim");
            _scrim.style.display = DisplayStyle.None;
            frame.Add(_scrim);

            _window = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _window.AddToClassList("gw-resource-window");
            _window.style.display = DisplayStyle.None;
            frame.Add(_window);

            ScrollView scroll = UiBuilder.VerticalScroll(_window, "gw-resource-scroll");
            VisualElement content = scroll.contentContainer;

            VisualElement header = UiBuilder.Passive(content, "gw-resource-header");
            VisualElement title = UiBuilder.Passive(header, "gw-resource-title");
            _titleIconHolder = UiBuilder.Passive(title, "gw-resource-title-icon");
            _title = UiBuilder.Text(title, "", "gw-tag", "gw-tag-small");
            UiBuilder.CloseButton(header, session, Close);

            _stats = new ResourceStats(content, session);

            _outlook = UiBuilder.Text(content, "", "gw-resource-outlook", "gw-f-courier-700");

            UiBuilder.Text(content, "Tüm kaynaklar", "gw-resource-all", "gw-f-oswald-700");
            UiBuilder.Text(content, "Karta dokununca detayı açılır. Raptiye, kaynağı açtığın kutuya taşır.", "gw-resource-hint");

            foreach (ResourceGroupData group in session.Ui.ResourceGroups)
            {
                UiBuilder.Text(content, group.Name, "gw-resource-group", "gw-f-oswald-600");
                AddGroup(content, group);
            }
        }

        public bool IsOpen
        {
            get { return _cell >= 0; }
        }

        public int OpenCell
        {
            get { return _cell; }
        }

        public void Open(int cell)
        {
            _cell = cell;
            _focus = _session.Bar.Items[cell];
            _detail.Close();
            _scrim.style.display = DisplayStyle.Flex;
            _window.style.display = DisplayStyle.Flex;
            Refresh();
            Debug.Log("GeoWarUi resource open=" + _session.Definition.ResourceKeys[_focus]);
        }

        public void Close()
        {
            if (_cell < 0)
            {
                return;
            }

            _cell = -1;
            _detail.Close();
            _scrim.style.display = DisplayStyle.None;
            _window.style.display = DisplayStyle.None;
            _closed();
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_cell < 0)
            {
                return;
            }

            RefreshFocus();
            RefreshCards();
        }

        // Karta dokununca: kaynak seçilir ve bilgi penceresi açılır.
        public void ShowDetail(int resource)
        {
            _focus = resource;
            RefreshFocus();
            RefreshCards();
            _detail.Open(resource);
        }

        private void AddGroup(VisualElement parent, ResourceGroupData group)
        {
            VisualElement row = null;
            for (int i = 0; i < group.Keys.Count; i++)
            {
                if (i % CardsPerRow == 0)
                {
                    row = UiBuilder.Passive(parent, "gw-resource-row");
                }

                AddCard(row, _session.Definition.ResourceIndex(group.Keys[i]), i % CardsPerRow == 0 ? 0f : CardGap);
            }

            int remainder = group.Keys.Count % CardsPerRow;
            if (remainder != 0)
            {
                for (int i = remainder; i < CardsPerRow; i++)
                {
                    VisualElement spacer = UiBuilder.Passive(row, "gw-resource-card-spacer");
                    spacer.style.marginLeft = CardGap;
                }
            }
        }

        private void AddCard(VisualElement row, int resource, float marginLeft)
        {
            ResourceViewData view = _session.ResourceViews[resource];

            VisualElement root = UiBuilder.Passive(row, "gw-resource-card");
            root.style.marginLeft = marginLeft;
            UiBuilder.SetBorderWidth(root, 1f);

            MetalPanel pick = new MetalPanel().WithGradient(UiColors.Clear, UiColors.Clear).WithClick(() => ShowDetail(resource));
            pick.AddToClassList("gw-resource-pick");
            root.Add(pick);

            VisualElement icon = _session.Icons.CreateResourceIcon(view, StrokeCard);
            icon.AddToClassList("gw-resource-card-icon");
            pick.Add(icon);

            VisualElement text = UiBuilder.Passive(pick, "gw-resource-card-text");
            UiBuilder.Text(text, view.Name, "gw-resource-card-name", "gw-f-600");
            VisualElement values = UiBuilder.Passive(text, "gw-resource-card-values");
            Label note = UiBuilder.Text(values, "", "gw-resource-card-note");
            Label rate = UiBuilder.Text(values, "", "gw-resource-card-rate", "gw-f-600");

            MetalPanel pin = new MetalPanel().WithGradient(UiColors.Clear, UiColors.Clear).WithClick(() => PinResource(resource));
            pin.AddToClassList("gw-resource-pin");
            root.Add(pin);

            var pinIcon = new IconElement(_session.Icons.Get("pin"), PinIdle, StrokeCard);
            pinIcon.AddToClassList("gw-resource-pin-icon");
            pin.Add(pinIcon);

            _cards.Add(new Card { Resource = resource, Root = root, Note = note, Rate = rate, Pin = pinIcon });
        }

        private void PinResource(int resource)
        {
            _session.Bar.Place(_cell, resource);
            _focus = resource;
            Debug.Log("GeoWarUi resource pin=" + _session.Definition.ResourceKeys[resource] + " cell=" + _cell);
            _barChanged();
            RefreshFocus();
            RefreshCards();
        }

        private void RefreshFocus()
        {
            ResourceViewData view = _session.ResourceViews[_focus];
            _title.text = NumberFormat.Upper(view.Name);

            _titleIconHolder.Clear();
            VisualElement icon = _session.Icons.CreateResourceIcon(view, StrokeTitle);
            icon.style.width = Length.Percent(100f);
            icon.style.height = Length.Percent(100f);
            _titleIconHolder.Add(icon);

            _stats.Refresh(_focus);

            long net = _session.PlayerProducedMilli(_focus) - _session.PlayerConsumedMilli(_focus);
            _outlook.style.color = ResourceStats.NetColor(net);
            _outlook.text = Outlook(_session.PlayerStockMilli(_focus), net);
        }

        private static string Outlook(long stock, long net)
        {
            if (net > 0)
            {
                return "Stok günde " + NumberFormat.Amount(net) + " artıyor.";
            }

            return net < 0
                ? "Bu hızla stok " + NumberFormat.Compact(stock / -net) + " günde biter."
                : "Stok sabit kalıyor.";
        }

        // Günde ne kadar arttığı veya azaldığı: fazlaysa yeşil, açıksa turuncu, değişmiyorsa yazılmaz.
        private void RefreshRate(Card card)
        {
            long net = _session.PlayerProducedMilli(card.Resource) - _session.PlayerConsumedMilli(card.Resource);
            card.Rate.style.display = net == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (net != 0)
            {
                card.Rate.text = NumberFormat.SignedAmount(net);
                card.Rate.style.color = net > 0 ? UiColors.Good : UiColors.Warn;
            }
        }

        private void RefreshCards()
        {
            foreach (Card card in _cards)
            {
                bool selected = card.Resource == _focus;
                bool pinned = _session.Bar.Contains(card.Resource);

                card.Note.text = NumberFormat.Amount(_session.PlayerStockMilli(card.Resource));
                RefreshRate(card);
                card.Root.style.backgroundColor = selected ? CardFillSelected : CardFill;
                UiBuilder.SetBorder(card.Root, selected ? CardBorderSelected : CardBorder);
                UiBuilder.SetBorderWidth(card.Root, selected ? 2f : 1f);
                card.Pin.SetColor(pinned ? UiColors.Gold : PinIdle);
                card.Pin.SetFilled(pinned);
            }
        }
    }
}
