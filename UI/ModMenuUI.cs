using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using Rooster.UI;

namespace Rooster.UI
{
    /// <summary>
    /// Manages the UI for the Mod Menu, which lists all installed BepInEx plugins.
    /// Handles the creation and management of the scrollable mod list within the tablet interface.
    /// </summary>
    public class ModMenuUI
    {
        private static TabletButton _buttonTemplate;
        private static List<GameObject> _modButtons = new List<GameObject>();
        private static GameObject _viewportObj;
        private static GameObject _scrollbarObj;
        private static Vector2? _originalSize;
        private static Coroutine _cleanupCoroutine;

        public static void SetVisible(bool visible)
        {
            if (_viewportObj != null) _viewportObj.SetActive(visible);
            if (_scrollbarObj != null) _scrollbarObj.SetActive(visible);
            
            foreach (var btn in _modButtons)
            {
                if (btn != null) btn.SetActive(visible);
            }
        }

        public static void ShowModMenu()
        {
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.ModMenu;
            if (_cleanupCoroutine != null)
            {
                RoosterPlugin.Instance.StopCoroutine(_cleanupCoroutine);
                _cleanupCoroutine = null;
            }

            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null)
            {
                RoosterPlugin.LogError("Cannot open mod menu: Tablet overlay not available");
                return;
            }

            var modal = Tablet.clickEventReceiver.modalOverlay;
            
            int modCount = Chainloader.PluginInfos.Count;
            modal.ShowSimpleMessage($"Installed Mods ({modCount})", "", () => { });
            
            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) => {
                modal.Close();
                _cleanupCoroutine = RoosterPlugin.Instance.StartCoroutine(CleanupCoroutine(modal));
            });

            modal.onOffContainer.gameObject.SetActive(false);

            _buttonTemplate = modal.okButton;

            try
            {
                ApplyStyling(modal);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to apply mod menu styling: {ex}");
            }
        }

        private static void ApplyStyling(TabletModalOverlay modal)
        {
            DestroyUI(modal);

            var textObj = modal.simpleMessageText.gameObject;
            var container = modal.simpleMessageContainer;
            if (container == null) return;
            var containerRect = container.GetComponent<RectTransform>();

            if (!_originalSize.HasValue)
            {
                _originalSize = containerRect.sizeDelta;
            }

            containerRect.sizeDelta = new Vector2(1000, 900);
            
            var bgImg = container.gameObject.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            bgImg.color = Color.clear;
            bgImg.raycastTarget = true;

            // Remove any existing LayoutGroup or Fitter that might interfere with manual positioning
            var existingLayout = container.GetComponent<LayoutGroup>();
            if (existingLayout != null) UnityEngine.Object.DestroyImmediate(existingLayout);
            
            var existingFitter = container.GetComponent<ContentSizeFitter>();
            if (existingFitter != null) UnityEngine.Object.DestroyImmediate(existingFitter);

             // Enforce size using LayoutElement in case parent forces layout
            var containerLE = container.GetComponent<LayoutElement>() ?? container.gameObject.AddComponent<LayoutElement>();
            containerLE.preferredWidth = 1000f;
            containerLE.preferredHeight = 900f;
            containerLE.minWidth = 1000f;
            containerLE.minHeight = 900f;
            containerLE.flexibleWidth = 0f;
            containerLE.flexibleHeight = 0f;

            foreach (var btn in _modButtons)
            {
                if (btn != null) UnityEngine.Object.DestroyImmediate(btn);
            }
            _modButtons.Clear();

            textObj.SetActive(false);

            textObj.SetActive(false);

            // Use unified ScrollLayout
            // Top Margin: 80 (was -80 offsetMax)
            // Bottom Margin: 100 (was 100 offsetMin)
            // Side Margin: 0
            // Scrollbar Width: 40
            // Padding: 10
            var scrollLayout = UIHelpers.CreateScrollLayout(container.gameObject, "ModMenu", 80, 100, 0, 40, 10);
            
            _viewportObj = scrollLayout.Viewport.gameObject;
            _scrollbarObj = scrollLayout.ScrollbarObj;
            var contentRect = scrollLayout.Content;
            var contentObj = contentRect.gameObject;

            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.spacing = 12f;
            layout.padding = new RectOffset(20, 20, 15, 15);

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Browse Button - Moved to bottom
            CreateBrowseButton(containerRect);

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                CreateModButton(contentRect, plugin);
            }

            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private static void CreateBrowseButton(RectTransform parent)
        {
            if (_buttonTemplate == null) return;
            
            // Clone the 'on' button template for a smaller style
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "BrowseModsButton";
            
            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
             if (label != null)
            {
                label.text = "Browse Online";
                label.transform.localScale = new Vector3(0.6f, 0.6f, 1f); 
                
                var txt = label.GetComponent<Text>();
                if (txt != null)
                {
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.alignment = TextAnchor.MiddleCenter;
                }
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                 if (tabletBtn.colorScheme == null) tabletBtn.colorScheme = _buttonTemplate.colorScheme;
                 
                 tabletBtn.OnClick = new TabletButtonEvent();
                 tabletBtn.OnClick.AddListener((cursor) => {
                     DestroyUI(Tablet.clickEventReceiver.modalOverlay);
                     ModBrowserUI.ShowModBrowser();
                 });
                 tabletBtn.SetDisabled(false);
                 tabletBtn.SetInteractable(true);
                 
                 // Rounded style
                 tabletBtn.buttonType = TabletButton.ButtonType.Simple; 
                 tabletBtn.ResetStyles();
                 
                 // Create custom color scheme to handle hover states natively
                 var newScheme = UIHelpers.CloneColorScheme(_buttonTemplate.colorScheme, btnObj);
                 
                 Color normalColor = new Color(0.2f, 0.7f, 0.3f);
                 Color hoverColor = new Color(0.3f, 0.8f, 0.4f); // Lighter green
                 
                 newScheme.buttonBgColor = normalColor;
                 newScheme.buttonBgColor_Hover = hoverColor;
                 newScheme.buttonBgColor_Disabled = new Color(0.2f, 0.2f, 0.2f);
                 
                 tabletBtn.colorScheme = newScheme;
                 tabletBtn.ResetStyles();
                 
                 // Ensure background is enabled
                 if (tabletBtn.background != null) {
                     tabletBtn.background.color = normalColor;
                 }
            }
            
            // Positioning at Bottom Center
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            
            rect.anchoredPosition = new Vector2(0, 10); 
            rect.sizeDelta = new Vector2(500, 80); // Even wider

            // Remove LayoutElement
            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);

            // Ensure z order
            btnObj.transform.SetAsLastSibling();
            btnObj.SetActive(true);
            _modButtons.Add(btnObj);
        }

        private static void CreateModButton(RectTransform parent, PluginInfo plugin)
        {
            if (_buttonTemplate == null)
            {
                RoosterPlugin.LogError("Button template is null!");
                return;
            }

            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = $"ModButton_{plugin.Metadata.GUID}";
            btnObj.transform.localScale = Vector3.one;

            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(0, 70);

            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                string displayName = plugin.Metadata.Name;
                if (UpdateChecker.MatchedPackages.ContainsKey(plugin.Metadata.GUID))
                {
                    displayName = "<color=#00FF00>*</color> " + displayName;
                }
                
                var uiText = label.GetComponent<Text>();
                if (uiText != null) uiText.supportRichText = true;

                label.text = $"{displayName} v{plugin.Metadata.Version}";
                label.labelType = TabletTextLabel.LabelType.SmallText;
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                if (tabletBtn.colorScheme == null)
                {
                    tabletBtn.colorScheme = _buttonTemplate.colorScheme;
                }

                tabletBtn.OnClick = new TabletButtonEvent();
                tabletBtn.OnClick.AddListener((cursor) => {
                    try
                    {
                        var pkg = Services.ModMatcher.FindPackage(plugin, UpdateChecker.CachedPackages);
                        string tsFullName = pkg?.full_name;
                        ModSettingsUI.ShowModSettings(plugin, tsFullName);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Failed to open settings for {plugin.Metadata.Name}: {ex}");
                    }
                });
                tabletBtn.SetDisabled(false);
                tabletBtn.SetInteractable(true);
                
                if (tabletBtn.background != null)
                {
                    tabletBtn.background.raycastTarget = true;
                    tabletBtn.background.enabled = true;
                }

                tabletBtn.ResetStyles();
            }

            var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 70f;
            le.flexibleWidth = 1f;
            le.minHeight = 70f;

            btnObj.SetActive(true);

            _modButtons.Add(btnObj);
        }

        public static void DestroyUI(TabletModalOverlay modal = null)
        {
            if (_viewportObj != null)
            {
                 UnityEngine.Object.DestroyImmediate(_viewportObj);
                 _viewportObj = null;
            }
            if (_scrollbarObj != null)
            {
                UnityEngine.Object.DestroyImmediate(_scrollbarObj);
                _scrollbarObj = null;
            }
            
            foreach (var btn in _modButtons)
            {
                if (btn != null) UnityEngine.Object.DestroyImmediate(btn);
            }
            _modButtons.Clear();

            if (modal != null && _originalSize.HasValue)
            {
                var container = modal.simpleMessageContainer;
                if (container != null)
                {
                    var rect = container.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.sizeDelta = _originalSize.Value;
                    }
                    var textObj = modal.simpleMessageText.gameObject;
                    if (textObj != null) textObj.SetActive(true);
                }
                _originalSize = null;
            }
        }

        private static IEnumerator CleanupCoroutine(TabletModalOverlay modal)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            DestroyUI(modal);
            _cleanupCoroutine = null;
        }

        public static void ScheduleCleanup()
        {
            if (_cleanupCoroutine != null)
            {
                RoosterPlugin.Instance.StopCoroutine(_cleanupCoroutine);
            }
            var modal = Tablet.clickEventReceiver?.modalOverlay;
            _cleanupCoroutine = RoosterPlugin.Instance.StartCoroutine(CleanupCoroutine(modal));
        }
    }
}
