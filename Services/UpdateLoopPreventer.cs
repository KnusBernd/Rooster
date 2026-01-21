using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Rooster.Models;
using UnityEngine;
using Rooster.Services;

namespace Rooster.Services
{
    /// <summary>
    /// Prevents infinite update loops by identifying mods that fail to update correctly.
    /// Tracks pending installs and verifies if the expected version is present on the next startup.
    /// </summary>
    public static class UpdateLoopPreventer
    {
        private static string StoragePath => Path.Combine(Paths.ConfigPath, "Rooster_UpdateLoopData.json");

        private class LoopData
        {
            public List<string> PendingInstalls = new List<string>();
            public List<string> IgnoredVersions = new List<string>();
        }

        private static LoopData _data;

        public static void Init()
        {
            if (_data != null) return;
            Load();
            RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Loaded. Pending: {_data.PendingInstalls.Count}, Ignored: {_data.IgnoredVersions.Count}");
            CheckForFailedUpdates();
            SanitizeIgnoredVersions();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    var root = JSON.Parse(File.ReadAllText(StoragePath));
                    if (root != null)
                    {
                        _data = new LoopData();
                        var pending = root["PendingInstalls"].AsArray;
                        if (pending != null) foreach (var p in pending) _data.PendingInstalls.Add(p.Value);

                        var ignored = root["IgnoredVersions"].AsArray;
                        if (ignored != null) foreach (var i in ignored) _data.IgnoredVersions.Add(i.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to load update loop data: {ex.Message}");
            }

            if (_data == null) _data = new LoopData();
        }

        private static void Save()
        {
            try
            {
                var root = new JSONObject();

                var pending = new JSONArray();
                foreach (var p in _data.PendingInstalls) pending.Add(p);
                root["PendingInstalls"] = pending;

                var ignored = new JSONArray();
                foreach (var i in _data.IgnoredVersions) ignored.Add(i);
                root["IgnoredVersions"] = ignored;

                File.WriteAllText(StoragePath, root.ToString());
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to save update loop data: {ex.Message}");
            }
        }

        public static void RegisterPendingInstall(string guid, string version)
        {
            string entry = $"{guid}|{version}";
            if (!_data.PendingInstalls.Contains(entry))
            {
                RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Registering Pending Install: {entry}");
                _data.PendingInstalls.Add(entry);
                Save();
            }
        }

        private static void CheckForFailedUpdates()
        {
            if (_data.PendingInstalls.Count == 0) return;

            RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Verifying {_data.PendingInstalls.Count} pending installs...");

            var plugins = BepInEx.Bootstrap.Chainloader.PluginInfos;
            List<string> newIgnored = new List<string>();

            foreach (string entry in _data.PendingInstalls)
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 2) continue;

                string guid = parts[0];
                string expectedVer = parts[1];

                if (plugins.TryGetValue(guid, out var info))
                {
                    VerifyVersion(info, expectedVer, guid, newIgnored);
                }
                else
                {
                    var heuristicMatch = FindHeuristicMatch(guid, plugins.Values);

                    if (heuristicMatch != null)
                    {
                        RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Heuristic Match: {guid} -> {heuristicMatch.Metadata.Name}");
                        VerifyVersion(heuristicMatch, expectedVer, guid, newIgnored);
                    }
                    else
                    {
                        RoosterPlugin.LogWarning($"[UpdateLoopPreventer] Plugin {guid} missing after update!");
                    }
                }
            }

            foreach (var ig in newIgnored)
            {
                if (!_data.IgnoredVersions.Contains(ig))
                {
                    RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Ignoring version: {ig}");
                    _data.IgnoredVersions.Add(ig);
                }
            }

            _data.PendingInstalls.Clear();
            Save();
        }

        private static void SanitizeIgnoredVersions()
        {
            if (_data.IgnoredVersions.Count == 0) return;

            var plugins = BepInEx.Bootstrap.Chainloader.PluginInfos.Values;
            List<string> toRemove = new List<string>();

            foreach (string entry in _data.IgnoredVersions)
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 2) continue;

                string guid = parts[0];
                string ignoredVer = parts[1];

                PluginInfo match = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out var p) ? p : FindHeuristicMatch(guid, plugins);

                if (match != null)
                {
                    string currentVer = match.Metadata.Version.ToString();

                    if (currentVer == ignoredVer)
                    {
                        RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Validating previously ignored update for {guid}. It is now installed.");
                        toRemove.Add(entry);
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var rem in toRemove) _data.IgnoredVersions.Remove(rem);
                Save();
                RoosterPlugin.LogInfo($"[UpdateLoopPreventer] Restored {toRemove.Count} validated updates.");
            }
        }

        private static void VerifyVersion(PluginInfo info, string expectedVer, string originalKey, List<string> newIgnored)
        {
            string currentVer = info.Metadata.Version.ToString();

            if (currentVer != expectedVer)
            {
                RoosterPlugin.LogWarning($"[UpdateLoopPreventer] Update Failed for {info.Metadata.Name}! Expected: {expectedVer}, Found: {currentVer}. Ignoring {expectedVer}.");
                newIgnored.Add($"{originalKey}|{expectedVer}");
            }
        }

        private static PluginInfo FindHeuristicMatch(string shortGuid, ICollection<PluginInfo> plugins)
        {
            foreach (var plugin in plugins)
            {
                if (IsAcronymMatch(shortGuid, plugin.Metadata.Name)) return plugin;

                string nShort = ModMatcher.NormalizeName(shortGuid);
                string nName = ModMatcher.NormalizeName(plugin.Metadata.Name);
                if (nShort.Contains(nName) || nName.Contains(nShort)) return plugin;

                string simpleGuid = plugin.Metadata.GUID;
                int dotIndex = simpleGuid.LastIndexOf('.');
                if (dotIndex >= 0 && dotIndex < simpleGuid.Length - 1)
                {
                    simpleGuid = simpleGuid.Substring(dotIndex + 1);
                    if (IsAcronymMatch(shortGuid, simpleGuid)) return plugin;
                }
            }
            return null;
        }

        private static bool IsAcronymMatch(string acronym, string fullName)
        {
            if (string.IsNullOrEmpty(acronym) || string.IsNullOrEmpty(fullName)) return false;
            if (acronym.Length >= fullName.Length) return false;

            string caps = "";
            foreach (char c in fullName) if (char.IsUpper(c)) caps += c;
            if (caps.Equals(acronym, StringComparison.OrdinalIgnoreCase)) return true;

            string[] words = fullName.Split(new char[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            string initials = "";
            foreach (var word in words) if (word.Length > 0) initials += word[0];

            return initials.Equals(acronym, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVersionIgnored(string guid, string version)
        {
            if (string.IsNullOrEmpty(version)) return false;

            foreach (var entry in _data.IgnoredVersions)
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 2) continue;

                string ignoredGuid = parts[0];
                string ignoredVer = parts[1];

                if (ignoredGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                {
                    if (ignoredVer.Equals(version, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    try
                    {
                        Version v1 = ParseVersionSafe(ignoredVer);
                        Version v2 = ParseVersionSafe(version);
                        if (v1 != null && v2 != null && v1.Equals(v2))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }
            return false;
        }

        private static Version ParseVersionSafe(string v)
        {
            try
            {
                v = VersionComparer.CleanVersionString(v);
                return Version.TryParse(v, out var ver) ? ver : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
