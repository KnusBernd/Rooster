using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ThunderstoreUpdateChecker.Patches;

namespace ThunderstoreUpdateChecker
{
    [BepInPlugin("ThunderstoreUpdateChecker", "Thunderstore Update Checker", "1.0.0")]
    public class ThunderstoreUpdateCheckerPlugin : BaseUnityPlugin
    {
        internal static ThunderstoreUpdateCheckerPlugin Instance { get; private set; }
        internal static ManualLogSource LoggerInstance => Instance.Logger;

        private void Awake()
        {
            Instance = this;
            var harmony = new Harmony("ThunderstoreUpdateChecker");
            MainMenuPopupPatch.ApplyPatch(harmony);
            
            LogInfo("Plugin loaded.");
        }

        private void Start()
        {
             StartCoroutine(UpdateChecker.CheckForUpdates());
        }

        public static void LogInfo(string message)
        {
            LoggerInstance.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            LoggerInstance.LogWarning(message);
        }

        public static void LogError(string message)
        {
            LoggerInstance.LogError(message);
        }
    }
}
