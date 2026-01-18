using System;

namespace Rooster.Models
{
    /// <summary>
    /// Container for deserializing the mod mapping configuration JSON.
    /// Used for manual overrides of mod-to-package mappings.
    /// </summary>
    [Serializable]
    public class ModMapData
    {
        /// <summary>
        /// Array of mod mapping items.
        /// </summary>
        public ModMapItem[] mods;
    }

    /// <summary>
    /// Represents a single manual mapping between a local mod GUID and a Thunderstore package.
    /// </summary>
    [Serializable]
    public class ModMapItem
    {
        /// <summary>
        /// The GUID of the local BepInEx plugin.
        /// </summary>
        public string guid;
        
        /// <summary>
        /// The namespace of the package on Thunderstore (e.g., "TeamUCH").
        /// </summary>
        public string thunderstore_namespace;
        
        /// <summary>
        /// The name of the package on Thunderstore (e.g., "Rooster").
        /// </summary>
        public string thunderstore_name;
    }
}
