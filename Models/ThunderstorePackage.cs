using System;

namespace Rooster.Models
{
    /// <summary>
    /// Represents a package returned from the Thunderstore API.
    /// Lightweight DTO containing only necessary metadata.
    /// </summary>
    [Serializable]
    public class ThunderstorePackage
    {
        /// <summary>
        /// The package name (e.g., "Rooster").
        /// </summary>
        public string name;
        
        /// <summary>
        /// The full package name including namespace (e.g., "knusbernd-Rooster").
        /// </summary>
        public string full_name;
        
        /// <summary>
        /// Information about the latest available version.
        /// </summary>
        public ThunderstoreVersion latest;
    }
}
