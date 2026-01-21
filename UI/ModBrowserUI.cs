using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Models;
using BepInEx;
using Rooster.Services;
using Rooster.UI.Components;

namespace Rooster.UI
{
    public class ModBrowserUI
    {
        private static GameObject _viewportObj;
        private static GameObject _scrollbarObj;
        private static ScrollRect _scrollRect;
        private static List<GameObject> _itemButtons = new List<GameObject>();
        private static Vector2? _originalSize;
        private static float _lastScrollPos = 1f;

        private static List<ThunderstorePackage> _thunderstoreMods = new List<ThunderstorePackage>();
        private static List<ThunderstorePackage> _curatedMods = new List<ThunderstorePackage>();
        private static bool _isThunderstoreTab = true;

        private static TabletModalOverlay _currentModal;
        private static TabletButton _buttonTemplate;

        // New Component System
        private static BrowserTabSystem _tabSystem;

        public static void SetVisible(bool visible)
        {
            if (_viewportObj != null) _viewportObj.SetActive(visible);
            if (_scrollbarObj != null) _scrollbarObj.SetActive(visible);
            if (_scrollRect != null) _scrollRect.enabled = visible;
            foreach (var b in _itemButtons) if (b != null) b.SetActive(visible);

            if (_tabSystem != null) _tabSystem.SetVisible(visible);
        }


        public static void ShowModBrowser(bool preserveScroll = false)
        {
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.ModBrowser;

            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null)
            {
                RoosterPlugin.LogError("Cannot open browser: Tablet overlay unavailable");
                return;
            }

            var modal = Tablet.clickEventReceiver.modalOverlay;
            _currentModal = modal;

            // Ensure ModMenu is cleaned up
            ModMenuUI.DestroyUI(modal);

            _buttonTemplate = modal.okButton;

            UIHelpers.SetupModal(modal, new Vector2(1000, 900), "Mod Browser", () =>
            {
                Close();
                ModMenuUI.ShowModMenu(); // Go back to mod menu
            });

            ApplyStyling(modal);

            // Initial Fetch
            RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(false, preserveScroll));
            SetVisible(true);
        }

        private static void Close()
        {
            DestroyUI(_currentModal);
        }

        private static IEnumerator FetchAndDisplay(bool forceRefresh, bool preserveScroll = false)
        {
            RoosterPlugin.LogInfo($"ModBrowser: FetchAndDisplay Started (Force: {forceRefresh})");

            if (_tabSystem != null) _tabSystem.SetRefreshState(true);

            if (forceRefresh)
            {
                _thunderstoreMods.Clear();
                _curatedMods.Clear();
                GitHubApi.IsCacheReady = false;
                GitHubApi.CachedPackages.Clear();
            }

            //  THUNDERSTORE FETCH
            if (_thunderstoreMods.Count == 0 && UpdateChecker.CachedPackages.Count > 0)
            {
                // Filter out GitHub packages from Thunderstore list
                _thunderstoreMods = new List<ThunderstorePackage>();
                foreach (var pkg in UpdateChecker.CachedPackages)
                {
                    if (pkg.Categories == null || !pkg.Categories.Contains("GitHub"))
                    {
                        _thunderstoreMods.Add(pkg);
                    }
                }
            }

            if (_thunderstoreMods.Count > 0 && !forceRefresh)
            {
                RoosterPlugin.LogInfo($"ModBrowser: Using cached Thunderstore list ({_thunderstoreMods.Count} items)");
            }
            else
            {
                bool tsComplete = false;
                string tsError = null;
                RoosterPlugin.Instance.StartCoroutine(ThunderstoreApi.FetchAllPackages((packages, error) =>
                {
                    if (error != null) tsError = error;
                    else _thunderstoreMods = packages;
                    tsComplete = true;
                }));

                float tsTimeout = UnityEngine.Time.realtimeSinceStartup + 45f;
                yield return new WaitUntil(() => tsComplete || UnityEngine.Time.realtimeSinceStartup > tsTimeout);

                if (!tsComplete)
                {
                    tsError = "Request timed out.";
                    RoosterPlugin.LogWarning("ModBrowser: Thunderstore fetch timed out.");
                }

                if (tsError != null)
                {
                    ShowErrorModal($"Thunderstore Error:\n{tsError}");
                    HideLoading();
                    if (_tabSystem != null) _tabSystem.SetRefreshState(false);
                    yield break;
                }
            }

            //  GITHUB FETCH
            if (GitHubApi.IsCacheReady && !forceRefresh)
            {
                _curatedMods = new List<ThunderstorePackage>(GitHubApi.CachedPackages);
            }
            else
            {
                // Use BuildCache to leverage disk cache logic
                yield return GitHubApi.BuildCache();

                if (GitHubApi.IsCacheReady)
                {
                    _curatedMods = new List<ThunderstorePackage>(GitHubApi.CachedPackages);
                }
                else
                {
                    string ghError = GitHubApi.LastError ?? "Unknown Error";
                    RoosterPlugin.LogWarning($"ModBrowser: GitHub fetch failed: {ghError}");
                    ShowErrorModal($"GitHub Error:\n{ghError}");
                }
            }

            try
            {
                RefreshList();

                Canvas.ForceUpdateCanvases();

                if (preserveScroll && _scrollRect != null)
                {
                    _scrollRect.verticalNormalizedPosition = _lastScrollPos;
                }
                else if (_scrollRect != null)
                {
                    _scrollRect.verticalNormalizedPosition = 1f; // Force Top
                }
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"ModBrowser: Error refreshing list: {ex}");
            }

            HideLoading();
            if (_tabSystem != null) _tabSystem.SetRefreshState(false);
        }

        private static void HideLoading()
        {
            if (_currentModal != null && _currentModal.simpleMessageText != null)
                _currentModal.simpleMessageText.gameObject.SetActive(false);
        }

        private static void ShowErrorModal(string message)
        {
            // Simplified for brevity, logic remains or can be extracted too
            RoosterPlugin.LogError("Showing Error Modal: " + message);
            if (_currentModal == null) return;

            var errorObj = new GameObject("ErrorPopup", typeof(RectTransform));
            errorObj.transform.SetParent(_currentModal.transform, false);
            var rect = errorObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;

            var img = errorObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.9f);
            img.raycastTarget = true; // Block clicks

            var vLayout = errorObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.spacing = 20;
            vLayout.padding = new RectOffset(50, 50, 50, 50);

            UIHelpers.AddText(errorObj.transform, "Error", 30, true, Color.red);
            UIHelpers.AddText(errorObj.transform, message, 20, false, Color.white);

            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, errorObj.transform);
            var lbl = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (lbl != null) lbl.text = "Dismiss";

            var btn = btnObj.GetComponent<TabletButton>();
            if (btn != null)
            {
                btn.OnClick = new TabletButtonEvent();
                btn.OnClick.AddListener((c) =>
                {
                    UnityEngine.Object.Destroy(errorObj);
                });
            }
        }

        private static void ApplyStyling(TabletModalOverlay modal)
        {
            RoosterPlugin.LogInfo("ModBrowser: ApplyStyling");
            DestroyUI(modal);

            var container = modal.simpleMessageContainer;

            if (!_originalSize.HasValue) _originalSize = container.GetComponent<RectTransform>().sizeDelta;

            var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
            UIHelpers.CleanContainer(container.gameObject, textObj);

            var layout = UIHelpers.CreateScrollLayout(container.gameObject, "Browser", 120, 0, 25, 40, 10);

            _viewportObj = layout.Viewport.gameObject;
            _scrollbarObj = layout.ScrollbarObj;
            _scrollRect = layout.ScrollRect;

            // Setup Content Layout
            var contentObj = layout.Content.gameObject;
            var vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.spacing = 10;
            vLayout.padding = new RectOffset(10, 10, 10, 10);

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Initialize Tab System
            _tabSystem = new BrowserTabSystem(_buttonTemplate);
            _tabSystem.OnRefreshClicked += () => RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(true));
            _tabSystem.OnThunderstoreTabClicked += () =>
            {
                if (!_isThunderstoreTab)
                {
                    _isThunderstoreTab = true;
                    RefreshList();
                    UpdateTitle(modal);
                }
            };
            _tabSystem.OnGitHubTabClicked += () =>
            {
                if (_isThunderstoreTab)
                {
                    if (RoosterConfig.GitHubWarningAccepted.Value)
                    {
                        _isThunderstoreTab = false;
                        RefreshList();
                        UpdateTitle(modal);
                    }
                    else
                    {
                        UIHelpers.ShowGitHubWarning(modal,
                            onAccept: () =>
                            {
                                RoosterConfig.GitHubWarningAccepted.Value = true;
                                RoosterConfig.SaveConfig();
                                _isThunderstoreTab = false;
                                ApplyStyling(modal);
                                RefreshList();
                            },
                            onCancel: () =>
                            {
                                // User cancelled, so we stick to Thunderstore. 
                                // Since _isThunderstoreTab is still true, we just need to refresh the UI 
                                // to clear the disclaimer and show the browser again.
                                ApplyStyling(modal);
                                RefreshList();
                            }
                        );
                    }
                }
            };

            _tabSystem.CreateTabs(container, _isThunderstoreTab);
            UpdateTitle(modal);
        }

        private static void UpdateTitle(TabletModalOverlay modal)
        {
            if (modal == null) return;

            if (modal.titleText != null)
            {
                modal.titleText.text = _isThunderstoreTab ? "Thunderstore Browser" : "GitHub Browser";
            }
            if (modal.simpleMessageText != null)
            {
                modal.simpleMessageText.gameObject.SetActive(false);
            }
        }

        private static void RefreshList()
        {
            if (_viewportObj == null) return;

            var scrollRect = _viewportObj.transform.parent.GetComponent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;

            var contentRect = scrollRect.content;

            foreach (Transform child in contentRect)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            var list = _isThunderstoreTab ? _thunderstoreMods : _curatedMods;

            foreach (var pkg in list)
            {
                var item = ModListItem.Create(_buttonTemplate, contentRect, pkg, (p) =>
                {
                    if (_scrollRect != null) _lastScrollPos = _scrollRect.verticalNormalizedPosition;
                    ModDetailsUI.ShowDetails(p);
                });

                if (item != null) _itemButtons.Add(item);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            // Fix for scrollbar handle vanishing - force visual refresh
            if (_scrollbarObj != null)
            {
                _scrollbarObj.SetActive(false);
                _scrollbarObj.SetActive(true);

                var sb = _scrollbarObj.GetComponent<Scrollbar>();
                if (sb != null)
                {
                    sb.value = 1f;
                    sb.size = 0.5f;
                }
            }
        }

        public static void DestroyUI(TabletModalOverlay modal)
        {
            if (_viewportObj != null) UnityEngine.Object.Destroy(_viewportObj);
            _viewportObj = null;
            if (_scrollbarObj != null) UnityEngine.Object.Destroy(_scrollbarObj);
            _scrollbarObj = null;
            foreach (var b in _itemButtons) if (b != null) UnityEngine.Object.Destroy(b);
            _itemButtons.Clear();
            _scrollRect = null;
            _tabSystem = null; // Clear ref

            if (modal != null && _originalSize.HasValue)
            {
                var container = modal.simpleMessageContainer;
                if (container != null)
                {
                    container.GetComponent<RectTransform>().sizeDelta = _originalSize.Value;
                    modal.simpleMessageText.gameObject.SetActive(true);
                }
                _originalSize = null;
            }

            ModDetailsUI.CleanupCustomUI();
        }
    }
}
