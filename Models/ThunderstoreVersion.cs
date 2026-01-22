using System;

namespace Rooster.Models
{
    [Serializable]
    public class ThunderstoreVersion
    {
        public string VersionNumber;
        public string DownloadUrl;
        public long FileSize;
        public System.Collections.Generic.List<string> Dependencies;

        public Rooster.Services.JSONNode ToJson()
        {
            var node = new Rooster.Services.JSONObject();
            node["version_number"] = VersionNumber;
            node["download_url"] = DownloadUrl;
            node["file_size"] = FileSize;
            if (Dependencies != null)
            {
                var arr = new Rooster.Services.JSONArray();
                foreach (var d in Dependencies) arr.Add(d);
                node["dependencies"] = arr;
            }
            return node;
        }

        public static ThunderstoreVersion FromJson(Rooster.Services.JSONNode node)
        {
            if (node == null) return null;
            var ver = new ThunderstoreVersion
            {
                VersionNumber = node["version_number"],
                DownloadUrl = node["download_url"],
                FileSize = node["file_size"].AsLong
            };
            var deps = node["dependencies"].AsArray;
            if (deps != null)
            {
                ver.Dependencies = new System.Collections.Generic.List<string>();
                foreach (var d in deps) ver.Dependencies.Add(d.Value);
            }
            return ver;
        }
    }
}
