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

            foreach (var btn in _modButtons)
            {
                if (btn != null) UnityEngine.Object.DestroyImmediate(btn);
            }
            _modButtons.Clear();

            textObj.SetActive(false);

            var viewportObj = new GameObject("ModMenuViewport", typeof(RectTransform));
            _viewportObj = viewportObj;
            viewportObj.layer = container.gameObject.layer;
            viewportObj.transform.SetParent(container, false);
            var viewportRect = viewportObj.GetComponent<RectTransform>();
            
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-50, -10);

            var vpImg = viewportObj.AddComponent<Image>();
            vpImg.sprite = UIHelpers.GetWhiteSprite();
            vpImg.color = Color.white;
            var mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var vpLayout = viewportObj.AddComponent<LayoutElement>();
            vpLayout.preferredHeight = 800f;
            vpLayout.flexibleHeight = 0f;
            vpLayout.flexibleWidth = 1f;

            var contentObj = new GameObject("ModListContent", typeof(RectTransform));
            contentObj.layer = container.gameObject.layer;
            contentObj.transform.SetParent(viewportRect, false);
            var contentRect = contentObj.GetComponent<RectTransform>();

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

            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            var scrollRect = container.gameObject.GetComponent<ScrollRect>() ?? container.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.enabled = true;

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                CreateModButton(contentRect, plugin);
            }

            _scrollbarObj = UIHelpers.CreateScrollbar(container, scrollRect, "ModMenu");

            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
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
