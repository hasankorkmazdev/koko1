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

        private static readonly Color RowFill = UiColors.Hex("#14130C");
        private static readonly Color RowFillSelected = UiColors.Hex("#3D3B2A");
        private static readonly Color RowBorder = UiColors.Hex("#3A392A");
        private static readonly Color RowBorderSelected = UiColors.Hex("#CFAB52");

        private readonly GameSession _session;
        private readonly MetalPanel _root;
        private readonly VisualElement _iconHolder;
        private readonly Label _name;
        private readonly Label[] _values = new Label[ValueCount];

        public int Resource { get; private set; } = NoResource;

        public ResourceRow(VisualElement parent, GameSession session, Action<int> picked)
        {
            _session = session;
            _root = new MetalPanel().WithGradient(RowFill, RowFill).WithClick(() => picked(Resource));
            _root.AddToClassList("gw-table-row");
            parent.Add(_root);

            _iconHolder = UiBuilder.Passive(_root, "gw-table-icon");
            _name = UiBuilder.Text(_root, "", "gw-table-name", "gw-f-600");
            for (int i = 0; i < ValueCount; i++)
            {
                _values[i] = UiBuilder.Text(_root, "", "gw-table-value", "gw-f-600");
            }
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
            var icon = new IconElement(_session.Icons.Get(view.Icon), UiColors.Hex(view.Tint), IconStroke);
            icon.style.width = Length.Percent(100f);
            icon.style.height = Length.Percent(100f);
            _iconHolder.Add(icon);
        }

        public void SetValue(int column, string text, Color color)
        {
            _values[column].text = text;
            _values[column].style.color = color;
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
