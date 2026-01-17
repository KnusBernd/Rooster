using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.Networking;

namespace ThunderstoreUpdateChecker
{
    public class UpdateChecker
    {
        public static List<string> UpdatesAvailable = new List<string>();
        public static bool CheckComplete = false;

        private const string THUNDERSTORE_API_URL = "https://thunderstore.io/api/experimental/package/";

        public static IEnumerator CheckForUpdates()
        {
            // Delay to ensure UnityWebRequest system is ready and game is initialized
            yield return new WaitForSecondsRealtime(2.0f);

            ThunderstoreUpdateCheckerPlugin.LogInfo("Starting Update Check...");
            List<Tuple<BepInEx.PluginInfo, ModMapItem>> contentToCheck = new List<Tuple<BepInEx.PluginInfo, ModMapItem>>();

            try 
            {
                // ... (Load ModMap logic is fine) ...
                UpdatesAvailable.Clear();
                CheckComplete = false;

                // 1. Load Mod Map
                string dllPath = Path.GetDirectoryName(typeof(ThunderstoreUpdateCheckerPlugin).Assembly.Location);
                string mapPath = Path.Combine(dllPath, "ModMap.json");
                ThunderstoreUpdateCheckerPlugin.LogInfo($"Looking for ModMap at: {mapPath}");

                ModMapData modMap = null;
                if (File.Exists(mapPath)) 
                {
                    try 
                    {
                        string json = File.ReadAllText(mapPath);
                        ThunderstoreUpdateCheckerPlugin.LogInfo($"ModMap JSON: {json}");
                        
                        // Try JsonUtility first
                        try {
                            modMap = JsonUtility.FromJson<ModMapData>(json);
                        } catch { }

                        // Fallback to manual parsing if JsonUtility failed or returned null mods
                        if (modMap == null || modMap.mods == null || modMap.mods.Length == 0)
                        {
                            ThunderstoreUpdateCheckerPlugin.LogInfo("JsonUtility failed. Attempting manual parsing...");
                            modMap = ManualParseModMap(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        ThunderstoreUpdateCheckerPlugin.LogError($"Failed to load ModMap.json: {ex.Message}");
                    }
                }
                else
                {
                    ThunderstoreUpdateCheckerPlugin.LogWarning($"ModMap.json not found at {mapPath}. Creating default.");
                    modMap = new ModMapData();
                    modMap.mods = new ModMapItem[]
                    {
                        new ModMapItem { guid = "BuildingPlus", thunderstore_namespace = "Daniel", thunderstore_name = "BuildingPlus" }
                    };
                    try 
                    {
                       File.WriteAllText(mapPath, JsonUtility.ToJson(modMap, true));
                    } catch { }
                }

                if (modMap == null)
                {
                    ThunderstoreUpdateCheckerPlugin.LogWarning("ModMap object is null.");
                    CheckComplete = true; // Still marked complete so popup knows we are done (even if failed)
                }
                else if (modMap.mods == null)
                {
                     ThunderstoreUpdateCheckerPlugin.LogWarning("ModMap.mods is null (Deserialization failed).");
                     // Do not set CheckComplete here, allowing scan to run for debug purposes
                }
                else
                {
                    ThunderstoreUpdateCheckerPlugin.LogInfo($"ModMap loaded with {modMap.mods.Length} entries.");
                }

                // 2. Scan Installed Plugins (Running always for debug)
                ThunderstoreUpdateCheckerPlugin.LogInfo($"Scanning {Chainloader.PluginInfos.Count} plugins...");
                foreach (var plugin in Chainloader.PluginInfos.Values)
                {
                    string guid = plugin.Metadata.GUID;
                    string currentVersion = plugin.Metadata.Version.ToString();
                    
                    // Debug log to see installed plugins
                    ThunderstoreUpdateCheckerPlugin.LogInfo($"Installed Plugin: GUID='{guid}', Ver='{currentVersion}'");

                    if (modMap != null && modMap.mods != null)
                    {
                        // Use simple loop to avoid LINQ issues if any
                        ModMapItem mapItem = null;
                        foreach(var item in modMap.mods)
                        {
                            if(item.guid == guid) { mapItem = item; break; }
                        }

                        if (mapItem != null)
                        {
                            contentToCheck.Add(new Tuple<BepInEx.PluginInfo, ModMapItem>(plugin, mapItem));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ThunderstoreUpdateCheckerPlugin.LogError($"Update Check Setup Error: {ex}");
            }

            // Execute checks
            ThunderstoreUpdateCheckerPlugin.LogInfo($"Found {contentToCheck.Count} plugins to check.");
            
            // Loop cannot be comfortably wrapped in try-catch with yield return inside due to C# iterator constraints in some versions
            // but in recent C# it is fine.
            // Safe approach: Log errors inside the loop logic (which is in CheckSingleMod).
            // But if `CheckSingleMod` start throws?
            
            foreach(var item in contentToCheck)
            {
                var plugin = item.Item1;
                var mapItem = item.Item2;
                ThunderstoreUpdateCheckerPlugin.LogInfo($"Checking update for {mapItem.thunderstore_name}...");
                yield return CheckSingleMod(plugin.Metadata.Name, plugin.Metadata.Version.ToString(), mapItem.thunderstore_namespace, mapItem.thunderstore_name);
            }

            CheckComplete = true;
            ThunderstoreUpdateCheckerPlugin.LogInfo($"Update Check Complete. Found {UpdatesAvailable.Count} updates.");
        }

        private static IEnumerator CheckSingleMod(string modName, string currentVersion, string ns, string name)
        {
            string url = $"{THUNDERSTORE_API_URL}{ns}/{name}/";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    ThunderstoreUpdateCheckerPlugin.LogError($"Update check failed for {modName}: {www.error}");
                }
                else
                {
                    try
                    {
                        string json = www.downloadHandler.text;
                        ThunderstorePackage package = JsonUtility.FromJson<ThunderstorePackage>(json);
                        string latestVersion = null;

                        if (package != null && package.latest != null)
                        {
                            latestVersion = package.latest.version_number;
                        }
                        else
                        {
                            ThunderstoreUpdateCheckerPlugin.LogWarning($"[CheckSingleMod] JsonUtility failed to parse 'latest' for {modName}. Trying manual.");
                            // Simple manual extraction for "latest" block's "version_number"
                            // Assumes "latest" block contains "version_number"
                            // We scan for "latest" then "version_number" in proximity? 
                            // Dangerous because of "versions" list.
                            
                            // Better: regex for "latest" followed by content containing version_number
                            // "latest":\s*\{[^}]*"version_number":\s*"([^"]+)"
                            var match = System.Text.RegularExpressions.Regex.Match(json, "\"latest\"\\s*:\\s*\\{[^}]*\"version_number\"\\s*:\\s*\"([^\"]+)\"");
                            if (match.Success)
                            {
                                latestVersion = match.Groups[1].Value;
                                ThunderstoreUpdateCheckerPlugin.LogInfo($"[CheckSingleMod] Manual parse found version: {latestVersion}");
                            }
                        }

                        if (!string.IsNullOrEmpty(latestVersion))
                        {
                            if (CompareVersions(currentVersion, latestVersion))
                            {
                                UpdatesAvailable.Add($"{modName}: v{currentVersion} -> v{latestVersion}");
                                ThunderstoreUpdateCheckerPlugin.LogInfo($"Update found for {modName}: {latestVersion}");
                            }
                            else
                            {
                                ThunderstoreUpdateCheckerPlugin.LogInfo($"{modName} is up to date (Latest: {latestVersion}).");
                            }
                        }
                        else
                        {
                             ThunderstoreUpdateCheckerPlugin.LogError($"Could not determine latest version for {modName} (Parsing failed).");
                        }
                    }
                    catch (Exception ex)
                    {
                        ThunderstoreUpdateCheckerPlugin.LogError($"Failed to parse response for {modName}: {ex}");
                    }
                }
            }
        }

        private static bool CompareVersions(string current, string latest)
        {
            try
            {
                Version vCurrent = new Version(current);
                Version vLatest = new Version(latest);
                return vLatest > vCurrent;
            }
            catch
            {
                return string.Compare(latest, current) > 0;
            }
        }

        private static ModMapData ManualParseModMap(string json)
        {
            var data = new ModMapData();
            var list = new List<ModMapItem>();

            // 1. Remove outer wrapper to get array content
            int firstBracket = json.IndexOf('[');
            int lastBracket = json.LastIndexOf(']');
            if (firstBracket < 0 || lastBracket < 0) return data;

            string content = json.Substring(firstBracket + 1, lastBracket - firstBracket - 1);
            
            // 2. Split by objects. A simple split by "}," works for flat objects.
            string[] rawItems = content.Split(new string[] { "}," }, StringSplitOptions.None);

            foreach (string rawItem in rawItems)
            {
                var item = new ModMapItem();
                item.guid = ExtractJsonValue(rawItem, "guid");
                item.thunderstore_namespace = ExtractJsonValue(rawItem, "thunderstore_namespace");
                item.thunderstore_name = ExtractJsonValue(rawItem, "thunderstore_name");

                if (!string.IsNullOrEmpty(item.guid))
                {
                    list.Add(item);
                }
            }
            
            data.mods = list.ToArray();
            return data;
        }

        private static string ExtractJsonValue(string source, string key)
        {
            // Matches "key": "value"
            string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(source, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
    }

    [Serializable]
    public class ModMapData
    {
        public ModMapItem[] mods;
    }

    [Serializable]
    public class ModMapItem
    {
        public string guid;
        public string thunderstore_namespace;
        public string thunderstore_name;
    }

    [Serializable]
    public class ThunderstorePackage
    {
        public string name;
        public string full_name;
        public ThunderstoreVersion latest;
    }

    [Serializable]
    public class ThunderstoreVersion
    {
        public string version_number;
    }
}
