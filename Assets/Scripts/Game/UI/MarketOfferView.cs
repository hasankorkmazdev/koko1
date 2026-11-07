using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Pazarda bir kaynağa dokununca açılan pencere: ülkelerin teklifleri ve alım satım.
    // Oyuncu teklifi kendi seçer, en fazla o teklifin miktarı kadar alır. Sattığı mal teklif olur, para alıcı çıkınca gelir.
    // Satın alınan mal yola çıkar: pencere ulaşım türünü, süreyi ve yoldaki malları gösterir.
    public sealed class MarketOfferView
    {
        private const float StrokeTitle = 2f;
        private const float AmountGap = 3f;
        private const float ActionGap = 6f;
        private const int NoResource = -1;
        private const int NoSeller = -1;
        private const int PermilleToMilliPercent = 100;
        private const string NoValue = "—";

        private static readonly Color Muted = UiColors.Hex("#B5AE92");
        private static readonly Color RowFill = UiColors.Hex("#14130C");
        private static readonly Color RowFillSelected = UiColors.Hex("#3D3B2A");
        private static readonly Color RowBorder = UiColors.Hex("#3A392A");
        private static readonly Color RowBorderSelected = UiColors.Hex("#CFAB52");

        private sealed class OfferRow
        {
            public int Seller;
            public MetalPanel Root;
            public Label Name;
            public Label Distance;
            public Label Amount;
            public Label Fee;
            public Label Cost;
        }

        private sealed class TransitRow
        {
            public VisualElement Root;
            public Label Name;
            public Label Note;
            public Label Amount;
        }

        private readonly GameSession _session;
        private readonly ResourceDetailView _detail;
        private readonly Action _traded;
        private readonly VisualElement _scrim;
        private readonly MetalPanel _window;
        private readonly ScrollView _scroll;
        private readonly VisualElement _titleIconHolder;
        private readonly Label _title;
        private readonly Label _marketPrice;
        private readonly Label _basePrice;
        private readonly Label _stock;
        private readonly Label _offersTitle;
        private readonly VisualElement _offerRows;
        private readonly Label _noOffers;
        private readonly VisualElement _ownBox;
        private readonly Label _ownText;
        private readonly MetalPanel[] _amountButtons;
        private readonly Label[] _amountLabels;
        private readonly MetalPanel _buyButton;
        private readonly Label _buyLabel;
        private readonly Label _sellLabel;
        private readonly Label _transitTitle;
        private readonly VisualElement _transitRows;
        private readonly List<OfferRow> _rows = new List<OfferRow>();
        private readonly List<TransitRow> _transit = new List<TransitRow>();
        private readonly List<int> _sellers = new List<int>();
        private readonly List<Shipment> _shipments = new List<Shipment>();

        private int _resource = NoResource;
        private int _seller = NoSeller;
        private int _amountIndex;

        public MarketOfferView(VisualElement frame, GameSession session, ResourceDetailView detail, Action traded)
        {
            _session = session;
            _detail = detail;
            _traded = traded;

            _scrim = new MetalPanel().WithGradient(UiColors.Clear, UiColors.Clear).WithClick(Close);
            _scrim.AddToClassList("gw-scrim");
            _scrim.style.display = DisplayStyle.None;
            frame.Add(_scrim);

            _window = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiBuilder.PanelBottom).WithRivets();
            _window.AddToClassList("gw-info");
            _window.style.display = DisplayStyle.None;
            frame.Add(_window);

            VisualElement header = UiBuilder.Passive(_window, "gw-info-header");
            VisualElement title = UiBuilder.Passive(header, "gw-resource-title");
            _titleIconHolder = UiBuilder.Passive(title, "gw-resource-title-icon");
            _title = UiBuilder.Text(title, "", "gw-tag", "gw-tag-small");
            UiBuilder.CloseButton(header, session, Close);

            _scroll = UiBuilder.VerticalScroll(_window, "gw-resource-scroll");
            VisualElement content = _scroll.contentContainer;

            VisualElement prices = UiBuilder.Passive(content, "gw-market-prices");
            _marketPrice = AddPrice(prices, "Piyasa");
            _basePrice = AddPrice(prices, "· Taban");
            _stock = UiBuilder.Text(content, "", "gw-info-text");

            _offersTitle = UiBuilder.Text(content, "", "gw-resource-group", "gw-f-oswald-600");
            ResourceRow.AddHeader(content, "Ülke", "Miktar", "Yol", "Birim").AddToClassList("gw-table-header-inner");
            _offerRows = UiBuilder.Passive(content, "gw-info-cities");
            _noOffers = UiBuilder.Text(content, "Bu kaynakta satış teklifi yok.", "gw-info-text", "gw-muted");

            _ownBox = UiBuilder.Passive(content, "gw-market-own");
            _ownText = UiBuilder.Text(_ownBox, "", "gw-info-text");
            MetalPanel withdraw = UiBuilder.MetalButton().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OnWithdraw);
            withdraw.AddToClassList("gw-text-button");
            UiBuilder.Text(withdraw, "Geri çek", "gw-f-700");
            _ownBox.Add(withdraw);

            _transitTitle = UiBuilder.Text(content, "", "gw-resource-group", "gw-f-oswald-600");
            _transitRows = UiBuilder.Passive(content, "gw-info-cities");

            MetalPanel footer = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiColors.HeaderBottom);
            footer.AddToClassList("gw-builder-footer");
            _window.Add(footer);

            VisualElement amounts = UiBuilder.Passive(footer, "gw-market-amounts");
            int amountCount = session.Ui.MarketAmounts.Count;
            _amountButtons = new MetalPanel[amountCount];
            _amountLabels = new Label[amountCount];
            for (int i = 0; i < amountCount; i++)
            {
                int index = i;
                MetalPanel button = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(() => PickAmount(index));
                button.AddToClassList("gw-builder-tab");
                button.style.marginLeft = i == 0 ? 0f : AmountGap;
                amounts.Add(button);
                _amountButtons[i] = button;
                _amountLabels[i] = UiBuilder.Text(button, NumberFormat.Compact(session.Ui.MarketAmounts[i]), "gw-f-700");
            }

            VisualElement actions = UiBuilder.Passive(footer, "gw-builder-actions");

            MetalPanel info = UiBuilder.MetalButton().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OpenDetail);
            info.AddToClassList("gw-builder-action");
            info.style.marginRight = ActionGap;
            UiBuilder.Text(info, "Bilgi", "gw-f-700");
            actions.Add(info);

            MetalPanel sell = UiBuilder.MetalButton().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OnSell);
            sell.AddToClassList("gw-builder-action");
            sell.style.marginRight = ActionGap;
            _sellLabel = UiBuilder.Text(sell, "", "gw-f-oswald-700");
            actions.Add(sell);

            _buyButton = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OnBuy);
            _buyButton.AddToClassList("gw-builder-action");
            _buyLabel = UiBuilder.Text(_buyButton, "", "gw-f-oswald-700");
            actions.Add(_buyButton);
        }

        private Label AddPrice(VisualElement parent, string caption)
        {
            UiBuilder.Text(parent, caption, "gw-market-price-text", "gw-f-courier-700");
            VisualElement icon = _session.Icons.CreateMoneyIcon();
            icon.AddToClassList("gw-market-price-icon");
            parent.Add(icon);
            return UiBuilder.Text(parent, "", "gw-market-price-text", "gw-f-courier-700");
        }

        public void Open(int resource)
        {
            _resource = resource;
            _seller = NoSeller;
            _scrim.BringToFront();
            _window.BringToFront();
            _scrim.style.display = DisplayStyle.Flex;
            _window.style.display = DisplayStyle.Flex;
            _scroll.scrollOffset = Vector2.zero;

            ResourceViewData view = _session.ResourceViews[resource];
            _title.text = NumberFormat.Upper(view.Name);
            _titleIconHolder.Clear();
            VisualElement icon = _session.Icons.CreateResourceIcon(view, StrokeTitle);
            icon.style.width = Length.Percent(100f);
            icon.style.height = Length.Percent(100f);
            _titleIconHolder.Add(icon);

            Refresh();
            Debug.Log("GeoWarUi market offers=" + _session.Definition.ResourceKeys[resource]);
        }

        public void Close()
        {
            if (_resource == NoResource)
            {
                return;
            }

            _resource = NoResource;
            _scrim.style.display = DisplayStyle.None;
            _window.style.display = DisplayStyle.None;
        }

        // Gün bitince ve her işlemden sonra çağrılır; her karede değil.
        public void Refresh()
        {
            if (_resource == NoResource)
            {
                return;
            }

            WorldState world = _session.World;
            _marketPrice.text = NumberFormat.Amount(world.PriceMilli[_resource]);
            _basePrice.text = NumberFormat.Amount(_session.Definition.BasePriceMilli[_resource]);

            long net = StockReport.NetMilli(world, _session.Player, _resource);
            _stock.text = "Stoğun " + NumberFormat.Amount(_session.PlayerStockMilli(_resource)) + " · Net/gün " + NumberFormat.SignedAmount(net);

            RefreshOffers();
            RefreshOwnOffer();
            RefreshTransit();
            RefreshActions();
        }

        private long AmountMilli
        {
            get { return _session.Ui.MarketAmounts[_amountIndex] * (long)EconomyDefinition.MilliPerUnit; }
        }

        // Seçilen miktar teklifi aşarsa teklifin tamamı alınır; başka teklifle tamamlanmaz.
        private long BuyAmountMilli
        {
            get { return _seller == NoSeller ? 0 : Math.Min(AmountMilli, MarketCommands.OfferMilli(_session.World, _seller, _resource)); }
        }

        private long SellAmountMilli
        {
            get { return Math.Min(AmountMilli, _session.PlayerStockMilli(_resource)); }
        }

        private void RefreshOffers()
        {
            WorldState world = _session.World;
            MarketCommands.CollectOffers(world, _session.Player, _resource, _sellers);

            int count = _sellers.Count;
            _offersTitle.text = "Satış teklifleri (" + count + ")";
            _noOffers.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // En yakın teklif hazır seçilir; seçili teklif bittiyse sıradakine geçilir.
            if (!_sellers.Contains(_seller))
            {
                _seller = count > 0 ? _sellers[0] : NoSeller;
            }

            while (_rows.Count < count)
            {
                _rows.Add(MakeRow());
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].Root.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used)
                {
                    FillRow(_rows[i], _sellers[i]);
                }
            }
        }

        private OfferRow MakeRow()
        {
            var row = new OfferRow();
            row.Root = new MetalPanel().WithGradient(RowFill, RowFill).WithClick(() => PickSeller(row.Seller));
            row.Root.AddToClassList("gw-table-row");
            _offerRows.Add(row.Root);

            VisualElement text = UiBuilder.Passive(row.Root, "gw-info-row-text");
            row.Name = UiBuilder.Text(text, "", "gw-info-city", "gw-f-600");
            row.Distance = UiBuilder.Text(text, "", "gw-info-note", "gw-muted");
            row.Amount = UiBuilder.Text(row.Root, "", "gw-table-value", "gw-f-600");
            row.Fee = UiBuilder.Text(row.Root, "", "gw-table-value", "gw-f-600");
            row.Cost = ResourceRow.AddMoneyValue(row.Root, _session, "gw-f-700");
            return row;
        }

        private void FillRow(OfferRow row, int seller)
        {
            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;
            int buyer = _session.Player;

            row.Seller = seller;
            row.Name.text = _session.CountryTitle(seller);
            row.Amount.text = NumberFormat.Amount(MarketCommands.OfferMilli(world, seller, _resource));
            row.Fee.style.color = Muted;

            string km = NumberFormat.Compact(world.CountryDistanceKm[buyer * world.CountryCount + seller]) + " km";
            int transport = CountryRoutes.Route(definition, world, buyer, seller);
            if (transport == CountryRoutes.NoRoute)
            {
                row.Distance.text = km + " · Yol yok";
                row.Distance.style.color = UiColors.Warn;
                row.Fee.text = NoValue;
                row.Cost.text = NoValue;
                ResourceRow.SetMoneyIconVisible(row.Cost, false);
            }
            else
            {
                row.Distance.text = km + " · " + _session.TransportName(transport) + " · " + CountryRoutes.TransitDays(definition, world, buyer, seller, transport) + " gün";
                row.Distance.style.color = Muted;
                row.Fee.text = "+%" + NumberFormat.Milli(MarketCommands.RoadFeePermille(definition, world, buyer, seller) * PermilleToMilliPercent);
                row.Cost.text = NumberFormat.Amount(MarketCommands.UnitCostMilli(definition, world, buyer, seller, _resource));
                ResourceRow.SetMoneyIconVisible(row.Cost, true);
            }

            bool selected = seller == _seller;
            Color fill = selected ? RowFillSelected : RowFill;
            row.Root.WithGradient(fill, fill);
            UiBuilder.SetBorder(row.Root, selected ? RowBorderSelected : RowBorder);
        }

        private void RefreshOwnOffer()
        {
            long own = _session.PlayerOfferMilli(_resource);
            _ownBox.style.display = own > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (own > 0)
            {
                _ownText.text = "Pazardaki teklifin: " + NumberFormat.Amount(own);
            }
        }

        private void RefreshTransit()
        {
            Shipments.CollectFor(_session.World, _session.Player, _resource, _shipments);

            int count = _shipments.Count;
            _transitTitle.text = "Yolda (" + count + ")";
            _transitTitle.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            while (_transit.Count < count)
            {
                _transit.Add(MakeTransitRow());
            }

            for (int i = 0; i < _transit.Count; i++)
            {
                bool used = i < count;
                _transit[i].Root.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used)
                {
                    FillTransitRow(_transit[i], _shipments[i]);
                }
            }
        }

        private TransitRow MakeTransitRow()
        {
            VisualElement root = UiBuilder.Passive(_transitRows, "gw-info-row");
            VisualElement text = UiBuilder.Passive(root, "gw-info-row-text");
            return new TransitRow
            {
                Root = root,
                Name = UiBuilder.Text(text, "", "gw-info-city", "gw-f-600"),
                Note = UiBuilder.Text(text, "", "gw-info-note", "gw-muted"),
                Amount = UiBuilder.Text(root, "", "gw-info-amount", "gw-f-700")
            };
        }

        private void FillTransitRow(TransitRow row, Shipment shipment)
        {
            WorldState world = _session.World;
            long trip = _session.Definition.TransportTripMilli[shipment.Transport];
            int days = shipment.ArrivalDay - world.Day;

            string note = _session.TransportName(shipment.Transport) + " · " + (days <= 0 ? "bugün" : days + " gün");
            if (shipment.RemainingMilli > trip)
            {
                note += " · seferde " + NumberFormat.Amount(trip);
            }

            row.Name.text = _session.CountryTitle(shipment.Seller);
            row.Note.text = note;
            row.Amount.text = NumberFormat.Amount(shipment.RemainingMilli);
        }

        private void RefreshActions()
        {
            for (int i = 0; i < _amountButtons.Length; i++)
            {
                ScreenFrame.StyleChoice(_amountButtons[i], _amountLabels[i], i == _amountIndex);
            }

            RefreshBuy();

            long sell = SellAmountMilli;
            _sellLabel.style.color = sell > 0 ? UiColors.Text : Muted;
            _sellLabel.text = sell > 0 ? "SAT " + NumberFormat.Amount(sell) : "STOK YOK";
        }

        private void RefreshBuy()
        {
            WorldState world = _session.World;
            long amount = BuyAmountMilli;
            TradeBlock block = _seller == NoSeller
                ? TradeBlock.NoOffer
                : MarketCommands.CheckBuy(_session.Definition, world, _session.Player, _seller, _resource, amount);
            bool canBuy = block == TradeBlock.None;

            UiBuilder.ApplyGold(_buyButton, canBuy);
            _buyLabel.style.color = canBuy ? UiColors.DarkText : Muted;
            if (canBuy)
            {
                long cost = MarketCommands.BuyCostMilli(_session.Definition, world, _session.Player, _seller, _resource, amount);
                _buyLabel.text = "AL " + NumberFormat.Amount(amount) + " · " + NumberFormat.Compact(cost / EconomyDefinition.MilliPerUnit);
            }
            else
            {
                _buyLabel.text = BuyBlockText(block);
            }
        }

        private static string BuyBlockText(TradeBlock block)
        {
            switch (block)
            {
                case TradeBlock.NoRoute: return "YOL YOK";
                case TradeBlock.NoMoney: return "PARA YETMİYOR";
                default: return "TEKLİF YOK";
            }
        }

        private void PickSeller(int seller)
        {
            _seller = seller;
            Refresh();
        }

        private void PickAmount(int index)
        {
            _amountIndex = index;
            RefreshActions();
        }

        private void OpenDetail()
        {
            _detail.Open(_resource);
        }

        private void OnBuy()
        {
            long amount = BuyAmountMilli;
            if (_seller != NoSeller && _session.BuyOffer(_seller, _resource, amount))
            {
                Debug.Log("GeoWarUi market buy=" + _session.Definition.ResourceKeys[_resource] + " seller=" + _session.CountryKey(_seller) + " amount=" + amount);
                _traded();
            }
        }

        private void OnSell()
        {
            long amount = SellAmountMilli;
            if (_session.ListForSale(_resource, amount))
            {
                Debug.Log("GeoWarUi market list=" + _session.Definition.ResourceKeys[_resource] + " amount=" + amount);
                _traded();
            }
        }

        private void OnWithdraw()
        {
            if (_session.WithdrawOffer(_resource))
            {
                Debug.Log("GeoWarUi market withdraw=" + _session.Definition.ResourceKeys[_resource]);
                _traded();
            }
        }
    }
}
