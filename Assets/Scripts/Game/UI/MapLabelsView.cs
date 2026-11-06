using System.Collections.Generic;
using GeoWar.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Haritanın üstündeki yazılar: il ve ülke adları, deniz adları, başkent ve alarm işaretleri.
    // Sadece kamera oynayınca güncellenir. Küçük kalan illerin adı yazılmaz, yabancı iller yakında görünür.
    public sealed class MapLabelsView
    {
        private const float ForeignProvinceMinZoom = 1.6f;
        private const float OwnCountryMaxZoom = 1f;
        private const float CountryLabelMaxZoom = 1.6f;
        private const float ProvinceCharWidth = 5.1f;
        private const float ProvinceFit = 0.9f;
        private const float TokenOffsetY = 16f;
        private const float CountryIconStroke = 2.4f;

        private sealed class CountryLabel
        {
            public VisualElement Anchor;
            public Vector2 World;
            public bool IsOwn;
        }

        private sealed class SeaLabel
        {
            public VisualElement Anchor;
            public Vector2 World;
        }

        private sealed class CapitalToken
        {
            public VisualElement Anchor;
            public int Province;
        }

        private readonly MapData _map;
        private readonly GameSession _session;
        private readonly VisualElement _layer;
        private readonly VisualElement[] _provinces;
        private readonly Label[] _provinceTexts;
        private readonly bool[] _provinceVisible;
        private readonly VisualElement[] _alarmTokens;
        private readonly List<CountryLabel> _countries = new List<CountryLabel>();
        private readonly List<SeaLabel> _seas = new List<SeaLabel>();
        private readonly List<CapitalToken> _capitals = new List<CapitalToken>();

        private int _selectedProvince = MapHitTest.NotFound;

        public MapLabelsView(VisualElement parent, GameSession session)
        {
            _session = session;
            _map = session.Map;
            _layer = UiBuilder.Box(parent, "gw-layer");
            _layer.pickingMode = PickingMode.Ignore;

            _provinces = new VisualElement[_map.ProvinceCount];
            _provinceTexts = new Label[_map.ProvinceCount];
            _provinceVisible = new bool[_map.ProvinceCount];
            _alarmTokens = new VisualElement[_map.ProvinceCount];

            BuildSeas();
            BuildProvinces();
            BuildCountries();
            BuildCapitals();
        }

        public void SelectProvince(int province)
        {
            if (_selectedProvince != MapHitTest.NotFound)
            {
                _provinceTexts[_selectedProvince].RemoveFromClassList("gw-label-selected");
            }

            _selectedProvince = province;
            if (province != MapHitTest.NotFound)
            {
                _provinceTexts[province].AddToClassList("gw-label-selected");
            }
        }

        public void UpdatePositions(Vector2 center, float zoom)
        {
            float width = MapLayout.ViewWidth;
            float height = MapLayout.ViewHeight;
            float offsetX = width * 0.5f - center.x * zoom;
            float offsetY = height * 0.5f + center.y * zoom;

            UpdateSeas(zoom, offsetX, offsetY);
            UpdateProvinces(zoom, offsetX, offsetY, width, height);
            UpdateCountries(zoom, offsetX, offsetY);
            UpdateTokens(zoom, offsetX, offsetY);
        }

        private void BuildSeas()
        {
            foreach (SeaLabelData sea in _session.Ui.SeaLabels)
            {
                VisualElement anchor = MakeAnchor();
                Label text = UiBuilder.Text(anchor, sea.Name, "gw-sea-label", "gw-f-oswald-600");
                text.style.fontSize = sea.FontSize;
                text.style.letterSpacing = sea.LetterSpacing;
                _seas.Add(new SeaLabel { Anchor = anchor, World = MapLayout.ToWorld(sea.Lon, sea.Lat) });
            }
        }

        private void BuildProvinces()
        {
            for (int province = 0; province < _map.ProvinceCount; province++)
            {
                byte country = _map.ProvinceCountry[province];
                if (country == MapData.NoCountry)
                {
                    continue;
                }

                VisualElement anchor = MakeAnchor();
                anchor.style.display = DisplayStyle.None;
                _provinces[province] = anchor;
                _provinceTexts[province] = UiBuilder.Text(
                    anchor,
                    _map.ProvinceNames[province],
                    "gw-province-label",
                    "gw-f-600",
                    LabelClass(_session.Relations[country]));
            }
        }

        private void BuildCountries()
        {
            foreach (CountryViewData country in _session.Ui.Countries)
            {
                int index = System.Array.IndexOf(_map.Countries, country.Key);
                if (index < 0)
                {
                    continue;
                }

                CountryRelation relation = _session.Relations[index];
                VisualElement anchor = MakeAnchor();
                VisualElement row = UiBuilder.Box(anchor, "gw-country-row");
                row.pickingMode = PickingMode.Ignore;

                string iconKey = relation == CountryRelation.Ally ? "shield" : relation == CountryRelation.War ? "swords" : null;
                if (iconKey != null)
                {
                    Color tint = relation == CountryRelation.Ally ? UiColors.Hex("#10241A") : Color.white;
                    var icon = new IconElement(_session.Icons.Get(iconKey), tint, CountryIconStroke);
                    icon.AddToClassList("gw-country-icon");
                    row.Add(icon);
                }

                UiBuilder.Text(row, country.Name, "gw-country-label", "gw-f-oswald-700", LabelClass(relation));

                _countries.Add(new CountryLabel
                {
                    Anchor = anchor,
                    World = MapLayout.ToWorld(country.Lon, country.Lat),
                    IsOwn = relation == CountryRelation.Own
                });
            }
        }

        // Her ülkenin başkenti aynı altın yıldızla işaretlenir.
        private void BuildCapitals()
        {
            foreach (CountryViewData country in _session.Ui.Countries)
            {
                int province = System.Array.IndexOf(_map.ProvinceIds, country.Capital);
                if (province < 0)
                {
                    continue;
                }

                VisualElement anchor = MakeAnchor();
                VisualElement token = UiBuilder.Box(anchor, "gw-token", "gw-token-capital");
                token.pickingMode = PickingMode.Ignore;
                var icon = new IconElement(_session.Icons.Get("star"), UiColors.DarkText, 0f, true);
                icon.AddToClassList("gw-token-icon");
                token.Add(icon);
                _capitals.Add(new CapitalToken { Anchor = anchor, Province = province });
            }
        }

        private VisualElement MakeAnchor()
        {
            VisualElement anchor = UiBuilder.Box(_layer, "gw-anchor");
            anchor.pickingMode = PickingMode.Ignore;
            anchor.usageHints = UsageHints.DynamicTransform;
            return anchor;
        }

        private static string LabelClass(CountryRelation relation)
        {
            switch (relation)
            {
                case CountryRelation.Ally: return "gw-label-light-ally";
                case CountryRelation.Peace: return "gw-label-light-peace";
                default: return "gw-label-dark";
            }
        }

        private void UpdateSeas(float zoom, float offsetX, float offsetY)
        {
            foreach (SeaLabel sea in _seas)
            {
                MoveTo(sea.Anchor, sea.World.x * zoom + offsetX, offsetY - sea.World.y * zoom);
                sea.Anchor.style.scale = new Scale(new Vector2(zoom, zoom));
            }
        }

        private void UpdateProvinces(float zoom, float offsetX, float offsetY, float width, float height)
        {
            for (int province = 0; province < _map.ProvinceCount; province++)
            {
                VisualElement anchor = _provinces[province];
                if (anchor == null)
                {
                    continue;
                }

                float x = _map.CenterX[province] * zoom + offsetX;
                float y = offsetY - _map.CenterY[province] * zoom;
                bool visible = x >= 0f && x <= width && y >= 0f && y <= height && ShouldShowProvince(province, zoom);

                if (visible != _provinceVisible[province])
                {
                    _provinceVisible[province] = visible;
                    anchor.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (visible)
                {
                    MoveTo(anchor, x, y);
                }
            }
        }

        private bool ShouldShowProvince(int province, float zoom)
        {
            if (province == _selectedProvince)
            {
                return true;
            }

            bool foreign = _session.Relations[_map.ProvinceCountry[province]] != CountryRelation.Own;
            if (foreign && zoom < ForeignProvinceMinZoom)
            {
                return false;
            }

            float room = (_map.MaxX[province] - _map.MinX[province]) * zoom * ProvinceFit;
            return room >= _map.ProvinceNames[province].Length * ProvinceCharWidth;
        }

        private void UpdateCountries(float zoom, float offsetX, float offsetY)
        {
            foreach (CountryLabel country in _countries)
            {
                bool visible = zoom < CountryLabelMaxZoom && !(country.IsOwn && zoom >= OwnCountryMaxZoom);
                country.Anchor.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                {
                    continue;
                }

                // Ad ülkenin ortasında sabit durur; ülke ekrandan çıkınca adı da çıkar.
                MoveTo(country.Anchor, country.World.x * zoom + offsetX, offsetY - country.World.y * zoom);
            }
        }

        private void UpdateTokens(float zoom, float offsetX, float offsetY)
        {
            foreach (CapitalToken capital in _capitals)
            {
                MoveTo(
                    capital.Anchor,
                    _map.CenterX[capital.Province] * zoom + offsetX,
                    offsetY - _map.CenterY[capital.Province] * zoom - TokenOffsetY);
            }

            for (int province = 0; province < _alarmTokens.Length; province++)
            {
                VisualElement token = _alarmTokens[province];
                if (token != null && token.style.display != DisplayStyle.None)
                {
                    MoveTo(token, _map.CenterX[province] * zoom + offsetX, offsetY - _map.CenterY[province] * zoom - TokenOffsetY);
                }
            }
        }

        // Gün bitince çağrılır: alarmı olan illerin işaretini açar, biteninkini kapatır.
        public void RefreshAlarms(Vector2 center, float zoom)
        {
            bool[] alarms = _session.ProvinceAlarm;
            for (int province = 0; province < alarms.Length; province++)
            {
                VisualElement token = _alarmTokens[province];
                if (alarms[province] && token == null)
                {
                    token = MakeAnchor();
                    VisualElement box = UiBuilder.Box(token, "gw-token", "gw-token-alarm");
                    box.pickingMode = PickingMode.Ignore;
                    UiBuilder.Text(box, "!", "gw-token-text", "gw-f-oswald-700");
                    _alarmTokens[province] = token;
                }

                if (token != null)
                {
                    token.style.display = alarms[province] ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            float offsetX = MapLayout.ViewWidth * 0.5f - center.x * zoom;
            float offsetY = MapLayout.ViewHeight * 0.5f + center.y * zoom;
            UpdateTokens(zoom, offsetX, offsetY);
        }

        private static void MoveTo(VisualElement element, float x, float y)
        {
            element.style.translate = new Translate(x, y);
        }
    }
}
