using System.Diagnostics;
using System.Globalization;
using GeoWar.Map;
using UnityEngine;

namespace GeoWar.Game
{
    // Oyunun ana döngüsü: harita, kamera ve arayüzü kurar; zamanı akıtır.
    public sealed class GameRunner : MonoBehaviour
    {
        private const float MaxSimulationDeltaSeconds = 0.1f;
        private const float LogIntervalSeconds = 5f;
        private const float SlowFrameSeconds = 0.02f;
        private const string TestSecondsArgument = "-geowarTestSeconds";
        private const string ScreenshotArgument = "-geowarScreenshot";
        private const string TestViewArgument = "-geowarTestView";
        private const string TestViewBuilder = "builder";
        private const string TestViewForeign = "foreign";
        private const string TestViewCountry = "country";
        private const string TestViewOwnCountry = "own-country";
        private const string TestViewResource = "resource";
        private const string TestViewResourceDetail = "resource-detail";
        private const string TestViewStock = "stock";
        private const string TestViewMarket = "market";
        private const string TestViewRebellion = "rebellion";
        private const string TestViewRebelState = "rebel-state";
        private const string TestViewRebelMap = "rebel-map";
        private const int TestResourceCell = 1;
        private const int TestRebellionCities = 3;
        private const float ForeignTestLon = 44.4f;
        private const float ForeignTestLat = 33.3f;
        private const float TestTapFraction = 0.5f;
        private const float TestScreenshotFraction = 0.9f;
        private const float HeaderProbeY = 20f;

        private GameSession _session;
        private MapView _mapView;
        private MapCameraController _cameraController;
        private GameUi _ui;
        private Camera _mainCamera;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Geliştirme aracı oyuna buradan ulaşır.
        public static GameRunner Current { get; private set; }

        public GameSession Session
        {
            get { return _session; }
        }

        public GameUi Ui
        {
            get { return _ui; }
        }

        public MapCameraController CameraController
        {
            get { return _cameraController; }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
#endif
        private int _appliedSpeed = -1;

        private float _logTime;
        private int _logFrames;
        private int _logSlowFrames;
        private float _logWorstFrame;
        private long _logSimTicks;
        private long _logWorstSimTicks;

        private float _testSeconds;
        private float _testElapsed;
        private string _screenshotPath;
        private string _testView;
        private bool _testTapped;
        private bool _screenshotTaken;
        private bool _pickTested;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            ReadTestArguments();

            long start = Stopwatch.GetTimestamp();
            _session = GameSession.Load();
            long sessionLoaded = Stopwatch.GetTimestamp();

            _mapView = new GameObject("Map").AddComponent<MapView>();
            _mapView.Build(_session.Map, _session.Relations, _session.ProvinceOwner);
            long mapBuilt = Stopwatch.GetTimestamp();

            _mainCamera = CreateCamera();
            _cameraController = gameObject.AddComponent<MapCameraController>();
            Vector2 world = MapLayout.ToWorld(MapLayout.CountryCenterLon, MapLayout.CountryCenterLat);
            _cameraController.Configure(_mainCamera, world, MapLayout.StartZoom, MapLayout.OpenFocusY);
            _cameraController.Tapped += OnMapTapped;

            _ui = new GameUi(gameObject, _session, _mapView, _cameraController);
            _cameraController.IsOverUi = _ui.IsOverUi;
            long uiBuilt = Stopwatch.GetTimestamp();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Current = this;
#endif

            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            UnityEngine.Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "GeoWarStartup sessionMs={0:F0} mapMs={1:F0} uiMs={2:F0} vertices={3} triangles={4}",
                (sessionLoaded - start) * ticksToMs,
                (mapBuilt - sessionLoaded) * ticksToMs,
                (uiBuilt - mapBuilt) * ticksToMs,
                _mapView.VertexCount,
                _mapView.TriangleCount));
        }

        private static Camera CreateCamera()
        {
            var mapCamera = new GameObject("MapCamera").AddComponent<Camera>();
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = MapPalette.Sea;
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane = 100f;
            return mapCamera;
        }

        private void OnMapTapped(Vector2 worldPoint)
        {
            _ui.SelectProvince(MapHitTest.FindProvince(_session.Map, worldPoint.x, worldPoint.y));
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            ApplySleepSetting();
            _cameraController.Tick();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GameDebugApi.Pump != null)
            {
                GameDebugApi.Pump();
            }
#endif

            // Açılış veya arka plandan dönüş gibi uzun bir kare, hesabı bir anda günlerce ileri atmasın.
            long start = Stopwatch.GetTimestamp();
            int finishedDays = _session.Advance(Mathf.Min(deltaTime, MaxSimulationDeltaSeconds));
            long simTicks = Stopwatch.GetTimestamp() - start;

            if (finishedDays > 0)
            {
                _ui.OnDayFinished();
            }

            _ui.OnFrame();
            TrackFrame(deltaTime, simTicks);
            ProbePicking();

            if (_testSeconds > 0f)
            {
                RunTestStep(deltaTime);
            }
        }

        // Zaman akarken ekran kapanmasın; oyun durunca telefonun kendi ayarı geçerli olsun.
        private void ApplySleepSetting()
        {
            if (_appliedSpeed != _session.Speed)
            {
                _appliedSpeed = _session.Speed;
                Screen.sleepTimeout = _appliedSpeed > 0 ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            }
        }

        private void TrackFrame(float deltaTime, long simTicks)
        {
            _logTime += deltaTime;
            _logFrames++;
            _logWorstFrame = Mathf.Max(_logWorstFrame, deltaTime);
            _logSimTicks += simTicks;
            _logWorstSimTicks = System.Math.Max(_logWorstSimTicks, simTicks);
            if (deltaTime > SlowFrameSeconds)
            {
                _logSlowFrames++;
            }

            if (_logTime < LogIntervalSeconds)
            {
                return;
            }

            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            UnityEngine.Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "GeoWarPerf fps={0:F1} worstFrameMs={1:F1} slowFrames={2} simMsPerFrame={3:F3} worstSimMs={4:F2} day={5} speed={6} gc={7} camX={8:F0} camY={9:F0} zoom={10:F2} selected={11} alarm={12}",
                _logFrames / _logTime,
                _logWorstFrame * 1000f,
                _logSlowFrames,
                _logSimTicks * ticksToMs / _logFrames,
                _logWorstSimTicks * ticksToMs,
                _session.World.Day,
                _session.Speed,
                System.GC.CollectionCount(0),
                _cameraController.Center.x,
                _cameraController.Center.y,
                _cameraController.Zoom,
                _ui.SelectedProvince == MapHitTest.NotFound ? "-" : _session.Map.ProvinceNames[_ui.SelectedProvince],
                _session.AlarmProvinceCount));

            _logTime = 0f;
            _logFrames = 0;
            _logSlowFrames = 0;
            _logWorstFrame = 0f;
            _logSimTicks = 0;
            _logWorstSimTicks = 0;
        }

        // Yerleşim bitince bir kez: harita boşluğu dokunuşu geçirmeli, üst bar tutmalı.
        private void ProbePicking()
        {
            if (_pickTested || _logTime < 1f)
            {
                return;
            }

            _pickTested = true;
            bool overCenter = _ui.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            float safeTop = Screen.height - Screen.safeArea.yMax;
            bool overHeader = _ui.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height - safeTop - HeaderProbeY * MapLayout.UiScale));
            UnityEngine.Debug.Log("GeoWarUiPick center=" + overCenter + " header=" + overHeader);
        }

        private void ReadTestArguments()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == TestSecondsArgument)
                {
                    float.TryParse(arguments[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out _testSeconds);
                }
                else if (arguments[i] == ScreenshotArgument)
                {
                    _screenshotPath = arguments[i + 1];
                }
                else if (arguments[i] == TestViewArgument)
                {
                    _testView = arguments[i + 1];
                }
            }

            // Editörde başka pencereye geçilince de çalışsın: geliştirme aracı arka planda komut gönderir.
            Application.runInBackground = _testSeconds > 0f || Application.isEditor;
        }

        // Komut satırıyla açılan test: ortada bir dokunuş dener, ekran görüntüsü alır, uygulamayı kapatır.
        private void RunTestStep(float deltaTime)
        {
            _testElapsed += deltaTime;
            if (!_testTapped && _testElapsed >= _testSeconds * TestTapFraction)
            {
                _testTapped = true;
                bool isForeign = _testView == TestViewForeign || _testView == TestViewCountry;
                OnMapTapped(isForeign
                    ? MapLayout.ToWorld(ForeignTestLon, ForeignTestLat)
                    : MapLayout.ToWorld(MapLayout.CountryCenterLon, MapLayout.CountryCenterLat));
                if (_testView == TestViewBuilder)
                {
                    _ui.OpenBuilderForSelected();
                }
                else if (_testView == TestViewCountry || _testView == TestViewOwnCountry)
                {
                    _ui.OpenCountryForSelected();
                }
                else if (_testView == TestViewResource)
                {
                    _ui.ToggleResource(TestResourceCell);
                }
                else if (_testView == TestViewResourceDetail)
                {
                    _ui.OpenResourceDetail(TestResourceCell);
                }
                else if (_testView == TestViewStock)
                {
                    _ui.ToggleStock();
                }
                else if (_testView == TestViewMarket)
                {
                    _ui.ToggleMarket();
                }
                else if (_testView == TestViewRebellion)
                {
                    _session.StartRebellionsForTest(TestRebellionCities);
                    _ui.OnDayFinished();
                }
                else if (_testView == TestViewRebelState)
                {
                    _session.LoseCitiesForTest(TestRebellionCities);
                    _ui.OnDayFinished();
                }
                else if (_testView == TestViewRebelMap)
                {
                    _session.LoseCitiesForTest(TestRebellionCities);
                    _ui.OnDayFinished();
                    _ui.CloseRebellions();
                    _ui.SelectProvince(MapHitTest.NotFound);
                }
            }

            if (!_screenshotTaken && _screenshotPath != null && _testElapsed >= _testSeconds * TestScreenshotFraction)
            {
                _screenshotTaken = true;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
            }

            if (_testElapsed >= _testSeconds)
            {
                Application.Quit();
            }
        }
    }
}
