using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Centralizes Thunderstore API communication.
    /// Uses SimpleJSON for robust parsing.
    /// </summary>
    public static class ThunderstoreApi
    {
        public const string API_URL = "https://thunderstore.io/c/ultimate-chicken-horse/api/v1/package/";

        /// <summary>Fetches all packages from Thunderstore with retry logic.</summary>
        /// <summary>Fetches all packages from Thunderstore with retry logic.</summary>
        public static IEnumerator FetchAllPackages(Action<List<ThunderstorePackage>, string> onComplete)
        {
            yield return NetworkHelper.Get(API_URL, null, (success, result) =>
            {
                if (success)
                {
                    try
                    {
                        var packages = ParsePackageList(result);
                        onComplete?.Invoke(packages, null);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Error parsing package list: {ex}");
                        onComplete?.Invoke(new List<ThunderstorePackage>(), $"Parse Error: {ex.Message}");
                    }
                }
                else
                {
                    RoosterPlugin.LogError($"Failed to fetch packages: {result}");
                    onComplete?.Invoke(new List<ThunderstorePackage>(), result);
                }
            }, retries: 3);
        }

        public static List<ThunderstorePackage> ParsePackageList(string json)
        {
            var packages = new List<ThunderstorePackage>();
            var root = JSON.Parse(json);

            if (root == null || !root.IsArray) return packages;

            foreach (JSONNode pkgNode in root.AsArray)
            {
                string pkgName = pkgNode["name"];
                string owner = pkgNode["owner"];
                string fullName = pkgNode["full_name"];
                string dateCreated = pkgNode["date_created"];
                string dateUpdated = pkgNode["date_updated"];

                var versionsNode = pkgNode["versions"].AsArray;
                if (versionsNode == null || versionsNode.Count == 0) continue;

                JSONNode latestNode = versionsNode[0];
                JSONNode oldestNode = versionsNode[versionsNode.Count - 1];

                string verNum = latestNode["version_number"];
                string dlUrl = latestNode["download_url"];
                string websiteUrl = latestNode["website_url"];
                string description = latestNode["description"];

                string finalDate = (!string.IsNullOrEmpty(dateUpdated)) ? dateUpdated : dateCreated;

                if (!string.IsNullOrEmpty(pkgName) && !string.IsNullOrEmpty(verNum))
                {
                    var categories = new List<string>();
                    var catsNode = pkgNode["categories"].AsArray;
                    if (catsNode != null)
                    {
                        foreach (JSONNode c in catsNode) categories.Add(c.Value);
                    }

                    packages.Add(new ThunderstorePackage
                    {
                        Name = pkgName,
                        FullName = fullName ?? $"{owner}-{pkgName}",
                        WebsiteUrl = websiteUrl ?? "",
                        Description = description ?? "",
                        DateUpdated = finalDate ?? "",
                        Latest = new ThunderstoreVersion
                        {
                            VersionNumber = verNum,
                            DownloadUrl = dlUrl ?? "",
                        },
                        Oldest = new ThunderstoreVersion
                        {
                            VersionNumber = oldestNode["version_number"] ?? verNum,
                            DownloadUrl = oldestNode["download_url"] ?? dlUrl ?? "",
                        },
                        Categories = categories
                    });
                }
            }

            packages.Sort((a, b) => string.Compare(b.DateUpdated, a.DateUpdated, StringComparison.Ordinal));

            return packages;
        }

        public static IEnumerator FetchFreshVersion(string owner, string packageName, Action<string, string> onComplete)
        {
            string expUrl = $"https://thunderstore.io/api/experimental/package/{owner}/{packageName}/";

            yield return NetworkHelper.Get(expUrl, null, (success, result) =>
            {
                if (success)
                {
                    try
                    {
                        var root = JSON.Parse(result);
                        var latest = root["latest"];
                        string version = latest["version_number"];
                        string downloadUrl = latest["download_url"];
                        onComplete?.Invoke(version, downloadUrl);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Error parsing fresh version: {ex}");
                        onComplete?.Invoke(null, null);
                    }
                }
                else
                {
                    RoosterPlugin.LogError($"Failed to fetch fresh version for {owner}/{packageName}: {result}");
                    onComplete?.Invoke(null, null);
                }
            });
        }
    }
}
