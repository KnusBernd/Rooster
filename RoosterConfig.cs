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

        /// <summary>
        /// Helper access to the "Show Beta Warning" configuration setting.
        /// </summary>
        public static ConfigEntry<bool> ShowBetaWarning { get; private set; }

        /// <summary>
        /// Initializes the configuration with the provided ConfigFile.
        /// </summary>
        /// <param name="config">The BepInEx ConfigFile instance.</param>
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

        /// <summary>
        /// Registers a mod for configuration if it hasn't been registered yet.
        /// Creates config entries for auto-update and ignore settings.
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <param name="modName">The display name of the mod.</param>
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

        /// <summary>
        /// Checks if a mod is set to be ignored for updates.
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <returns>True if the mod is ignored; otherwise, false.</returns>
        public static bool IsModIgnored(string guid)
        {
            if (_modIgnoreSettings.TryGetValue(guid, out var entry)) return entry.Value;
            return false;
        }

        /// <summary>
        /// Sets the ignore status for a mod.
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <param name="ignored">True to ignore updates; otherwise, false.</param>
        public static void SetModIgnored(string guid, bool ignored)
        {
            if (_modIgnoreSettings.TryGetValue(guid, out var entry)) entry.Value = ignored;
        }

        /// <summary>
        /// Checks if auto-update was previously enabled for this mod (legacy/data check).
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <param name="thunderstoreFullName">The Thunderstore full name (optional).</param>
        /// <returns>True if auto-update is enabled; otherwise, false.</returns>
        public static bool IsDataAutoUpdate(string guid, string thunderstoreFullName)
        {
            if (_modAutoUpdateSettings.TryGetValue(guid, out var entry))
            {
                if (entry.Value) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if auto-update is enabled for a specific mod.
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <returns>True if auto-update is enabled; otherwise, false.</returns>
        public static bool IsModAutoUpdateEnabled(string guid)
        {
            if (_modAutoUpdateSettings.TryGetValue(guid, out var entry))
            {
                return entry.Value;
            }
            return false;
        }

        /// <summary>
        /// Sets the auto-update status for a mod.
        /// </summary>
        /// <param name="guid">The GUID of the mod.</param>
        /// <param name="enabled">True to enable auto-updates; otherwise, false.</param>
        public static void SetModAutoUpdate(string guid, bool enabled)
        {
            if (_modAutoUpdateSettings.TryGetValue(guid, out var entry))
            {
                entry.Value = enabled;
            }
        }
        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        public static void SaveConfig()
        {
            _config?.Save();
        }
    }
}
