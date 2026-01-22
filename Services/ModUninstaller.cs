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
                string manifestPath = GetManifestPath(pkg.FullName);
                if (File.Exists(manifestPath)) return UninstallScope.Tracked;
            }

            string dir = Path.GetDirectoryName(plugin.Location);
            if (File.Exists(Path.Combine(dir, "manifest.json")))
            {
                return UninstallScope.Tracked;
            }

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

            if (plugin.Metadata.GUID.Equals("bepinex", StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(false, "Cannot uninstall BepInEx.");
                return;
            }

            try
            {
                string guid = plugin.Metadata.GUID;
                string dllPath = plugin.Location;
                string dir = Path.GetDirectoryName(dllPath);

                bool restartRequired = true;

                if (IsProtectedDirectory(dir))
                {
                    RoosterPlugin.LogWarning($"[Uninstall] Safety Triggered: Directory {dir} is protected. Switching to file-only mode.");
                }

                List<string> filesToDelete = new List<string>();

                // Try to find manifest
                string manifestPath = null;
                if (UpdateChecker.MatchedPackages.TryGetValue(guid, out var pkg))
                {
                    manifestPath = GetManifestPath(pkg.FullName);
                }

                if (!File.Exists(manifestPath))
                {
                    manifestPath = Path.Combine(dir, "manifest.json");
                }

                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var node = JSON.Parse(File.ReadAllText(manifestPath));
                        var tpkg = ThunderstorePackage.FromJson(node);
                        if (tpkg != null && tpkg.Files != null && tpkg.Files.Count > 0)
                        {
                            filesToDelete = tpkg.Files;
                            if (Path.GetDirectoryName(manifestPath) == dir) filesToDelete.Add("manifest.json");
                        }
                    }
                    catch { /* Invalid manifest */ }
                }

                bool isCentralManifest = manifestPath != null && manifestPath.Contains("Rooster");

                if (IsProtectedDirectory(dir))
                {
                    // Case A: Root/Protected Dir (e.g. plugins/)
                    if (filesToDelete.Count > 0)
                    {
                        foreach (var file in filesToDelete)
                        {
                            TryDeleteFile(Path.Combine(dir, file));
                        }
                    }
                    else
                    {
                        TryDeleteFile(dllPath);
                    }
                }
                else
                {
                    if (filesToDelete.Count > 0)
                    {
                        foreach (var file in filesToDelete)
                        {
                            try { TryDeleteFile(Path.Combine(dir, file)); } catch { }
                        }

                        try
                        {
                            if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                                Directory.Delete(dir, false);
                        }
                        catch { }
                    }
                    else
                    {
                        // Legacy behavior: Delete entire folder
                        if (Directory.Exists(dir))
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch
                            {
                                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                                {
                                    TryDeleteFile(file);
                                }
                                try { Directory.Delete(dir, true); } catch { }
                            }
                        }
                    }
                }

                if (isCentralManifest && File.Exists(manifestPath))
                {
                    TryDeleteFile(manifestPath);
                }

                if (deleteConfig)
                {
                    string configPath = Path.Combine(Paths.ConfigPath, $"{plugin.Metadata.GUID}.cfg");
                    if (File.Exists(configPath))
                    {
                        TryDeleteFile(configPath);
                    }
                }

                if (restartRequired) // Always true
                {
                    UpdateChecker.RestartRequired = true;
                }

                UpdateChecker.PendingUninstalls.Add(plugin.Metadata.GUID);

                onComplete?.Invoke(true, null);
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

            try
            {
                string deletedName = $"{path}.old_{DateTime.Now.Ticks}";

                if (File.Exists(deletedName)) File.Delete(deletedName);
                File.Move(path, deletedName);

                try { File.Delete(deletedName); } catch { }

                return true;
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"[Rooster] Failed to mark {Path.GetFileName(path)} for deletion: {ex.Message}");
                return false;
            }
        }
    }
}
