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
    /// </summary>
    public static class GitHubApi
    {
        // TODO: Replace with the actual URL provided by the user
        public const string CURATED_LIST_URL = "https://raw.githubusercontent.com/KnusBernd/RoosterCuratedList/main/curated-mods.json";

        public static IEnumerator FetchCuratedList(Action<List<ThunderstorePackage>> onComplete)
        {
             RoosterPlugin.LogInfo($"Fetching curated list from: {CURATED_LIST_URL}");
            
             var allPackages = new List<ThunderstorePackage>();

             using (UnityWebRequest www = UnityWebRequest.Get(CURATED_LIST_URL))
             {
                 yield return www.SendWebRequest();

                 if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                 {
                     RoosterPlugin.LogError($"Failed to fetch curated list: {www.error}");
                     onComplete?.Invoke(allPackages);
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
                     RoosterPlugin.LogError($"Error parsing curated list JSON: {ex}");
                     onComplete?.Invoke(allPackages);
                     yield break;
                 }

                 if (repos.Count == 0)
                 {
                     onComplete?.Invoke(allPackages);
                     yield break;
                 }

                 // Fetch each repo
                 foreach (var repo in repos)
                 {
                     RoosterPlugin.Instance.StartCoroutine(FetchRepoReleases(repo, (pkgs) => {
                         if (pkgs != null) allPackages.AddRange(pkgs);
                         completed++;
                     }));
                 }
                 
                 // Wait until all are done
                 yield return new WaitUntil(() => completed >= repos.Count);
                 
                 onComplete?.Invoke(allPackages);
             }
        }
        
        private static IEnumerator FetchRepoReleases(CuratedRepo repoInfo, Action<List<ThunderstorePackage>> onRepoComplete)
        {
            // GitHub API: https://api.github.com/repos/{owner}/{repo}/releases/latest
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Repo}/releases/latest";
            
            using (UnityWebRequest www = UnityWebRequest.Get(apiUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    // Fallback to Contents API if releases not found (e.g. only loose DLL in repo)
                    RoosterPlugin.LogInfo($"Releases not found for {repoInfo.Repo}, trying contents...");
                    RoosterPlugin.Instance.StartCoroutine(FetchRepoContents(repoInfo, onRepoComplete));
                }
                else
                {
                    try
                    {
                        string json = www.downloadHandler.text;
                        var packs = ParseReleaseJson(json, repoInfo);
                        onRepoComplete?.Invoke(packs);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Error parsing release for {repoInfo.Repo}: {ex}");
                        onRepoComplete?.Invoke(null);
                    }
                }
            }
        }

        private static IEnumerator FetchRepoContents(CuratedRepo repoInfo, Action<List<ThunderstorePackage>> onRepoComplete)
        {
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Repo}/contents";
             using (UnityWebRequest www = UnityWebRequest.Get(apiUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    RoosterPlugin.LogError($"Failed to fetch contents for {repoInfo.Repo}: {www.error}");
                    onRepoComplete?.Invoke(null);
                }
                else
                {
                     try
                    {
                        string json = www.downloadHandler.text;
                        var packs = ParseContentJson(json, repoInfo);
                        onRepoComplete?.Invoke(packs);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Error parsing contents for {repoInfo.Repo}: {ex}");
                        onRepoComplete?.Invoke(null);
                    }
                }
            }
        }

        private static List<ThunderstorePackage> ParseReleaseJson(string json, CuratedRepo repoInfo)
        {
            var packages = new List<ThunderstorePackage>();
            
            string tagName = ThunderstoreApi.ExtractJsonValue(json, "tag_name");
            string body = ThunderstoreApi.ExtractJsonValue(json, "body");
            string author = repoInfo.Repo.Split('/')[0];
            
            // Assets Array
            int assetsIdx = json.IndexOf("\"assets\":");
            if (assetsIdx < 0) return packages;
            
            int openBracket = json.IndexOf('[', assetsIdx);
            int closeBracket = FindMatchingClosingChar(json, openBracket, '[', ']');
            
            string assetsJson = json.Substring(openBracket, closeBracket - openBracket + 1);
            
            // Reuse logic for array parsing? Or copy loop
            int walker = 0;
            while (walker < assetsJson.Length)
            {
                 int objStart = assetsJson.IndexOf('{', walker);
                 if (objStart < 0) break;
                 int objEnd = FindMatchingClosingChar(assetsJson, objStart, '{', '}');
                 if (objEnd < 0) break;
                 
                 string assetObj = assetsJson.Substring(objStart, objEnd - objStart + 1);
                 
                 string name = ThunderstoreApi.ExtractJsonValue(assetObj, "name");
                 string downloadUrl = ThunderstoreApi.ExtractJsonValue(assetObj, "browser_download_url");
                 
                 ProcessAsset(packages, name, downloadUrl, author, repoInfo.Description ?? body, tagName);
                 
                 walker = objEnd + 1;
            }

            return packages;
        }

        private static List<ThunderstorePackage> ParseContentJson(string json, CuratedRepo repoInfo)
        {
             // Contents is an array of file objects
             var packages = new List<ThunderstorePackage>();
             string author = repoInfo.Repo.Split('/')[0];

             int walker = 0;
             // Skip outer brackets? json is [ ... ]
             if (!json.TrimStart().StartsWith("[")) return packages; // Should be array

             while (walker < json.Length)
             {
                 int objStart = json.IndexOf('{', walker);
                 if (objStart < 0) break;
                 int objEnd = FindMatchingClosingChar(json, objStart, '{', '}');
                 if (objEnd < 0) break;
                 
                 string fileObj = json.Substring(objStart, objEnd - objStart + 1);
                 
                 string name = ThunderstoreApi.ExtractJsonValue(fileObj, "name");
                 string downloadUrl = ThunderstoreApi.ExtractJsonValue(fileObj, "download_url");
                 string type = ThunderstoreApi.ExtractJsonValue(fileObj, "type");
                 
                 if (type == "file")
                 {
                      ProcessAsset(packages, name, downloadUrl, author, repoInfo.Description, "1.0.0"); // Default version for raw files
                 }
                 
                 walker = objEnd + 1;
             }
             return packages;
        }

        private static void ProcessAsset(List<ThunderstorePackage> packages, string name, string downloadUrl, string author, string desc, string version)
        {
             // Filter: accept .zip and .dll
             // Exclude source code
             if (string.IsNullOrEmpty(name)) return;
             
             bool isZip = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
             bool isDll = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
             
             if ((isZip || isDll) && 
                 !name.StartsWith("source code", StringComparison.OrdinalIgnoreCase) && 
                 !name.StartsWith("src", StringComparison.OrdinalIgnoreCase))
             {
                 string modName = name.Replace(".zip", "")
                                      .Replace(".dll", "");
                 
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

        private class CuratedRepo
        {
            public string Repo; // "KnusBernd/Rooster"
            public string Description;
        }

        private static List<CuratedRepo> ParseRepoList(string json)
        {
            var list = new List<CuratedRepo>();
            int index = 0;
            while(index < json.Length)
            {
                int objStart = json.IndexOf('{', index);
                if (objStart < 0) break;
                int objEnd = FindMatchingClosingChar(json, objStart, '{', '}');
                if (objEnd < 0) break;
                
                string itemJson = json.Substring(objStart, objEnd - objStart + 1);
                string repo = ThunderstoreApi.ExtractJsonValue(itemJson, "repo");
                string desc = ThunderstoreApi.ExtractJsonValue(itemJson, "description");
                
                if (!string.IsNullOrEmpty(repo))
                {
                    list.Add(new CuratedRepo { Repo = repo, Description = desc });
                }
                index = objEnd + 1;
            }
            return list;
        }

        private static int FindMatchingClosingChar(string json, int openIndex, char openChar, char closeChar)
        {
            // Reusing logic from ThunderstoreApi would be better if public, duplicating strictly for simplicity in this task scope 
            // but actually I can't easily access private method. 
            // I'll copy-paste the logic or make ThunderstoreApi's method public? 
            // ThunderstoreApi.FindMatchingClosingChar is private. 
            // I'll make it public in ThunderstoreApi in next step or duplicate it.
            // Duplicate for now to avoid altering ThunderstoreApi visibility just for this.
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
