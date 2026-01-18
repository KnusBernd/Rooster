using BepInEx;

namespace Rooster.Models
{
    /// <summary>
    /// Represents a pending update for a specific mod.
    /// Contains local plugin info and remote update details.
    /// </summary>
    public class ModUpdateInfo
    {
        public string ModName;
        public string Version;
        public string DownloadUrl;
        public string FileHash;
        public PluginInfo PluginInfo;
    }
}
