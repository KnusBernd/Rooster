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
        private static string _currentNamespace;
        private static string _thunderstoreFullName;
        private static GameObject _settingsContainer;

        private static TabletButton _autoUpdateOnBtn;
        private static TabletButton _autoUpdateOffBtn;
        private static TabletButton _ignoreOnBtn;
        private static TabletButton _ignoreOffBtn;

        public static void ShowModSettings(PluginInfo plugin, string thunderstoreFullName)
        {
            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null)
            {
                RoosterPlugin.LogError("Cannot open mod settings: Tablet overlay not available");
                return;
            }

            _currentPlugin = plugin;
            _thunderstoreFullName = thunderstoreFullName;
            _currentNamespace = ExtractNamespace(thunderstoreFullName);
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.ModSettings;

            var modal = Tablet.clickEventReceiver.modalOverlay;

            ModMenuUI.DestroyUI();

            modal.ShowSimpleMessage($"{plugin.Metadata.Name} Settings", "", null);

            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";
            
            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) => {
                RoosterPlugin.LogInfo($"Returning to Mod List from {plugin.Metadata.Name}");
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

            string guid = _currentPlugin.Metadata.GUID;
            bool autoUpdateEnabled = RoosterConfig.IsModAutoUpdate(guid);
            bool isIgnored = RoosterConfig.IsModIgnored(guid);
            bool isDiscovered = !string.IsNullOrEmpty(_thunderstoreFullName);

            CreateToggleRow(_settingsContainer.transform, modal, "Auto-Update", autoUpdateEnabled,
                onOn: () => {
                    RoosterConfig.SetModAutoUpdate(guid, true);
                    UpdateButtonStyles(_autoUpdateOnBtn, _autoUpdateOffBtn, true);
                },
                onOff: () => {
                    RoosterConfig.SetModAutoUpdate(guid, false);
                    UpdateButtonStyles(_autoUpdateOnBtn, _autoUpdateOffBtn, false);
                },
                out _autoUpdateOnBtn, out _autoUpdateOffBtn);

            CreateToggleRow(_settingsContainer.transform, modal, "Ignore Updates", isIgnored,
                onOn: () => {
                    RoosterConfig.SetModIgnored(guid, true);
                    UpdateButtonStyles(_ignoreOnBtn, _ignoreOffBtn, true);
                    if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(true);
                    if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(true);
                },
                onOff: () => {
                    RoosterConfig.SetModIgnored(guid, false);
                    UpdateButtonStyles(_ignoreOnBtn, _ignoreOffBtn, false);
                    if (isDiscovered)
                    {
                        if (_autoUpdateOnBtn != null) _autoUpdateOnBtn.SetDisabled(false);
                        if (_autoUpdateOffBtn != null) _autoUpdateOffBtn.SetDisabled(false);
                    }
                },
                out _ignoreOnBtn, out _ignoreOffBtn);

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

            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        private static void CreateToggleRow(Transform parent, TabletModalOverlay modal, string labelText,
            bool initialValue, Action onOn, Action onOff, 
            out TabletButton onBtn, out TabletButton offBtn)
        {
            int layer = parent.gameObject.layer;

            var rowObj = new GameObject($"Row_{labelText.Replace(" ", "")}", typeof(RectTransform));
            rowObj.layer = layer;
            rowObj.transform.SetParent(parent, false);
            
            var rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(700, 70);

            var hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 20f;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 70f;
            rowLE.flexibleWidth = 1f;

            var labelObj = UnityEngine.Object.Instantiate(modal.titleText.gameObject, rowObj.transform);
            labelObj.name = "Label";
            labelObj.transform.localScale = Vector3.one;
            
            var labelTxt = labelObj.GetComponent<TabletTextLabel>();
            if (labelTxt != null)
            {
                labelTxt.text = labelText;
                labelTxt.labelType = TabletTextLabel.LabelType.Normal;
            }

            var labelLE = labelObj.GetComponent<LayoutElement>() ?? labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 350f;
            labelLE.flexibleWidth = 1f;

            var onBtnObj = UnityEngine.Object.Instantiate(modal.onButton.gameObject, rowObj.transform);
            onBtnObj.name = "OnBtn";
            onBtnObj.transform.localScale = Vector3.one;
            
            onBtn = onBtnObj.GetComponent<TabletButton>();
            if (onBtn != null)
            {
                var onLabel = onBtnObj.GetComponentInChildren<TabletTextLabel>();
                if (onLabel != null) 
                {
                    onLabel.text = "On";
                    onLabel.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                    
                    var txt = onLabel.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.alignment = TextAnchor.MiddleCenter;
                    }
                }
                
                onBtn.OnClick = new TabletButtonEvent();
                onBtn.OnClick.AddListener((cursor) => onOn());
                onBtn.SetDisabled(false);
                onBtn.SetInteractable(true);
            }

            var onLE = onBtnObj.GetComponent<LayoutElement>() ?? onBtnObj.AddComponent<LayoutElement>();
            onLE.preferredWidth = 120f;
            onLE.preferredHeight = 70f;
            onLE.minWidth = 120f;
            onLE.minHeight = 70f;

            var offBtnObj = UnityEngine.Object.Instantiate(modal.offButton.gameObject, rowObj.transform);
            offBtnObj.name = "OffBtn";
            offBtnObj.transform.localScale = Vector3.one;
            
            offBtn = offBtnObj.GetComponent<TabletButton>();
            if (offBtn != null)
            {
                var offLabel = offBtnObj.GetComponentInChildren<TabletTextLabel>();
                if (offLabel != null)
                {
                    offLabel.text = "Off";
                    offLabel.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

                    var txt = offLabel.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.alignment = TextAnchor.MiddleCenter;
                    }
                }
                
                offBtn.OnClick = new TabletButtonEvent();
                offBtn.OnClick.AddListener((cursor) => onOff());
                offBtn.SetDisabled(false);
                offBtn.SetInteractable(true);
            }

            var offLE = offBtnObj.GetComponent<LayoutElement>() ?? offBtnObj.AddComponent<LayoutElement>();
            offLE.preferredWidth = 120f;
            offLE.preferredHeight = 70f;
            offLE.minWidth = 120f;
            offLE.minHeight = 70f;

            UpdateButtonStyles(onBtn, offBtn, initialValue);

            onBtnObj.SetActive(true);
            offBtnObj.SetActive(true);
        }

        private static void UpdateButtonStyles(TabletButton onBtn, TabletButton offBtn, bool isOn)
        {
            if (onBtn != null)
            {
                onBtn.buttonType = isOn ? TabletButton.ButtonType.Simple : TabletButton.ButtonType.Transparent;
                onBtn.ResetStyles();
            }
            if (offBtn != null)
            {
                offBtn.buttonType = isOn ? TabletButton.ButtonType.Transparent : TabletButton.ButtonType.Simple;
                offBtn.ResetStyles();
            }
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
