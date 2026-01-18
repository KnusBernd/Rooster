using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BepInEx;

namespace Rooster
{
    /// <summary>
    /// Handles the installation of downloaded mod updates.
    /// Extracts ZIP files, identifies DLLs, and performs hot-swapping (file replacement).
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>Installs a mod update from a ZIP file.</summary>
        public static void InstallMod(string zipPath, BepInEx.PluginInfo pluginInfo, Action<bool, string> onComplete)
        {
            // Temporary extraction path
            string tempExtractPath = Path.Combine(Path.GetDirectoryName(zipPath), "extracted_" + Path.GetFileNameWithoutExtension(zipPath));

            try
            {
                RoosterPlugin.LogInfo($"Installing update for {pluginInfo.Metadata.Name} from {zipPath}");

                // Cleanup and Extract
                if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
                Directory.CreateDirectory(tempExtractPath);

                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);
                
                // Find Package Root
                string packageRoot = Directory.GetFiles(tempExtractPath, "manifest.json", SearchOption.AllDirectories)
                                              .FirstOrDefault();
                
                if (string.IsNullOrEmpty(packageRoot))
                {
                    throw new Exception("Invalid package: manifest.json not found.");
                }

                packageRoot = Path.GetDirectoryName(packageRoot);
                
                // Handle BepInExPack: Special case where we strip 'BepInExPack' folder
                string bepInExPackFolder = Path.Combine(packageRoot, "BepInExPack");
                if (Directory.Exists(bepInExPackFolder))
                {
                    RoosterPlugin.LogInfo("Detected BepInExPack structure. Stripping 'BepInExPack' folder.");
                    packageRoot = bepInExPackFolder;
                }

                RoosterPlugin.LogInfo($"Package Root identified at: {packageRoot}");

                // Determine Installation Strategy
                
                string sourceBepInEx = Path.Combine(packageRoot, "BepInEx");
                string sourcePlugins = Path.Combine(packageRoot, "plugins");
                string sourceConfig = Path.Combine(packageRoot, "config");
                
                string targetDirectory;
                
                if (Directory.Exists(sourceBepInEx))
                {
                    RoosterPlugin.LogInfo("Structure 'BepInEx' detected. Strategy: Merge into Game Root.");
                    targetDirectory = Paths.GameRootPath;
                }
                else if (Directory.Exists(sourcePlugins) || Directory.Exists(sourceConfig))
                {
                    RoosterPlugin.LogInfo("Structure 'plugins' or 'config' detected. Strategy: Merge into BepInEx Root.");
                    targetDirectory = Paths.BepInExRootPath;
                }
                else
                {
                    RoosterPlugin.LogInfo("No root 'BepInEx', 'plugins', or 'config' folder. Strategy: Update specific Plugin folder.");
                    targetDirectory = Path.GetDirectoryName(pluginInfo.Location);
                    
                    // Fix for nested folder duplication (e.g. duplicating CoolMod/CoolMod.dll -> CoolMod/CoolMod/CoolMod.dll)
                    // If the package root contains a single directory that matches the target directory name, use that as the source.
                    var rootDirs = Directory.GetDirectories(packageRoot);
                    if (rootDirs.Length == 1)
                    {
                        // Check if the single folder inside is the one we want to install
                        string subDirName = Path.GetFileName(rootDirs[0]);
                        string targetDirName = Path.GetFileName(targetDirectory);
                        
                        // If we are updating "ModA" and the zip contains "ModA/Plugin.dll", we want "ModA" to match source.
                        // However, standard zip extraction gives us "ModA" as root.
                        // If the zip contains "ModA/ModA/Plugin.dll" (nested), we peel.
                        
                        if (string.Equals(subDirName, targetDirName, StringComparison.OrdinalIgnoreCase))
                        {
                            RoosterPlugin.LogInfo($"Detected matching subfolder '{subDirName}'. unwrapping to prevent nesting.");
                            packageRoot = rootDirs[0]; 
                        }
                    }
                }

                RoosterPlugin.LogInfo($"Target Directory: {targetDirectory}");

                // Anti-Duplicate Logic:
                // Check if the update installs the main DLL to a new location (e.g. folder rename).
                // If so, delete the old file to prevent duplicate plugins.
                string dllName = Path.GetFileName(pluginInfo.Location);
                string newParamsPath = Directory.GetFiles(packageRoot, dllName, SearchOption.AllDirectories).FirstOrDefault();
                
                if (!string.IsNullOrEmpty(newParamsPath))
                {
                    string relativePath = newParamsPath.Substring(packageRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string projectedPath = Path.Combine(targetDirectory, relativePath);
                    string currentPath = pluginInfo.Location;

                    // Normalize paths for comparison
                    projectedPath = Path.GetFullPath(projectedPath);
                    currentPath = Path.GetFullPath(currentPath);

                    if (!string.Equals(projectedPath, currentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        RoosterPlugin.LogWarning($"Detected install path change for {pluginInfo.Metadata.Name}.");
                        RoosterPlugin.LogWarning($"Old: {currentPath}");
                        RoosterPlugin.LogWarning($"New: {projectedPath}");
                        
                        try 
                        {
                            if (File.Exists(currentPath))
                            {
                                string oldBackup = currentPath + ".old";
                                if (File.Exists(oldBackup)) File.Delete(oldBackup);
                                File.Move(currentPath, oldBackup);
                                RoosterPlugin.LogInfo("Moved old plugin file to .old to prevent duplicates.");
                            }
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogError($"Failed to cleanup old plugin file: {ex.Message}");
                        }
                    }
                }

                // Copy Files Recursively
                CopyDirectory(packageRoot, targetDirectory, true);

                RoosterPlugin.LogInfo("Installation successful. Restart required.");
                onComplete?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Installation failed: {ex}");
                onComplete?.Invoke(false, ex.Message);
            }
            finally
            {
                // Cleanup
                try 
                { 
                    if (Directory.Exists(tempExtractPath)) 
                        Directory.Delete(tempExtractPath, true); 
                } 
                catch { /* Best effort cleanup */ }
                
                try 
                { 
                    if (File.Exists(zipPath)) 
                        File.Delete(zipPath); 
                } 
                catch { /* Best effort cleanup */ }
            }
        }

        /// <summary>Installs a new package (or update) given a mod name, handling fresh installs.</summary>
        public static void InstallPackage(string zipPath, string modName, Action<bool, string> onComplete)
        {
            // Temporary extraction path
            string tempExtractPath = Path.Combine(Path.GetDirectoryName(zipPath), "extracted_" + Path.GetFileNameWithoutExtension(zipPath));

            try
            {
                RoosterPlugin.LogInfo($"Installing package {modName} from {zipPath}");

                // Cleanup and Extract
                if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
                Directory.CreateDirectory(tempExtractPath);

                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);
                
                // Find Package Root
                string packageRoot = Directory.GetFiles(tempExtractPath, "manifest.json", SearchOption.AllDirectories)
                                              .FirstOrDefault();
                
                if (string.IsNullOrEmpty(packageRoot)) throw new Exception("Invalid package: manifest.json not found.");
                
                packageRoot = Path.GetDirectoryName(packageRoot);
                
                // Handle BepInExPack: Special case where we strip 'BepInExPack' folder
                string bepInExPackFolder = Path.Combine(packageRoot, "BepInExPack");
                if (Directory.Exists(bepInExPackFolder))
                {
                     RoosterPlugin.LogInfo("Detected BepInExPack structure. Stripping 'BepInExPack' folder.");
                     packageRoot = bepInExPackFolder;
                }
                
                string sourceBepInEx = Path.Combine(packageRoot, "BepInEx");
                string sourcePlugins = Path.Combine(packageRoot, "plugins");
                string sourceConfig = Path.Combine(packageRoot, "config");
                
                string targetDirectory;
                
                if (Directory.Exists(sourceBepInEx))
                {
                    targetDirectory = Paths.GameRootPath;
                }
                else if (Directory.Exists(sourcePlugins) || Directory.Exists(sourceConfig))
                {
                    targetDirectory = Paths.BepInExRootPath;
                }
                else
                {
                    // Default to creating a new folder in plugins
                    // OLD LOGIC: targetDirectory = Path.Combine(Paths.PluginPath, modName);
                    
                    // NEW LOGIC: Check if we should use the inner folder name
                    var rootDirs = Directory.GetDirectories(packageRoot);
                    var rootFiles = Directory.GetFiles(packageRoot);
                    
                    // Filter out metadata files from root files check
                    string[] ignoredFiles = new[] { "manifest.json", "icon.png", "readme.md", "changelog.md", "manifest.yml" };
                    bool hasLooseFiles = rootFiles.Any(f => !ignoredFiles.Contains(Path.GetFileName(f).ToLowerInvariant()));

                    if (rootDirs.Length == 1 && !hasLooseFiles)
                    {
                        string subDirName = Path.GetFileName(rootDirs[0]);
                        // If the zip is {ModName}/{Files}, we want to install {Files} into plugins/{ModName}
                        // So we unwrap AND use the subdirectory name as the target folder
                        RoosterPlugin.LogInfo($"Detected single inner folder '{subDirName}'. unwrapping and using as target folder name.");
                        packageRoot = rootDirs[0];
                        targetDirectory = Path.Combine(Paths.PluginPath, subDirName);
                    }
                    else
                    {
                        // Multiple folders or loose files -> Create a container folder using the package name
                         targetDirectory = Path.Combine(Paths.PluginPath, modName);
                    }
                }

                RoosterPlugin.LogInfo($"Target Directory: {targetDirectory}");
                CopyDirectory(packageRoot, targetDirectory, true);

                RoosterPlugin.LogInfo("Installation (Chaos) successful.");
                onComplete?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Installation failed: {ex}");
                onComplete?.Invoke(false, ex.Message);
            }
            finally
            {
                 try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { }
                 try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            // Files to strictly ignore (Metadata)
            string[] ignoredFiles = new[] { "manifest.json", "icon.png", "readme.md", "changelog.md", "manifest.yml" };

            foreach (FileInfo file in dir.GetFiles())
            {
                if (ignoredFiles.Contains(file.Name.ToLowerInvariant()))
                {
                    // Skip metadata files
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDir, file.Name);

                // Hot-Swap capability for DLLs
                if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetFilePath))
                    {
                        string oldPath = targetFilePath + ".old";
                        if (File.Exists(oldPath)) File.Delete(oldPath);
                        try
                        {
                            File.Move(targetFilePath, oldPath);
                            RoosterPlugin.LogInfo($"Hot-swap: Moving {file.Name} to .old");
                        }
                        catch (Exception ex)
                        {
                            RoosterPlugin.LogWarning($"Failed to move existing DLL {file.Name} for hot-swap: {ex.Message}. Trying overwrite...");
                        }
                    }
                }

                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
