using System;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

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
            bool isMatched = UpdateChecker.MatchedPackages.ContainsKey(guid);

            if (!isMatched)
            {
                return UninstallScope.Manual;
            }

            // check for manifest.json in the directory of the DLL
            // If it exists, we assume it's a "Tracked" (managed) subfolder install
            string dir = Path.GetDirectoryName(plugin.Location);
            if (File.Exists(Path.Combine(dir, "manifest.json")))
            {
                return UninstallScope.Tracked;
            }

            // Matched but no manifest => likely a flat install or just a DLL dropped in
            return UninstallScope.Discovered;
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
                var scope = GetUninstallScope(plugin);
                string dllPath = plugin.Location;
                string dir = Path.GetDirectoryName(dllPath);

                // Always require restart for DLL uninstallation
                bool restartRequired = true;

                // 1. Delete Main Content based on Scope
                if (scope == UninstallScope.Tracked)
                {
                    // Delete entire folder
                    if (Directory.Exists(dir))
                    {
                        try 
                        {
                            Directory.Delete(dir, true); 
                        }
                        catch 
                        {
                            // Fallback
                            foreach(var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) 
                            { 
                                TryDeleteFile(file);
                            }
                            // Try deleting empty dirs
                             try { Directory.Delete(dir, true); } catch { } 
                        }
                    }
                }
                else
                {
                    // Manual or Discovered: Only delete the DLL
                    TryDeleteFile(dllPath);
                }

                // 2. Delete Config (Optional)
                if (deleteConfig)
                {
                    string configPath = Path.Combine(Paths.ConfigPath, $"{plugin.Metadata.GUID}.cfg");
                    if (File.Exists(configPath))
                    {
                        TryDeleteFile(configPath);
                    }
                }

                // 3. Update State
                if (restartRequired) // Always true
                {
                    UpdateChecker.RestartRequired = true;
                }
                
                // Track pending uninstall
                UpdateChecker.PendingUninstalls.Add(plugin.Metadata.GUID);

                onComplete?.Invoke(true, null);

            }
            catch (Exception ex)
            {
                Debug.LogError($"[Rooster] Uninstallation error: {ex}");
                onComplete?.Invoke(false, ex.Message);
            }
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path)) return true;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                // Fallback to rename for ANY exception (IOException, UnauthorizedAccess, etc.)
                try
                {
                    string deletedName = $"{path}.deleted_{DateTime.Now.Ticks}";
                    if (File.Exists(deletedName)) File.Delete(deletedName);
                    File.Move(path, deletedName);
                    return true;
                }
                catch (Exception finalEx)
                {
                    Debug.LogError($"[Rooster] Failed to safe-delete {Path.GetFileName(path)}: {finalEx.Message}");
                    return false;
                }
            }
        }
    }
}
