using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using Rooster.Models;

namespace Rooster.Services
{
    public enum UninstallScope
    {
        Manual,
        Discovered,
        Tracked
    }

    public static class ModUninstaller
    {
        public static UninstallScope GetUninstallScope(PluginInfo plugin)
        {
            if (plugin == null || plugin.Metadata == null) return UninstallScope.Manual;

            string guid = plugin.Metadata.GUID;
            if (UpdateChecker.MatchedPackages.TryGetValue(guid, out var pkg))
            {
                 // Check central manifest location
                 string manifestPath = GetManifestPath(pkg.full_name);
                 if (File.Exists(manifestPath)) return UninstallScope.Tracked;
            }

            // Fallback: check for manifest.json in the directory of the DLL (legacy support)
            string dir = Path.GetDirectoryName(plugin.Location);
            if (File.Exists(Path.Combine(dir, "manifest.json")))
            {
                return UninstallScope.Tracked;
            }

            // Matched but no manifest => likely a flat install or just a DLL dropped in
            return UninstallScope.Discovered;
        }

        private static string GetManifestPath(string fullName)
        {
            string filename = string.Join("_", fullName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Paths.ConfigPath, "Rooster", "Manifests", $"{filename}.json");
        }

        public static void UninstallMod(PluginInfo plugin, bool deleteConfig, Action<bool, string> onComplete)
        {
            if (plugin.Metadata.GUID.Equals("de.knusbernd.rooster", StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(false, "Cannot uninstall Rooster itself.");
                return;
            }

            try
            {
                string guid = plugin.Metadata.GUID;
                string dllPath = plugin.Location;
                string dir = Path.GetDirectoryName(dllPath);

                // Always require restart for DLL uninstallation
                bool restartRequired = true;
                
                // SAFETY GUARD: Never delete the plugins root or BepInEx root
                if (IsProtectedDirectory(dir))
                {
                    RoosterPlugin.LogWarning($"[Uninstall] Safety Triggered: Directory {dir} is protected. Switching to file-only mode.");
                }
                
                RoosterPlugin.LogInfo("[Uninstall] Trace: Resolving files to delete...");

                List<string> filesToDelete = new List<string>();
                
                // Try to find manifest
                string manifestPath = null;
                if (UpdateChecker.MatchedPackages.TryGetValue(guid, out var pkg))
                {
                    manifestPath = GetManifestPath(pkg.full_name);
                }

                // Legacy fallback
                if (!File.Exists(manifestPath))
                {
                     manifestPath = Path.Combine(dir, "manifest.json");
                }

                if (File.Exists(manifestPath))
                {
                    RoosterPlugin.LogInfo($"[Uninstall] Trace: Found manifest at {manifestPath}");
                    try 
                    {
                        var tpkg = JsonUtility.FromJson<ThunderstorePackage>(File.ReadAllText(manifestPath));
                        if (tpkg != null && tpkg.files != null && tpkg.files.Count > 0)
                        {
                            filesToDelete = tpkg.files;
                            // Do NO add manifest.json if it is central
                            if (Path.GetDirectoryName(manifestPath) == dir) filesToDelete.Add("manifest.json"); 
                        }
                    } 
                    catch { /* Invalid manifest */ }
                }

                RoosterPlugin.LogInfo($"[Uninstall] Trace: Files to delete count: {filesToDelete.Count}, Mode: {(IsProtectedDirectory(dir) ? "Protected" : "Folder")}");

                // Installation cleanup
                bool isCentralManifest = manifestPath != null && manifestPath.Contains("Rooster");
                
                if (IsProtectedDirectory(dir))
                {
                    // Case A: Root/Protected Dir (e.g. plugins/)
                    if (filesToDelete.Count > 0)
                    {
                        foreach(var file in filesToDelete)
                        {
                            RoosterPlugin.LogInfo($"[Uninstall] Trace: Processing file {file}");
                            TryDeleteFile(Path.Combine(dir, file));
                        }
                    }
                    else
                    {
                        RoosterPlugin.LogInfo($"[Uninstall] Trace: Deleting main DLL {dllPath}");
                        TryDeleteFile(dllPath);
                    }
                }
                else
                {
                    // Case B: Subfolder
                    if (filesToDelete.Count > 0)
                    {
                        foreach(var file in filesToDelete)
                        {
                            try { TryDeleteFile(Path.Combine(dir, file)); } catch {}
                        }
                        
                         try { 
                             if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                                Directory.Delete(dir, false); 
                         } catch { } 
                    }
                    else
                    {
                        // Legacy behavior: Delete entire folder
                        if (Directory.Exists(dir))
                        {
                            RoosterPlugin.LogInfo($"[Uninstall] Trace: Deleting directory {dir}");
                            try 
                            {
                                Directory.Delete(dir, true); 
                            }
                            catch 
                            {
                                foreach(var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) 
                                { 
                                    TryDeleteFile(file);
                                }
                                 try { Directory.Delete(dir, true); } catch { } 
                            }
                        }
                    }
                }

                // Cleanup Central Manifest
                if (isCentralManifest && File.Exists(manifestPath))
                {
                    RoosterPlugin.LogInfo("[Uninstall] Trace: Deleting central manifest");
                    TryDeleteFile(manifestPath);
                }

                // 2. Delete Config (Optional)
                if (deleteConfig)
                {
                    string configPath = Path.Combine(Paths.ConfigPath, $"{plugin.Metadata.GUID}.cfg");
                    if (File.Exists(configPath))
                    {
                         RoosterPlugin.LogInfo($"[Uninstall] Trace: Deleting config {configPath}");
                        TryDeleteFile(configPath);
                    }
                }

                // 3. Update State
                if (restartRequired) // Always true
                {
                    UpdateChecker.RestartRequired = true;
                }
                
                // Track pending uninstall
                RoosterPlugin.LogInfo("[Uninstall] Trace: Adding to pending uninstalls...");
                UpdateChecker.PendingUninstalls.Add(plugin.Metadata.GUID);

                RoosterPlugin.LogInfo("[Uninstall] Trace: Invoking onComplete(true)...");
                onComplete?.Invoke(true, null);
                RoosterPlugin.LogInfo("[Uninstall] Trace: Finished.");

            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"[Rooster] Uninstallation error: {ex}");
                onComplete?.Invoke(false, ex.Message);
            }
        }

        private static bool IsProtectedDirectory(string path)
        {
            string absPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string absPlugins = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string absBepInEx = Path.GetFullPath(Paths.BepInExRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return absPath.Equals(absPlugins, StringComparison.OrdinalIgnoreCase) || 
                   absPath.Equals(absBepInEx, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path)) return true;

            // "Hot Swap" style uninstall: Rename to .old so it's ignored by BepInEx and deleted on next start
            try
            {
                // Use .old extension which is standard for BepInEx nuances
                string deletedName = $"{path}.old_{DateTime.Now.Ticks}";
                
                if (File.Exists(deletedName)) File.Delete(deletedName);
                File.Move(path, deletedName);
                
                // Optional: Try to delete immediately if not locked, but don't fail if we can't
                try { File.Delete(deletedName); } catch { }
                
                return true;
            }
            catch (Exception ex)
            {
                // If move fails, we really can't do anything
                RoosterPlugin.LogError($"[Rooster] Failed to mark {Path.GetFileName(path)} for deletion: {ex.Message}");
                return false;
            }
        }
    }
}
