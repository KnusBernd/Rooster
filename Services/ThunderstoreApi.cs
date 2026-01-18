using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Handles communication with the Thunderstore API.
    /// Fetches and parses the package list for Ultimate Chicken Horse.
    /// </summary>
    public static class ThunderstoreApi
    {
        public const string API_URL = "https://thunderstore.io/c/ultimate-chicken-horse/api/v1/package/";

        /// <summary>Fetches all packages from Thunderstore with retry logic.</summary>
        public static IEnumerator FetchAllPackages(Action<List<ThunderstorePackage>> onComplete)
        {
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                RoosterPlugin.LogInfo($"Fetching all packages from: {API_URL} (Attempt {i+1}/{maxRetries})");
                
                using (UnityWebRequest www = UnityWebRequest.Get(API_URL))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        RoosterPlugin.LogError($"Failed to fetch packages: {www.error}");
                    }
                    else
                    {
                        try
                        {
                            string json = www.downloadHandler.text;
                            
                            // Debug: Write raw JSON to file to verify content (e.g. check for 'categories' or 'tags')
                            try {
                                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "Rooster_RawResponse.json"), json);
                                RoosterPlugin.LogInfo($"Wrote raw API response to: {System.IO.Path.Combine(Application.persistentDataPath, "Rooster_RawResponse.json")}");
                            } catch (Exception ex) {
                                RoosterPlugin.LogError($"Failed to write raw response to file: {ex}");
                            }

                            var packages = ParsePackageList(json);
                            RoosterPlugin.LogInfo($"Fetched and parsed {packages.Count} packages.");
                            onComplete?.Invoke(packages);
                            yield break;
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogError($"Error parsing package list: {ex}");
                        }
                    }
                }
                
                if (i < maxRetries - 1)
                {
                    RoosterPlugin.LogInfo("Retrying in 2 seconds...");
                    yield return new WaitForSecondsRealtime(2.0f);
                }
            }
            
            RoosterPlugin.LogError($"Gave up after {maxRetries} attempts.");
            onComplete?.Invoke(new List<ThunderstorePackage>());
        }

        /// <summary>Manually parses JSON to extract package data (avoids full deserialization overhead).</summary>
        public static List<ThunderstorePackage> ParsePackageList(string json)
        {
            var packages = new List<ThunderstorePackage>();
            
            // Manual JSON parsing to avoid allocation overhead
            int index = 0;
            while (index < json.Length)
            {
                // Find next owner field
                int ownerIdx = json.IndexOf("\"owner\":", index);
                if (ownerIdx < 0) break;
                
                // Backtrack to find name field
                int pkgStart = json.LastIndexOf("{\"name\":", ownerIdx);
                if (pkgStart < 0 || pkgStart < index) 
                {
                    index = ownerIdx + 8;
                    continue;
                }
                
                // Extract package name
                int nameStart = pkgStart + 9;
                int nameEnd = json.IndexOf('"', nameStart);
                if (nameEnd < 0) break;
                
                string pkgName = json.Substring(nameStart, nameEnd - nameStart);
                
                // Find versions array
                int versionsKeyIdx = json.IndexOf("\"versions\":", ownerIdx);
                if (versionsKeyIdx < 0) break;
                
                // Find first version object (latest)
                int arrStart = json.IndexOf('[', versionsKeyIdx);
                if (arrStart < 0) break;
                int firstVerObjStart = json.IndexOf('{', arrStart);
                if (firstVerObjStart < 0) break;
                
                // Find closing brace of first version object
                int firstVerObjEnd = FindMatchingClosingChar(json, firstVerObjStart, '{', '}');
                if (firstVerObjEnd < 0) break;
                
                string firstVerObj = json.Substring(firstVerObjStart, firstVerObjEnd - firstVerObjStart + 1);
                
                // Extract version details
                string verNum = ExtractJsonValue(firstVerObj, "version_number");
                string dlUrl = ExtractJsonValue(firstVerObj, "download_url");
                string websiteUrl = ExtractJsonValue(firstVerObj, "website_url");
                
                // Extract package metadata
                string packageMeta = json.Substring(pkgStart, versionsKeyIdx - pkgStart);
                string fullName = ExtractJsonValue(packageMeta, "full_name");
                // website_url is in the version object, not package meta

                if (!string.IsNullOrEmpty(pkgName) && !string.IsNullOrEmpty(verNum))
                {
                    // Parse oldest version by iterating through all versions in the array
                    string oldestVerNum = null;
                    string oldestDlUrl = null;

                    int versionsStartIdx = json.IndexOf('[', versionsKeyIdx);
                    if (versionsStartIdx >= 0)
                    {
                        int versionsEndIdx = FindMatchingClosingChar(json, versionsStartIdx, '[', ']');
                        if (versionsEndIdx > versionsStartIdx)
                        {
                            // Iterate children of the versions array
                            int walker = versionsStartIdx + 1;
                            while (walker < versionsEndIdx)
                            {
                                int nextObjStart = json.IndexOf('{', walker);
                                if (nextObjStart < 0 || nextObjStart >= versionsEndIdx) break;

                                int nextObjEnd = FindMatchingClosingChar(json, nextObjStart, '{', '}');
                                if (nextObjEnd < 0) break; // Should not happen if JSON is valid

                                // This is a candidate for oldest (since we are moving Newest -> Oldest)
                                // We just keep updating it, so the last one we see is the Oldest.
                                string vObj = json.Substring(nextObjStart, nextObjEnd - nextObjStart + 1);
                                oldestVerNum = ExtractJsonValue(vObj, "version_number");
                                oldestDlUrl = ExtractJsonValue(vObj, "download_url");

                                walker = nextObjEnd + 1;
                            }
                        }
                    }

                    packages.Add(new ThunderstorePackage
                    {
                        name = pkgName,
                        full_name = fullName ?? "",
                        website_url = websiteUrl ?? "",
                        latest = new ThunderstoreVersion
                        {
                            version_number = verNum,
                            download_url = dlUrl ?? "",
                        },
                        oldest = new ThunderstoreVersion
                        {
                            version_number = oldestVerNum ?? verNum,
                            download_url = oldestDlUrl ?? dlUrl ?? "",
                        },
                        categories = ExtractJsonStringArray(packageMeta, "categories")
                    });
                }
                
                // Advance index
                index = firstVerObjEnd + 1;
            }

            return packages;
        }
        
        private static int FindMatchingClosingChar(string json, int openIndex, char openChar, char closeChar)
        {
            int depth = 0;
            bool inString = false;
            // Iterate through string considering escape characters
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

        /// <summary>Extracts a string value for a given key from a JSON fragment.</summary>
        public static string ExtractJsonValue(string source, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(source, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        public static List<string> ExtractJsonStringArray(string source, string key)
        {
            var list = new List<string>();
            string pattern = $"\"{key}\"\\s*:\\s*\\[(.*?)\\]";
            var match = System.Text.RegularExpressions.Regex.Match(source, pattern);
            if (match.Success)
            {
                string content = match.Groups[1].Value;
                // Capture content inside quotes
                var matches = System.Text.RegularExpressions.Regex.Matches(content, "\"([^\"]*)\"");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    list.Add(m.Groups[1].Value);
                }
            }
            return list;
        }

        /// <summary>Fetches fresh version data for a package from the experimental API (bypasses CDN cache).</summary>
        public static IEnumerator FetchFreshVersion(string owner, string packageName, Action<string, string> onComplete)
        {
            string expUrl = $"https://thunderstore.io/api/experimental/package/{owner}/{packageName}/";
            RoosterPlugin.LogInfo($"Fetching fresh version from experimental API: {expUrl}");
            
            using (UnityWebRequest www = UnityWebRequest.Get(expUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    RoosterPlugin.LogError($"Failed to fetch fresh version for {owner}/{packageName}: {www.error}");
                    onComplete?.Invoke(null, null);
                }
                else
                {
                    try
                    {
                        string json = www.downloadHandler.text;
                        // Parse latest.version_number from experimental API response
                        string version = ExtractJsonValue(json, "version_number");
                        string downloadUrl = ExtractJsonValue(json, "download_url");
                        RoosterPlugin.LogInfo($"Fresh version for {owner}/{packageName}: {version}");
                        onComplete?.Invoke(version, downloadUrl);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Error parsing fresh version: {ex}");
                        onComplete?.Invoke(null, null);
                    }
                }
            }
        }
    }
}
