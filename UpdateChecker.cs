using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using System.Linq;
using Rooster.Models;
using Rooster.Services;

namespace Rooster
{
    public class UpdateChecker
    {

        public static bool CheckComplete = false;
        public static List<ThunderstorePackage> CachedPackages = new List<ThunderstorePackage>();
        public static Dictionary<string, ThunderstorePackage> MatchedPackages = new Dictionary<string, ThunderstorePackage>(StringComparer.OrdinalIgnoreCase);
        public static bool RestartRequired = false;

        public static List<ModUpdateInfo> PendingUpdates = new List<ModUpdateInfo>();

        /// <summary>Runs the update check process as a coroutine.</summary>
        public static IEnumerator CheckForUpdates()
        {
            yield return new WaitForSecondsRealtime(2.0f);

            RoosterPlugin.LogInfo("Starting Update Check (Auto-Discovery)...");

            yield return ThunderstoreApi.FetchAllPackages((packages) => {
                CachedPackages = packages;
            });

            if (CachedPackages.Count == 0)
            {
                RoosterPlugin.LogError("Failed to fetch packages from Thunderstore. Aborting check.");
                CheckComplete = true;
                yield break;
            }


            PendingUpdates.Clear();
            MatchedPackages.Clear();
            CheckComplete = false;

            RoosterPlugin.LogInfo($"Scanning {Chainloader.PluginInfos.Count} plugins against {CachedPackages.Count} online packages...");
            
            List<ModUpdateInfo> manualUpdates = new List<ModUpdateInfo>();
            List<ModUpdateInfo> autoUpdates = new List<ModUpdateInfo>();

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string modName = plugin.Metadata.Name;
                string guid = plugin.Metadata.GUID;

                RoosterConfig.RegisterMod(guid, modName);

                // Removed early continue so matchedPkg and green star still populate
                // if (RoosterConfig.IsModIgnored(guid)) { ... continue; }

                ThunderstorePackage matchedPkg = ModMatcher.FindPackage(plugin, CachedPackages);

                if (matchedPkg != null)
                {
                    RoosterPlugin.LogInfo($"Matched {modName} -> {matchedPkg.full_name}");
                    MatchedPackages[guid] = matchedPkg;

                    var updateInfo = VersionComparer.CheckForUpdate(plugin, matchedPkg);
                    if (updateInfo != null)
                    {
                        if (RoosterConfig.IsModIgnored(guid))
                        {
                            RoosterPlugin.LogInfo($"Skipping update for ignored mod: {modName}");
                            // Do not add to autoUpdates or manualUpdates
                        }
                        else if (RoosterConfig.IsModAutoUpdate(guid))
                        {
                            RoosterPlugin.LogInfo($"Auto-Update triggered for {modName}");
                            autoUpdates.Add(updateInfo);
                        }
                        else
                        {
                            manualUpdates.Add(updateInfo);
                        }
                    }
                }
            }

            if (autoUpdates.Count > 0)
            {
                RoosterPlugin.LogInfo($"Processing {autoUpdates.Count} auto-updates...");
                PendingUpdates.AddRange(autoUpdates);
                

                RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(autoUpdates, (status) => {}, () => {

                    RestartRequired = true;
                    RoosterPlugin.LogInfo("Auto-Updates complete.");
                    Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
                }));
            }

            PendingUpdates = manualUpdates;


            CheckComplete = true;
            RoosterPlugin.LogInfo($"Update Check Complete. Found {manualUpdates.Count} manual updates and {autoUpdates.Count} auto updates.");
        }



        /// <summary>Initiates mass update for all pending updates.</summary>
        public static void UpdateAll(Action<string> onStatusUpdate, Action onComplete)
        {
            RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(PendingUpdates, onStatusUpdate, onComplete));
        }

        private static IEnumerator UpdateAllCoroutine(List<ModUpdateInfo> updates, Action<string> onStatusUpdate, Action onComplete)
        {
            RoosterPlugin.LogInfo($"Starting Mass Update for {updates.Count} mods.");
            
            foreach (var update in updates)
            {
                if (string.IsNullOrEmpty(update.DownloadUrl))
                {
                    string msg = $"Skipping {update.ModName} (No URL)";
                    RoosterPlugin.LogWarning(msg);
                    onStatusUpdate?.Invoke(msg);

                    continue;
                }

                RoosterPlugin.LogInfo($"Processing update for {update.ModName}...");
                string statusMsg = $"Downloading {update.ModName}...";
                onStatusUpdate?.Invoke(statusMsg);


                string cacheDir = Path.Combine(Path.GetDirectoryName(typeof(RoosterPlugin).Assembly.Location), "cache");
                string zipPath = Path.Combine(cacheDir, $"{update.ModName}_{update.Version}.zip");

                bool downloadSuccess = false;
                yield return UpdateDownloader.DownloadFile(update.DownloadUrl, zipPath, (success, error) => 
                {
                    downloadSuccess = success;
                    if (!success) 
                    {
                        onStatusUpdate?.Invoke($"Download failed for {update.ModName}: {error}");
                    }
                });

                if (downloadSuccess)
                {
                    onStatusUpdate?.Invoke($"Installing {update.ModName}...");
                    bool installSuccess = false;
                    UpdateInstaller.InstallMod(zipPath, update.PluginInfo, (success, error) =>
                    {
                        installSuccess = success;
                        if (!success)
                        {
                            onStatusUpdate?.Invoke($"Install failed for {update.ModName}: {error}");
                        }
                    });

                    if (installSuccess)
                    {
                        onStatusUpdate?.Invoke($"Updated {update.ModName}!");
                    }
                }
            }

            onStatusUpdate?.Invoke("All updates processed. Restart required.");
            
            RestartRequired = true;

            PendingUpdates.Clear();
            
            yield return null; 
            onComplete?.Invoke();
        }
    }
}
