using System;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Models;
using Rooster.Services;
using BepInEx;

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

            modal.ShowSimpleMessage($"{pkg.name} Details", "", null);
            modal.simpleMessageText.gameObject.SetActive(false);

            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) => {
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

            // Title & Author
            UIHelpers.AddText(_detailsContainer.transform, pkg.name.Replace('_', ' '), 40, true, Color.white);
            string author = pkg.full_name?.Split('-')[0] ?? "Unknown";
            UIHelpers.AddText(_detailsContainer.transform, $"by {author}", 24, false, new Color(0.8f, 0.8f, 0.8f));

            // Description
            UIHelpers.AddText(_detailsContainer.transform, pkg.description, 20, false, Color.white);

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
            bool isInstalled = UpdateChecker.IsPackageInstalled(pkg.full_name);

            if (tabletBtn != null)
            {
                var newScheme = tabletBtn.colorScheme; // Already cloned by CreateButton
                
                if (isInstalled)
                {
                    // Find the plugin info to enable uninstallation
                    string guid = null;
                    PluginInfo pluginInfo = null;

                    // Try to find the GUID causing this Match
                    // This is slightly expensive; we iterate installed plugins to find match
                    foreach (var pair in UpdateChecker.MatchedPackages)
                    {
                        if (pair.Value.full_name == pkg.full_name)
                        {
                            guid = pair.Key;
                            break;
                        }
                    }

                    if (guid != null && BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out pluginInfo))
                    {
                        label.text = "Uninstall";
                        UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Danger); // Red button

                        tabletBtn.OnClick = new TabletButtonEvent();
                        tabletBtn.OnClick.AddListener((cursor) => {
                             UIHelpers.ShowUninstallConfirmation(
                                 Tablet.clickEventReceiver.modalOverlay, 
                                 pluginInfo, 
                                 _detailsContainer,
                                 () => ShowDetails(pkg),
                                 (deleteConfig) => {
                                     ModUninstaller.UninstallMod(pluginInfo, deleteConfig, (success, err) => {
                                         if (success)
                                         {
                                            var overlay = Tablet.clickEventReceiver.modalOverlay;
                                            
                                            // Clean up the uninstall UI (toggles/buttons) before showing success message
                                            var textObj = overlay.simpleMessageText != null ? overlay.simpleMessageText.gameObject : null;
                                            UIHelpers.CleanContainer(overlay.simpleMessageContainer.gameObject, textObj);

                                            string msg = deleteConfig 
                                                ? "Uninstall staged; configuration deleted. Dll will be cleaned up with the next Restart." 
                                                : "Uninstall staged. Dll will be cleaned up with the next Restart.";

                                            overlay.ShowSimpleMessage("Uninstall Successful", msg, () => {
                                                ShowDetails(pkg);
                                            });
                                         }
                                         else
                                         {
                                            var overlay = Tablet.clickEventReceiver.modalOverlay;
                                            var textObj = overlay.simpleMessageText != null ? overlay.simpleMessageText.gameObject : null;
                                            UIHelpers.CleanContainer(overlay.simpleMessageContainer.gameObject, textObj);
                                            overlay.ShowSimpleMessage("Uninstall Failed", err, () => ShowDetails(pkg));
                                         }
                                     });
                                 }
                             );
                        });

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
                else if (UpdateChecker.PendingInstalls.Contains(pkg.full_name))
                {
                    label.text = "Pending Restart";
                    tabletBtn.SetInteractable(false);
                    tabletBtn.SetDisabled(true);
                    UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Warning);
                }
                else
                {
                    UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Success);

                    tabletBtn.OnClick = new TabletButtonEvent();
                    tabletBtn.OnClick.AddListener((cursor) => {
                        label.text = "Installing...";
                        tabletBtn.SetInteractable(false);
                        tabletBtn.SetDisabled(true);
                        
                        // Force update visual state
                        if (tabletBtn.background != null) tabletBtn.background.color = UIHelpers.Themes.Success.Disabled;

                        string url = pkg.latest.download_url;
                        string zipName = $"{pkg.name}.zip";
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), zipName);

                        RoosterPlugin.Instance.StartCoroutine(UpdateDownloader.DownloadFile(url, tempPath, (success, err) => {
                             if (success)
                             {
                                 UpdateInstaller.InstallPackage(tempPath, pkg.name, (installSuccess, installErr) => {
                                     if (installSuccess)
                                     {
                                         label.text = "Pending Restart";
                                         UpdateChecker.PendingInstalls.Add(pkg.full_name);
                                         
                                         // Update color scheme to disabled state
                                         UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Warning); // Or disabled style
                                     }
                                     else
                                     {
                                         label.text = "Error";
                                         RoosterPlugin.LogError($"Install error: {installErr}");
                                         // Re-enable?
                                     }
                                 });
                             }
                             else
                             {
                                 label.text = "Failed";
                                 tabletBtn.SetInteractable(true);
                                 RoosterPlugin.LogError($"Download error: {err}");
                             }
                        }));
                    });
                    
                    tabletBtn.SetInteractable(true);
                    tabletBtn.SetDisabled(false);
                }
                
            }
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
