using System;

namespace Rooster.Models
{
    /// <summary>
    /// Represents version information for a Thunderstore package.
    /// </summary>
    [Serializable]
    public class ThunderstoreVersion
    {
        public string version_number;
        public string download_url;
        public string file_hash;
    }
}
