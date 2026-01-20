using System;
using System.Collections.Generic; // Added for List<string>

namespace Rooster.Models
{
    [Serializable]
    public class ThunderstorePackage
    {
        public string Name;
        public string FullName;
        public string Description;
        public string WebsiteUrl;
        public string DateUpdated;
        public ThunderstoreVersion Latest;
        public ThunderstoreVersion Oldest;
        public List<string> Categories;
        public List<string> Files; // For uninstall tracking

        public Rooster.Services.JSONNode ToJson()
        {
            var json = new Rooster.Services.JSONObject(); // Changed node to json
            json["name"] = Name;
            json["full_name"] = FullName;
            json["website_url"] = WebsiteUrl;
            json["description"] = Description;
            json["date_updated"] = DateUpdated;
            if (Latest != null) json["latest"] = Latest.ToJson();
            if (Oldest != null) json["oldest"] = Oldest.ToJson();

            if (Categories != null)
            {
                var arr = new Rooster.Services.JSONArray(); // Changed to arr, kept full namespace
                foreach (var c in Categories) arr.Add(c);
                json["categories"] = arr;
            }
            if (Files != null)
            {
                var arr = new Rooster.Services.JSONArray(); // Changed to arr, kept full namespace
                foreach (var f in Files) arr.Add(f);
                json["files"] = arr;
            }
            return json; // Changed node to json
        }

        public static ThunderstorePackage FromJson(Rooster.Services.JSONNode node)
        {
            if (node == null) return null;
            var pkg = new ThunderstorePackage
            {
                Name = node["name"],
                FullName = node["full_name"],
                Description = node["description"],
                WebsiteUrl = node["website_url"],
                DateUpdated = node["date_updated"],
                Latest = ThunderstoreVersion.FromJson(node["latest"]),
                Oldest = ThunderstoreVersion.FromJson(node["oldest"])
            };

            var cats = node["categories"].AsArray;
            if (cats != null)
            {
                pkg.Categories = new List<string>();
                foreach (var c in cats) pkg.Categories.Add(c.Value);
            }

            var fs = node["files"].AsArray;
            if (fs != null)
            {
                pkg.Files = new System.Collections.Generic.List<string>();
                foreach (var f in fs) pkg.Files.Add(f.Value);
            }
            return pkg;
        }
    }
}
