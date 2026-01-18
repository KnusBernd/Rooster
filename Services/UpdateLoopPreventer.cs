using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Rooster.Models;
using UnityEngine;

namespace Rooster.Services
{
    /// <summary>
    /// Prevents infinite update loops by identifying mods that fail to update correctly.
    /// Tracks pending installs and verifies if the expected version is present on the next startup.
    /// </summary>
    public static class UpdateLoopPreventer
    {
        private static string StoragePath => Path.Combine(Paths.ConfigPath, "Rooster_UpdateLoopData.json");

        [Serializable]
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
            CheckForFailedUpdates();
            SanitizeIgnoredVersions();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    _data = JsonUtility.FromJson<LoopData>(File.ReadAllText(StoragePath));
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
                File.WriteAllText(StoragePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to save update loop data: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a pending update for a plugin. 
        /// This version is expected to be present on the next game launch.
        /// </summary>
        public static void RegisterPendingInstall(string guid, string version)
        {
            string entry = $"{guid}|{version}";
            if (!_data.PendingInstalls.Contains(entry))
            {
                _data.PendingInstalls.Add(entry);
                Save();
            }
        }

        /// <summary>
        /// Verifies that all pending installs from the previous session were successful.
        /// If a plugin is still on the old version, the target version is marked as "Ignored".
        /// </summary>
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
                    // Heuristic: Check if the plugin exists under a different GUID but matches the name
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
                if (!_data.IgnoredVersions.Contains(ig)) _data.IgnoredVersions.Add(ig);
            }

            _data.PendingInstalls.Clear();
            Save();
        }

        /// <summary>
        /// Re-evaluates ignored versions. If a previously failed update is now installed correctly,
        /// it removes the ignore flag to allow future updates.
        /// </summary>
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
                    
                    // Logic:
                    // 1. Direct match with correct version = Fixed.
                    // 2. Heuristic match with correct version = Fixed (likely a GUID change issue resolved).
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

        /// <summary>
        /// Attempts to find a plugin that matches the given GUID based on acronyms or name similarity.
        /// Useful when a Mod's GUID changes but the name remains similar.
        /// </summary>
        private static PluginInfo FindHeuristicMatch(string shortGuid, ICollection<PluginInfo> plugins)
        {
            foreach (var plugin in plugins)
            {
                if (IsAcronymMatch(shortGuid, plugin.Metadata.Name)) return plugin;
                
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

        /// <summary>
        /// Checks if the input string is a valid acronym for the full name.
        /// (e.g., "RPP" matches "RemovePlayerPlacements")
        /// </summary>
        private static bool IsAcronymMatch(string acronym, string fullName)
        {
            if (string.IsNullOrEmpty(acronym) || string.IsNullOrEmpty(fullName)) return false;
            if (acronym.Length >= fullName.Length) return false;

            // Strategy 1: PascalCase Capitals
            string caps = "";
            foreach (char c in fullName) if (char.IsUpper(c)) caps += c;
            if (caps.Equals(acronym, StringComparison.OrdinalIgnoreCase)) return true;

            // Strategy 2: First letter of words
            string[] words = fullName.Split(new char[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            string initials = "";
            foreach (var word in words) if (word.Length > 0) initials += word[0];
            
            return initials.Equals(acronym, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVersionIgnored(string guid, string version)
        {
            return _data.IgnoredVersions.Contains($"{guid}|{version}");
        }
    }
}
