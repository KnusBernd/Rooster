using System;

namespace Rooster.Models
{
    [Serializable]
    public class ThunderstoreVersion
    {
        public string VersionNumber;
        public string DownloadUrl;

        public Rooster.Services.JSONNode ToJson()
        {
            var node = new Rooster.Services.JSONObject();
            node["version_number"] = VersionNumber;
            node["download_url"] = DownloadUrl;
            return node;
        }

        public static ThunderstoreVersion FromJson(Rooster.Services.JSONNode node)
        {
            if (node == null) return null;
            return new ThunderstoreVersion
            {
                VersionNumber = node["version_number"],
                DownloadUrl = node["download_url"]
            };
        }
    }
}
