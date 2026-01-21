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
        public static List<ThunderstorePackage> CachedPackages = new List<ThunderstorePackage>();
        public static bool IsCacheReady = false;
        public static bool IsCaching = false;
        public static string LastError = null;

        private static string _cachedToken = null;
        private static bool _hasCheckedToken = false;

        public static IEnumerator BuildCache()
        {
            if (IsCaching || IsCacheReady) yield break;

            IsCaching = true;
            LastError = null;
            // RoosterPlugin.LogInfo("Starting GitHub cache build...");

            var cached = LoadCache();

            if (cached != null)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int duration = RoosterConfig.GitHubCacheDuration.Value;

                if (now - cached.Timestamp < duration && cached.Packages != null && cached.Packages.Count > 0)
                {
                    // RoosterPlugin.LogInfo($"Using Valid Disk Cache (Age: {(now - cached.Timestamp)}s)");
                    CachedPackages = cached.Packages;
                    IsCacheReady = true;
                    IsCaching = false;
                    yield break;
                }
                else
                {
                    // RoosterPlugin.LogInfo("Disk Cache expired. Attempting to fetch fresh...");
                }
            }

            yield return FetchCuratedList((packages, error) =>
            {
                if (error != null)
                {
                    bool isRateLimit = error.Contains("Rate Limit") || error.Contains("403") || error.Contains("429");

                    if (isRateLimit && cached != null && cached.Packages != null && cached.Packages.Count > 0)
                    {
                        RoosterPlugin.LogWarning($"GitHub Rate Limit hit. Falling back to stale disk cache (Age: {(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - cached.Timestamp)}s).");
                        CachedPackages = cached.Packages;
                        IsCacheReady = true;
                    }
                    else
                    {
                        RoosterPlugin.LogError($"GitHub Code Cache failed: {error}");
                        LastError = error;
                    }
                }
                else
                {
                    CachedPackages = packages;
                    IsCacheReady = true;
                    SaveCache(packages);
                }
                IsCaching = false;
            });
        }

        private static void SaveCache(List<ThunderstorePackage> packages)
        {
            try
            {
                var root = new JSONObject();
                root["Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var arr = new JSONArray();
                foreach (var pkg in packages)
                {
                    arr.Add(pkg.ToJson());
                }
                root["Packages"] = arr;

                string json = root.ToString();
                string path = System.IO.Path.Combine(RoosterConfig.RoosterConfigPath, "RoosterCache.json");
                System.IO.File.WriteAllText(path, json);
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
                string path = System.IO.Path.Combine(RoosterConfig.RoosterConfigPath, "RoosterCache.json");
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var root = JSON.Parse(json);
                    if (root != null)
                    {
                        var cache = new RoosterCache();
                        cache.Timestamp = root["Timestamp"].AsLong;
                        cache.Packages = new List<ThunderstorePackage>();

                        var arr = root["Packages"].AsArray;
                        if (arr != null)
                        {
                            foreach (JSONNode node in arr)
                            {
                                cache.Packages.Add(ThunderstorePackage.FromJson(node));
                            }
                        }
                        return cache;
                    }
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
            string url = RoosterConfig.GitHubCuratedUrl.Value;
            var allPackages = new List<ThunderstorePackage>();

            bool done = false;
            string resultJson = null;
            string errorStr = null;

            yield return NetworkHelper.Get(url, null, (success, result) =>
            {
                if (success) resultJson = result;
                else errorStr = result;
                done = true;
            });

            yield return new WaitUntil(() => done);

            if (!string.IsNullOrEmpty(errorStr))
            {
                string errorMsg = $"Failed to list: {errorStr}";
                RoosterPlugin.LogError(errorMsg);
                onComplete?.Invoke(allPackages, errorMsg);
                yield break;
            }

            List<CuratedRepo> repos = new List<CuratedRepo>();
            try
            {
                repos = ParseRepoList(resultJson);
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

            yield return ProcessRepos(repos, allPackages, onComplete);
        }

        private static IEnumerator ProcessRepos(List<CuratedRepo> repos, List<ThunderstorePackage> allPackages, Action<List<ThunderstorePackage>, string> onComplete)
        {
            int completed = 0;
            object lockObj = new object();
            bool rateLimitHit = false;
            string rateLimitError = null;

            foreach (var repo in repos)
            {
                RoosterPlugin.Instance.StartCoroutine(ProcessSingleRepo(repo, allPackages, (error) =>
                {
                    lock (lockObj)
                    {
                        if (error != null && (error.Contains("403") || error.Contains("429")))
                        {
                            rateLimitHit = true;
                            rateLimitError = "GitHub API Rate Limit Exceeded. Please wait before trying again.";
                        }
                        completed++;
                    }
                }));
            }

            yield return new WaitUntil(() => completed >= repos.Count);

            if (rateLimitHit)
            {
                onComplete?.Invoke(allPackages, rateLimitError);
            }
            else
            {
                allPackages.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                onComplete?.Invoke(allPackages, null);
            }
        }

        private static IEnumerator ProcessSingleRepo(CuratedRepo repo, List<ThunderstorePackage> allPackages, Action<string> onRepoDone)
        {
            RepoMetadata metadata = null;
            string lastError = null;

            // 1. Fetch Metadata (Fork info)
            yield return FetchRepoMetadata(repo, (meta, err) =>
            {
                metadata = meta;
                if (err != null) lastError = err;
            });

            // mod data
            yield return FetchRepoReleases(repo, (pkgs, err) =>
            {
                if (err != null) lastError = err;

                if (pkgs != null)
                {
                    if (metadata != null && !string.IsNullOrEmpty(metadata.ParentOwner))
                    {
                        foreach (var pkg in pkgs)
                        {
                            pkg.SecondaryAuthor = metadata.ParentOwner;
                        }
                    }
                    lock (allPackages)
                    {
                        allPackages.AddRange(pkgs);
                    }
                }
            });

            onRepoDone?.Invoke(lastError);
        }

        private static IEnumerator FetchRepoMetadata(CuratedRepo repoInfo, Action<RepoMetadata, string> onComplete)
        {
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Repo}";
            var headers = new Dictionary<string, string>();
            string token = GetToken();
            if (!string.IsNullOrEmpty(token)) headers["Authorization"] = $"Bearer {token}";

            yield return NetworkHelper.Get(apiUrl, headers, (success, result) =>
            {
                if (!success)
                {
                    onComplete?.Invoke(null, result);
                    return;
                }

                try
                {
                    var node = JSON.Parse(result);
                    var meta = new RepoMetadata();
                    meta.IsFork = node["fork"].AsBool;
                    if (meta.IsFork)
                    {
                        meta.ParentOwner = node["parent"]["owner"]["login"];
                    }
                    onComplete?.Invoke(meta, null);
                }
                catch (Exception ex)
                {
                    onComplete?.Invoke(null, ex.Message);
                }
            });
        }

        private static string GetToken()
        {
            if (_hasCheckedToken) return _cachedToken;

            _hasCheckedToken = true;
            try
            {
                string rawPath = RoosterConfig.GitHubTokenPath.Value;
                string path = System.IO.Path.IsPathRooted(rawPath)
                    ? rawPath
                    : System.IO.Path.Combine(RoosterConfig.RoosterConfigPath, rawPath);
                if (System.IO.File.Exists(path))
                {
                    string token = System.IO.File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
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
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Repo}/releases";
            var headers = new Dictionary<string, string>();
            string token = GetToken();
            if (!string.IsNullOrEmpty(token)) headers["Authorization"] = $"Bearer {token}";

            bool done = false;
            string resultJson = null;
            string errorStr = null;

            yield return NetworkHelper.Get(apiUrl, headers, (success, result) =>
            {
                if (success) resultJson = result;
                else errorStr = result;
                done = true;
            });

            yield return new WaitUntil(() => done);

            if (string.IsNullOrEmpty(resultJson))
            {
                if (errorStr != null && (errorStr.Contains("403") || errorStr.Contains("429")))
                {
                    RoosterPlugin.LogInfo($"GitHub API Error: {errorStr}");
                    onRepoComplete?.Invoke(null, errorStr);
                    yield break;
                }

                RoosterPlugin.LogInfo($"[Releases] Failed for {repoInfo.Repo} ({errorStr}), trying contents...");
                yield return FetchRepoContents(repoInfo, onRepoComplete);
                yield break;
            }

            bool shouldFetchRepoContents = false;
            List<ThunderstorePackage> packs = null;
            try
            {
                packs = ParseReleasesListJson(resultJson, repoInfo);
                if (packs.Count == 0)
                {
                    RoosterPlugin.LogInfo($"[Releases] No valid assets found in releases for {repoInfo.Repo}, trying contents...");
                    shouldFetchRepoContents = true;
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"[Releases] Error parsing for {repoInfo.Repo}: {ex}");
                shouldFetchRepoContents = true;
            }

            if (shouldFetchRepoContents)
            {
                yield return FetchRepoContents(repoInfo, onRepoComplete);
            }
            else if (packs != null)
            {
                onRepoComplete?.Invoke(packs, null);
            }
        }

        private static IEnumerator FetchRepoContents(CuratedRepo repoInfo, Action<List<ThunderstorePackage>, string> onRepoComplete)
        {
            string commitUrl = $"https://api.github.com/repos/{repoInfo.Repo}/commits/HEAD";
            var headers = new Dictionary<string, string>();
            string token = GetToken();
            if (!string.IsNullOrEmpty(token)) headers["Authorization"] = $"Bearer {token}";

            string headSha = null;

            bool done = false;
            string commitJson = null;
            string errorStr = null;

            yield return NetworkHelper.Get(commitUrl, headers, (success, result) =>
            {
                if (success) commitJson = result;
                else errorStr = result;
                done = true;
            });

            yield return new WaitUntil(() => done);

            if (string.IsNullOrEmpty(commitJson))
            {
                string err = $"[RecursiveFetch] Failed to get HEAD for {repoInfo.Repo}: {errorStr}";
                RoosterPlugin.LogError(err);
                onRepoComplete?.Invoke(null, err);
                yield break;
            }

            try
            {
                var node = JSON.Parse(commitJson);
                headSha = node["sha"];
            }
            catch { }

            if (string.IsNullOrEmpty(headSha))
            {
                 if (headSha == null) // If it wasn't set in callback, we already invoked error there or it failed silently.
                 {
                     // If we are here and success was true but parse failed, we need to handle it.
                     // But simplify: if headSha is null here, stop.
                     yield break;
                 }
            }

            string treeUrl = $"https://api.github.com/repos/{repoInfo.Repo}/git/trees/{headSha}?recursive=1";

            bool treeDone = false;
            string treeJson = null;
            string treeError = null;

            yield return NetworkHelper.Get(treeUrl, headers, (success, result) =>
            {
                if (success) treeJson = result;
                else treeError = result;
                treeDone = true;
            });

            yield return new WaitUntil(() => treeDone);

            if (string.IsNullOrEmpty(treeJson))
            {
                string err = $"[RecursiveFetch] Failed to get Tree for {repoInfo.Repo}: {treeError}";
                RoosterPlugin.LogError(err);
                onRepoComplete?.Invoke(null, err);
                yield break;
            }

            try
            {
                var packs = ParseTreeJson(treeJson, repoInfo, headSha);
                onRepoComplete?.Invoke(packs, null);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"[RecursiveFetch] Error parsing tree for {repoInfo.Repo}: {ex}");
                onRepoComplete?.Invoke(null, ex.Message);
            }
        }

        private static List<ThunderstorePackage> ParseReleasesListJson(string json, CuratedRepo repoInfo)
        {
            var packages = new List<ThunderstorePackage>();
            var root = JSON.Parse(json);

            if (root == null || !root.IsArray) return packages;

            foreach (JSONNode release in root.AsArray)
            {
                bool isDraft = release["draft"].AsBool;
                string tagName = release["tag_name"];

                if (!isDraft && !string.IsNullOrEmpty(tagName) && !tagName.StartsWith("untagged", StringComparison.OrdinalIgnoreCase))
                {
                    var result = ParseSingleReleaseNode(release, repoInfo);
                    if (result.Count > 0)
                    {
                        return result;
                    }
                }
            }

            return packages;
        }

        private static List<ThunderstorePackage> ParseSingleReleaseNode(JSONNode release, CuratedRepo repoInfo)
        {
            var packages = new List<ThunderstorePackage>();

            string tagName = release["tag_name"];
            string body = release["body"];
            string author = repoInfo.Repo.Split('/')[0];

            var assets = release["assets"].AsArray;
            if (assets != null)
            {
                foreach (JSONNode asset in assets)
                {
                    string name = asset["name"];
                    string downloadUrl = asset["browser_download_url"];

                    ProcessAsset(packages, name, downloadUrl, author, repoInfo.Description ?? body, tagName, repoInfo.Repo);
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
                            ProcessAsset(packages, filename, url, author, repoInfo.Description ?? body, tagName, repoInfo.Repo);
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

            var root = JSON.Parse(json);
            var tree = root["tree"].AsArray;

            if (tree == null)
            {
                RoosterPlugin.LogError($"[RecursiveFetch] 'tree' key not found or not array in JSON for {repoInfo.Repo}");
                return packages;
            }

            foreach (JSONNode item in tree)
            {
                string path = item["path"];
                string type = item["type"];

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
                            // RoosterPlugin.LogInfo($"[RecursiveFetch] Ignored source artifact: {path}");
                        }
                        else
                        {
                            string downloadUrl = $"https://raw.githubusercontent.com/{repoInfo.Repo}/{sha}/{path}";

                            ProcessAsset(packages, filename, downloadUrl, author, repoInfo.Description, "1.0.0", repoInfo.Repo);
                        }
                    }
                }
            }

            return packages;
        }

        private static void ProcessAsset(List<ThunderstorePackage> packages, string name, string downloadUrl, string author, string desc, string version, string repoName)
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
                    Name = modName,
                    FullName = $"{author}-{modName}",
                    Description = desc ?? "No description provided.",
                    WebsiteUrl = $"https://github.com/{repoName}",
                    Latest = new ThunderstoreVersion
                    {
                        VersionNumber = version != null ? version.TrimStart('v') : "1.0.0",
                        DownloadUrl = downloadUrl
                    },
                    Categories = new List<string> { "GitHub" }
                });
            }
        }

        private static string StripVersionFromName(string name)
        {
            int lastHyphen = name.LastIndexOf('-');
            if (lastHyphen > 0 && lastHyphen < name.Length - 1)
            {
                string potentialVersion = name.Substring(lastHyphen + 1);
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

        private class RepoMetadata
        {
            public bool IsFork;
            public string ParentOwner;
        }

        private static List<CuratedRepo> ParseRepoList(string json)
        {
            var list = new List<CuratedRepo>();
            if (string.IsNullOrEmpty(json)) return list;

            var root = JSON.Parse(json);
            if (root == null) return list;

            if (root.IsArray)
            {
                foreach (JSONNode item in root.AsArray)
                {
                    string repo = item["repo"];
                    string desc = item["description"];

                    if (!string.IsNullOrEmpty(repo))
                    {
                        list.Add(new CuratedRepo { Repo = repo, Description = desc });
                    }
                }
            }
            else if (root.IsObject)
            {
                string repo = root["repo"];
                string desc = root["description"];
                if (!string.IsNullOrEmpty(repo))
                {
                    list.Add(new CuratedRepo { Repo = repo, Description = desc });
                }
            }

            return list;
        }
    }
}
