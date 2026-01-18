using BepInEx.Configuration;
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

        public static ConfigEntry<bool> ShowBetaWarning { get; private set; }

        public static void Init(ConfigFile config)
        {
            _config = config;

            ShowBetaWarning = config.Bind(
                "General",
                "ShowBetaWarning",
                true,
                "If true, a beta warning popup will be shown at the main menu."
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
