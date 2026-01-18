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

        /// <summary>
        /// Initializes the plugin, loads configuration, and applies Harmony patches.
        /// </summary>
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

        /// <summary>
        /// Starts the update check coroutine.
        /// </summary>
        private void Start()
        {
             StartCoroutine(UpdateChecker.CheckForUpdates());
        }

        /// <summary>
        /// Renders debug UI when an auto-update is in progress.
        /// </summary>
        private void OnGUI()
        {
            if (UpdateChecker.IsAutoUpdating && !string.IsNullOrEmpty(UpdateChecker.AutoUpdateStatus))
            {
                GUI.Box(new Rect(Screen.width - 320, 10, 300, 30), UpdateChecker.AutoUpdateStatus);
            }
        }

        /// <summary>
        /// Deletes old .old files from the plugins directory.
        /// </summary>
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

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string message)
        {
            LoggerInstance.LogInfo(message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public static void LogWarning(string message)
        {
            LoggerInstance.LogWarning(message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public static void LogError(string message)
        {
            LoggerInstance.LogError(message);
        }
    }
}
