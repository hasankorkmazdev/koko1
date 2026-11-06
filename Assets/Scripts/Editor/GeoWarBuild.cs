using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.EditorTools
{
    // Proje ayarlarını kurar ve komut satırından derler; Unity arayüzünü açmak gerekmez.
    public static class GeoWarBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ApplicationId = "com.geowar.game";
        private const string ProductName = "GeoWar";
        private const string AndroidOutput = "Build/Android/GeoWar.apk";
        private const string WindowsOutput = "Build/Windows/GeoWar.exe";
        private const string PanelSettingsPath = "Assets/Art/Resources/UI/GeoWarPanel.asset";
        private const string ThemePath = "Assets/Art/Resources/UI/GeoWarTheme.tss";
        private const int DesignWidth = 390;
        private const int DesignHeight = 844;
        private const float MatchWidth = 0f;
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 1;

        public static void BuildAndroid()
        {
            Configure();
            EditorUserBuildSettings.buildAppBundle = false;
            Build(AndroidOutput, BuildTarget.Android);
        }

        public static void BuildWindows()
        {
            Configure();
            Build(WindowsOutput, BuildTarget.StandaloneWindows64);
        }

        private static void Configure()
        {
            PlayerSettings.companyName = ProductName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Sahne boş, her şey koddan kurulduğu için kırpma gereken motor sınıflarını atabiliyor (Class ID 115).
            PlayerSettings.stripEngineCode = false;
            // Ölçüm satırları her yazıldığında yığın izi toplanmasın.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            EnsureScene();
            EnsurePanelSettings();
            AssetDatabase.SaveAssets();
        }

        // Panel ayarı editörde dosya olarak üretilir; UI Toolkit'in gölgelendiricileri derlemeye ancak böyle girer.
        private static void EnsurePanelSettings()
        {
            if (File.Exists(PanelSettingsPath))
            {
                return;
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(DesignWidth, DesignHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = MatchWidth;
            settings.clearColor = false;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
        }

        // Oyun sahnesiz başlamaz; içi boş bir sahne yeter, gerisini GameBootstrap kurar.
        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void Build(string output, BuildTarget target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("GeoWarBuild " + target + ": " + summary.result + ", " + summary.totalSize + " bayt, " + summary.totalTime + ", " + summary.totalErrors + " hata");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? SuccessExitCode : FailureExitCode);
        }
    }
}
