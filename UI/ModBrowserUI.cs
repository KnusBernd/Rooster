using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Models;
using BepInEx;
using Rooster.Services;

namespace Rooster.UI
{
    public class ModBrowserUI
    {
        private static GameObject _viewportObj;
        private static GameObject _scrollbarObj;
        private static ScrollRect _scrollRect;
        private static List<GameObject> _itemButtons = new List<GameObject>();
        private static Vector2? _originalSize;
        
        private static List<ThunderstorePackage> _thunderstoreMods = new List<ThunderstorePackage>();
        private static List<ThunderstorePackage> _curatedMods = new List<ThunderstorePackage>();
        private static bool _isThunderstoreTab = true;
        
        // Caching the modal for refreshing
        private static TabletModalOverlay _currentModal;
        private static TabletButton _refreshButton;
        private static TabletTextLabel _refreshLabel;
        private static GameObject _loadingSpinner;

        public static void SetVisible(bool visible)
        {
            if (_viewportObj != null) _viewportObj.SetActive(visible);
            if (_scrollbarObj != null) _scrollbarObj.SetActive(visible);
            if (_scrollRect != null) _scrollRect.enabled = visible;
            foreach (var b in _itemButtons) if (b != null) b.SetActive(visible);
            if (_refreshButton != null) _refreshButton.gameObject.SetActive(visible);
        }


        public static void ShowModBrowser()
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

             UIHelpers.SetupModal(modal, new Vector2(1000, 900), "Mod Browser", () => {
                 Close();
                 ModMenuUI.ShowModMenu(); // Go back to mod menu
             });

            ApplyStyling(modal);
            
            // Initial Fetch
            RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(false));
            SetVisible(true); // Ensure visible
        }

        private static void Close()
        {
            DestroyUI(_currentModal);
        }

        private static IEnumerator FetchAndDisplay(bool forceRefresh)
        {
            RoosterPlugin.LogInfo($"ModBrowser: FetchAndDisplay Started (Force: {forceRefresh})");
            
            if (_refreshButton != null) 
            {
                _refreshButton.SetInteractable(false);
                _refreshButton.SetDisabled(true); 
            }
            
            if (_refreshLabel != null) _refreshLabel.text = "";
            if (_loadingSpinner != null) _loadingSpinner.SetActive(true);

            if (forceRefresh)
            {
                _thunderstoreMods.Clear();
                _curatedMods.Clear();
                GitHubApi.IsCacheReady = false; 
                GitHubApi.CachedPackages.Clear();
            }

                //  THUNDERSTORE FETCH
                if (_thunderstoreMods.Count > 0 && !forceRefresh)
                {
                     RoosterPlugin.LogInfo($"ModBrowser: Using cached Thunderstore list ({_thunderstoreMods.Count} items)");
                }
                else
                {
                    bool tsComplete = false;
                    string tsError = null;
                    RoosterPlugin.Instance.StartCoroutine(ThunderstoreApi.FetchAllPackages((packages, error) => {
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
                        // Ensure we clean up even on error
                        HideLoading();
                        if (_refreshButton != null) 
                        {
                            _refreshButton.SetInteractable(true);
                            _refreshButton.SetDisabled(false);
                        }
                        if (_refreshLabel != null) _refreshLabel.text = "Refresh";
                        yield break;  
                    }
                }

                //  GITHUB FETCH
                if (GitHubApi.IsCacheReady && !forceRefresh)
                {
                     RoosterPlugin.LogInfo($"ModBrowser: Using Startup GitHub Cache ({GitHubApi.CachedPackages.Count} items)");
                     _curatedMods = new List<ThunderstorePackage>(GitHubApi.CachedPackages);
                }
                else if (_curatedMods.Count > 0 && !forceRefresh)
                {
                     RoosterPlugin.LogInfo($"ModBrowser: Using local GitHub list ({_curatedMods.Count} items)");
                }
                else
                {
                    bool ghComplete = false;
                    string ghError = null;
                    
                    RoosterPlugin.Instance.StartCoroutine(GitHubApi.FetchCuratedList((packages, error) => {
                        if (error != null) ghError = error;
                        else 
                        {
                            _curatedMods = packages;
                            GitHubApi.CachedPackages = packages;
                            GitHubApi.IsCacheReady = true; 
                        }
                        ghComplete = true;
                    }));
                    
                    float ghTimeout = UnityEngine.Time.realtimeSinceStartup + 45f;
                    yield return new WaitUntil(() => ghComplete || UnityEngine.Time.realtimeSinceStartup > ghTimeout);
                    
                    if (!ghComplete)
                    {
                         ghError = "Request timed out.";
                         RoosterPlugin.LogWarning("ModBrowser: GitHub fetch timed out.");
                    }
                    
                    if (ghError != null)
                    {
                        ShowErrorModal($"GitHub Error:\n{ghError}");
                    }
                }
            
            try 
            {
                RefreshList();
            }
            catch(Exception ex)
            {
                RoosterPlugin.LogError($"ModBrowser: Error refreshing list: {ex}");
            }
            
            HideLoading();
            if (_refreshButton != null) 
            {
                _refreshButton.SetInteractable(true);
                _refreshButton.SetDisabled(false);
            }
            if (_refreshLabel != null) _refreshLabel.text = "Refresh";
        }

        private static void HideLoading()
        {
            RoosterPlugin.LogInfo("ModBrowser: Hiding loading text/spinner.");
            if (_currentModal != null && _currentModal.simpleMessageText != null) 
            _currentModal.simpleMessageText.gameObject.SetActive(false);
            
            if (_loadingSpinner != null) _loadingSpinner.SetActive(false);
        }
        
        private static void ShowErrorModal(string message)
        {
             RoosterPlugin.LogError("Showing Error Modal: " + message);
             if (_currentModal == null) return;
             
             var errorObj = new GameObject("ErrorPopup", typeof(RectTransform));
             errorObj.transform.SetParent(_currentModal.transform, false);
             var rect = errorObj.GetComponent<RectTransform>();
             rect.anchorMin = Vector2.zero;
             rect.anchorMax = Vector2.one;
             
             var img = errorObj.AddComponent<Image>();
             img.color = new Color(0,0,0, 0.9f);
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
                 btn.OnClick.AddListener((c) => {
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

            UIHelpers.CleanContainer(container.gameObject);

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
            
            CreateTabs(container);

            // Hide default text
            modal.simpleMessageText.gameObject.SetActive(false);
        }

        private static void CreateTabs(Transform parent)
        {
            CreateTabButton(parent, "Thunderstore", -315, () => {
                if (!_isThunderstoreTab) {
                    _isThunderstoreTab = true;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, _isThunderstoreTab);

            CreateTabButton(parent, "GitHub", 15, () => {
                if (_isThunderstoreTab) {
                    _isThunderstoreTab = false;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, !_isThunderstoreTab);
            
            CreateRefreshButton(parent);
        }
        
        private static void CreateRefreshButton(Transform parent)
        {
            if (_buttonTemplate == null) return;
            
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "RefreshButton";
             
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);     
            rect.sizeDelta = new Vector2(200, 80); 
            rect.anchoredPosition = new Vector2(300, -10); // Far Right Spot, Center Anchor

            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
             if (label != null)
            {
                _refreshLabel = label;
                label.text = "Refresh";
                label.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                label.labelType = TabletTextLabel.LabelType.Normal;
                
                var t = label.GetComponent<Text>();
                if (t != null)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                }
                
                // Fix layout - ensure matches parent size
                var lblRect = label.GetComponent<RectTransform>();
                if (lblRect != null)
                {
                    lblRect.anchorMin = Vector2.zero;
                    lblRect.anchorMax = Vector2.one;
                    lblRect.sizeDelta = Vector2.zero;
                    lblRect.anchoredPosition = Vector2.zero;
                }
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                  UIHelpers.ApplyButtonStyle(tabletBtn,
                    new Color(0.8f, 0.6f, 0.2f), // Orange-ish
                    new Color(0.9f, 0.7f, 0.3f),
                    new Color(0.5f, 0.5f, 0.5f)
                  );
                  
                  tabletBtn.OnClick = new TabletButtonEvent();
                  tabletBtn.OnClick.AddListener((cursor) => {
                      // Trigger Refresh
                      RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(true));
                  });
                  tabletBtn.SetDisabled(false);
                  tabletBtn.SetInteractable(true);
                  tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                  tabletBtn.ResetStyles();
                  
                  _refreshButton = tabletBtn;
            }
            
            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);
            
            _itemButtons.Add(btnObj); 
            
            GameObject playOnline = GameObject.Find("main Buttons/Play Online");
            if (playOnline != null)
            {
                var originalSpinner = playOnline.transform.Find("LoadingSpinner");
                if (originalSpinner != null)
                {
                    _loadingSpinner = UnityEngine.Object.Instantiate(originalSpinner.gameObject, btnObj.transform);
                    _loadingSpinner.name = "RefreshSpinner";
                    _loadingSpinner.SetActive(false);
                    
                    var spinRect = _loadingSpinner.GetComponent<RectTransform>();
                    if (spinRect != null)
                    {
                        spinRect.anchorMin = new Vector2(0.5f, 0.5f);
                        spinRect.anchorMax = new Vector2(0.5f, 0.5f);
                        spinRect.pivot = new Vector2(0.5f, 0.5f);
                        spinRect.anchoredPosition = Vector2.zero;
                        _loadingSpinner.transform.localScale = new Vector3(0.375f, 0.375f, 1f);
                    }
                }
            }
        }
        
        private static void CreateTabButton(Transform parent, string text, float xOffset, Action onClick, bool active)
        {
            if (_buttonTemplate == null) return;

            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "Tab_" + text;
             
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(300, 80); // Slightly smaller to fit refresh
            rect.anchoredPosition = new Vector2(xOffset, -10);

            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
             if (label != null)
            {
                label.text = text;
                label.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // Smaller text
                label.labelType = TabletTextLabel.LabelType.Normal;
                
                var t = label.GetComponent<Text>();
                if (t != null) 
                {
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                 Color activeColor = new Color(0.2f, 0.6f, 1f); // Blue
                 Color inactiveColor = new Color(0.5f, 0.5f, 0.5f); // Grey
                 Color hoverColor = active ? new Color(0.3f, 0.7f, 1f) : new Color(0.6f, 0.6f, 0.6f);
                 
                 Color baseColor = active ? activeColor : inactiveColor;
                 
                 UIHelpers.ApplyButtonStyle(tabletBtn,
                    baseColor,
                    hoverColor,
                    inactiveColor
                 );
                 
                 tabletBtn.OnClick = new TabletButtonEvent();
                 tabletBtn.OnClick.AddListener((cursor) => {
                     RoosterPlugin.LogInfo($"Tab Clicked: {text}");
                     onClick();
                 });
                 tabletBtn.SetDisabled(false);
                 tabletBtn.SetInteractable(true);
                 tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                 tabletBtn.ResetStyles();
            }
            
            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);
            
            _itemButtons.Add(btnObj); 
        }

        private static void RefreshList()
        {
            RoosterPlugin.LogInfo("ModBrowser: RefreshList");
            if (_viewportObj == null) 
            {
                RoosterPlugin.LogError("ModBrowser: Viewport is null!");
                return;
            }
            
            var scrollRect = _viewportObj.transform.parent.GetComponent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) 
            {
                RoosterPlugin.LogError("ModBrowser: ScrollRect or Content is null!");
                return;
            }
            
            var contentRect = scrollRect.content;
            RoosterPlugin.LogInfo($"ModBrowser: Clearing content (children: {contentRect.childCount})");
            
            foreach (Transform child in contentRect)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            
            var list = _isThunderstoreTab ? _thunderstoreMods : _curatedMods;
            RoosterPlugin.LogInfo($"ModBrowser: Populating {list.Count} items. (Tab: {(_isThunderstoreTab ? "Thunderstore" : "GitHub")})");
            
            foreach(var pkg in list)
            {
                CreatePackageItem(contentRect, pkg);
            }
            
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private static TabletButton _buttonTemplate;

        private static void CreatePackageItem(RectTransform parent, ThunderstorePackage pkg)
        {
            if (_buttonTemplate == null) return;
            
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "Pkg_" + pkg.name;
            
            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                label.text = $"{pkg.name.Replace('_', ' ')} v{pkg.latest.version_number}";
                // Maybe append " (Installed)" if detected?
                label.labelType = TabletTextLabel.LabelType.SmallText;
                
                var uiText = label.GetComponent<Text>();
                if (uiText != null) uiText.supportRichText = true;
            }
            
            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                if (tabletBtn.colorScheme == null) tabletBtn.colorScheme = _buttonTemplate.colorScheme;
                
                tabletBtn.OnClick = new TabletButtonEvent();
                tabletBtn.OnClick.AddListener((cursor) => ModDetailsUI.ShowDetails(pkg));
                tabletBtn.SetDisabled(false);
                tabletBtn.SetInteractable(true);
                tabletBtn.ResetStyles();
                
                // Check installation status
                bool isInstalled = false;
                if (UpdateChecker.MatchedPackages != null)
                {
                    foreach (var installedPkg in UpdateChecker.MatchedPackages.Values)
                    {
                        if (installedPkg.full_name == pkg.full_name)
                        {
                            isInstalled = true;
                            break;
                        }
                    }
                }
                
                Color normalColor;
                Color hoverColor;
                
                if (isInstalled)
                {
                    normalColor = new Color(0.2f, 0.7f, 0.3f); // Standard Green for installed
                    hoverColor = new Color(0.3f, 0.8f, 0.4f);
                }
                else if (UpdateChecker.PendingInstalls.Contains(pkg.full_name))
                {
                    normalColor = new Color(0.8f, 0.6f, 0.2f); // Orange/Amber for Pending
                    hoverColor = new Color(0.9f, 0.7f, 0.3f);
                }
                else
                {
                    normalColor = new Color(0.8f, 0.8f, 0.8f); // Grey
                    hoverColor = new Color(0.9f, 0.9f, 0.9f); // Lighter Grey
                }
                
                UIHelpers.ApplyButtonStyle(tabletBtn,
                    normalColor,
                    hoverColor,
                    normalColor
                );
             }

             var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
             le.preferredHeight = 70f;
             le.flexibleWidth = 1f;
             le.minHeight = 70f;
             
             btnObj.SetActive(true);
        }

        public static void DestroyUI(TabletModalOverlay modal)
        {
            if (_viewportObj != null) UnityEngine.Object.Destroy(_viewportObj);
            _viewportObj = null;
            if (_scrollbarObj != null) UnityEngine.Object.Destroy(_scrollbarObj);
            _scrollbarObj = null;
            foreach (var b in _itemButtons) if (b != null) UnityEngine.Object.Destroy(b);
            _itemButtons.Clear();
            _refreshButton = null;
            _scrollRect = null;

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
