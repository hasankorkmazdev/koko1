using System;
using System.Collections.Generic;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Tesis Kur ekranı: türe göre sekmeler, tesis kartları, reçete ve maliyet, KUR.
    public sealed class BuilderView
    {
        private const int ItemsPerRow = 3;
        private const float TabGap = 3f;
        private const float ItemGap = 4f;
        private const float CostGap = 3f;
        private const float StrokeBold = 2.4f;
        private const float StrokeItem = 1.8f;
        private const float StrokeCost = 2f;
        private const string DefaultTab = "industry";
        private const string ExtractionTab = "extract";
        private const string NoCost = "—";
        private const float KombinaPercentDivisor = 10f;

        private static readonly Color SelectedFill = UiColors.Hex("#3D3B2A");
        private static readonly Color SelectedBorder = UiColors.Hex("#CFAB52");
        private static readonly Color ItemBorder = UiColors.Hex("#3A392A");
        private static readonly Color LockedText = UiColors.Hex("#A39C82");
        private static readonly Color Muted = UiColors.Hex("#B5AE92");
        private static readonly Color DisabledFill = UiColors.Hex("#2C2B20");

        private readonly GameSession _session;
        private readonly Action<int> _built;
        private readonly Action _changed;

        private readonly MetalPanel _root;
        private readonly Label _subtitle;
        private readonly List<MetalPanel> _tabViews = new List<MetalPanel>();
        private readonly List<Label> _tabLabels = new List<Label>();
        private readonly ScrollView _items;
        private readonly Label _detailName;
        private readonly Label _detailDays;
        private readonly RecipeLine _produces;
        private readonly RecipeLine _inputs;
        private readonly Label _detailNote;
        private readonly Label _detailKombina;
        private readonly Label[] _costValues;
        private readonly int[] _costResources;
        private readonly MetalPanel _buildButton;
        private readonly Label _buildLabel;

        private readonly List<MetalPanel> _itemViews = new List<MetalPanel>();
        private readonly List<int> _itemBuildings = new List<int>();

        private int _city = -1;
        private int _tab;
        private int _picked = -1;

        // Üretir ve Girdi satırlarının gösterdiği tesis: reçete değişmez, satırlar yalnızca seçim değişince kurulur.
        private int _recipeBuilding = WorldState.NoBuilding;

        public BuilderView(VisualElement frame, GameSession session, Action<int> built, Action changed)
        {
            _session = session;
            _built = built;
            _changed = changed;

            _root = new MetalPanel().WithGradient(UiBuilder.PanelBottom, UiBuilder.PanelBottom).WithRivets();
            _root.AddToClassList("gw-builder");
            _root.style.display = DisplayStyle.None;
            frame.Add(_root);

            MetalPanel header = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiColors.HeaderBottom);
            header.AddToClassList("gw-builder-header");
            _root.Add(header);
            MetalPanel back = UiBuilder.MetalButton().WithHitOutset(UiBuilder.SmallButtonHitOutset, UiBuilder.SmallButtonHitOutset).WithClick(Close);
            back.AddToClassList("gw-icon-button");
            back.style.marginRight = 8f;
            var backIcon = new IconElement(session.Icons.Get("back"), UiColors.Text, StrokeBold);
            backIcon.AddToClassList("gw-back-icon");
            back.Add(backIcon);
            header.Add(back);
            VisualElement title = UiBuilder.Passive(header, "gw-panel-title-column");
            UiBuilder.Text(title, "TESİS KUR", "gw-tag", "gw-tag-small");
            _subtitle = UiBuilder.Text(title, "", "gw-panel-sub-below");

            VisualElement tabs = UiBuilder.Passive(_root, "gw-builder-tabs");
            for (int i = 0; i < session.Ui.Tabs.Count; i++)
            {
                int index = i;
                MetalPanel tab = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(() => SelectTab(index));
                tab.AddToClassList("gw-builder-tab");
                tab.style.marginLeft = i == 0 ? 0f : TabGap;
                tabs.Add(tab);
                _tabViews.Add(tab);
                _tabLabels.Add(UiBuilder.Text(tab, session.Ui.Tabs[i].Label, "gw-f-700"));
            }

            _items = UiBuilder.VerticalScroll(_root, "gw-builder-scroll");

            MetalPanel footer = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiColors.HeaderBottom);
            footer.AddToClassList("gw-builder-footer");
            _root.Add(footer);

            VisualElement nameRow = UiBuilder.Passive(footer, "gw-detail-row");
            _detailName = UiBuilder.Text(nameRow, "", "gw-builder-name", "gw-f-oswald-700");
            _detailDays = UiBuilder.Text(nameRow, "", "gw-builder-days", "gw-f-700");
            _produces = new RecipeLine(footer, session, "Üretir:", UiColors.Text);
            _inputs = new RecipeLine(footer, session, "Girdi:", Muted);
            _detailNote = UiBuilder.Text(footer, "", "gw-builder-line", "gw-muted");
            _detailKombina = UiBuilder.Text(footer, "", "gw-builder-kombina", "gw-f-600");

            int materialCount = session.Ui.BuildMaterials.Count;
            _costValues = new Label[materialCount + 1];
            _costResources = new int[materialCount];
            VisualElement costs = UiBuilder.Passive(footer, "gw-costs");
            _costValues[0] = AddCost(costs, "Para", 0f, session.Icons.CreateMoneyIcon());
            for (int i = 0; i < materialCount; i++)
            {
                int resource = session.Definition.ResourceIndex(session.Ui.BuildMaterials[i]);
                _costResources[i] = resource;
                ResourceViewData view = session.ResourceViews[resource];
                _costValues[i + 1] = AddCost(costs, view.Name, CostGap, session.Icons.CreateResourceIcon(view, StrokeCost));
            }

            VisualElement actions = UiBuilder.Passive(footer, "gw-builder-actions");
            _buildButton = new MetalPanel().WithHitOutset(0f, UiBuilder.SmallButtonHitOutset).WithClick(OnBuild);
            _buildButton.AddToClassList("gw-builder-action");
            _buildLabel = UiBuilder.Text(_buildButton, "", "gw-f-oswald-700");
            actions.Add(_buildButton);
        }

        public bool IsOpen
        {
            get { return _city >= 0; }
        }

        public void Open(int city)
        {
            _city = city;
            _root.style.display = DisplayStyle.Flex;
            _tab = FindTab(DefaultTab);
            _picked = -1;
            BuildItems();
            Refresh();
        }

        public void Close()
        {
            _city = -1;
            _root.style.display = DisplayStyle.None;
        }

        // Gün bitince çağrılır; her karede değil.
        public void Refresh()
        {
            if (_city < 0)
            {
                return;
            }

            WorldState world = _session.World;
            _subtitle.text = _session.Map.ProvinceNames[_city] + " · Slot " + FacilityCommands.UsedSlots(world, _city) + "/" + FacilityCommands.SlotCount(world, _city);
            RefreshTabs();
            RefreshItems();
            RefreshDetail();
        }

        private static Label AddCost(VisualElement parent, string caption, float marginLeft, VisualElement icon)
        {
            VisualElement cell = UiBuilder.Passive(parent, "gw-cost");
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-stat-caption");

            VisualElement valueRow = UiBuilder.Passive(cell, "gw-value-row");
            icon.AddToClassList("gw-cost-icon");
            valueRow.Add(icon);
            return UiBuilder.Text(valueRow, "", "gw-cost-value", "gw-f-700");
        }

        private int FindTab(string key)
        {
            for (int i = 0; i < _session.Ui.Tabs.Count; i++)
            {
                if (_session.Ui.Tabs[i].Key == key)
                {
                    return i;
                }
            }

            return 0;
        }

        private void SelectTab(int tab)
        {
            _tab = tab;
            _picked = -1;
            BuildItems();
            Refresh();
        }

        private void BuildItems()
        {
            _items.Clear();
            _itemViews.Clear();
            _itemBuildings.Clear();

            EconomyDefinition definition = _session.Definition;
            string tabKey = _session.Ui.Tabs[_tab].Key;
            bool isExtraction = tabKey == ExtractionTab;

            VisualElement row = null;
            for (int building = 0; building < definition.BuildingCount; building++)
            {
                if (definition.BuildingTabs[building] != tabKey)
                {
                    continue;
                }

                // Çıkarma sekmesinde sadece şehirde bulunan kaynakların tesisi görünür.
                if (isExtraction && !FacilityCommands.HasDeposit(_session.World, _city, building))
                {
                    continue;
                }

                if (_itemViews.Count % ItemsPerRow == 0)
                {
                    row = UiBuilder.Passive(_items.contentContainer, "gw-builder-row");
                }

                AddItem(row, building);
            }

            FillLastRow(row);

            if (isExtraction)
            {
                UiBuilder.Text(_items.contentContainer, "Yeni çıkarma tesisi için önce şehirde kaynak ara.", "gw-builder-hint");
            }

            _picked = FirstOpenItem();
        }

        private void AddItem(VisualElement row, int building)
        {
            int current = building;
            MetalPanel item = new MetalPanel().WithClick(() => PickItem(current));
            item.AddToClassList("gw-builder-item");
            item.style.marginLeft = _itemViews.Count % ItemsPerRow == 0 ? 0f : ItemGap;
            row.Add(item);

            BuildBlock block = FacilityCommands.CheckPlacement(_session.Definition, _session.World, _city, building);
            Color color = block == BuildBlock.None ? UiColors.Text : LockedText;

            BuildingViewData view = _session.BuildingViews[building];
            VisualElement icon = _session.Icons.CreateBuildingIcon(view, color, StrokeItem, block != BuildBlock.None);
            icon.AddToClassList("gw-builder-item-icon");
            item.Add(icon);

            Label name = UiBuilder.Text(item, view.Name, "gw-builder-item-name", "gw-f-600");
            name.style.color = color;

            string lockText = LockText(block);
            if (lockText != null)
            {
                UiBuilder.Text(item, lockText, "gw-builder-item-lock");
            }

            _itemViews.Add(item);
            _itemBuildings.Add(building);
        }

        private void FillLastRow(VisualElement row)
        {
            if (row == null)
            {
                return;
            }

            int remainder = _itemViews.Count % ItemsPerRow;
            for (int i = remainder == 0 ? ItemsPerRow : remainder; i < ItemsPerRow; i++)
            {
                VisualElement spacer = UiBuilder.Passive(row, "gw-builder-item-spacer");
                spacer.style.marginLeft = ItemGap;
            }
        }

        private int FirstOpenItem()
        {
            foreach (int building in _itemBuildings)
            {
                if (FacilityCommands.CheckPlacement(_session.Definition, _session.World, _city, building) == BuildBlock.None)
                {
                    return building;
                }
            }

            return _itemBuildings.Count > 0 ? _itemBuildings[0] : -1;
        }

        private void PickItem(int building)
        {
            _picked = building;
            RefreshItems();
            RefreshDetail();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < _tabViews.Count; i++)
            {
                bool active = i == _tab;
                _tabViews[i]
                    .WithGradient(active ? UiColors.ActiveTop : UiColors.ButtonTop, active ? UiColors.ActiveBottom : UiColors.ButtonBottom)
                    .WithInsets(active ? UiColors.ActiveInset : UiColors.ButtonInset, UiColors.Clear);
                _tabLabels[i].style.color = active ? UiColors.DarkText : UiColors.Text;
            }
        }

        private void RefreshItems()
        {
            for (int i = 0; i < _itemViews.Count; i++)
            {
                bool selected = _itemBuildings[i] == _picked;
                Color fill = selected ? SelectedFill : UiColors.Well;
                _itemViews[i].WithGradient(fill, fill);
                UiBuilder.SetBorder(_itemViews[i], selected ? SelectedBorder : ItemBorder);
            }
        }

        private void RefreshDetail()
        {
            if (_picked < 0)
            {
                ShowNothingToBuild();
                return;
            }

            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;
            BuildingViewData view = _session.BuildingViews[_picked];

            _detailName.text = view.Name;
            int days = definition.BuildingDays[_picked];
            _detailDays.text = days > 0 ? days + " gün" : "";
            RefreshRecipe(view);

            string note = !string.IsNullOrEmpty(view.Note) ? view.Note : _session.Ui.Tabs[_tab].Note;
            _detailNote.text = note;
            _detailNote.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;

            RefreshKombina();
            RefreshCosts();

            BuildBlock block = FacilityCommands.CheckBuild(definition, world, _session.Player, _city, _picked);
            bool canBuild = block == BuildBlock.None;
            UiBuilder.ApplyGold(_buildButton, canBuild);
            _buildLabel.style.color = canBuild ? UiColors.DarkText : Muted;
            _buildLabel.text = BuildLabel(block, days);
        }

        private void RefreshRecipe(BuildingViewData view)
        {
            if (_recipeBuilding == _picked)
            {
                return;
            }

            _recipeBuilding = _picked;
            EconomyDefinition definition = _session.Definition;
            if (string.IsNullOrEmpty(view.Produces))
            {
                _produces.ShowResources(definition.BuildingOutputResources(_picked));
            }
            else
            {
                _produces.ShowText(view.ProducesIcon, view.Produces);
            }

            _inputs.ShowResources(definition.BuildingInputResources(_picked));
        }

        private void ShowNothingToBuild()
        {
            _detailName.text = "Kurulacak tesis yok";
            _detailDays.text = "";
            _recipeBuilding = WorldState.NoBuilding;
            _produces.Hide();
            _inputs.Hide();
            _detailNote.text = "Bu şehirde bilinen kaynak yok.";
            _detailNote.style.display = DisplayStyle.Flex;
            _detailKombina.text = "";
            UiBuilder.ApplyGold(_buildButton, false);
            _buildLabel.style.color = Muted;
            _buildLabel.text = "KAYNAK YOK";
            for (int i = 0; i < _costValues.Length; i++)
            {
                _costValues[i].text = NoCost;
            }
        }

        private void RefreshKombina()
        {
            EconomyDefinition definition = _session.Definition;
            int links = definition.IsFactory(_picked) ? FacilityCommands.LinkCount(definition, _session.World, _city, _picked) : 0;
            if (links == 0)
            {
                _detailKombina.text = "Bu şehirde kombina bağlantısı yok";
                _detailKombina.style.color = Muted;
                return;
            }

            float percent = links * definition.Balance.KombinaPermillePerLink / KombinaPercentDivisor;
            _detailKombina.text = "Kombina: +%" + percent.ToString("0.#").Replace('.', ',') + " verim (" + LinkedNames() + ")";
            _detailKombina.style.color = UiColors.Good;
        }

        private string LinkedNames()
        {
            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;
            var names = new List<string>();
            for (int slot = world.CitySlotStart[_city]; slot < world.CitySlotStart[_city + 1]; slot++)
            {
                int other = world.FacilityBuilding[slot];
                if (other != WorldState.NoBuilding
                    && world.FacilityBuildDays[slot] == 0
                    && definition.BuildingLinked[_picked * definition.BuildingCount + other])
                {
                    names.Add(_session.BuildingViews[other].Name);
                }
            }

            return string.Join(", ", names);
        }

        private void RefreshCosts()
        {
            EconomyDefinition definition = _session.Definition;
            WorldState world = _session.World;

            long money = definition.BuildingMoneyMilli[_picked];
            _costValues[0].text = money > 0 ? NumberFormat.Compact(money / EconomyDefinition.MilliPerUnit) : NoCost;
            _costValues[0].style.color = money > world.Money[_session.Player] ? UiColors.Warn : UiColors.Text;

            int stockBase = _session.Player * world.ResourceCount;
            for (int i = 0; i < _costResources.Length; i++)
            {
                long need = MaterialNeed(_costResources[i]);
                long have = world.Stock[stockBase + _costResources[i]];
                Label value = _costValues[i + 1];
                value.text = need > 0
                    ? NumberFormat.Compact(have / EconomyDefinition.MilliPerUnit) + "/" + NumberFormat.Compact(need / EconomyDefinition.MilliPerUnit)
                    : NoCost;
                value.style.color = need > have ? UiColors.Warn : UiColors.Text;
            }
        }

        private long MaterialNeed(int resource)
        {
            EconomyDefinition definition = _session.Definition;
            for (int i = definition.BuildingMaterialStart[_picked]; i < definition.BuildingMaterialStart[_picked + 1]; i++)
            {
                if (definition.BuildingMaterialResource[i] == resource)
                {
                    return definition.BuildingMaterialMilli[i];
                }
            }

            return 0;
        }

        private static string LockText(BuildBlock block)
        {
            switch (block)
            {
                case BuildBlock.Locked: return "Ordu tasarlanınca";
                case BuildBlock.NeedsCoast: return "Kıyı gerekir";
                case BuildBlock.AlreadyBuilt: return "Kurulu";
                default: return null;
            }
        }

        private static string BuildLabel(BuildBlock block, int days)
        {
            switch (block)
            {
                case BuildBlock.Locked: return "ORDU TASARLANINCA";
                case BuildBlock.NeedsCoast: return "KIYI GEREKİR";
                case BuildBlock.AlreadyBuilt: return "KURULU";
                case BuildBlock.NoDeposit: return "KAYNAK YOK";
                case BuildBlock.NoSlot: return "BOŞ SLOT YOK";
                case BuildBlock.NoMoney: return "PARA YETMİYOR";
                case BuildBlock.NoMaterials: return "MALZEME EKSİK";
                default: return "KUR · " + days + " GÜN";
            }
        }

        private void OnBuild()
        {
            if (_picked < 0)
            {
                return;
            }

            int slot = _session.Build(_city, _picked);
            if (slot >= 0)
            {
                Debug.Log("GeoWarUi build building=" + _session.Definition.BuildingKeys[_picked] + " slot=" + slot);
                Close();
                _changed();
                _built(slot);
            }
        }
    }

    // Tesis Kur'daki "Üretir" ve "Girdi" satırı: başlık ve yanında ikonlu kaynaklar. Sığmayan alt satıra iner.
    public sealed class RecipeLine
    {
        private const float IconStroke = 2f;

        private readonly GameSession _session;
        private readonly Color _textColor;
        private readonly VisualElement _root;
        private readonly VisualElement _items;

        public RecipeLine(VisualElement parent, GameSession session, string caption, Color textColor)
        {
            _session = session;
            _textColor = textColor;
            _root = UiBuilder.Passive(parent, "gw-builder-recipe");
            Label captionLabel = UiBuilder.Text(_root, caption, "gw-builder-recipe-caption");
            captionLabel.style.color = textColor;
            _items = UiBuilder.Passive(_root, "gw-builder-recipe-items");
        }

        // Liste boşsa satır görünmez: girdisiz tesiste "Girdi" yazmaz.
        public void ShowResources(List<int> resources)
        {
            _items.Clear();
            foreach (int resource in resources)
            {
                ResourceViewData view = _session.ResourceViews[resource];
                AddItem(_session.Icons.CreateResourceIcon(view, IconStroke), view.Name);
            }

            _root.style.display = resources.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Ürünü kaynak olmayan tesis: enerji, sevkiyat veya henüz tasarlanmamış. İkon verilmemişse yalnızca yazı durur.
        public void ShowText(string iconKey, string text)
        {
            _items.Clear();
            bool hasIcon = !string.IsNullOrEmpty(iconKey);
            AddItem(hasIcon ? new IconElement(_session.Icons.Get(iconKey), UiColors.Gold, IconStroke) : null, text);
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        private void AddItem(VisualElement icon, string text)
        {
            VisualElement item = UiBuilder.Passive(_items, "gw-builder-recipe-item");
            if (icon != null)
            {
                icon.AddToClassList("gw-builder-recipe-icon");
                item.Add(icon);
            }

            Label name = UiBuilder.Text(item, text, "gw-builder-recipe-name");
            name.style.color = _textColor;
        }
    }
}
