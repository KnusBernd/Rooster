using System;

namespace Rooster.Models
{
    /// <summary>
    /// Wrapper class for deserializing the top-level list of packages from the Thunderstore API.
    /// Currently unused if custom manual parsing is employed.
    /// </summary>
    [Serializable]
    public class PackageListWrapper
    {
        public ThunderstorePackage[] items;
    }
}
