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


        public static void InstallMod(string zipPath, BepInEx.PluginInfo pluginInfo, ThunderstorePackage metadata, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, "Update", metadata, (root, hasLoose) =>
            {
                if (pluginInfo != null)
                {
                    return Path.GetDirectoryName(pluginInfo.Location);
                }

                return null;
            }, onComplete);
        }


        public static void InstallPackage(string zipPath, ThunderstorePackage metadata, Action<bool, string> onComplete)
        {
            PerformInstall(zipPath, metadata.Name, metadata, (root, hasLoose) =>
            {
                return Path.Combine(Paths.PluginPath, metadata.Name);
            }, onComplete);
        }

        private static void PerformInstall(string zipPath, string contextName, ThunderstorePackage metadata, Func<string, bool, string> defaultTargetStrategy, Action<bool, string> onComplete)
        {
            string tempExtractPath = Path.Combine(Path.GetDirectoryName(zipPath), "extracted_" + Path.GetFileNameWithoutExtension(zipPath));

            try
            {
                string packageRoot = ExtractAndFindRoot(zipPath, tempExtractPath);

                string targetDirectory = DetermineTargetDirectory(packageRoot, defaultTargetStrategy);

                List<string> installedFiles = new List<string>();

                if (File.Exists(packageRoot) && packageRoot.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(targetDirectory);
                    string fileName = Path.GetFileName(packageRoot);
                    string targetFile = Path.Combine(targetDirectory, fileName);

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

                GenerateManifest(targetDirectory, metadata, installedFiles);

                ClearBepInExCache();

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

            string filename = string.Join("_", metadata.FullName.Split(Path.GetInvalidFileNameChars()));
            string manifestPath = Path.Combine(manifestDir, $"{filename}.json");

            ThunderstorePackage finalPackage = null;

            if (File.Exists(manifestPath))
            {
                try
                {
                    var node = JSON.Parse(File.ReadAllText(manifestPath));
                    finalPackage = ThunderstorePackage.FromJson(node);
                }
                catch { }
            }

            if (finalPackage == null)
            {
                finalPackage = new ThunderstorePackage
                {
                    Name = metadata.Name,
                    FullName = metadata.FullName,
                    Description = metadata.Description,
                    WebsiteUrl = metadata.WebsiteUrl
                };
            }

            if (metadata.Latest != null)
            {
                finalPackage.Latest = new ThunderstoreVersion { VersionNumber = metadata.Latest.VersionNumber };
            }

            finalPackage.Files = installedFiles;

            try
            {
                string json = finalPackage.ToJson().ToString();
                File.WriteAllText(manifestPath, json);
                RoosterPlugin.LogInfo($"[Manifest] Updated: {metadata.FullName}");
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to write manifest: {ex.Message}");
            }
        }

        private static string ExtractAndFindRoot(string zipPath, string extractPath)
        {
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

            if (!string.IsNullOrEmpty(manifest))
            {
                string root = Path.GetDirectoryName(manifest);

                string bepInExPack = Path.Combine(root, "BepInExPack");
                if (Directory.Exists(bepInExPack))
                {
                    return bepInExPack;
                }
                return root;
            }

            var dlls = Directory.GetFiles(extractPath, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length > 0)
            {
                return Path.GetDirectoryName(dlls[0]);
            }

            return extractPath;
        }

        private static string DetermineTargetDirectory(string packageRoot, Func<string, bool, string> defaultStrategy)
        {
            if (File.Exists(packageRoot)) return defaultStrategy(packageRoot, true);

            if (Directory.Exists(Path.Combine(packageRoot, "BepInEx"))) return Paths.GameRootPath;
            if (Directory.Exists(Path.Combine(packageRoot, "plugins")) || Directory.Exists(Path.Combine(packageRoot, "config"))) return Paths.BepInExRootPath;

            string sourcePatchers = Path.Combine(packageRoot, "patchers");
            if (Directory.Exists(sourcePatchers))
            {
                CopyDirectory(sourcePatchers, Paths.PatcherPluginPath, true, null);
                Directory.Delete(sourcePatchers, true);
            }

            var rootDirs = Directory.GetDirectories(packageRoot);
            var rootFiles = Directory.GetFiles(packageRoot);
            bool hasLooseFiles = rootFiles.Any(f => !IgnoredFiles.Contains(Path.GetFileName(f).ToLowerInvariant()));

            if (rootDirs.Length == 0 && hasLooseFiles)
            {
                return Paths.PluginPath;
            }

            if (rootDirs.Length == 1 && !hasLooseFiles)
            {
                string subDirName = Path.GetFileName(rootDirs[0]);
                return Path.Combine(Paths.PluginPath, subDirName);
            }

            string strategyResult = defaultStrategy(packageRoot, hasLooseFiles);
            return strategyResult ?? Paths.PluginPath;
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

                    List<string> subDirFiles = new List<string>();
                    CopyDirectory(subDir.FullName, newDestinationDir, true, subDirFiles);

                    if (installedFiles != null)
                    {
                        foreach (var f in subDirFiles)
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
                    foreach (var dat in Directory.GetFiles(cacheDir, "*.dat"))
                    {
                        File.Delete(dat);
                    }
                }
            }
            catch (Exception ex) { RoosterPlugin.LogWarning($"Failed to clear BepInEx cache: {ex.Message}"); }
        }

        private static void SafeDelete(string path, bool isDir)
        {
            try
            {
                if (isDir && Directory.Exists(path)) Directory.Delete(path, true);
                else if (!isDir && File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            { 
                 RoosterPlugin.LogWarning($"[Cleanup] Failed to delete {path}: {ex.Message}");
            }
        }
    }
}
