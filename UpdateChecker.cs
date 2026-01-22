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
        public static HashSet<string> PendingInstalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> PendingUninstalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> InstalledPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsModInstalled(string guid) => MatchedPackages.ContainsKey(guid);
        public static bool IsPackageInstalled(string fullNameOrDep)
        {
            if (string.IsNullOrEmpty(fullNameOrDep)) return false;
            
            string current = fullNameOrDep;
            while (true)
            {
                if (InstalledPackageIds.Contains(current)) return true;
                
                int lastDash = current.LastIndexOf('-');
                if (lastDash <= 0) break;
                
                current = current.Substring(0, lastDash);
            }
            return false;
        }

        public static bool IsPendingUninstall(string fullName)
        {
            foreach (var pair in MatchedPackages)
            {
                if (pair.Value.FullName == fullName && PendingUninstalls.Contains(pair.Key))
                    return true;
            }
            return false;
        }

        public static IEnumerator CheckForUpdates()
        {
            yield return new WaitForSecondsRealtime(2.0f);

            CheckComplete = false;

            CheckComplete = false;

            yield return ThunderstoreApi.FetchAllPackages((packages, error) =>
            {
                if (error != null) RoosterPlugin.LogError($"Thunderstore Fetch Error: {error}");
                CachedPackages = packages;
            });

            yield return GitHubApi.BuildCache();
            yield return new WaitUntil(() => !GitHubApi.IsCaching);

            if (GitHubApi.CachedPackages != null && GitHubApi.CachedPackages.Count > 0)
            {
                RoosterPlugin.LogInfo($"UpdateChecker: Merging {GitHubApi.CachedPackages.Count} GitHub packages.");
                CachedPackages.AddRange(GitHubApi.CachedPackages);
            }

            if (CachedPackages.Count == 0)
            {
                RoosterPlugin.LogError("Failed to fetch packages from Thunderstore or GitHub. Aborting check.");
                CheckComplete = true;
                CheckComplete = true;
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

                if (matchedPkg != null)
                {
                    MatchedPackages[guid] = matchedPkg;
                    InstalledPackageIds.Add(matchedPkg.FullName);

                    if (matchedPkg.Categories != null && matchedPkg.Categories.Contains("GitHub"))
                    {
                        continue;
                    }

                    string[] parts = matchedPkg.FullName.Split('-');
                    if (parts.Length >= 2)
                    {
                        string owner = parts[0];
                        string pkgName = parts[1];

                        pendingRequests++;
                        RoosterPlugin.Instance.StartCoroutine(ThunderstoreApi.FetchFreshVersion(owner, pkgName, (ver, url) =>
                        {
                            if (!string.IsNullOrEmpty(ver))
                            {
                                matchedPkg.Latest.VersionNumber = ver;
                                if (!string.IsNullOrEmpty(url)) matchedPkg.Latest.DownloadUrl = url;
                            }
                            pendingRequests--;
                        }));
                    }
                }
            }

            while (pendingRequests > 0)
            {
                yield return null;
            }

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                string guid = plugin.Metadata.GUID;
                if (!MatchedPackages.TryGetValue(guid, out var matchedPkg)) continue;
                string modName = plugin.Metadata.Name;

                var updateInfo = VersionComparer.CheckForUpdate(plugin, matchedPkg);

                if (updateInfo != null)
                {
                    updateInfo.WebsiteUrl = matchedPkg.WebsiteUrl;

                if (RoosterConfig.IsModIgnored(guid) || UpdateLoopPreventer.IsVersionIgnored(guid, matchedPkg.Latest.VersionNumber))
                {
                    // Ignored
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

            try
            {
                var bepPkg = CachedPackages.FirstOrDefault(p => p.FullName == "BepInEx-BepInExPack");
                string bepVersion = typeof(Chainloader).Assembly.GetName().Version.ToString();

                if (bepPkg != null)
                {
                    MatchedPackages["bepinex"] = bepPkg;
                    InstalledPackageIds.Add(bepPkg.FullName);

                    string searchVersion = bepVersion;
                    var vParts = bepVersion.Split('.');
                    if (vParts.Length == 4 && int.TryParse(vParts[3], out int build))
                    {
                        searchVersion = $"{vParts[0]}.{vParts[1]}.{vParts[2]}{build:D2}";
                    }

                    var bepUpdate = VersionComparer.CheckForUpdate("BepInEx", searchVersion, bepPkg);
                    if (bepUpdate != null)
                    {
                        bepUpdate.FullName = bepPkg.FullName;
                        bepUpdate.Description = bepPkg.Description;
                        bepUpdate.WebsiteUrl = bepPkg.WebsiteUrl;

                        if (!RoosterConfig.IsModIgnored("bepinex"))
                        {
                            manualUpdates.Add(bepUpdate);
                            RoosterPlugin.LogInfo("BepInEx update available!");
                        }
                    }

                    RoosterConfig.RegisterMod("bepinex", "BepInEx");
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to process BepInEx pseudo-plugin: {ex.Message}");
            }

            if (autoUpdates.Count > 0)
            {
                PendingUpdates.AddRange(autoUpdates);
                RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(autoUpdates, (info, status) => { }, () =>
                {
                    RestartRequired = true;
                    Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
                }));
            }
            PendingUpdates = manualUpdates;
            CheckComplete = true;
            RoosterPlugin.LogInfo($"Update Check Complete. Found {manualUpdates.Count} manual updates and {autoUpdates.Count} auto updates.");

            if (manualUpdates.Count > 0)
            {
                 Patches.MainMenuPopupPatch.ShowPopupIfNeeded();
            }
        }


        public static void UpdateAll(Action<ModUpdateInfo, string> onStatusUpdate, Action onComplete)
        {
            RoosterPlugin.Instance.StartCoroutine(UpdateAllCoroutine(PendingUpdates, onStatusUpdate, onComplete));
        }

        private static IEnumerator UpdateAllCoroutine(List<ModUpdateInfo> upgradesRaw, Action<ModUpdateInfo, string> onStatusUpdate, Action onComplete)
        {
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
                string ext = update.DownloadUrl.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? ".dll" : ".zip";
                string zipPath = Path.Combine(cacheDir, $"{update.ModName}_{update.Version}{ext}");

                yield return UpdateDownloader.DownloadFile(update.DownloadUrl, zipPath, (success, error) =>
                {
                    downloadSuccess = success;
                    if (!success)
                    {
                        try { onStatusUpdate?.Invoke(update, "Download Failed"); } catch { }
                    }
                });

                if (downloadSuccess)
                {
                    bool installSuccess = false;
                    try { onStatusUpdate?.Invoke(update, "Installing..."); } catch { }

                    UpdateInstaller.InstallMod(zipPath, update.PluginInfo, new ThunderstorePackage
                    {
                        Name = update.ModName,
                        FullName = update.FullName,
                        Description = update.Description,
                        WebsiteUrl = update.WebsiteUrl,
                        Latest = new ThunderstoreVersion { VersionNumber = update.Version }
                    }, (success, error) =>
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
                        try { onStatusUpdate?.Invoke(update, "Ready"); } catch { }
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



            try
            {
                RoosterPlugin.LogInfo("UpdateAllCoroutine: Invoking onComplete callback.");
                onComplete?.Invoke();
            }
            catch (Exception ex2)
            {
                RoosterPlugin.LogError($"onComplete callback failed: {ex2}");
            }
        }
    }
}
