using System;

namespace Rooster.Models
{
    /// <summary>
    /// Represents version information for a Thunderstore package.
    /// </summary>
    [Serializable]
    public class ThunderstoreVersion
    {
        /// <summary>
        /// The semantic version string (e.g., "1.0.0").
        /// </summary>
        public string version_number;
        
        /// <summary>
        /// The direct download URL for this version.
        /// </summary>
        public string download_url;
        
        /// <summary>
        /// The checksum or hash of the file.
        /// </summary>
        public string file_hash;
    }
}
