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
        /// <summary>
        /// Checks if a newer version is available for a given plugin compared to the remote package.
        /// </summary>
        /// <param name="plugin">The local plugin info.</param>
        /// <param name="remotePkg">The remote Thunderstore package.</param>
        /// <returns>A ModUpdateInfo object if an update is available; otherwise, null.</returns>
        public static ModUpdateInfo CheckForUpdate(PluginInfo plugin, ThunderstorePackage remotePkg)
        {
            if (remotePkg?.latest == null) return null;
             
            string modName = plugin.Metadata.Name;
            string currentVersion = plugin.Metadata.Version.ToString();
            string latestVersion = remotePkg.latest.version_number;

            if (IsNewer(currentVersion, latestVersion))
            {
                return new ModUpdateInfo 
                { 
                    ModName = modName, 
                    Version = latestVersion, 
                    DownloadUrl = remotePkg.latest.download_url,
                    FileHash = remotePkg.latest.file_hash,
                    PluginInfo = plugin
                };
            }
            return null;
        }

        /// <summary>
        /// Compares two version strings to determine if the 'latest' is newer than 'current'.
        /// Handles standard semantic versioning (Major.Minor.Patch).
        /// </summary>
        /// <param name="current">The current version string.</param>
        /// <param name="latest">The latest version string.</param>
        /// <returns>True if 'latest' is strictly greater than 'current'; otherwise, false.</returns>
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

        /// <summary>
        /// Cleans a version string by removing 'v' prefixes and build metadata (after hyphen).
        /// </summary>
        /// <param name="version">The raw version string.</param>
        /// <returns>The cleaned version string (e.g., "1.0.0").</returns>
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
