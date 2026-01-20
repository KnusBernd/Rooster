using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using Rooster.Models;

namespace Rooster.Services
{
    /// <summary>
    /// Handles the installation of downloaded mod updates.
    /// Extracts ZIP files, identifies DLLs, and performs hot-swapping (file replacement).
    /// </summary>
    public static class UpdateInstaller
    {
        private static readonly string[] IgnoredFiles = { "manifest.json", "icon.png", "readme.md", "changelog.md", "manifest.yml" };

        /// <summary>
        /// Installs a mod update.
        /// </summary>
        public static void InstallMod(string zipPath, BepInEx.PluginInfo pluginInfo, ThunderstorePackage metadata, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, "Update", metadata, (root, hasLoose) =>
            {
                // updates default to the current specific plugin location's parent
                return Path.GetDirectoryName(pluginInfo.Location);
            }, onComplete);
        }

        /// <summary>
        /// Installs a new package.
        /// </summary>
        public static void InstallPackage(string zipPath, ThunderstorePackage metadata, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, metadata.name, metadata, (root, hasLoose) =>
            {
                // Fresh install logic for target directory
                return Path.Combine(Paths.PluginPath, metadata.name);
            }, onComplete);
        }

        private static void PerformInstall(string zipPath, string contextName, ThunderstorePackage metadata, Func<string, bool, string> defaultTargetStrategy, Action<bool, string> onComplete)
        {
            string tempExtractPath = Path.Combine(Path.GetDirectoryName(zipPath), "extracted_" + Path.GetFileNameWithoutExtension(zipPath));
            
            try
            {
                RoosterPlugin.LogInfo($"Installing {contextName} from {zipPath}...");

                // Extract and Find Root
                string packageRoot = ExtractAndFindRoot(zipPath, tempExtractPath);
                
                // Determine Target Directory
                string targetDirectory = DetermineTargetDirectory(packageRoot, defaultTargetStrategy);
                
                RoosterPlugin.LogInfo($"Target Directory: {targetDirectory}");

                List<string> installedFiles = new List<string>();

                // Install Files
                if (File.Exists(packageRoot) && packageRoot.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(targetDirectory);
                    string fileName = Path.GetFileName(packageRoot);
                    string targetFile = Path.Combine(targetDirectory, fileName);
                    
                    // Hot-swap logic for direct DLL
                    if (File.Exists(targetFile))
                    {
                        string backupPath = targetFile + ".old_" + DateTime.Now.Ticks;
                        File.Move(targetFile, backupPath);
                    }
                    
                    File.Copy(packageRoot, targetFile, true);
                    installedFiles.Add(fileName);
                }
                else
                {
                    CopyDirectory(packageRoot, targetDirectory, true, installedFiles);
                }

                // Generate or Update Manifest with tracked files
                GenerateManifest(targetDirectory, metadata, installedFiles);

                // Clear Cache
                ClearBepInExCache();

                RoosterPlugin.LogInfo("Installation successful. Restart required.");
                onComplete?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Installation failed: {ex.Message}");
                onComplete?.Invoke(false, ex.Message);
            }
            finally
            {
                SafeDelete(tempExtractPath, true);
                SafeDelete(zipPath, false);
            }
        }

        private static void GenerateManifest(string targetDirectory, ThunderstorePackage metadata, List<string> installedFiles)
        {
            if (metadata == null) return;

            string manifestDir = Path.Combine(Paths.ConfigPath, "Rooster", "Manifests");
            Directory.CreateDirectory(manifestDir);

            // Sanitize filename just in case, though full_name is usually safe (Team-Mod)
            string filename = string.Join("_", metadata.full_name.Split(Path.GetInvalidFileNameChars()));
            string manifestPath = Path.Combine(manifestDir, $"{filename}.json");

            ThunderstorePackage finalPackage = null;
            
            // Try to merge with existing if it exists
            if (File.Exists(manifestPath))
            {
                 try { finalPackage = JsonUtility.FromJson<ThunderstorePackage>(File.ReadAllText(manifestPath)); } catch {}
            }

            if (finalPackage == null)
            {
                finalPackage = new ThunderstorePackage
                {
                    name = metadata.name,
                    full_name = metadata.full_name,
                    description = metadata.description,
                    website_url = metadata.website_url
                };
            }
            
            if (metadata.latest != null)
            {
                finalPackage.latest = new ThunderstoreVersion { version_number = metadata.latest.version_number };
            }
            
            finalPackage.files = installedFiles;

            try 
            {
                string json = JsonUtility.ToJson(finalPackage, true);
                File.WriteAllText(manifestPath, json);
                RoosterPlugin.LogInfo($"[Manifest] Tracking {installedFiles.Count} files for {metadata.full_name} at {manifestPath}");
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to write manifest: {ex.Message}");
            }
        }

        private static string ExtractAndFindRoot(string zipPath, string extractPath)
        {
            // If it's already a DLL, just return it
            if (zipPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return zipPath;

            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            
            try 
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to extract ZIP: {ex.Message}");
                throw;
            }

            var manifest = Directory.GetFiles(extractPath, "manifest.json", SearchOption.AllDirectories).FirstOrDefault();
            
            // If we have a manifest, use its directory as root
            if (!string.IsNullOrEmpty(manifest))
            {
                string root = Path.GetDirectoryName(manifest);
                
                // Strip 'BepInExPack' if present
                string bepInExPack = Path.Combine(root, "BepInExPack");
                if (Directory.Exists(bepInExPack))
                {
                    RoosterPlugin.LogInfo("Detected BepInExPack structure. Stripping wrapper.");
                    return bepInExPack;
                }
                return root;
            }

            // If no manifest, check for any DLLs
            var dlls = Directory.GetFiles(extractPath, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length > 0)
            {
                // Return the directory containing the first DLL found
                return Path.GetDirectoryName(dlls[0]);
            }

            // Fallback to the extract path as root
            return extractPath;
        }

        private static string DetermineTargetDirectory(string packageRoot, Func<string, bool, string> defaultStrategy)
        {
            // If it's a file (direct DLL download), use the default strategy
            if (File.Exists(packageRoot)) return defaultStrategy(packageRoot, true);

            // Detect standard BepInEx root structure (e.g. from BepInExPack) to ensure files land in valid game locations
            if (Directory.Exists(Path.Combine(packageRoot, "BepInEx"))) return Paths.GameRootPath;
            if (Directory.Exists(Path.Combine(packageRoot, "plugins")) || Directory.Exists(Path.Combine(packageRoot, "config"))) return Paths.BepInExRootPath;

            // Handle special 'patchers' case
            string sourcePatchers = Path.Combine(packageRoot, "patchers");
            if (Directory.Exists(sourcePatchers))
            {
                RoosterPlugin.LogInfo("Detected 'patchers' folder. Merging into BepInEx/patchers.");
                // We simplify here: CopyDirectory handles merging, but we don't track patcher files yet in this specific block easily without refactor.
                // Assuming patchers are rare for normal user installs, or complex enough to need own logic.
                // For now, let's just do it and accept we might not track them perfectly in manifest if they go outside target dir.
                // NOTE: The installedFiles list only tracks things in 'targetDirectory'. Files moved here won't be in manifest.
                // This is an edge case acceptable for now.
                CopyDirectory(sourcePatchers, Paths.PatcherPluginPath, true, null);
                Directory.Delete(sourcePatchers, true);
            }

            // Check for loose files vs folders
            var rootDirs = Directory.GetDirectories(packageRoot);
            var rootFiles = Directory.GetFiles(packageRoot);
            bool hasLooseFiles = rootFiles.Any(f => !IgnoredFiles.Contains(Path.GetFileName(f).ToLowerInvariant()));

            // Logic to unwrap single folders or flat installs
            if (rootDirs.Length == 0 && hasLooseFiles)
            {
                // Flat packages (only loose files) must go directly to plugins root or they won't load
                RoosterPlugin.LogInfo("Detected flat package (no subfolders). Installing to plugins root.");
                return Paths.PluginPath;
            }
            
            if (rootDirs.Length == 1 && !hasLooseFiles)
            {
                string subDirName = Path.GetFileName(rootDirs[0]);
                // Single folder packages are unwrapped to avoid double-nesting (e.g. plugins/ModName/ModName/Mod.dll)
                RoosterPlugin.LogInfo($"Detected single inner folder '{subDirName}'. Using it as container.");
                return Path.Combine(Paths.PluginPath, subDirName);
            }

            return defaultStrategy(packageRoot, hasLooseFiles);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, List<string> installedFiles)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                if (IgnoredFiles.Contains(file.Name.ToLowerInvariant())) continue;

                string targetFilePath = Path.Combine(destinationDir, file.Name);

                // Hot-swap / Backup Logic
                if (File.Exists(targetFilePath))
                {
                    try 
                    {
                        string backupPath = targetFilePath + ".old_" + DateTime.Now.Ticks;
                        File.Move(targetFilePath, backupPath);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Failed to backup locked file {targetFilePath}: {ex.Message}");
                    }
                }

                // Loose Duplicate Cleanup Logic
                if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(targetFilePath);
                    string parentDir = Directory.GetParent(destinationDir).FullName;
                    
                    if (Path.GetFileName(parentDir).Equals("plugins", StringComparison.OrdinalIgnoreCase)) 
                    {
                        string loosePath = Path.Combine(parentDir, fileName);
                        if (File.Exists(loosePath) && loosePath != targetFilePath)
                        {
                             try
                             {
                                 string looseBackup = loosePath + ".old_loose_" + DateTime.Now.Ticks;
                                 File.Move(loosePath, looseBackup);
                                 RoosterPlugin.LogInfo($"[Auto-Fix] Archived loose duplicate: {fileName}");
                             }
                             catch (Exception ex) { RoosterPlugin.LogError($"[Auto-Fix] Failed to move loose duplicate {loosePath}: {ex.Message}"); }
                        }
                    }
                }

                File.Copy(file.FullName, targetFilePath, true);
                installedFiles?.Add(file.Name);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    // Pass null for tracker because we only track relative paths? 
                    // Or we should improve tracking to include relative subpaths.
                    // For flat lists in manifest, standard practice is relative path from manifest location.
                    
                    // Improve tracking buffer for recursion
                    List<string> subDirFiles = new List<string>();
                    CopyDirectory(subDir.FullName, newDestinationDir, true, subDirFiles);
                    
                    if (installedFiles != null)
                    {
                        foreach(var f in subDirFiles)
                        {
                            installedFiles.Add(Path.Combine(subDir.Name, f));
                        }
                    }
                }
            }
        }

        private static void ClearBepInExCache()
        {
            try
            {
                string cacheDir = Path.Combine(Paths.BepInExRootPath, "cache");
                if (Directory.Exists(cacheDir))
                {
                    foreach(var dat in Directory.GetFiles(cacheDir, "*.dat")) 
                    {
                        File.Delete(dat); 
                    }
                }
            } 
            catch(Exception ex) { RoosterPlugin.LogWarning($"Failed to clear BepInEx cache: {ex.Message}"); }
        }

        private static void SafeDelete(string path, bool isDir)
        {
            try 
            { 
                if (isDir && Directory.Exists(path)) Directory.Delete(path, true); 
                else if (!isDir && File.Exists(path)) File.Delete(path);
            } 
            catch { /* Ignore cleanup errors */ }
        }
    }
}
