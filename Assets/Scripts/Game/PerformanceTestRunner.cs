using System.Diagnostics;
using System.Globalization;
using GeoWar.Map;
using GeoWar.Simulation;
using UnityEngine;

namespace GeoWar.Game
{
    // İlk performans testi: gerçek harita çizilirken dünya ölçeğinde ekonomi 3x hızda ülkelere dağıtılarak hesaplanır.
    // Ekranda FPS, kare süresi ve bir günün hesap süresi yazar. Arayüz gelince oyun başlatıcısı bunun yerini alır.
    public sealed class PerformanceTestRunner : MonoBehaviour
    {
        private const string EconomyResource = "economy";
        private const int DaysPerSecond = 3;
        private const float ReportIntervalSeconds = 0.5f;
        private const float StartLon = 35.5f;
        private const float StartLat = 39f;
        private const float LonScale = 30f;
        private const float LatScale = 38.6f;
        private const float MaxSimulationDeltaSeconds = 0.1f;
        private const int OverlayFontDivisor = 32;
        private const int OverlayMargin = 24;
        private const float LogIntervalSeconds = 5f;
        private const float SlowFrameSeconds = 0.02f;
        private const string TestSecondsArgument = "-geowarTestSeconds";
        private const string ScreenshotArgument = "-geowarScreenshot";
        private const float TestTapFraction = 0.5f;
        private const float TestScreenshotFraction = 0.9f;

        private static readonly Color SeaColor = new Color32(31, 47, 51, 255);

        private MapData _map;
        private MapView _mapView;
        private Camera _mainCamera;
        private MapCameraController _cameraController;
        private EconomyDefinition _definition;
        private WorldState _world;
        private GUIStyle _style;
        private string _report = "Yükleniyor";
        private string _selectedName = "-";

        private float _countryBudget;
        private int _nextCountry;
        private long _dayTicks;
        private long _lastDayTicks;
        private long _worstDayTicks;
        private long _simFrameTicks;
        private long _worstSimFrameTicks;

        private float _intervalTime;
        private int _intervalFrames;
        private float _worstFrameTime;

        private float _logTime;
        private int _logFrames;
        private float _logWorstFrameTime;
        private long _logWorstSimFrameTicks;
        private long _logWorstDayTicks;
        private int _logSlowFrames;

        private float _testSeconds;
        private float _testElapsed;
        private string _screenshotPath;
        private bool _testTapped;
        private bool _screenshotTaken;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            // Uzun ölçüm sırasında telefonun ekranı kapanıp oyunu durdurmasın.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            ReadTestArguments();

            long startupStart = Stopwatch.GetTimestamp();
            _map = GameSession.LoadMap();

            long mapLoaded = Stopwatch.GetTimestamp();
            var economyAsset = Resources.Load<TextAsset>(EconomyResource);
            _definition = EconomyDefinition.Build(JsonUtility.FromJson<EconomyData>(economyAsset.text));
            _world = WorldGenerator.Create(_definition, new WorldGeneratorSettings());

            long worldCreated = Stopwatch.GetTimestamp();
            var mapObject = new GameObject("Map");
            _mapView = mapObject.AddComponent<MapView>();
            _mapView.Build(_map, GameSession.BuildRelations(_map, GameSession.LoadUi()));
            long meshBuilt = Stopwatch.GetTimestamp();

            _mainCamera = CreateCamera();
            var controller = gameObject.AddComponent<MapCameraController>();
            controller.Configure(_mainCamera, new Vector2(StartLon * LonScale, StartLat * LatScale), MapLayout.StartZoom, MapLayout.OpenFocusY);
            controller.Tapped += OnMapTapped;
            _cameraController = controller;

            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            UnityEngine.Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "GeoWarStartup mapMs={0:F0} worldMs={1:F0} meshMs={2:F0}",
                (mapLoaded - startupStart) * ticksToMs,
                (worldCreated - mapLoaded) * ticksToMs,
                (meshBuilt - worldCreated) * ticksToMs));
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
            }

            // Test, pencere odağını kaybetse de durmadan bitsin.
            Application.runInBackground = _testSeconds > 0f;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("MapCamera");
            var mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = SeaColor;
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane = 100f;
            return mapCamera;
        }

        private void OnMapTapped(Vector2 worldPoint)
        {
            int province = MapHitTest.FindProvince(_map, worldPoint.x, worldPoint.y);
            _mapView.Select(province);
            _selectedName = province == MapHitTest.NotFound ? "-" : _map.ProvinceNames[province];
        }

        private void Update()
        {
            _cameraController.Tick();

            // Açılış veya arka plandan dönüş gibi uzun bir kare, hesabı bir anda günlerce ileri atmasın.
            AdvanceSimulation(Mathf.Min(Time.unscaledDeltaTime, MaxSimulationDeltaSeconds));
            TrackFrame(Time.unscaledDeltaTime);
            if (_testSeconds > 0f)
            {
                RunTestStep(Time.unscaledDeltaTime);
            }
        }

        // Komut satırıyla açılan test: ortada bir dokunuş dener, ekran görüntüsü alır, uygulamayı kapatır.
        private void RunTestStep(float deltaTime)
        {
            _testElapsed += deltaTime;
            if (!_testTapped && _testElapsed >= _testSeconds * TestTapFraction)
            {
                _testTapped = true;
                OnMapTapped(new Vector2(StartLon * LonScale, StartLat * LatScale));
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

        // Günün hesabı karelere bölünür, böylece hiçbir kare tüm dünyayı tek başına hesaplamaz.
        private void AdvanceSimulation(float deltaTime)
        {
            long start = Stopwatch.GetTimestamp();

            _countryBudget += _world.CountryCount * DaysPerSecond * deltaTime;
            while (_countryBudget >= 1f)
            {
                _countryBudget -= 1f;
                EconomySimulator.AdvanceCountry(_definition, _world, _nextCountry);
                _nextCountry++;
                if (_nextCountry == _world.CountryCount)
                {
                    EconomySimulator.FinishDay(_definition, _world);
                    _nextCountry = 0;
                    _lastDayTicks = _dayTicks + (Stopwatch.GetTimestamp() - start);
                    _worstDayTicks = System.Math.Max(_worstDayTicks, _lastDayTicks);
                    _logWorstDayTicks = System.Math.Max(_logWorstDayTicks, _lastDayTicks);
                    _dayTicks = -(Stopwatch.GetTimestamp() - start);
                }
            }

            long elapsed = Stopwatch.GetTimestamp() - start;
            _dayTicks += elapsed;
            _simFrameTicks = elapsed;
            _worstSimFrameTicks = System.Math.Max(_worstSimFrameTicks, elapsed);
        }

        // Telefonda ekrana bakmadan okunabilsin diye beş saniyede bir tek satır günlüğe yazar.
        private void TrackLog(float deltaTime)
        {
            _logTime += deltaTime;
            _logFrames++;
            _logWorstFrameTime = Mathf.Max(_logWorstFrameTime, deltaTime);
            _logWorstSimFrameTicks = System.Math.Max(_logWorstSimFrameTicks, _simFrameTicks);
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
                "GeoWarPerf fps={0:F1} worstFrameMs={1:F1} slowFrames={2} dayMs={3:F2} worstDayMs={4:F2} worstSimFrameMs={5:F2} day={6} gc={7} camX={8:F0} camY={9:F0} camSize={10:F0} selected={11}",
                _logFrames / _logTime,
                _logWorstFrameTime * 1000f,
                _logSlowFrames,
                _lastDayTicks * ticksToMs,
                _logWorstDayTicks * ticksToMs,
                _logWorstSimFrameTicks * ticksToMs,
                _world.Day,
                System.GC.CollectionCount(0),
                _mainCamera.transform.position.x,
                _mainCamera.transform.position.y,
                _mainCamera.orthographicSize,
                _selectedName));

            _logTime = 0f;
            _logFrames = 0;
            _logWorstFrameTime = 0f;
            _logWorstSimFrameTicks = 0;
            _logWorstDayTicks = 0;
            _logSlowFrames = 0;
        }

        private void TrackFrame(float deltaTime)
        {
            TrackLog(deltaTime);
            _intervalTime += deltaTime;
            _intervalFrames++;
            _worstFrameTime = Mathf.Max(_worstFrameTime, deltaTime);
            if (_intervalTime < ReportIntervalSeconds)
            {
                return;
            }

            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            _report = "FPS " + (_intervalFrames / _intervalTime).ToString("F0")
                + "\nEn yavaş kare " + (_worstFrameTime * 1000f).ToString("F1") + " ms"
                + "\nBir gün " + (_lastDayTicks * ticksToMs).ToString("F2") + " ms, en kötü " + (_worstDayTicks * ticksToMs).ToString("F2") + " ms"
                + "\nKarede hesap " + (_simFrameTicks * ticksToMs).ToString("F2") + " ms, en kötü " + (_worstSimFrameTicks * ticksToMs).ToString("F2") + " ms"
                + "\nGün " + _world.Day + ", " + _world.FacilityCount + " tesis"
                + "\nKöşe " + _mapView.VertexCount + ", üçgen " + _mapView.TriangleCount
                + "\nSeçili: " + _selectedName;

            _intervalTime = 0f;
            _intervalFrames = 0;
            _worstFrameTime = 0f;
            _worstSimFrameTicks = 0;
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Screen.width / OverlayFontDivisor };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(OverlayMargin, OverlayMargin, Screen.width - OverlayMargin * 2, Screen.height / 2f), _report, _style);
        }
    }
}
