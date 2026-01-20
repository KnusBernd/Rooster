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

            _buttonTemplate = modal.okButton;

            UIHelpers.SetupModal(modal, new Vector2(1000, 900), $"Installed Mods ({Chainloader.PluginInfos.Count})", () =>
            {
                modal.Close();
                _cleanupCoroutine = RoosterPlugin.Instance.StartCoroutine(CleanupCoroutine(modal));
            });

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

            var container = modal.simpleMessageContainer;
            if (container == null)
            {
                RoosterPlugin.LogError("ApplyStyling: Container is null");
                return;
            }

            var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
            UIHelpers.CleanContainer(container.gameObject, textObj);

            // Use unified ScrollLayout
            var scrollLayout = UIHelpers.CreateScrollLayout(container.gameObject, "ModMenu", 80, 125, 0, 40, 10);

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

            CreateBrowseButton(container.GetComponent<RectTransform>());

            var displayList = new List<ModDisplayData>();

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                displayList.Add(new ModDisplayData
                {
                    Name = plugin.Metadata.Name,
                    Version = plugin.Metadata.Version.ToString(),
                    Guid = plugin.Metadata.GUID,
                    Plugin = plugin
                });
            }

            displayList.Add(new ModDisplayData
            {
                Name = "BepInEx",
                Version = typeof(Chainloader).Assembly.GetName().Version.ToString(),
                Guid = "bepinex",
                Plugin = null
            });

            displayList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            int count = 0;
            foreach (var data in displayList)
            {
                CreateModButton(contentRect, data);
                count++;
            }
            RoosterPlugin.LogInfo($"ApplyStyling: Created {count} mod buttons (including BepInEx)");

            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (scrollLayout.ScrollRect != null) scrollLayout.ScrollRect.verticalNormalizedPosition = 1f;
        }

        private static void CreateBrowseButton(RectTransform parent)
        {
            if (_buttonTemplate == null) return;

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
                tabletBtn.OnClick.AddListener((cursor) =>
                {
                    DestroyUI(Tablet.clickEventReceiver.modalOverlay);
                    ModBrowserUI.ShowModBrowser();
                });
                tabletBtn.SetDisabled(false);
                tabletBtn.SetInteractable(true);

                tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                tabletBtn.ResetStyles();

                UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Success);
            }

            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);

            rect.anchoredPosition = new Vector2(0, 10);
            rect.sizeDelta = new Vector2(500, 80); // Even wider

            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);

            btnObj.transform.SetAsLastSibling();
            btnObj.SetActive(true);
            _modButtons.Add(btnObj);
        }

        private struct ModDisplayData
        {
            public string Name;
            public string Version;
            public string Guid;
            public PluginInfo Plugin;
        }

        private static void CreateModButton(RectTransform parent, ModDisplayData data)
        {
            if (_buttonTemplate == null)
            {
                RoosterPlugin.LogError("Button template is null!");
                return;
            }

            string guid = data.Guid;
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = $"ModButton_{guid}";
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
                string displayName = data.Name;
                string prefix = "";

                if (UpdateChecker.PendingUninstalls.Contains(guid))
                {
                    prefix = "<color=#FF0000>-</color> ";
                }
                else if (UpdateChecker.PendingInstalls.Contains(guid))
                {
                    prefix = "<color=#FFFF00>+</color> ";
                }
                else
                {
                    bool hasUpdate = false;
                    foreach (var update in UpdateChecker.PendingUpdates)
                    {
                        string updateGuid = update.PluginInfo != null ? update.PluginInfo.Metadata.GUID : "bepinex";
                        if (updateGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                        {
                            hasUpdate = true;
                            break;
                        }
                    }

                    if (hasUpdate)
                    {
                        prefix = "<color=#00FFFF>↑</color> ";
                    }
                    else if (UpdateChecker.MatchedPackages.ContainsKey(guid))
                    {
                        prefix = "<color=#00FF00>*</color> ";
                    }
                }

                var uiText = label.GetComponent<Text>();
                if (uiText != null) uiText.supportRichText = true;

                label.text = $"{prefix}{displayName} v{data.Version}";
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
                tabletBtn.OnClick.AddListener((cursor) =>
                {
                    try
                    {
                        var pkg = UpdateChecker.MatchedPackages.ContainsKey(guid) ? UpdateChecker.MatchedPackages[guid] : null;
                        string tsFullName = pkg?.FullName;
                        ModSettingsUI.ShowModSettings(data.Plugin, guid, tsFullName);
                    }
                    catch (Exception ex)
                    {
                        RoosterPlugin.LogError($"Failed to open settings for {data.Name}: {ex}");
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
