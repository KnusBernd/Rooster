using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Rooster.Patches;
using Rooster.Services;
using UnityEngine;

namespace Rooster
{
    [BepInPlugin("de.knusbernd.rooster", "Rooster", "1.0.2")]
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

            LogInfo($"Rooster loaded. [Build: {System.DateTime.Now}]");
        }

        private void Start()
        {
            UpdateLoopPreventer.Init();
            StartCoroutine(UpdateChecker.CheckForUpdates());
            StartCoroutine(GitHubApi.BuildCache());

            if (RoosterConfig.DeveloperMode.Value)
            {
                gameObject.AddComponent<Rooster.UI.DeveloperUI>();
                LogInfo($"Developer Tools Enabled. Press '{RoosterConfig.DeveloperKey.Value}' to toggle.");
            }
        }

        /// <summary>
        /// Removes temporary backup files (.old, .deleted) created during hot-swapping updates and mod uninstallation.
        /// </summary>
        private void CleanupOldFiles()
        {
            string pluginsPath = Paths.GameRootPath;
            if (!System.IO.Directory.Exists(pluginsPath)) return;

            try
            {
                var oldFiles = System.IO.Directory.GetFiles(pluginsPath, "*.old*", System.IO.SearchOption.AllDirectories)
                    .Concat(System.IO.Directory.GetFiles(pluginsPath, "*.deleted*", System.IO.SearchOption.AllDirectories));

                int deletedCount = 0;
                foreach (var f in oldFiles)
                {
                    try
                    {
                        System.IO.File.Delete(f);
                        LogInfo($"[Cleanup] Deleted stale file: {System.IO.Path.GetFileName(f)}");
                        deletedCount++;
                    }
                    catch { }
                }
                if (deletedCount > 0) LogInfo($"[Cleanup] Removed {deletedCount} stale files.");
            }
            catch { }
        }

        public static void LogInfo(string message) => LoggerInstance.LogInfo(message);
        public static void LogWarning(string message) => LoggerInstance.LogWarning(message);
        public static void LogError(string message) => LoggerInstance.LogError(message);
    }
}
