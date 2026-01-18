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
            harmony.PatchAll(typeof(MainMenuPopupPatch));
            harmony.PatchAll(typeof(MainMenuButtonPatch));
            harmony.PatchAll(typeof(PickCursorScrollPatch));
            
            CleanupOldFiles();

            LogInfo("Rooster loaded.");
        }

        private void Start()
        {
            // Initialize loop preventer here ensuring all plugins are loaded
            Rooster.Services.UpdateLoopPreventer.Init();
            StartCoroutine(UpdateChecker.CheckForUpdates());
        }

        private void CleanupOldFiles()
        {
            try 
            {
                string pluginsPath = Paths.PluginPath;
                if (!System.IO.Directory.Exists(pluginsPath)) return;

                var oldFiles = System.IO.Directory.GetFiles(pluginsPath, "*.old*", System.IO.SearchOption.AllDirectories);
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
