using System;

namespace Rooster.Models
{
    [Serializable]
    public class ThunderstorePackage
    {
        public string name;
        public string full_name;
        public string website_url;
        public string description;
        public string date_updated;
        public ThunderstoreVersion latest;
        public ThunderstoreVersion oldest;
        public System.Collections.Generic.List<string> categories;
        public System.Collections.Generic.List<string> files;
    }
}
