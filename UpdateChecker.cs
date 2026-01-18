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

            CheckComplete = false;
            
            // Start notification loop
            Coroutine notificationRoutine = RoosterPlugin.Instance.StartCoroutine(KeepAliveNotification());

            RoosterPlugin.LogInfo("Starting Update Check...");

            yield return ThunderstoreApi.FetchAllPackages((packages) => {
                CachedPackages = packages;
            });

            if (CachedPackages.Count == 0)
            {
                RoosterPlugin.LogError("Failed to fetch packages from Thunderstore. Aborting check.");
                CheckComplete = true;
                if (notificationRoutine != null) RoosterPlugin.Instance.StopCoroutine(notificationRoutine);
                yield break;
            }

            PendingUpdates.Clear();
            MatchedPackages.Clear();

            RoosterPlugin.LogInfo($"Scanning {Chainloader.PluginInfos.Count} plugins against {CachedPackages.Count} online packages...");
            
            List<ModUpdateInfo> manualUpdates = new List<ModUpdateInfo>();
            List<ModUpdateInfo> autoUpdates = new List<ModUpdateInfo>();

            int pendingRequests = 0;

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string modName = plugin.Metadata.Name;
                string guid = plugin.Metadata.GUID;

                RoosterConfig.RegisterMod(guid, modName);

                ThunderstorePackage matchedPkg = ModMatcher.FindPackage(plugin, CachedPackages);

                if (matchedPkg != null)
                {
                    RoosterPlugin.LogInfo($"Matched {modName} -> {matchedPkg.full_name}");
                    MatchedPackages[guid] = matchedPkg;

                    // Fetch fresh version from API (bypasses CDN cache) in PARALLEL
                    string[] parts = matchedPkg.full_name.Split('-');
                    if (parts.Length >= 2)
                    {
                        string owner = parts[0];
                        string pkgName = parts[1];
                        
                        pendingRequests++;
                        RoosterPlugin.Instance.StartCoroutine(ThunderstoreApi.FetchFreshVersion(owner, pkgName, (ver, url) => {
                            if (!string.IsNullOrEmpty(ver))
                            {
                                matchedPkg.latest.version_number = ver;
                                if (!string.IsNullOrEmpty(url)) matchedPkg.latest.download_url = url;
                            }
                            pendingRequests--;
                        }));
                    }
                }
                else
                {
                    RoosterPlugin.LogWarning($"Could not find matching Thunderstore package for installed plugin: {modName} ({guid})");
                }
            }

            // Wait for all parallel requests to complete
            while (pendingRequests > 0)
            {
                yield return null;
            }

            // Process versions AFTER all fresh data is fetched
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string guid = plugin.Metadata.GUID;
                if (!MatchedPackages.TryGetValue(guid, out var matchedPkg)) continue;
                string modName = plugin.Metadata.Name;

                var updateInfo = VersionComparer.CheckForUpdate(plugin, matchedPkg);
                if (updateInfo != null)
                {
                    if (RoosterConfig.IsModIgnored(guid) || UpdateLoopPreventer.IsVersionIgnored(guid, matchedPkg.latest.version_number))
                    {
                        RoosterPlugin.LogInfo($"Skipping update for ignored mod: {modName}");
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
            
            if (autoUpdates.Count > 0)
            {
                PendingUpdates.AddRange(autoUpdates);
                RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(autoUpdates, (status) => {}, () => {
                    RestartRequired = true;
                    Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
                }));
            }

            PendingUpdates = manualUpdates;
            CheckComplete = true;
            RoosterPlugin.LogInfo($"Update Check Complete. Found {manualUpdates.Count} manual updates and {autoUpdates.Count} auto updates.");
            
            if (notificationRoutine != null) RoosterPlugin.Instance.StopCoroutine(notificationRoutine);
            
            if (manualUpdates.Count > 0)
            {
                Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
            }
        }

        private static IEnumerator KeepAliveNotification()
        {
            while (!CheckComplete)
            {
                // Pulse the notification every second to keep it alive
                // We use reflection/TryCatch just in case UserMessageManager isn't ready
                try
                {
                    if (UserMessageManager.Instance != null && UserMessageManager.Instance.MessageHolderPrefab != null)
                    {
                         // Use a short duration (2s) and refresh it every 1s
                        UserMessageManager.Instance.UserMessage("Checking for updates...", 2.0f, UserMessageManager.UserMsgPriority.lo, false);
                    }
                }
                catch { }

                yield return new WaitForSecondsRealtime(1.0f);
            }
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
                    onStatusUpdate?.Invoke($"Skipping {update.ModName} (No URL)");
                    continue;
                }

                onStatusUpdate?.Invoke($"Downloading {update.ModName}...");

                string cacheDir = Path.Combine(Paths.BepInExRootPath, "cache");
                string zipPath = Path.Combine(cacheDir, $"{update.ModName}_{update.Version}.zip");

                bool downloadSuccess = false;
                yield return UpdateDownloader.DownloadFile(update.DownloadUrl, zipPath, (success, error) => 
                {
                    downloadSuccess = success;
                    if (!success) onStatusUpdate?.Invoke($"Download failed: {error}");
                });

                if (downloadSuccess)
                {
                    onStatusUpdate?.Invoke($"Installing {update.ModName}...");
                    bool installSuccess = false;
                    
                    UpdateInstaller.InstallMod(zipPath, update.PluginInfo, (success, error) =>
                    {
                        if (success) UpdateLoopPreventer.RegisterPendingInstall(update.PluginInfo.Metadata.GUID, update.Version);
                        
                        installSuccess = success;
                        if (!success) onStatusUpdate?.Invoke($"Install failed: {error}");
                    });

                    if (installSuccess) onStatusUpdate?.Invoke($"Updated {update.ModName}!");
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
