using UnityEngine;

namespace GeoWar.Game
{
    // Sahneye elle bir şey eklemeden oyunu başlatır.
    public static class GameBootstrap
    {
        private const string PerformanceTestArgument = "-geowarPerfTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                existing.gameObject.SetActive(false);
            }

            bool isPerformanceTest = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), PerformanceTestArgument) >= 0;
            if (isPerformanceTest)
            {
                new GameObject("PerformanceTest").AddComponent<PerformanceTestRunner>();
            }
            else
            {
                new GameObject("Game").AddComponent<GameRunner>();
            }
        }
    }
}
