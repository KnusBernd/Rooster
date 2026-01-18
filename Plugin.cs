using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Rooster.Patches;
using UnityEngine;

namespace Rooster
{
    [BepInPlugin("de.knusbernd.rooster", "Rooster", "1.0.1")]
    public class RoosterPlugin : BaseUnityPlugin
    {
        internal static RoosterPlugin Instance { get; private set; }
        internal static ManualLogSource LoggerInstance => Instance.Logger;

        private void Awake()
        {
            Instance = this;
            RoosterConfig.Init(Config);
            
            var harmony = new Harmony("de.knusbernd.rooster");
            MainMenuPopupPatch.ApplyPatch(harmony);
            MainMenuButtonPatch.ApplyPatch(harmony);
            harmony.PatchAll(typeof(PickCursorScrollPatch));
            
            CleanupOldFiles();

            LogInfo("Rooster loaded.");
        }

        private void Start()
        {
            StartCoroutine(UpdateChecker.CheckForUpdates());
        }

        private void OnGUI()
        {
            if (UpdateChecker.IsAutoUpdating && !string.IsNullOrEmpty(UpdateChecker.AutoUpdateStatus))
            {
                GUI.Box(new Rect(Screen.width - 320, 10, 300, 30), UpdateChecker.AutoUpdateStatus);
            }
        }

        private void CleanupOldFiles()
        {
            try 
            {
                string pluginsPath = Paths.PluginPath;
                var oldFiles = System.IO.Directory.GetFiles(pluginsPath, "*.old", System.IO.SearchOption.AllDirectories);
                foreach (var f in oldFiles)
                {
                    try { System.IO.File.Delete(f); LogInfo($"Deleted old file: {f}"); } catch { }
                }
            } 
            catch { }
        }

        public static void LogInfo(string message) => LoggerInstance.LogInfo(message);

        public static void LogWarning(string message) => LoggerInstance.LogWarning(message);

        public static void LogError(string message) => LoggerInstance.LogError(message);
    }
}
