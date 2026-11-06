using System.Collections.Generic;
using System.Text;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Kaynak kartına dokununca açılan pencere: kaynağın bilgileri ve hangi şehirde ne kadar üretildiği.
    public sealed class ResourceDetailView
    {
        private const float StrokeTitle = 2f;
        private const string ListSeparator = ", ";
        private const string NoteSeparator = " · ";
        private const string InputSeparator = " + ";
        private const int OneDay = 1;
        private const int NoResource = -1;

        private static readonly Color Muted = UiColors.Hex("#B5AE92");

        private sealed class Row
        {
            public VisualElement Root;
            public Label City;
            public Label Note;
            public Label Amount;
        }

        private readonly GameSession _session;
        private readonly VisualElement _scrim;
        private readonly MetalPanel _window;
        private readonly ScrollView _scroll;
        private readonly VisualElement _titleIconHolder;
        private readonly Label _title;
        private readonly ResourceStats _stats;
        private readonly Label _price;
        private readonly Label _makes;
        private readonly Label _uses;
        private readonly Label _citiesTitle;
        private readonly Label _noCities;
        private readonly VisualElement _cities;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<CityProduction> _production = new List<CityProduction>();
        private readonly List<int> _recipes = new List<int>();

        private int _resource = NoResource;

        public ResourceDetailView(VisualElement frame, GameSession session)
        {
            _session = session;

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

            _stats = new ResourceStats(content, session);
            _price = UiBuilder.Text(content, "", "gw-resource-outlook", "gw-f-courier-700");

            UiBuilder.Text(content, "Nasıl üretilir", "gw-resource-group", "gw-f-oswald-600");
            _makes = UiBuilder.Text(content, "", "gw-info-text");

            UiBuilder.Text(content, "Nerede kullanılır", "gw-resource-group", "gw-f-oswald-600");
            _uses = UiBuilder.Text(content, "", "gw-info-text");

            _citiesTitle = UiBuilder.Text(content, "", "gw-resource-group", "gw-f-oswald-600");
            _noCities = UiBuilder.Text(content, "Hiçbir şehirde üretilmiyor.", "gw-info-text", "gw-muted");
            _cities = UiBuilder.Passive(content, "gw-info-cities");
        }

        public void Open(int resource)
        {
            _resource = resource;
            _scrim.BringToFront();
            _window.BringToFront();
            _scrim.style.display = DisplayStyle.Flex;
            _window.style.display = DisplayStyle.Flex;
            _scroll.scrollOffset = Vector2.zero;

            ResourceViewData view = _session.ResourceViews[resource];
            _title.text = NumberFormat.Upper(view.Name);
            _titleIconHolder.Clear();
            var icon = new IconElement(_session.Icons.Get(view.Icon), UiColors.Hex(view.Tint), StrokeTitle);
            icon.style.width = Length.Percent(100f);
            icon.style.height = Length.Percent(100f);
            _titleIconHolder.Add(icon);

            _makes.text = MakesText();
            RefreshUses();
            Refresh();
            Debug.Log("GeoWarUi resource detail=" + _session.Definition.ResourceKeys[resource]);
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

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_resource == NoResource)
            {
                return;
            }

            _stats.Refresh(_resource);
            _price.text = "Piyasa fiyatı " + NumberFormat.Amount(_session.World.PriceMilli[_resource])
                + NoteSeparator + "Taban " + NumberFormat.Amount(_session.Definition.BasePriceMilli[_resource]);
            RefreshCities();
        }

        private string MakesText()
        {
            EconomyDefinition definition = _session.Definition;
            ProductionReport.CollectMakers(definition, _resource, _recipes);
            if (_recipes.Count == 0)
            {
                return "Üretilmiyor, pazardan alınır.";
            }

            var text = new StringBuilder();
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }

                AppendRecipe(text, _recipes[i]);
            }

            return text.ToString();
        }

        // "Çelik fabrikası: 3 demir + 2 kömür + 1 krom → 3 çelik, 1 gün" veya "Altın madeni: 5 günde 1 altın".
        private void AppendRecipe(StringBuilder text, int recipe)
        {
            EconomyDefinition definition = _session.Definition;
            text.Append(_session.BuildingViews[definition.RecipeBuilding[recipe]].Name).Append(": ");

            int output = ProductionReport.FindOutput(definition, recipe, _resource);
            string made = NumberFormat.Milli(definition.OutputMilliPerCycle[output]) + " " + NumberFormat.Lower(_session.ResourceViews[_resource].Name);
            int days = definition.RecipeDays[recipe];
            int inputStart = definition.InputStart[recipe];
            int inputEnd = definition.InputStart[recipe + 1];

            if (inputStart == inputEnd)
            {
                text.Append(days == OneDay ? "günde " : days + " günde ").Append(made);
                return;
            }

            for (int input = inputStart; input < inputEnd; input++)
            {
                if (input > inputStart)
                {
                    text.Append(InputSeparator);
                }

                text.Append(NumberFormat.Milli(definition.InputMilliPerCycle[input])).Append(' ');
                text.Append(NumberFormat.Lower(_session.ResourceViews[definition.InputResource[input]].Name));
            }

            text.Append(" → ").Append(made).Append(", ").Append(days).Append(" gün");
        }

        private void RefreshUses()
        {
            EconomyDefinition definition = _session.Definition;
            var names = new List<string>();

            ProductionReport.CollectUsers(definition, _resource, _recipes);
            foreach (int recipe in _recipes)
            {
                names.Add(definition.Kinds[recipe] == RecipeKind.Energy
                    ? _session.BuildingViews[definition.RecipeBuilding[recipe]].Name
                    : _session.ResourceViews[definition.OutputResource[definition.OutputStart[recipe]]].Name);
            }

            if (ProductionReport.IsPeopleNeed(definition, _resource))
            {
                names.Add("Halk tüketimi");
            }

            if (ProductionReport.IsFactoryUpkeep(definition, _resource))
            {
                names.Add("Fabrika bakımı");
            }

            if (ProductionReport.IsBuildMaterial(definition, _resource))
            {
                names.Add("Tesis inşaatı");
            }

            bool unused = names.Count == 0;
            _uses.text = unused ? "Hiçbir yerde kullanılmıyor." : string.Join(ListSeparator, names);
            _uses.style.color = unused ? UiColors.Warn : UiColors.Text;
        }

        private void RefreshCities()
        {
            ProductionReport.CollectCities(_session.Definition, _session.World, _session.Player, _resource, _production);

            int count = _production.Count;
            _citiesTitle.text = "Üretilen şehirler (" + count + ")" + NoteSeparator + "günlük";
            _noCities.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

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
                    FillRow(_rows[i], _production[i]);
                }
            }
        }

        private Row MakeRow()
        {
            VisualElement root = UiBuilder.Passive(_cities, "gw-info-row");
            VisualElement text = UiBuilder.Passive(root, "gw-info-row-text");
            return new Row
            {
                Root = root,
                City = UiBuilder.Text(text, "", "gw-info-city", "gw-f-600"),
                Note = UiBuilder.Text(text, "", "gw-info-note"),
                Amount = UiBuilder.Text(root, "", "gw-info-amount", "gw-f-700")
            };
        }

        private void FillRow(Row row, CityProduction production)
        {
            row.City.text = _session.Map.ProvinceNames[production.City];
            row.Note.text = NoteText(production);
            row.Note.style.color = production.Stopped > 0 ? UiColors.Warn : Muted;

            bool producing = production.ProducedMilli > 0;
            row.Amount.text = producing ? NumberFormat.SignedAmount(production.ProducedMilli) : "0";
            row.Amount.style.color = producing ? UiColors.Good : Muted;
        }

        private static string NoteText(CityProduction production)
        {
            var parts = new List<string>();
            if (production.Working > 0)
            {
                parts.Add(production.Working + " tesis");
            }

            if (production.Stopped > 0)
            {
                parts.Add(production.Stopped + " durdu");
            }

            if (production.Building > 0)
            {
                parts.Add(production.Building + " inşaatta");
            }

            return string.Join(NoteSeparator, parts);
        }
    }
}
