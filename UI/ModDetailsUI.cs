using System;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Models;
using Rooster.Services;
using BepInEx;
using System.Collections.Generic;
using System.Linq;

namespace Rooster.UI
{
    public static class ModDetailsUI
    {
        private static GameObject _detailsContainer;
        private static ThunderstorePackage _currentPackage;

        public static void ShowDetails(ThunderstorePackage pkg)
        {
            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null)
            {
                RoosterPlugin.LogError("Cannot open mod details: Tablet overlay unavailable");
                return;
            }

            var modal = Tablet.clickEventReceiver.modalOverlay;
            _currentPackage = pkg;
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.ModDetails;

            // Hide Browser components
            ModBrowserUI.SetVisible(false);

            modal.ShowSimpleMessage($"{pkg.Name} Details", "", null);
            modal.simpleMessageText.gameObject.SetActive(false);

            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) =>
            {
                Close();
                ModBrowserUI.ShowModBrowser(true);
            });

            CreateDetailsUI(modal, pkg);
        }

        private static void CreateDetailsUI(TabletModalOverlay modal, ThunderstorePackage pkg)
        {
            CleanupCustomUI();

            var parent = modal.simpleMessageContainer;
            if (parent == null) return;

            _detailsContainer = new GameObject("ModDetailsContainer", typeof(RectTransform));
            _detailsContainer.layer = parent.gameObject.layer;
            _detailsContainer.transform.SetParent(parent, false);

            var containerRect = _detailsContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(50, 50);
            containerRect.offsetMax = new Vector2(-50, -50);

            var vLayout = _detailsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(40, 40, 20, 40);
            vLayout.spacing = 20;
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childControlHeight = true;

            // Header Section (Icon + Name)
            var headerObj = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(_detailsContainer.transform, false);
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.sizeDelta = new Vector2(192, 192);
            var headerImg = headerObj.GetComponent<Image>();
            headerImg.color = new Color(1, 1, 1, 0.1f);
            headerImg.preserveAspect = true;

            IconService.Instance.GetIcon(pkg, (sprite) => {
                if (headerImg == null || sprite == null) return;
                headerImg.sprite = sprite;
                headerImg.color = Color.white;
            });

            // Title & Author
            UIHelpers.AddText(_detailsContainer.transform, pkg.Name.Replace('_', ' '), 40, true, Color.white);
            string author = pkg.FullName?.Split('-')[0] ?? "Unknown";
            string authorText = $"by {author}";
            if (!string.IsNullOrEmpty(pkg.SecondaryAuthor))
            {
                authorText = $"by {pkg.SecondaryAuthor} (forked by {author})";
            }
            UIHelpers.AddText(_detailsContainer.transform, authorText, 24, false, new Color(0.8f, 0.8f, 0.8f));

            // Description
            UIHelpers.AddText(_detailsContainer.transform, pkg.Description, 20, false, Color.white);

            // Dependencies section
            if (pkg.Latest != null && pkg.Latest.Dependencies != null && pkg.Latest.Dependencies.Count > 0)
            {
                var depsToDisplay = pkg.Latest.Dependencies.FindAll(d => !d.Contains("BepInExPack"));
                if (depsToDisplay.Count > 0)
                {
                    UIHelpers.AddText(_detailsContainer.transform, "\nDependencies:", 22, true, Color.white);
                    foreach (var dep in depsToDisplay)
                    {
                        bool isDepInstalled = UpdateChecker.IsPackageInstalled(dep);
                        Color depColor = isDepInstalled ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.8f, 0.2f);
                        string depStatus = isDepInstalled ? " (Installed)" : " (Missing)";
                        UIHelpers.AddText(_detailsContainer.transform, $"• {dep}{depStatus}", 18, false, depColor);
                    }
                    UIHelpers.AddText(_detailsContainer.transform, "", 10, false, Color.clear); // Spacer
                }
            }

            // View Online Button
            if (!string.IsNullOrEmpty(pkg.WebsiteUrl))
            {
                var viewBtn = UIHelpers.CreateButton(_detailsContainer.transform, modal.okButton, "Open in Browser", 450, 80);
                if (viewBtn != null)
                {
                    UIHelpers.ApplyTheme(viewBtn, UIHelpers.Themes.Neutral);
                    viewBtn.OnClick = new TabletButtonEvent();
                    viewBtn.OnClick.AddListener((cursor) =>
                    {
                        string targetUrl = pkg.WebsiteUrl;

                        // For Thunderstore mods, construct the official package page URL
                        bool isGitHub = pkg.Categories != null && pkg.Categories.Contains("GitHub");
                        if (!isGitHub)
                        {
                            string author = pkg.FullName?.Split('-')[0] ?? "Unknown";
                            targetUrl = $"https://thunderstore.io/c/ultimate-chicken-horse/p/{author}/{pkg.Name}/";
                        }

                        Application.OpenURL(targetUrl);
                    });
                    viewBtn.SetInteractable(true);
                    viewBtn.SetDisabled(false);
                }
            }

            // Installation Logic
            CreateInstallButton(_detailsContainer.transform, pkg);

            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        private static void CreateInstallButton(Transform parent, ThunderstorePackage pkg)
        {
            // Use modal.okButton (Back button) as template
            var modal = Tablet.clickEventReceiver.modalOverlay;
            var template = modal.okButton;

            var tabletBtn = UIHelpers.CreateButton(parent, template, "Install", 450, 80);
            var label = tabletBtn.GetComponentInChildren<TabletTextLabel>();

            // Check installation status
            bool isInstalled = UpdateChecker.IsPackageInstalled(pkg.FullName);

            if (tabletBtn != null)
            {
                var newScheme = tabletBtn.colorScheme; // Already cloned by CreateButton

                if (isInstalled)
                {
                    // SAFETY: Prevent BepInEx uninstallation
                    if (pkg.FullName == "BepInEx-BepInExPack")
                    {
                        label.text = "Installed";
                        tabletBtn.SetInteractable(false);
                        tabletBtn.SetDisabled(true);
                        UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Success);
                        return;
                    }

                    // Find the plugin info to enable uninstallation
                    string guid = null;
                    PluginInfo pluginInfo = null;

                    // Try to find the GUID causing this Match
                    // This is slightly expensive; we iterate installed plugins to find match
                    foreach (var pair in UpdateChecker.MatchedPackages)
                    {
                        if (pair.Value.FullName == pkg.FullName)
                        {
                            guid = pair.Key;
                            break;
                        }
                    }

                    if (guid != null && BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out pluginInfo))
                    {
                        // Check for Pending Uninstall
                        if (UpdateChecker.PendingUninstalls.Contains(guid))
                        {
                            label.text = "Uninstall Pending (Restart required)";
                            UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Warning);
                            tabletBtn.SetInteractable(false);
                            tabletBtn.SetDisabled(true);
                            return;
                        }

                        label.text = "Uninstall";
                        UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Danger); // Red button

                        tabletBtn.OnClick = new TabletButtonEvent();
                        tabletBtn.OnClick.AddListener((cursor) =>
                        {
                            Debug.Log($"[UI Debug] Install/Uninstall button clicked for {pkg.Name}");
                            UIHelpers.ShowUninstallConfirmation(
                                Tablet.clickEventReceiver.modalOverlay,
                                pluginInfo,
                                _detailsContainer,
                                () => ShowDetails(pkg),
                                (deleteConfig) => HandleUninstallConfirmation(pkg, pluginInfo, deleteConfig)
                           );
                        });

                        tabletBtn.SetInteractable(true);
                        tabletBtn.SetDisabled(false);
                        tabletBtn.SetInteractable(true);
                        tabletBtn.SetDisabled(false);
                    }
                    else
                    {
                        // Fallback if we can't link back to the DLL (should represent Installed)
                        label.text = "Installed";
                        tabletBtn.SetInteractable(false);
                        tabletBtn.SetDisabled(true);
                        UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Success);
                    }
                }
                else if (UpdateChecker.PendingInstalls.Contains(pkg.FullName))
                {
                    label.text = "Install Pending (Restart required)";
                    tabletBtn.SetInteractable(false);
                    tabletBtn.SetDisabled(true);
                    UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Warning);
                }
                else
                {
                    UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Success);

                    tabletBtn.OnClick = new TabletButtonEvent();
                    tabletBtn.OnClick.AddListener((cursor) =>
                    {
                        Action startInstall = null;
                        startInstall = () =>
                        {
                            // Gather all missing dependencies (excluding BepInExPack)
                            List<ThunderstorePackage> missingDeps = new List<ThunderstorePackage>();
                            if (pkg.Latest != null && pkg.Latest.Dependencies != null)
                            {
                                foreach (var depName in pkg.Latest.Dependencies)
                                {
                                    if (depName.IndexOf("BepInExPack", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                                    if (UpdateChecker.IsPackageInstalled(depName)) continue;
                                    if (UpdateChecker.PendingInstalls.Contains(depName)) continue;

                                    var depPkg = UpdateChecker.CachedPackages.FirstOrDefault(p => p.FullName == depName);
                                    if (depPkg != null) missingDeps.Add(depPkg);
                                }
                            }

                            if (missingDeps.Count > 0)
                            {
                                string depList = string.Join("\n", missingDeps.Select(d => d.Name));
                                UIHelpers.SetupModal(modal, new Vector2(1000, 700), "Missing Dependencies", () => ShowDetails(pkg));

                                var container = modal.simpleMessageContainer;
                                var layout = container.gameObject.GetComponent<VerticalLayoutGroup>() ?? container.gameObject.AddComponent<VerticalLayoutGroup>();
                                layout.childAlignment = TextAnchor.MiddleCenter;
                                layout.spacing = 30f;

                                UIHelpers.AddText(container.transform, $"The following dependencies are required for <b>{pkg.Name}</b> and will be installed automatically:", 28, false, Color.white);
                                UIHelpers.AddText(container.transform, depList, 24, true, new Color(1f, 0.8f, 0.2f));

                                var installAllBtn = UIHelpers.CreateButton(container.transform, modal.okButton, "Install All", 450, 80);
                                UIHelpers.ApplyTheme(installAllBtn, UIHelpers.Themes.Success);
                                installAllBtn.OnClick = new TabletButtonEvent();
                                installAllBtn.OnClick.AddListener((c) =>
                                {
                                    // Install all dependencies first
                                    Action installNext = null;
                                    int depIdx = 0;

                                    installNext = () =>
                                    {
                                        if (depIdx < missingDeps.Count)
                                        {
                                            var d = missingDeps[depIdx++];
                                            InstallOne(d, installNext, (err) => { /* Fatal error logic */ });
                                        }
                                        else
                                        {
                                            // Finally install the main mod
                                            ShowDetails(pkg); // Refresh UI to show pending dependencies
                                            InstallOne(pkg, null, null);
                                        }
                                    };
                                    installNext();
                                });
                                return;
                            }

                            InstallOne(pkg, null, null);
                        };

                        // Check for GitHub disclaimer if downloading from GitHub
                        bool isGitHub = pkg.Latest.DownloadUrl.Contains("github.com") || pkg.Latest.DownloadUrl.Contains("githubusercontent.com");
                        if (isGitHub && !RoosterConfig.GitHubWarningAccepted.Value)
                        {
                            UIHelpers.ShowGitHubWarning(
                                Tablet.clickEventReceiver.modalOverlay,
                                onAccept: () =>
                                {
                                    RoosterConfig.GitHubWarningAccepted.Value = true;
                                    RoosterConfig.SaveConfig();
                                    // Refresh details UI to clear disclaimer and proceed
                                    ShowDetails(pkg);
                                    // Optionally we could just call startInstall() here, 
                                    // but ShowDetails(pkg) is safer to reset UI.
                                },
                                onCancel: () =>
                                {
                                    // Just go back to details
                                    ShowDetails(pkg);
                                }
                            );
                        }
                        else
                        {
                            startInstall();
                        }
                    });

                    tabletBtn.SetInteractable(true);
                    tabletBtn.SetDisabled(false);
                }

            }
        }

        private static void HandleUninstallConfirmation(ThunderstorePackage pkg, PluginInfo pluginInfo, bool deleteConfig)
        {
            Debug.Log("[UI Debug] CALLBACK HIT: HandleUninstallConfirmation");
            Debug.Log($"[UI Debug] Params: Pkg={pkg?.Name}, GUID={pluginInfo?.Metadata?.GUID}, DelConfig={deleteConfig}");

            if (pluginInfo == null)
            {
                Debug.LogError("[UI Debug] PluginInfo is null in HandleUninstallConfirmation.");
                return;
            }

            ModUninstaller.UninstallMod(pluginInfo, deleteConfig, (success, err) =>
            {
                Debug.Log($"[UI Debug] ModUninstaller returned. Success: {success}");

                var overlay = Tablet.clickEventReceiver.modalOverlay;
                if (success)
                {
                    string msg = deleteConfig
                        ? "Uninstall staged; configuration deleted.\nThe DLL will be removed next time you restart the game."
                        : "Uninstall staged.\nThe DLL will be removed next time you restart the game.";

                    UIHelpers.ShowRestartPrompt(overlay, "Uninstall Successful", msg, () => ShowDetails(pkg));
                }
                else
                {
                    var textObj = overlay.simpleMessageText != null ? overlay.simpleMessageText.gameObject : null;
                    UIHelpers.CleanContainer(overlay.simpleMessageContainer.gameObject, textObj);
                    overlay.ShowSimpleMessage("Uninstall Failed", err, () => ShowDetails(pkg));
                }
            });
        }



        private static void InstallOne(ThunderstorePackage pkg, Action onSuccess, Action<string> onError)
        {
            string url = pkg.Latest.DownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                onError?.Invoke("No download URL");
                return;
            }

            string extension = url.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? ".dll" : ".zip";
            string fileName = $"{pkg.Name}{extension}";
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);

            RoosterPlugin.Instance.StartCoroutine(UpdateDownloader.DownloadFile(url, tempPath, (success, err) =>
            {
                if (success)
                {
                    UpdateInstaller.InstallPackage(tempPath, pkg, (installSuccess, installErr) =>
                    {
                        if (installSuccess)
                        {
                            UpdateChecker.PendingInstalls.Add(pkg.FullName);
                            UpdateLoopPreventer.RegisterPendingInstall(pkg.Name, pkg.Latest.VersionNumber);
                            onSuccess?.Invoke();
                        }
                        else
                        {
                            onError?.Invoke(installErr);
                        }
                    });
                }
                else
                {
                    onError?.Invoke(err);
                }
            }));
        }

        public static void Close()
        {
            CleanupCustomUI();
        }

        public static void CleanupCustomUI()
        {
            if (_detailsContainer != null)
            {
                UnityEngine.Object.Destroy(_detailsContainer);
                _detailsContainer = null;
            }
        }
    }
}
