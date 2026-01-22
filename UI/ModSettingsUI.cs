using System;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;

namespace Rooster.UI
{
    /// <summary>
    /// Manages the UI for individual Mod Settings.
    /// Allows users to toggle auto-updates and ignore status for specific mods.
    /// </summary>
    public static class ModSettingsUI
    {
        private static PluginInfo _currentPlugin;
        private static string _currentGuid;
        private static string _currentNamespace;
        private static string _thunderstoreFullName;
        private static GameObject _settingsContainer;

        private static TabletButton _autoUpdateOnBtn;
        private static TabletButton _autoUpdateOffBtn;
        private static TabletButton _ignoreOnBtn;
        private static TabletButton _ignoreOffBtn;

        public static void ShowModSettings(PluginInfo plugin, string guid, string thunderstoreFullName)
        {
            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null)
            {
                RoosterPlugin.LogError("Cannot open mod settings: Tablet overlay not available");
                return;
            }

            _currentPlugin = plugin;
            _currentGuid = guid;
            _thunderstoreFullName = thunderstoreFullName;
            _currentNamespace = ExtractNamespace(thunderstoreFullName);
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.ModSettings;

            var modal = Tablet.clickEventReceiver.modalOverlay;

            ModMenuUI.DestroyUI();

            string title = plugin != null ? plugin.Metadata.Name : "BepInEx";
            modal.ShowSimpleMessage($"{title} Settings", "", null);

            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) =>
            {
                string logName = plugin != null ? plugin.Metadata.Name : "BepInEx";
                RoosterPlugin.LogInfo($"Returning to Mod List from {logName}");
                CleanupCustomUI();
                ModMenuUI.ShowModMenu();
            });

            modal.onOffContainer.gameObject.SetActive(false);
            modal.simpleMessageText.gameObject.SetActive(false);

            try
            {
                CreateSettingsUI(modal);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to create settings UI: {ex}");
            }
        }

        private static void CreateSettingsUI(TabletModalOverlay modal)
        {
            CleanupCustomUI();

            var parent = modal.simpleMessageContainer;
            if (parent == null) return;

            int layer = parent.gameObject.layer;

            _settingsContainer = new GameObject("ModSettingsContainer", typeof(RectTransform));
            _settingsContainer.layer = layer;
            _settingsContainer.transform.SetParent(parent, false);

            var containerRect = _settingsContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            var layout = _settingsContainer.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 40f;
            layout.padding = new RectOffset(40, 40, 30, 30);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            string guid = _currentGuid;
            bool autoUpdateEnabled = RoosterConfig.IsModAutoUpdate(guid);
            bool isIgnored = RoosterConfig.IsModIgnored(guid);
            bool isDiscovered = !string.IsNullOrEmpty(_thunderstoreFullName);

            UIHelpers.CreateToggleRow(_settingsContainer.transform.GetComponent<RectTransform>(), modal, "Auto-Update", autoUpdateEnabled, (val) =>
            {
                RoosterConfig.SetModAutoUpdate(guid, val);
            }, out _autoUpdateOnBtn, out _autoUpdateOffBtn);

            UIHelpers.CreateToggleRow(_settingsContainer.transform.GetComponent<RectTransform>(), modal, "Ignore Updates", isIgnored, (val) =>
            {
                RoosterConfig.SetModIgnored(guid, val);
                if (val)
                {
                    if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(true);
                    if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(true);
                }
                else if (isDiscovered)
                {
                    if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(false);
                    if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(false);
                }
            }, out _ignoreOnBtn, out _ignoreOffBtn);

            if (!isDiscovered || isIgnored)
            {
                if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(true);
                if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(true);
            }

            if (!isDiscovered)
            {
                if (_ignoreOnBtn != null) _ignoreOnBtn.SetDisabled(true);
                if (_ignoreOffBtn != null) _ignoreOffBtn.SetDisabled(true);
            }

            bool isPendingUninstall = UpdateChecker.PendingUninstalls.Contains(guid);
            if (isPendingUninstall)
            {
                if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(true);
                if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(true);
                if (_ignoreOnBtn != null) _ignoreOnBtn.SetDisabled(true);
                if (_ignoreOffBtn != null) _ignoreOffBtn.SetDisabled(true);
            }

            // Uninstall Button
            if (!guid.Equals("de.knusbernd.rooster", StringComparison.OrdinalIgnoreCase) &&
                !guid.Equals("bepinex", StringComparison.OrdinalIgnoreCase))
            {
                var actionRow = new GameObject("ActionRow", typeof(RectTransform));
                actionRow.transform.SetParent(_settingsContainer.transform, false);

                var actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
                actionLayout.childAlignment = TextAnchor.MiddleCenter;
                actionLayout.childForceExpandWidth = false;
                actionLayout.childForceExpandHeight = false;

                var rowLe = actionRow.AddComponent<LayoutElement>();
                rowLe.preferredHeight = 110;
                rowLe.minHeight = 90;

                var btnText = isPendingUninstall ? "Uninstall Pending (Restart required)" : "Uninstall Mod";
                var uninstallBtn = UIHelpers.CreateButton(actionRow.transform, modal.okButton, btnText, 450, 80);

                if (isPendingUninstall)
                {
                    UIHelpers.ApplyTheme(uninstallBtn, UIHelpers.Themes.Neutral);
                    uninstallBtn.SetDisabled(true);
                }
                else
                {
                    UIHelpers.ApplyTheme(uninstallBtn, UIHelpers.Themes.Danger);
                    uninstallBtn.OnClick = new TabletButtonEvent();
                    uninstallBtn.OnClick.AddListener((c) =>
                    {
                        UIHelpers.ShowUninstallConfirmation(modal, _currentPlugin, _settingsContainer,
                        () => ShowModSettings(_currentPlugin, _currentGuid, _thunderstoreFullName),
                        (deleteConfig) =>
                        {
                            Services.ModUninstaller.UninstallMod(_currentPlugin, deleteConfig, (success, err) =>
                            {
                                if (success)
                                {
                                    // Clean up the uninstall UI (toggles/buttons) before showing success message
                                    var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
                                    UIHelpers.CleanContainer(modal.simpleMessageContainer.gameObject, textObj);

                                    string msg = deleteConfig
                                        ? "Uninstall staged; configuration deleted. Dll will be cleaned up with the next Restart."
                                        : "Uninstall staged. Dll will be cleaned up with the next Restart.";

                                    UIHelpers.ShowRestartPrompt(modal, "Uninstall Successful", msg, () =>
                                    {
                                        CleanupCustomUI();
                                        modal.Close();
                                    });
                                }
                                else
                                {
                                    var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
                                    UIHelpers.CleanContainer(modal.simpleMessageContainer.gameObject, textObj);
                                    modal.ShowSimpleMessage("Uninstall Failed", err, () => ShowModSettings(_currentPlugin, _currentGuid, _thunderstoreFullName));
                                }
                            });
                        });
                    });
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }


        public static void CleanupCustomUI()
        {
            if (_settingsContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(_settingsContainer);
                _settingsContainer = null;
            }
            _autoUpdateOnBtn = null;
            _autoUpdateOffBtn = null;
            _ignoreOnBtn = null;
            _ignoreOffBtn = null;
        }

        private static string ExtractNamespace(string thunderstoreFullName)
        {
            if (string.IsNullOrEmpty(thunderstoreFullName)) return null;
            string[] parts = thunderstoreFullName.Split('-');
            return parts.Length > 0 ? parts[0] : null;
        }
    }
}
