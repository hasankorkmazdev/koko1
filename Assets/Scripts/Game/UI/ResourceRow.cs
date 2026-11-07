using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Kaynak tablosunun bir satırı: ikon, ad ve üç değer. Stok ve Pazar ekranları kullanır.
    public sealed class ResourceRow
    {
        private const int ValueCount = 3;
        private const int NoResource = -1;
        private const float IconStroke = 2f;
        private const int MoneyIconIndex = 0;
        private const string WarningIconKey = "warning";
        private const string WarningDimClass = "gw-table-warning-dim";

        public const int NoMoneyColumn = -1;

        private static readonly Color RowFill = UiColors.Hex("#14130C");
        private static readonly Color RowFillSelected = UiColors.Hex("#3D3B2A");
        private static readonly Color RowBorder = UiColors.Hex("#3A392A");
        private static readonly Color RowBorderSelected = UiColors.Hex("#CFAB52");

        private readonly GameSession _session;
        private readonly MetalPanel _root;
        private readonly VisualElement _iconHolder;
        private readonly Label _name;
        private readonly IconElement _warning;
        private readonly Label[] _values = new Label[ValueCount];

        private bool _warningShown;

        public int Resource { get; private set; } = NoResource;

        public ResourceRow(VisualElement parent, GameSession session, Action<int> picked, int moneyColumn = NoMoneyColumn)
        {
            _session = session;
            _root = new MetalPanel().WithGradient(RowFill, RowFill).WithClick(() => picked(Resource));
            _root.AddToClassList("gw-table-row");
            parent.Add(_root);

            _iconHolder = UiBuilder.Passive(_root, "gw-table-icon");
            VisualElement nameBox = UiBuilder.Passive(_root, "gw-table-name-box");
            _name = UiBuilder.Text(nameBox, "", "gw-table-name", "gw-f-600");

            _warning = new IconElement(session.Icons.Get(WarningIconKey), UiColors.Alarm, 0f, true);
            _warning.AddToClassList("gw-table-warning");
            _warning.style.display = DisplayStyle.None;
            nameBox.Add(_warning);

            for (int i = 0; i < ValueCount; i++)
            {
                _values[i] = i == moneyColumn
                    ? AddMoneyValue(_root, session, "gw-f-600")
                    : UiBuilder.Text(_root, "", "gw-table-value", "gw-f-600");
            }
        }

        // Para sütunu: ikon yazının solunda durur, sütun diğer değerlerle aynı genişlikte kalır.
        public static Label AddMoneyValue(VisualElement parent, GameSession session, string fontClass)
        {
            VisualElement box = UiBuilder.Passive(parent, "gw-table-money");
            VisualElement icon = session.Icons.CreateMoneyIcon();
            icon.AddToClassList("gw-table-money-icon");
            box.Add(icon);
            return UiBuilder.Text(box, "", "gw-table-money-value", fontClass);
        }

        // Fiyat yokken yalnızca çizgi görünür; ikon boşa durmaz.
        public static void SetMoneyIconVisible(Label value, bool visible)
        {
            value.parent[MoneyIconIndex].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static VisualElement AddHeader(VisualElement parent, string name, string first, string second, string third)
        {
            VisualElement header = UiBuilder.Passive(parent, "gw-table-header");
            UiBuilder.Text(header, name, "gw-table-header-name");
            UiBuilder.Text(header, first, "gw-table-header-value");
            UiBuilder.Text(header, second, "gw-table-header-value");
            UiBuilder.Text(header, third, "gw-table-header-value");
            return header;
        }

        public void SetResource(int resource)
        {
            if (resource == Resource)
            {
                return;
            }

            Resource = resource;
            ResourceViewData view = _session.ResourceViews[resource];
            _name.text = view.Name;
            _iconHolder.Clear();
            VisualElement icon = _session.Icons.CreateResourceIcon(view, IconStroke);
            icon.style.width = Length.Percent(100f);
            icon.style.height = Length.Percent(100f);
            _iconHolder.Add(icon);
        }

        public void SetValue(int column, string text, Color color)
        {
            _values[column].text = text;
            _values[column].style.color = color;
        }

        public void SetWarning(bool visible)
        {
            if (visible != _warningShown)
            {
                _warningShown = visible;
                _warning.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // Atışın kendisi USS geçişidir; burada yalnızca yönü değişir.
        public void SetWarningDim(bool dim)
        {
            if (_warningShown)
            {
                _warning.EnableInClassList(WarningDimClass, dim);
            }
        }

        public void SetSelected(bool selected)
        {
            Color fill = selected ? RowFillSelected : RowFill;
            _root.WithGradient(fill, fill);
            UiBuilder.SetBorder(_root, selected ? RowBorderSelected : RowBorder);
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
