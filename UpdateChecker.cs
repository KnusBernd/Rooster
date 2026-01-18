using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using System.Linq;
using Rooster.Models;
using Rooster.Services;

namespace Rooster
{
    /// <summary>
    /// Orchestrates the update check process.
    /// Manages the full lifecycle of fetching packages, matching them to installed plugins,
    /// and identifying available updates.
    /// </summary>
    public class UpdateChecker
    {
        public static List<string> UpdatesAvailable = new List<string>();
        public static bool CheckComplete = false;
        public static List<ThunderstorePackage> CachedPackages = new List<ThunderstorePackage>();
        public static Dictionary<string, ThunderstorePackage> MatchedPackages = new Dictionary<string, ThunderstorePackage>(StringComparer.OrdinalIgnoreCase);
        public static bool RestartRequired = false;
        public static bool IsAutoUpdating = false;
        public static string AutoUpdateStatus = "";
        public static List<ModUpdateInfo> PendingUpdates = new List<ModUpdateInfo>();

        /// <summary>
        /// Coroutine that runs the update check process.
        /// Fetches packages from Thunderstore, matches them with local plugins, and identifies updates.
        /// </summary>
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

            
            ConcurrentDictionary<string, ThunderstorePackage> packageMap = new ConcurrentDictionary<string, ThunderstorePackage>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in CachedPackages)
            {
                if (!packageMap.ContainsKey(pkg.name))
                {
                    packageMap[pkg.name] = pkg;
                }
            }
            
            UpdatesAvailable.Clear();
            PendingUpdates.Clear();
            MatchedPackages.Clear();
            CheckComplete = false;

            RoosterPlugin.LogInfo($"Scanning {Chainloader.PluginInfos.Count} plugins against {packageMap.Count} online packages...");
            
            List<ModUpdateInfo> manualUpdates = new List<ModUpdateInfo>();
            List<ModUpdateInfo> autoUpdates = new List<ModUpdateInfo>();

            // Iterate installed plugins
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string modName = plugin.Metadata.Name;
                string guid = plugin.Metadata.GUID;

                if (RoosterConfig.IsModIgnored(guid))
                {
                    RoosterPlugin.LogInfo($"Skipping ignored mod: {modName}");
                    continue;
                }

                RoosterConfig.RegisterMod(guid, modName);
                
                
                ThunderstorePackage matchedPkg = ModMatcher.FindPackage(plugin, CachedPackages);

                if (matchedPkg != null)
                {
                    RoosterPlugin.LogInfo($"Matched {modName} -> {matchedPkg.full_name}");
                    MatchedPackages[guid] = matchedPkg;

                    var updateInfo = VersionComparer.CheckForUpdate(plugin, matchedPkg);
                    if (updateInfo != null)
                    {
                        // Check auto-update configuration
                        if (RoosterConfig.IsDataAutoUpdate(guid, matchedPkg.full_name))
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

            
            // Execute pending auto-updates
            if (autoUpdates.Count > 0)
            {
                RoosterPlugin.LogInfo($"Processing {autoUpdates.Count} auto-updates...");
                PendingUpdates.AddRange(autoUpdates);
                
                IsAutoUpdating = true;
                RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(autoUpdates, (status) => {}, () => {
                    IsAutoUpdating = false;
                    RestartRequired = true;
                    RoosterPlugin.LogInfo("Auto-Updates complete.");
                    Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
                }));
            }

            // Track manual updates for UI
            PendingUpdates = manualUpdates;
            UpdatesAvailable = manualUpdates.Select(u => $"{u.ModName}: v{u.PluginInfo.Metadata.Version} -> v{u.Version}").ToList();

            CheckComplete = true;
            RoosterPlugin.LogInfo($"Update Check Complete. Found {manualUpdates.Count} manual updates and {autoUpdates.Count} auto updates.");
        }

        
        
        
        /// <summary>
        /// Finds the matching Thunderstore package for a given local plugin.
        /// </summary>
        /// <param name="plugin">The local plugin info.</param>
        /// <returns>The matching ThunderstorePackage, or null if not found.</returns>
        public static ThunderstorePackage FindPackage(PluginInfo plugin)
        {
            return ModMatcher.FindPackage(plugin, CachedPackages);
        }

        
        
        
        /// <summary>
        /// Initiates the mass update process for all pending updates.
        /// </summary>
        /// <param name="onStatusUpdate">Callback for status messages (e.g., "Downloading...").</param>
        /// <param name="onComplete">Callback invoked when all updates are processed.</param>
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
                    AutoUpdateStatus = msg;
                    continue;
                }

                RoosterPlugin.LogInfo($"Processing update for {update.ModName}...");
                string statusMsg = $"Downloading {update.ModName}...";
                onStatusUpdate?.Invoke(statusMsg);
                AutoUpdateStatus = statusMsg;

                string cacheDir = Path.Combine(Path.GetDirectoryName(typeof(RoosterPlugin).Assembly.Location), "cache");
                string zipPath = Path.Combine(cacheDir, $"{update.ModName}_{update.Version}.zip");

                bool downloadSuccess = false;
                yield return UpdateDownloader.DownloadFile(update.DownloadUrl, zipPath, update.FileHash, (success, error) => 
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
            
            // Flag restart required
            RestartRequired = true;
            UpdatesAvailable.Clear();
            PendingUpdates.Clear();
            
            yield return null; 
            onComplete?.Invoke();
        }
    }
}
