using System;
using System.Collections.Generic;

namespace Rooster.Models
{
    [Serializable]
    public class RoosterCache
    {
        public long Timestamp;
        public List<ThunderstorePackage> Packages;
    }
}
