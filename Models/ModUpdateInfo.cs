using BepInEx;

namespace Rooster.Models
{
    /// <summary>
    /// Represents a pending update for a specific mod.
    /// Contains local plugin info and remote update details.
    /// </summary>
    public class ModUpdateInfo
    {
        /// <summary>
        /// Display name of the mod.
        /// </summary>
        public string ModName;
        
        /// <summary>
        /// The version of the available update.
        /// </summary>
        public string Version;
        
        /// <summary>
        /// The download URL for the update ZIP file.
        /// </summary>
        public string DownloadUrl;
        
        /// <summary>
        /// The SHA256 hash or checksum of the update file (if available).
        /// </summary>
        public string FileHash;
        
        /// <summary>
        /// Reference to the existing local plugin.
        /// </summary>
        public PluginInfo PluginInfo;
    }
}
