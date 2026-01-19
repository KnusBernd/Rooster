using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BepInEx;

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
        /// Installs a mod update from a ZIP file using existing plugin info to determine target.
        /// </summary>
        public static void InstallMod(string zipPath, BepInEx.PluginInfo pluginInfo, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, "Update", (root, hasLoose) =>
            {
                // updates default to the current specific plugin location's parent
                // unless BepInEx structure dictates otherwise in DetermineTargetDirectory
                return Path.GetDirectoryName(pluginInfo.Location);
            }, onComplete);
        }

        /// <summary>
        /// Installs a new package (or update) given a mod name, handling fresh installs.
        /// </summary>
        public static void InstallPackage(string zipPath, string modName, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, modName, (root, hasLoose) =>
            {
                // Fresh install logic for target directory
                return Path.Combine(Paths.PluginPath, modName);
            }, onComplete);
        }

        private static void PerformInstall(string zipPath, string contextName, Func<string, bool, string> defaultTargetStrategy, Action<bool, string> onComplete)
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

                // Install Files
                CopyDirectory(packageRoot, targetDirectory, true);

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

        private static string ExtractAndFindRoot(string zipPath, string extractPath)
        {
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            var manifest = Directory.GetFiles(extractPath, "manifest.json", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(manifest)) throw new Exception("Invalid package: manifest.json not found.");

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

        private static string DetermineTargetDirectory(string packageRoot, Func<string, bool, string> defaultStrategy)
        {
            // Detect standard BepInEx root structure (e.g. from BepInExPack) to ensure files land in valid game locations
            if (Directory.Exists(Path.Combine(packageRoot, "BepInEx"))) return Paths.GameRootPath;
            if (Directory.Exists(Path.Combine(packageRoot, "plugins")) || Directory.Exists(Path.Combine(packageRoot, "config"))) return Paths.BepInExRootPath;

            // Handle special 'patchers' case
            string sourcePatchers = Path.Combine(packageRoot, "patchers");
            if (Directory.Exists(sourcePatchers))
            {
                RoosterPlugin.LogInfo("Detected 'patchers' folder. Merging into BepInEx/patchers.");
                CopyDirectory(sourcePatchers, Paths.PatcherPluginPath, true);
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

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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
