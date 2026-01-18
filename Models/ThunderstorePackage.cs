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
        public string name;
        public string full_name;
        public ThunderstoreVersion latest;
    }
}
