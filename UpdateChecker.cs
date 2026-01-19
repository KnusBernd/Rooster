using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
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
        public static HashSet<string> PendingInstalls = new HashSet<string>();

        public static HashSet<string> InstalledPackageIds = new HashSet<string>();

        public static bool IsModInstalled(string guid) => MatchedPackages.ContainsKey(guid);
        public static bool IsPackageInstalled(string fullName) => InstalledPackageIds.Contains(fullName);

        /// <summary>Runs the update check process as a coroutine.</summary>
        public static IEnumerator CheckForUpdates()
        {
            yield return new WaitForSecondsRealtime(2.0f);

            CheckComplete = false;
            
            // Start notification loop
            Coroutine notificationRoutine = RoosterPlugin.Instance.StartCoroutine(KeepAliveNotification());

            yield return ThunderstoreApi.FetchAllPackages((packages, error) => {
                if (error != null) RoosterPlugin.LogError($"Thunderstore Fetch Error: {error}");
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
            InstalledPackageIds.Clear();

            List<ModUpdateInfo> manualUpdates = new List<ModUpdateInfo>();
            List<ModUpdateInfo> autoUpdates = new List<ModUpdateInfo>();

            int pendingRequests = 0;

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string modName = plugin.Metadata.Name;
                string guid = plugin.Metadata.GUID;

                RoosterConfig.RegisterMod(guid, modName);

                ThunderstorePackage matchedPkg = ModMatcher.FindPackage(plugin, CachedPackages);

                if (matchedPkg == null && plugin.Metadata.Name.IndexOf("RemovePlayerPlacements", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RoosterPlugin.LogWarning($"[DEBUG] Failed to find match for 'RemovePlayerPlacements' (GUID: {guid}). Cached Packages: {CachedPackages?.Count}");
                }

                if (matchedPkg != null)
                {
                    // Debug log for RemovePlayerPlacements to trace the issue
                    if (plugin.Metadata.Name.IndexOf("RemovePlayerPlacements", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RoosterPlugin.LogInfo($"[DEBUG] Matched 'RemovePlayerPlacements' to '{matchedPkg.full_name}'. Latest: {matchedPkg.latest?.version_number}");
                    }

                    MatchedPackages[guid] = matchedPkg;
                    InstalledPackageIds.Add(matchedPkg.full_name);

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
                
                if (plugin.Metadata.Name.IndexOf("RemovePlayerPlacements", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                     RoosterPlugin.LogInfo($"[DEBUG] Version Check for {plugin.Metadata.Name}: Local={plugin.Metadata.Version}, Remote={matchedPkg.latest.version_number}, HasUpdate={updateInfo != null}");
                }

                if (updateInfo != null)
                {
                    if (RoosterConfig.IsModIgnored(guid) || UpdateLoopPreventer.IsVersionIgnored(guid, matchedPkg.latest.version_number))
                    {
                        // Update ignored code path
                        RoosterPlugin.LogWarning($"[DEBUG] Update ignored for {modName} ({guid}). ConfigIgnored: {RoosterConfig.IsModIgnored(guid)}, LoopIgnored: {UpdateLoopPreventer.IsVersionIgnored(guid, matchedPkg.latest.version_number)}");
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
                RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(autoUpdates, (info, status) => {}, () => {
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
                try {
                try {
                     // Notification removed as per user request
                } catch {}
                } catch {}
                Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
            }
            else
            {
                try {
                try {
                     // Notification removed as per user request
                } catch {}
                } catch {}
            }
        }



        private static IEnumerator KeepAliveNotification()
        {
            while (!CheckComplete)
            {
                if (UserMessageManager.Instance != null && UserMessageManager.Instance.MessageHolderPrefab != null)
                {
                    // Notification removed as per user request
                }
                yield return new WaitForSecondsRealtime(1.0f);
            }
        }

        /// <summary>Initiates mass update for all pending updates.</summary>
        /// <summary>Initiates mass update for all pending updates.</summary>
        public static void UpdateAll(Action<ModUpdateInfo, string> onStatusUpdate, Action onComplete)
        {
            RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(PendingUpdates, onStatusUpdate, onComplete));
        }

        private static IEnumerator UpdateAllCoroutine(List<ModUpdateInfo> upgradesRaw, Action<ModUpdateInfo, string> onStatusUpdate, Action onComplete)
        {
            // Defensive Copy
            var updates = new List<ModUpdateInfo>(upgradesRaw);
            RoosterPlugin.LogInfo($"Starting Mass Update for {updates.Count} mods.");

            yield return null; // Ensure next frame

            foreach (var update in updates)
            {
                if (string.IsNullOrEmpty(update.DownloadUrl))
                {
                    onStatusUpdate?.Invoke(update, "Skipped (No URL)");
                    continue;
                }

                bool downloadSuccess = false;

                try 
                {
                    onStatusUpdate?.Invoke(update, "Downloading...");
                }
                catch { /* Ignore callback errors */ }

                string cacheDir = Path.Combine(Paths.BepInExRootPath, "cache");
                string zipPath = Path.Combine(cacheDir, $"{update.ModName}_{update.Version}.zip");

                // Yield cannot be in try-catch
                yield return UpdateDownloader.DownloadFile(update.DownloadUrl, zipPath, (success, error) => 
                {
                    downloadSuccess = success;
                    if (!success) 
                    {
                        try { onStatusUpdate?.Invoke(update, "Download Failed"); } catch {}
                    }
                });

                if (downloadSuccess)
                {
                    bool installSuccess = false;
                    try { onStatusUpdate?.Invoke(update, "Installing..."); } catch {}

                    UpdateInstaller.InstallMod(zipPath, update.PluginInfo, (success, error) =>
                    {
                        try 
                        {
                            if (success) UpdateLoopPreventer.RegisterPendingInstall(update.PluginInfo.Metadata.GUID, update.Version);
                            
                            installSuccess = success;
                            if (!success) onStatusUpdate?.Invoke(update, "Install Failed");
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogError($"Install Callback Error: {ex}");
                        }
                    });

                    if (installSuccess) 
                    {
                        try { onStatusUpdate?.Invoke(update, "Ready"); } catch {}
                    }
                }
            }

            try 
            {
                onStatusUpdate?.Invoke(null, "All updates processed. Restart required.");
                RestartRequired = true;
                PendingUpdates.Clear();
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Finalizing Update Error: {ex}");
            }

            // ALWAYS fire completion
            try {
                RoosterPlugin.LogInfo("UpdateAllCoroutine: Invoking onComplete callback.");
                onComplete?.Invoke();
            } catch (Exception ex2) {
                 RoosterPlugin.LogError($"onComplete callback failed: {ex2}");
            }
        }
    }
}
