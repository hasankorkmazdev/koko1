using GeoWar.Map;
using GeoWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Arayüzün kökü: paneli açar, telefonun çentiğini boşlukla geçer, ekran parçalarını kurar ve seçimi yönetir.
    public sealed class GameUi
    {
        private const string PanelResource = "UI/GeoWarPanel";
        private const string StyleResource = "UI/GeoWar";
        private const float ZoomStep = 1.4f;
        private const float MinFocusZoom = 1.1f;
        private const float HeaderHeight = 86f;
        private const float DockHeight = 52f;
        private const float ProvincePanelHeight = 324f;
        private const float ForeignPanelHeight = 135f;
        private const float CountryPanelHeight = 348f;
        private const int NoSlot = -1;
        private const int NoCell = -1;

        private readonly MapCameraController _camera;
        private readonly MapView _mapView;
        private readonly GameSession _session;
        private readonly IPanel _panel;
        private readonly HudView _hud;
        private readonly MapLabelsView _labels;
        private readonly ProvincePanelView _provincePanel;
        private readonly ForeignPanelView _foreignPanel;
        private readonly CountryPanelView _countryPanel;
        private readonly BuilderView _builder;
        private readonly ResourceWindowView _resourceWindow;
        private readonly ResourceDetailView _resourceDetail;
        private readonly StockView _stock;
        private readonly MarketView _market;
        private readonly PanelSettings _panelSettings;
        private readonly VisualElement _frame;
        private readonly VisualElement _safeTopBar;
        private readonly VisualElement _safeBottomBar;
        private float _safeTop;
        private float _safeBottom;
        private int _screenWidth;
        private int _screenHeight;

        public GameUi(GameObject host, GameSession session, MapView mapView, MapCameraController cameraController)
        {
            _session = session;
            _mapView = mapView;
            _camera = cameraController;

            var documentObject = new GameObject("UI");
            documentObject.transform.SetParent(host.transform, false);
            documentObject.SetActive(false);
            var document = documentObject.AddComponent<UIDocument>();
            // Kopya kullanılır: ölçek kuralı oyun sırasında değişir, dosyadaki ayar bozulmasın.
            _panelSettings = Object.Instantiate(Resources.Load<PanelSettings>(PanelResource));
            _panelSettings.match = MapLayout.PanelMatch;
            document.panelSettings = _panelSettings;
            documentObject.SetActive(true);

            VisualElement documentRoot = document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;
            documentRoot.styleSheets.Add(Resources.Load<StyleSheet>(StyleResource));
            _panel = documentRoot.panel;

            VisualElement root = UiBuilder.Passive(documentRoot, "gw-root");

            var vignette = new VignetteElement();
            vignette.AddToClassList("gw-layer");
            root.Add(vignette);

            _labels = new MapLabelsView(root, session);

            VisualElement frame = UiBuilder.Passive(root, "gw-layer");
            _frame = frame;

            var text = new FacilityText(session);
            _hud = new HudView(
                frame,
                session,
                () => cameraController.AnimateZoom(ZoomStep),
                () => cameraController.AnimateZoom(1f / ZoomStep),
                FitCountry,
                () => JumpToNext(session.ProvinceAlarm, true),
                () => JumpToNext(session.ProvinceReady, false),
                ToggleResource,
                ToggleStock,
                ToggleMarket);

            _provincePanel = new ProvincePanelView(frame, session, text, ClearSelection, OpenCountry, OpenBuilder, OnStateChanged);
            _foreignPanel = new ForeignPanelView(frame, session, ClearSelection, OpenCountry);
            _countryPanel = new CountryPanelView(frame, session, CloseCountry);
            _builder = new BuilderView(frame, session, OnFacilityBuilt, OnStateChanged);
            _resourceDetail = new ResourceDetailView(frame, session);
            _resourceWindow = new ResourceWindowView(frame, session, _resourceDetail, () => _hud.SetOpenCell(NoCell), () => _hud.RebuildBar());
            _stock = new StockView(frame, session, _resourceDetail, () => _hud.SetActiveDock(HudView.NoDock));
            _market = new MarketView(frame, session, _resourceDetail, () => _hud.SetActiveDock(HudView.NoDock), OnStateChanged);

            _safeTopBar = UiBuilder.Box(root, "gw-safe-top");
            _safeBottomBar = UiBuilder.Box(root, "gw-safe-bottom");

            UiBuilder.Passive(root, "gw-grain");
            ApplySafeArea();

            _labels.RefreshAlarms(cameraController.Center, cameraController.Zoom);
            _labels.UpdatePositions(cameraController.Center, cameraController.Zoom);
            cameraController.Changed = false;
        }

        public int SelectedProvince { get; private set; } = MapHitTest.NotFound;

        // Ekran koordinatının altında dokunulabilir bir arayüz parçası var mı?
        public bool IsOverUi(Vector2 screenPoint)
        {
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            return _panel.Pick(panelPoint) != null;
        }

        public void SelectProvince(int province)
        {
            Select(province, NoSlot);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool IsBuilderOpen
        {
            get { return _builder.IsOpen; }
        }

        public int OpenResourceCell
        {
            get { return _resourceWindow.OpenCell; }
        }

        // Tasarım koordinatında ekranda ne varsa ona basar: düğme, dokunuşu tutan panel veya harita.
        public string DebugTap(float x, float y)
        {
            VisualElement picked = _panel.Pick(new Vector2(x, y));
            if (picked == null)
            {
                float scale = MapLayout.UiScale;
                _camera.DebugTap(new Vector2(x * scale, Screen.height - y * scale));
                return "map";
            }

            for (VisualElement element = picked; element != null; element = element.parent)
            {
                var button = element as MetalPanel;
                if (button != null && button.ClickAction != null)
                {
                    button.ClickAction();
                    return "button";
                }
            }

            return "blocked by " + picked.GetType().Name + " " + string.Join(".", picked.GetClasses());
        }

        public string DebugTapText(string text)
        {
            foreach (Label label in _panel.visualTree.Query<Label>().ToList())
            {
                if (IsShown(label) && label.text != null && label.text.Contains(text))
                {
                    Rect area = label.worldBound;
                    return DebugTap(area.center.x, area.center.y) + " at " + area.center.x + "," + area.center.y;
                }
            }

            return "not found";
        }

        public string DebugLabels(bool overflowOnly)
        {
            var items = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (Label label in _panel.visualTree.Query<Label>().ToList())
            {
                if (string.IsNullOrEmpty(label.text) || !IsShown(label))
                {
                    continue;
                }

                Rect area = label.worldBound;
                Vector2 measured = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                bool overflow = measured.x > area.width + 1f && label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap;
                if (overflowOnly && !overflow)
                {
                    continue;
                }

                var json = new JsonText();
                json.Add("text", label.text);
                json.Add("x", area.x);
                json.Add("y", area.y);
                json.Add("w", area.width);
                json.Add("h", area.height);
                json.Add("overflow", overflow);
                items.Append(first ? "" : ",").Append(json.Close());
                first = false;
            }

            return items.Append(']').ToString();
        }

        private static bool IsShown(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }

            return true;
        }
#endif

        // Otomatik test bu ekranı açıp görüntüsünü alır.
        public void OpenBuilderForSelected()
        {
            if (SelectedProvince != MapHitTest.NotFound && _session.IsOwnProvince(SelectedProvince))
            {
                OpenBuilder(SelectedProvince);
            }
        }

        // Otomatik test bu ekranı açıp görüntüsünü alır.
        public void OpenCountryForSelected()
        {
            OpenCountry();
        }

        // Otomatik test bu ekranı açıp görüntüsünü alır.
        public void OpenResourceDetail(int cell)
        {
            if (_resourceWindow.OpenCell != cell)
            {
                ToggleResource(cell);
            }

            _resourceWindow.ShowDetail(_session.Bar.Items[cell]);
        }

        public void OnDayFinished()
        {
            _hud.Refresh();
            _provincePanel.Refresh();
            _foreignPanel.Refresh();
            _countryPanel.Refresh();
            _builder.Refresh();
            _resourceWindow.Refresh();
            _resourceDetail.Refresh();
            _stock.Refresh();
            _market.Refresh();
            _labels.RefreshAlarms(_camera.Center, _camera.Zoom);
        }

        // Alt şeritteki düğme ekranı açar; aynı düğme kapatır, diğeri o ekrana geçer.
        public void ToggleStock()
        {
            bool wasOpen = _stock.IsOpen;
            CloseScreens();
            if (!wasOpen)
            {
                _stock.Open();
                _hud.SetActiveDock(HudView.DockStock);
            }
        }

        public void ToggleMarket()
        {
            bool wasOpen = _market.IsOpen;
            CloseScreens();
            if (!wasOpen)
            {
                _market.Open();
                _hud.SetActiveDock(HudView.DockMarket);
            }
        }

        private void CloseScreens()
        {
            _builder.Close();
            _resourceWindow.Close();
            _stock.Close();
            _market.Close();
        }

        // Aynı kutuya tekrar dokununca pencere kapanır, başka kutuya dokununca o kutuya geçer.
        public void ToggleResource(int cell)
        {
            if (_resourceWindow.OpenCell == cell)
            {
                _resourceWindow.Close();
                return;
            }

            _stock.Close();
            _market.Close();
            _resourceWindow.Open(cell);
            _hud.SetOpenCell(cell);
        }

        public void OnFrame()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
            {
                OnScreenChanged();
            }

            _hud.SetDayProgress(_session.DayProgress);

            if (_camera.Changed)
            {
                _camera.Changed = false;
                _labels.UpdatePositions(_camera.Center, _camera.Zoom);
            }
        }

        // Pencere boyutu oyun sırasında değişirse ölçek, boşluklar ve kamera yeniden hesaplanır.
        private void OnScreenChanged()
        {
            _panelSettings.match = MapLayout.PanelMatch;
            ApplySafeArea();
            _mapView.ApplyLineScale();
            _camera.OnScreenChanged();
        }

        private void ApplySafeArea()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _safeTop = (Screen.height - Screen.safeArea.yMax) / MapLayout.UiScale;
            _safeBottom = Screen.safeArea.yMin / MapLayout.UiScale;
            _frame.style.top = _safeTop;
            _frame.style.bottom = _safeBottom;
            _safeTopBar.style.height = _safeTop;
            _safeBottomBar.style.height = _safeBottom;
        }

        private void Select(int province, int preferredSlot)
        {
            SelectedProvince = province;
            _mapView.Select(province);
            _labels.SelectProvince(province);
            _builder.Close();
            _countryPanel.Close();

            bool hasSelection = province != MapHitTest.NotFound;
            _hud.SetHintVisible(!hasSelection);

            if (!hasSelection)
            {
                _provincePanel.Hide();
                _foreignPanel.Hide();
                _labels.UpdatePositions(_camera.Center, _camera.Zoom);
                return;
            }

            bool isOwn = _session.IsOwnProvince(province);
            if (isOwn)
            {
                _foreignPanel.Hide();
                _provincePanel.Show(province, preferredSlot);
            }
            else
            {
                _provincePanel.Hide();
                _foreignPanel.Show(province);
            }

            FocusOn(province, CityPanelHeight(province));
        }

        private float CityPanelHeight(int province)
        {
            return _session.IsOwnProvince(province) ? ProvincePanelHeight : ForeignPanelHeight;
        }

        private void ClearSelection()
        {
            Select(MapHitTest.NotFound, NoSlot);
        }

        // Ülke paneli şehir panelinin üstünde açılır; geri düğmesi yine o şehre döner.
        private void OpenCountry()
        {
            if (SelectedProvince == MapHitTest.NotFound)
            {
                return;
            }

            _countryPanel.Open(_session.Map.ProvinceCountry[SelectedProvince]);
            FocusOn(SelectedProvince, CountryPanelHeight);
        }

        private void CloseCountry()
        {
            _countryPanel.Close();
            if (SelectedProvince != MapHitTest.NotFound)
            {
                FocusOn(SelectedProvince, CityPanelHeight(SelectedProvince));
            }
        }

        // Seçilen ili haritanın panelin üstünde kalan kısmının ortasına getirir.
        private void FocusOn(int province, float panelHeight)
        {
            float frameHeight = MapLayout.ViewHeight - _safeTop - _safeBottom;
            float visibleTop = HeaderHeight;
            float visibleBottom = frameHeight - DockHeight - panelHeight;
            float focusY = _safeTop + (visibleTop + visibleBottom) * 0.5f;

            float zoom = Mathf.Max(_camera.Zoom, MinFocusZoom);
            var world = new Vector2(_session.Map.CenterX[province], _session.Map.CenterY[province]);
            _camera.AnimateTo(MapCameraController.CenterFor(world, zoom, focusY), zoom);
        }

        private void OpenBuilder(int city)
        {
            _builder.Open(city);
        }

        private void OnStateChanged()
        {
            _hud.Refresh();
        }

        private void OnFacilityBuilt(int slot)
        {
            _provincePanel.ShowSlot(slot);
        }

        // Uyarı düğmesi: sıradaki ile gider ve ilgili tesisi seçer.
        private void JumpToNext(bool[] flags, bool isAlarm)
        {
            int count = flags.Length;
            int start = SelectedProvince == MapHitTest.NotFound ? 0 : SelectedProvince + 1;
            for (int i = 0; i < count; i++)
            {
                int province = (start + i) % count;
                if (flags[province])
                {
                    Select(province, FindSlot(province, isAlarm));
                    return;
                }
            }
        }

        private int FindSlot(int city, bool isAlarm)
        {
            WorldState world = _session.World;
            for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
            {
                bool matches = isAlarm
                    ? world.FacilityAlarm[slot] == WorldState.AlarmActive
                    : FacilityCommands.IsReadyToUpgrade(_session.Definition, world, slot);
                if (matches)
                {
                    return slot;
                }
            }

            return NoSlot;
        }

        private void FitCountry()
        {
            Vector2 world = MapLayout.ToWorld(MapLayout.CountryCenterLon, MapLayout.CountryCenterLat);
            _camera.AnimateTo(MapCameraController.CenterFor(world, MapLayout.StartZoom, MapLayout.OpenFocusY), MapLayout.StartZoom);
        }
    }
}
