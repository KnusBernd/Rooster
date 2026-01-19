using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Fetches a curated list of mods from a GitHub repository.
    /// Supports caching and error bubbling.
    /// </summary>
    public static class GitHubApi
    {
        // TODO: Replace with the actual URL provided by the user
        public const string CURATED_LIST_URL = "https://raw.githubusercontent.com/KnusBernd/RoosterCuratedList/main/curated-mods.json";

        public static List<ThunderstorePackage> CachedPackages = new List<ThunderstorePackage>();
        public static bool IsCacheReady = false;
        public static bool IsCaching = false;
        public static string LastError = null;

        public static IEnumerator BuildCache()
        {
            if (IsCaching || IsCacheReady) yield break;
            
            IsCaching = true;
            LastError = null;
            RoosterPlugin.LogInfo("Starting GitHub cache build...");

            // 1. Try Load from Disk
            var cached = LoadCache();
            if (cached != null)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // 1 hour = 3600 seconds
                if (now - cached.Timestamp < 3600 && cached.Packages != null && cached.Packages.Count > 0)
                {
                    RoosterPlugin.LogInfo($"Using Valid Disk Cache (Age: {(now - cached.Timestamp)}s)");
                    CachedPackages = cached.Packages;
                    IsCacheReady = true;
                    IsCaching = false;
                    yield break;
                }
                else
                {
                    RoosterPlugin.LogInfo("Disk Cache expired or invalid. Fetching fresh...");
                }
            }

            yield return FetchCuratedList((packages, error) => {
                if (error != null)
                {
                    RoosterPlugin.LogError($"GitHub Code Cache failed: {error}");
                    LastError = error;
                }
                else
                {
                    CachedPackages = packages;
                    IsCacheReady = true;
                    RoosterPlugin.LogInfo($"GitHub Cache Built: {packages.Count} mods.");
                    SaveCache(packages);
                }
                IsCaching = false;
            });
        }
        
        private static void SaveCache(List<ThunderstorePackage> packages)
        {
            try
            {
                var cache = new RoosterCache
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Packages = packages
                };
                string json = UnityEngine.JsonUtility.ToJson(cache, true);
                string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "RoosterCache.json");
                System.IO.File.WriteAllText(path, json);
                RoosterPlugin.LogInfo($"Saved GitHub cache to {path}");
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to save cache: {ex.Message}");
            }
        }

        private static RoosterCache LoadCache()
        {
            try
            {
                string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "RoosterCache.json");
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    return UnityEngine.JsonUtility.FromJson<RoosterCache>(json);
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogWarning($"Failed to load cache: {ex.Message}");
            }
            return null;
        }

        public static IEnumerator FetchCuratedList(Action<List<ThunderstorePackage>, string> onComplete)
        {
             RoosterPlugin.LogInfo($"Fetching curated list from: {CURATED_LIST_URL}");
            
             var allPackages = new List<ThunderstorePackage>();

             using (UnityWebRequest www = UnityWebRequest.Get(CURATED_LIST_URL))
             {
                 yield return www.SendWebRequest();

                 if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                 {
                     string errorMsg = $"Failed to list: {www.error}";
                     RoosterPlugin.LogError(errorMsg);
                     onComplete?.Invoke(allPackages, errorMsg);
                     yield break;
                 }

                 List<CuratedRepo> repos = new List<CuratedRepo>();
                 int completed = 0;

                 try
                 {
                     string json = www.downloadHandler.text;
                     repos = ParseRepoList(json);
                 }
                 catch (Exception ex)
                 {
                     string errorMsg = $"JSON Parse Error: {ex.Message}";
                     RoosterPlugin.LogError(errorMsg);
                     onComplete?.Invoke(allPackages, errorMsg);
                     yield break;
                 }

                 if (repos.Count == 0)
                 {
                     onComplete?.Invoke(allPackages, null);
                     yield break;
                 }

                 bool rateLimitHit = false;
                 string rateLimitError = null;

                 // Fetch each repo
                 foreach (var repo in repos)
                 {
                     RoosterPlugin.Instance.StartCoroutine(FetchRepoReleases(repo, (pkgs, error) => {
                         if (error != null && (error.Contains("403") || error.Contains("429")))
                         {
                             rateLimitHit = true;
                             rateLimitError = "GitHub API Rate Limit Exceeded. Please wait 1 hour.";
                         }
                         
                         if (pkgs != null) allPackages.AddRange(pkgs);
                         completed++;
                     }));
                 }
                 
                 // Wait until all are done
                 yield return new WaitUntil(() => completed >= repos.Count);
                 
                 if (rateLimitHit)
                 {
                     onComplete?.Invoke(allPackages, rateLimitError);
                 }
                 else
                 {
                     // Sort alphabetically by name to ensure deterministic UI order
                     allPackages.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                     onComplete?.Invoke(allPackages, null);
                 }
             }
        }
        
        private static string _cachedToken = null;
        private static bool _hasCheckedToken = false;

        private static string GetToken()
        {
            if (_hasCheckedToken) return _cachedToken;

            _hasCheckedToken = true;
            try
            {
                string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "rooster_github_token.txt");
                if (System.IO.File.Exists(path))
                {
                    string token = System.IO.File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        RoosterPlugin.LogInfo("Loaded GitHub Token from config.");
                        _cachedToken = token;
                    }
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to load GitHub token: {ex.Message}");
            }
            return _cachedToken;
        }

        private static IEnumerator FetchRepoReleases(CuratedRepo repoInfo, Action<List<ThunderstorePackage>, string> onRepoComplete)
        {
            // GitHub API: Use /releases to get ALL releases (including pre-releases), not just /releases/latest
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Repo}/releases";
            
            using (UnityWebRequest www = UnityWebRequest.Get(apiUrl))
            {
                www.SetRequestHeader("User-Agent", "RoosterModManager");
                string token = GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    www.SetRequestHeader("Authorization", $"Bearer {token}");
                }
                
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    long code = (long)www.responseCode;
                    if (code == 403 || code == 429)
                    {
                        RoosterPlugin.LogInfo($"GitHub API Error {code}: {www.error}");
                        onRepoComplete?.Invoke(null, $"{code} Rate Limit");
                        yield break;
                    }

                    // Fallback to Contents API
                    RoosterPlugin.LogInfo($"[Releases] Failed for {repoInfo.Repo} (Error: {www.responseCode}), trying contents...");
                    RoosterPlugin.Instance.StartCoroutine(FetchRepoContents(repoInfo, onRepoComplete));
                }
                else
                {
                    try
                    {
                        string json = www.downloadHandler.text;
                        // Parse LIST of releases, take the first one
                        var packs = ParseReleasesListJson(json, repoInfo);
                        
                        if (packs.Count == 0)
                        {
                            RoosterPlugin.LogInfo($"[Releases] No valid assets found in releases for {repoInfo.Repo}, trying contents...");
                            RoosterPlugin.Instance.StartCoroutine(FetchRepoContents(repoInfo, onRepoComplete));
                        }
                        else
                        {
                            onRepoComplete?.Invoke(packs, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"[Releases] Error parsing for {repoInfo.Repo}: {ex}");
                        RoosterPlugin.Instance.StartCoroutine(FetchRepoContents(repoInfo, onRepoComplete));
                    }
                }
            }
        }

        private static IEnumerator FetchRepoContents(CuratedRepo repoInfo, Action<List<ThunderstorePackage>, string> onRepoComplete)
        {
            RoosterPlugin.LogInfo($"[RecursiveFetch] Starting search for {repoInfo.Repo}...");
            string commitUrl = $"https://api.github.com/repos/{repoInfo.Repo}/commits/HEAD";
            string headSha = null;

            using (UnityWebRequest www = UnityWebRequest.Get(commitUrl))
            {
                 www.SetRequestHeader("User-Agent", "RoosterModManager");
                 string token = GetToken();
                 if (!string.IsNullOrEmpty(token)) www.SetRequestHeader("Authorization", $"Bearer {token}");
                 
                 yield return www.SendWebRequest();
                 
                 if (www.result != UnityWebRequest.Result.Success)
                 {
                     string err = $"[RecursiveFetch] Failed to get HEAD for {repoInfo.Repo}: {www.error} ({www.responseCode})";
                     RoosterPlugin.LogError(err);
                     onRepoComplete?.Invoke(null, err);
                     yield break;
                 }
                 
                 headSha = ThunderstoreApi.ExtractJsonValue(www.downloadHandler.text, "sha");
                 RoosterPlugin.LogInfo($"[RecursiveFetch] HEAD SHA for {repoInfo.Repo}: {headSha}");
            }
            
            if (string.IsNullOrEmpty(headSha))
            {
                onRepoComplete?.Invoke(null, "Failed to parse HEAD SHA");
                yield break;
            }

            // 2. Get Tree
            string treeUrl = $"https://api.github.com/repos/{repoInfo.Repo}/git/trees/{headSha}?recursive=1";
            
            using (UnityWebRequest www = UnityWebRequest.Get(treeUrl))
            {
                www.SetRequestHeader("User-Agent", "RoosterModManager");
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) www.SetRequestHeader("Authorization", $"Bearer {token}");
                
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string err = $"[RecursiveFetch] Failed to get Tree for {repoInfo.Repo}: {www.error} ({www.responseCode})";
                    RoosterPlugin.LogError(err);
                    onRepoComplete?.Invoke(null, err);
                    yield break;
                }
                
                try
                {
                    string json = www.downloadHandler.text;
                    var packs = ParseTreeJson(json, repoInfo, headSha);
                    RoosterPlugin.LogInfo($"[RecursiveFetch] Found {packs.Count} valid mods in {repoInfo.Repo} tree.");
                    onRepoComplete?.Invoke(packs, null);
                }
                catch (Exception ex)
                {
                    RoosterPlugin.LogError($"[RecursiveFetch] Error parsing tree for {repoInfo.Repo}: {ex}");
                    onRepoComplete?.Invoke(null, ex.Message);
                }
            }
        }

        private static List<ThunderstorePackage> ParseReleasesListJson(string json, CuratedRepo repoInfo)
        {
            var packages = new List<ThunderstorePackage>();
            // Expecting Array [ { ... }, { ... } ]
            
            int arrayStart = json.IndexOf('[');
            if (arrayStart < 0) return packages; 
            
            int objStart = json.IndexOf('{', arrayStart);
            if (objStart < 0) return packages;
            
            int objEnd = FindMatchingClosingChar(json, objStart, '{', '}');
            if (objEnd < 0) return packages;
            
            string latestReleaseJson = json.Substring(objStart, objEnd - objStart + 1);
            
            return ParseSingleReleaseJson(latestReleaseJson, repoInfo);
        }

        private static List<ThunderstorePackage> ParseSingleReleaseJson(string json, CuratedRepo repoInfo)
        {
            var packages = new List<ThunderstorePackage>();
            
            string tagName = ThunderstoreApi.ExtractJsonValue(json, "tag_name");
            string body = ThunderstoreApi.ExtractJsonValue(json, "body");
            string author = repoInfo.Repo.Split('/')[0];
            
            int assetsIdx = json.IndexOf("\"assets\":");
            if (assetsIdx >= 0)
            {
                int openBracket = json.IndexOf('[', assetsIdx);
                int closeBracket = FindMatchingClosingChar(json, openBracket, '[', ']');
                
                if (openBracket >= 0 && closeBracket > openBracket)
                {
                    string assetsJson = json.Substring(openBracket, closeBracket - openBracket + 1);
                    int walker = 0;
                    while (walker < assetsJson.Length)
                    {
                         int aObjStart = assetsJson.IndexOf('{', walker);
                         if (aObjStart < 0) break;
                         int aObjEnd = FindMatchingClosingChar(assetsJson, aObjStart, '{', '}');
                         if (aObjEnd < 0) break;
                         
                         string assetObj = assetsJson.Substring(aObjStart, aObjEnd - aObjStart + 1);
                         string name = ThunderstoreApi.ExtractJsonValue(assetObj, "name");
                         string downloadUrl = ThunderstoreApi.ExtractJsonValue(assetObj, "browser_download_url");
                         
                         ProcessAsset(packages, name, downloadUrl, author, repoInfo.Description ?? body, tagName);
                         
                         walker = aObjEnd + 1;
                    }
                }
            }
            
            if (packages.Count == 0 && !string.IsNullOrEmpty(body))
            {
                int linkIndex = 0;
                while ((linkIndex = body.IndexOf("http", linkIndex, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int zipIndex = body.IndexOf(".zip", linkIndex, StringComparison.OrdinalIgnoreCase);
                    if (zipIndex != -1)
                    {
                        int endIndex = zipIndex + 4; // include .zip
                        string url = body.Substring(linkIndex, endIndex - linkIndex);
                        
                        if (!url.Contains(" ") && !url.Contains("\n") && !url.Contains("\""))
                        {
                             string filename = System.IO.Path.GetFileName(url);
                             RoosterPlugin.LogInfo($"[Releases] Found embedded link in body: {url}");
                             ProcessAsset(packages, filename, url, author, repoInfo.Description ?? body, tagName);
                        }
                    }
                    linkIndex++;
                }
            }

            return packages;
        }

        private static List<ThunderstorePackage> ParseTreeJson(string json, CuratedRepo repoInfo, string sha)
        {
             var packages = new List<ThunderstorePackage>();
             string author = repoInfo.Repo.Split('/')[0];
             
             int treeKeyIndex = json.IndexOf("\"tree\":");
             if (treeKeyIndex < 0) 
             {
                 RoosterPlugin.LogError($"[RecursiveFetch] 'tree' key not found in JSON for {repoInfo.Repo}");
                 return packages;
             }
             
             int arrayStart = json.IndexOf('[', treeKeyIndex);
             if (arrayStart < 0) return packages;
             
             int arrayEnd = FindMatchingClosingChar(json, arrayStart, '[', ']');
             if (arrayEnd < 0) return packages;

             string treeArrayJson = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
             
             RoosterPlugin.LogInfo($"[RecursiveFetch] Parsing tree array (Length: {treeArrayJson.Length}) for {repoInfo.Repo}");

             int walker = 0;
             while (walker < treeArrayJson.Length)
             {
                 int objStart = treeArrayJson.IndexOf('{', walker);
                 if (objStart < 0) break;
                 
                 int objEnd = FindMatchingClosingChar(treeArrayJson, objStart, '{', '}');
                 if (objEnd < 0) break;
                 
                 string itemJson = treeArrayJson.Substring(objStart, objEnd - objStart + 1);
                 
                 string path = ThunderstoreApi.ExtractJsonValue(itemJson, "path");
                 string type = ThunderstoreApi.ExtractJsonValue(itemJson, "type");
                 

                 if (type == "blob" && !string.IsNullOrEmpty(path))
                 {
                     string filename = System.IO.Path.GetFileName(path);
                     
                     bool isZip = filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                     bool isDll = filename.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                     
                     if (isZip || isDll)
                     {
                         if (filename.StartsWith("source code", StringComparison.OrdinalIgnoreCase) || 
                             filename.StartsWith("src", StringComparison.OrdinalIgnoreCase))
                         {
                             RoosterPlugin.LogInfo($"[RecursiveFetch] Ignored source artifact: {path}");
                         }
                         else
                         {
                            string downloadUrl = $"https://raw.githubusercontent.com/{repoInfo.Repo}/{sha}/{path}";
                            
                            RoosterPlugin.LogInfo($"[RecursiveFetch] ACCEPTED: {path}");
                            ProcessAsset(packages, filename, downloadUrl, author, repoInfo.Description, "1.0.0");
                         }
                     }
                 }
                 
                 walker = objEnd + 1;
             }
             
             return packages;
        }

        private static void ProcessAsset(List<ThunderstorePackage> packages, string name, string downloadUrl, string author, string desc, string version)
        {
             if (string.IsNullOrEmpty(name)) return;
             
             bool isZip = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
             bool isDll = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
             
             if ((isZip || isDll) && 
                 !name.StartsWith("source code", StringComparison.OrdinalIgnoreCase) && 
                 !name.StartsWith("src", StringComparison.OrdinalIgnoreCase))
             {
                 string modName = name.Replace(".zip", "")
                                      .Replace(".dll", "");
                 
                 modName = StripVersionFromName(modName);
                 
                 packages.Add(new ThunderstorePackage
                 {
                     name = modName,
                     full_name = $"{author}-{modName}",
                     description = desc ?? "No description provided.",
                     website_url = downloadUrl,
                     latest = new ThunderstoreVersion
                     {
                         version_number = version != null ? version.TrimStart('v') : "1.0.0",
                         download_url = downloadUrl
                     },
                     categories = new List<string> { "GitHub" }
                 });
             }
        }

        private static string StripVersionFromName(string name)
        {
            int lastHyphen = name.LastIndexOf('-');
            if (lastHyphen > 0 && lastHyphen < name.Length - 1)
            {
                string potentialVersion = name.Substring(lastHyphen + 1);
                // Check if it consists only of digits, dots, v
                bool isVersion = true;
                bool hasDigit = false;
                foreach (char c in potentialVersion)
                {
                    if (char.IsDigit(c)) hasDigit = true;
                    else if (c != '.' && c != 'v' && c != 'V')
                    {
                        isVersion = false;
                        break;
                    }
                }
                
                if (isVersion && hasDigit)
                {
                    return name.Substring(0, lastHyphen);
                }
            }
            return name;
        }

        private class CuratedRepo
        {
            public string Repo; // "KnusBernd/Rooster"
            public string Description;
        }

        private static List<CuratedRepo> ParseRepoList(string json)
        {
            RoosterPlugin.LogInfo($"Parsing Repo List (Length: {json?.Length ?? 0})");
            var list = new List<CuratedRepo>();
            if (string.IsNullOrEmpty(json)) return list;

            int index = 0;
            while(index < json.Length)
            {
                int objStart = json.IndexOf('{', index);
                if (objStart < 0) break;
                int objEnd = FindMatchingClosingChar(json, objStart, '{', '}');
                if (objEnd < 0) 
                {
                    RoosterPlugin.LogError($"Failed to find closing brace for object starting at {objStart}");
                    break;
                }
                
                string itemJson = json.Substring(objStart, objEnd - objStart + 1);
                string repo = ThunderstoreApi.ExtractJsonValue(itemJson, "repo");
                string desc = ThunderstoreApi.ExtractJsonValue(itemJson, "description");
                
                if (!string.IsNullOrEmpty(repo))
                {
                    list.Add(new CuratedRepo { Repo = repo, Description = desc });
                    RoosterPlugin.LogInfo($"Found Repo: {repo}");
                }
                else
                {
                    RoosterPlugin.LogWarning($"Found object but no repo field: {itemJson}");
                }
                index = objEnd + 1;
            }
            RoosterPlugin.LogInfo($"Parsed {list.Count} curated repos.");
            return list;
        }

        private static int FindMatchingClosingChar(string json, int openIndex, char openChar, char closeChar)
        {
            int depth = 0;
            bool inString = false;
            for (int i = openIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i-1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == openChar) depth++;
                    else if (c == closeChar)
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return -1;
        }
    }
}
