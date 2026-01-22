using System;
using System.Linq;
using BepInEx;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Provides utility methods for comparing mod versions.
    /// </summary>
    public static class VersionComparer
    {
        /// <summary>Checks if a newer version is available for a plugin.</summary>
        public static ModUpdateInfo CheckForUpdate(PluginInfo plugin, ThunderstorePackage remotePkg)
        {
            if (plugin == null) return null;
            return CheckForUpdate(plugin.Metadata.Name, plugin.Metadata.Version.ToString(), remotePkg, plugin);
        }

        /// <summary>Checks if a newer version is available for a manual version string.</summary>
        public static ModUpdateInfo CheckForUpdate(string modName, string currentVersion, ThunderstorePackage remotePkg, PluginInfo plugin = null)
        {
            if (remotePkg?.Latest == null) return null;

            string latestVersion = remotePkg.Latest.VersionNumber;

            if (IsNewer(currentVersion, latestVersion))
            {
                return new ModUpdateInfo
                {
                    ModName = modName,
                    Version = latestVersion,
                    DownloadUrl = remotePkg.Latest.DownloadUrl,
                    PluginInfo = plugin
                };
            }
            return null;
        }

        /// <summary>Returns true if 'latest' is strictly greater than 'current' (semver).</summary>
        public static bool IsNewer(string current, string latest)
        {
            RoosterPlugin.LogInfo($"Comparing versions: Local='{current}' vs Remote='{latest}'");

            try
            {
                current = CleanVersionString(current);
                latest = CleanVersionString(latest);

                int[] vC = current.Split('.').Select(s => int.TryParse(s, out int i) ? i : 0).ToArray();
                int[] vL = latest.Split('.').Select(s => int.TryParse(s, out int i) ? i : 0).ToArray();

                int len = Math.Max(vC.Length, vL.Length);
                for (int i = 0; i < len; i++)
                {
                    int cVal = i < vC.Length ? vC[i] : 0;
                    int lVal = i < vL.Length ? vL[i] : 0;

                    if (lVal > cVal) return true;
                    if (lVal < cVal) return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Version comparison failed for '{current}' vs '{latest}': {ex}");
                return string.Compare(latest, current) > 0;
            }
        }

        /// <summary>Strips 'v' prefix and build metadata (after hyphen).</summary>
        public static string CleanVersionString(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0.0.0";
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) version = version.Substring(1);
            int dashIdx = version.IndexOf('-');
            if (dashIdx > 0) version = version.Substring(0, dashIdx);
            return version;
        }
    }
}
