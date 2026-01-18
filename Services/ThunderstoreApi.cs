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
                int firstVerObjEnd = FindMatchingBrace(json, firstVerObjStart);
                if (firstVerObjEnd < 0) break;
                
                string firstVerObj = json.Substring(firstVerObjStart, firstVerObjEnd - firstVerObjStart + 1);
                
                // Extract version details
                string verNum = ExtractJsonValue(firstVerObj, "version_number");
                string dlUrl = ExtractJsonValue(firstVerObj, "download_url");
                // NOTE: Thunderstore API v1 does not provide file hashes. Hash verification is not currently possible.
                
                // Extract package metadata
                string packageMeta = json.Substring(pkgStart, versionsKeyIdx - pkgStart);
                string fullName = ExtractJsonValue(packageMeta, "full_name");

                if (!string.IsNullOrEmpty(pkgName) && !string.IsNullOrEmpty(verNum))
                {
                    packages.Add(new ThunderstorePackage
                    {
                        name = pkgName,
                        full_name = fullName ?? "",
                        latest = new ThunderstoreVersion
                        {
                            version_number = verNum,
                            download_url = dlUrl ?? "",
                            file_hash = "" // Thunderstore API v1 does not provide hashes
                        }
                    });
                }
                
                // Advance index
                index = firstVerObjEnd + 1;
            }

            return packages;
        }
        
        private static int FindMatchingBrace(string json, int openBraceIndex)
        {
            int depth = 0;
            bool inString = false;
            // Iterate through string considering escape characters
            for (int i = openBraceIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i-1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == '{') depth++;
                    else if (c == '}')
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
    }
}
