using BepInEx.Configuration;
using UnityEngine;
using System.Collections.Generic;

namespace Rooster
{
    /// <summary>
    /// Handles configuration settings for the Rooster mod.
    /// Manages per-mod auto-update and ignore settings.
    /// </summary>
    public static class RoosterConfig
    {
        private static Dictionary<string, ConfigEntry<bool>> _modAutoUpdateSettings = new Dictionary<string, ConfigEntry<bool>>();
        private static Dictionary<string, ConfigEntry<bool>> _modIgnoreSettings = new Dictionary<string, ConfigEntry<bool>>();
        private static ConfigFile _config;

        public static ConfigEntry<string> GitHubCuratedUrl { get; private set; }
        public static ConfigEntry<int> GitHubCacheDuration { get; private set; }
        public static ConfigEntry<string> GitHubTokenPath { get; private set; }
        public static ConfigEntry<bool> GitHubWarningAccepted { get; private set; }
        public static ConfigEntry<bool> AllowGameRootInstallation { get; private set; }
        public static string RoosterConfigPath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "Rooster");

        public static ConfigEntry<bool> ShowBetaWarning { get; private set; }
        public static ConfigEntry<bool> DeveloperMode { get; private set; }
        public static ConfigEntry<KeyCode> DeveloperKey { get; private set; }

        public static void Init(ConfigFile config)
        {
            _config = config;

            _config = config;

            if (!System.IO.Directory.Exists(RoosterConfigPath))
            {
                System.IO.Directory.CreateDirectory(RoosterConfigPath);
            }

            ShowBetaWarning = config.Bind(
                "General",
                "ShowBetaWarning",
                true,
                "If true, a beta warning popup will be shown at the main menu."
            );

            GitHubCuratedUrl = config.Bind(
                "GitHub",
                "CuratedModListUrl",
                "https://raw.githubusercontent.com/KnusBernd/RoosterCuratedList/main/curated-mods.json",
                "The URL to fetch the curated list of mods from."
            );

            GitHubCacheDuration = config.Bind(
                "GitHub",
                "CacheDurationSeconds",
                1800, // 30 minutes
                "Time in seconds to cache GitHub API responses. Default is 30 minutes (1800)."
            );

            GitHubTokenPath = config.Bind(
                "GitHub",
                "TokenPath",
                "github_token.txt",
                "The path to the GitHub token file. Can be absolute or relative to 'BepInEx/config/Rooster/'."
            );

            GitHubWarningAccepted = config.Bind(
                "GitHub",
                "WarningAccepted",
                false,
                "Whether the user has accepted the GitHub mod download warning."
            );

            AllowGameRootInstallation = config.Bind(
                "Security",
                "AllowGameRootInstallation",
                false,
                "If true, mods can install files directly to the Game Root (DANGEROUS). Defaults to false for security."
            );


            DeveloperMode = config.Bind(
                "General",
                "DeveloperMode",
                false,
                "Enables the 'Rooster Developer Tools' window."
            );

            DeveloperKey = config.Bind(
                "General",
                "DeveloperKey",
                KeyCode.F3,
                "The key to toggle the Developer Tools window."
            );
        }


        /// <summary>Registers a mod for config if not already registered.</summary>
        public static void RegisterMod(string guid, string modName)
        {
            if (_config == null) return;

            if (!_modAutoUpdateSettings.ContainsKey(guid))
            {
                var entry = _config.Bind("AutoUpdate.Mods", guid, false, $"Enable auto-updates for {modName}?");
                _modAutoUpdateSettings[guid] = entry;
            }

            if (!_modIgnoreSettings.ContainsKey(guid))
            {
                var ignoreEntry = _config.Bind("IgnoredMods", guid, false, $"Ignore updates for {modName}?");
                _modIgnoreSettings[guid] = ignoreEntry;
            }
        }

        public static bool IsModIgnored(string guid)
        {
            if (_modIgnoreSettings.TryGetValue(guid, out var entry)) return entry.Value;
            return false;
        }

        public static void SetModIgnored(string guid, bool ignored)
        {
            if (_modIgnoreSettings.TryGetValue(guid, out var entry)) entry.Value = ignored;
        }

        public static bool IsModAutoUpdate(string guid)
        {
            return _modAutoUpdateSettings.TryGetValue(guid, out var entry) && entry.Value;
        }

        public static void SetModAutoUpdate(string guid, bool enabled)
        {
            if (_modAutoUpdateSettings.TryGetValue(guid, out var entry))
            {
                entry.Value = enabled;
            }
        }

        public static void SaveConfig()
        {
            _config?.Save();
        }
    }
}
