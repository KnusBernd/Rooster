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
            
            modal.ShowSimpleMessage("Mod Browser", "", () => { });
            modal.okButtonContainer.gameObject.SetActive(true);
            _buttonTemplate = modal.okButton;
            
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) => {
               Close();
               ModMenuUI.ShowModMenu(); // Go back to mod menu
            });

            modal.onOffContainer.gameObject.SetActive(false); // Hide standard content for now

            ApplyStyling(modal);
            
            // Initial Fetch
            RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(false));
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
            if (_refreshLabel != null) _refreshLabel.text = ""; // Hide text for spinner
            if (_loadingSpinner != null) _loadingSpinner.SetActive(true);


            
            // Loading Indicator removed per user request


            // FORCE REFRESH: Clear Local Caches
            if (forceRefresh)
            {
                _thunderstoreMods.Clear();
                _curatedMods.Clear();
                GitHubApi.IsCacheReady = false; 
                GitHubApi.CachedPackages.Clear();
            }

                // 1. THUNDERSTORE FETCH
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
                    
                    // Wait with Timeout (45s)
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

                // 2. GITHUB FETCH
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
                    
                    // Wait with Timeout (45s)
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
             
             // We can use the simple message but it overrides everything.
             // Let's just create a popup overlay on top of our browser.
             
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
             
             // Text
             AddText(errorObj.transform, "Error", 30, true, Color.red);
             AddText(errorObj.transform, message, 20, false, Color.white);
             
             // Dismiss Button
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
            var containerRect = container.GetComponent<RectTransform>();
            
            if (!_originalSize.HasValue) _originalSize = containerRect.sizeDelta;
            
            containerRect.sizeDelta = new Vector2(1000, 900);
            
            var bgImg = container.gameObject.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            bgImg.color = Color.clear;

            // Use Unified Scroll Layout
            // Top: 120 (tabs)
            // Bottom: 0
            // Side: 25
            var layout = UIHelpers.CreateScrollLayout(container.gameObject, "Browser", 120, 0, 25, 40, 10);
            
            _viewportObj = layout.Viewport.gameObject;
            _scrollbarObj = layout.ScrollbarObj;
            
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
            
            // Create Tabs (attached to Container, not Viewport)
            CreateTabs(container);

            // Hide default text
            modal.simpleMessageText.gameObject.SetActive(false);
        }

        private static void CreateTabs(Transform parent)
        {
            // Evenly Spaced Buttons: -300, 0, 300
            // Widths: 300, 300, 200
            
            CreateTabButton(parent, "Thunderstore", -310, () => {
                if (!_isThunderstoreTab) {
                    _isThunderstoreTab = true;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, _isThunderstoreTab);

            CreateTabButton(parent, "GitHub", 10, () => {
                if (_isThunderstoreTab) {
                    _isThunderstoreTab = false;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, !_isThunderstoreTab);
            
            // REFRESH BUTTON
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
                 var newScheme = UIHelpers.CloneColorScheme(_buttonTemplate.colorScheme, btnObj);
                 newScheme.buttonBgColor = new Color(0.8f, 0.6f, 0.2f); // Orange-ish
                 newScheme.buttonBgColor_Hover = new Color(0.9f, 0.7f, 0.3f);
                 tabletBtn.colorScheme = newScheme;
                 
                 tabletBtn.OnClick = new TabletButtonEvent();
                 tabletBtn.OnClick.AddListener((cursor) => {
                     // Trigger Refresh
                     RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay(true));
                 });
                 tabletBtn.SetDisabled(false);
                 tabletBtn.SetInteractable(true);
                 tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                 tabletBtn.ResetStyles();
                 
                  if (tabletBtn.background != null) tabletBtn.background.color = newScheme.buttonBgColor;
                  
                  _refreshButton = tabletBtn;
            }
            
            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);
            
            _itemButtons.Add(btnObj); 
            
            // Try clone loading spinner
            // "main Buttons" is usually at top level when searching or via TabletMainMenuHome
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
                    }
                }
            }
        }
        
        private static void CreateTabButton(Transform parent, string text, float xOffset, Action onClick, bool active)
        {
            if (_buttonTemplate == null) return;

             // Create a temporary object to hold the button
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "Tab_" + text;
             
            // Position
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(300, 80); // Slightly smaller to fit refresh
            rect.anchoredPosition = new Vector2(xOffset, -10);

            // Label
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
                 // Create custom color scheme
                 var newScheme = UIHelpers.CloneColorScheme(_buttonTemplate.colorScheme, btnObj);
                 
                 Color activeColor = new Color(0.2f, 0.6f, 1f); // Blue
                 Color inactiveColor = new Color(0.5f, 0.5f, 0.5f); // Grey
                 // Hover color depends on state: Blue if active, Lighter Grey if inactive
                 Color hoverColor = active ? new Color(0.3f, 0.7f, 1f) : new Color(0.6f, 0.6f, 0.6f);
                 
                 Color baseColor = active ? activeColor : inactiveColor;
                 
                 newScheme.buttonBgColor = baseColor;
                 newScheme.buttonBgColor_Hover = hoverColor;
                 newScheme.buttonBgColor_Disabled = inactiveColor;
                 
                 tabletBtn.colorScheme = newScheme;
                 
                 tabletBtn.OnClick = new TabletButtonEvent();
                 tabletBtn.OnClick.AddListener((cursor) => {
                     RoosterPlugin.LogInfo($"Tab Clicked: {text}");
                     onClick();
                 });
                 tabletBtn.SetDisabled(false);
                 tabletBtn.SetInteractable(true);
                 tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                 tabletBtn.ResetStyles();
                 
                  if (tabletBtn.background != null) {
                     tabletBtn.background.color = baseColor;
                  }
            }
            
            // Remove LayoutElement if present from template
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
             
             // Get existing Content from ScrollRect (which is on Parent/Container)
             // CreateScrollLayout attached ScrollRect to Container
             var scrollRect = _viewportObj.transform.parent.GetComponent<ScrollRect>();
             if (scrollRect == null || scrollRect.content == null) 
             {
                 RoosterPlugin.LogError("ModBrowser: ScrollRect or Content is null!");
                 return;
             }
             
             var contentRect = scrollRect.content;
             RoosterPlugin.LogInfo($"ModBrowser: Clearing content (children: {contentRect.childCount})");
             
             // Destroy existing items (children)
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
                 label.text = $"{pkg.name} v{pkg.latest.version_number}";
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
                 tabletBtn.OnClick.AddListener((cursor) => OpenDetails(pkg));
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
                  else
                  {
                      normalColor = new Color(0.8f, 0.8f, 0.8f); // Grey
                      hoverColor = new Color(0.9f, 0.9f, 0.9f); // Lighter Grey
                  }
                  
                  // Use custom scheme
                  var newScheme = UIHelpers.CloneColorScheme(_buttonTemplate.colorScheme, btnObj);
                  newScheme.buttonBgColor = normalColor;
                  newScheme.buttonBgColor_Hover = hoverColor;
                  
                  tabletBtn.colorScheme = newScheme;
                  tabletBtn.ResetStyles();
             }

             var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
             le.preferredHeight = 70f;
             le.flexibleWidth = 1f;
             le.minHeight = 70f;
             
             btnObj.SetActive(true);
        }

        private static void OpenDetails(ThunderstorePackage pkg)
        {
            // Simple popup
            if (_currentModal == null) return;
            
            // We can't easily stack modals with current Tablet system (it's one overlay).
            // So we'll clear the content and show details, with a different back action.
            
            // Hide Browser List
            _viewportObj.SetActive(false);
            if (_scrollbarObj != null) _scrollbarObj.SetActive(false);
            foreach(var b in _itemButtons) b.SetActive(false);
            
            var detailsObj = new GameObject("DetailsView", typeof(RectTransform));
            detailsObj.transform.SetParent(_currentModal.simpleMessageContainer, false);
            var rect = detailsObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(50, 50);
            rect.offsetMax = new Vector2(-50, -100);
            
            var img = detailsObj.AddComponent<Image>();
            img.color = Color.white;
            
            var vLayout = detailsObj.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(40, 40, 40, 40);
            vLayout.spacing = 20;
            
            // Title
            AddText(detailsObj.transform, pkg.name, 40, true, Color.black);
            AddText(detailsObj.transform, "by " + pkg.full_name?.Split('-')[0], 24, false, Color.black);
            
            // Description
            AddText(detailsObj.transform, pkg.description, 20, false, Color.black);
            
            // Install Button
            var installBtnObj = new GameObject("InstallBtn", typeof(RectTransform));
            installBtnObj.transform.SetParent(detailsObj.transform, false);
            var le = installBtnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 60;
            le.preferredWidth = 200;
            
            var btnImg = installBtnObj.AddComponent<Image>();
            btnImg.color = Color.green;
            
            var btn = installBtnObj.AddComponent<Button>();
            
            var btnTxt = new GameObject("Text", typeof(RectTransform));
            btnTxt.transform.SetParent(installBtnObj.transform, false);
            var txt = btnTxt.AddComponent<Text>();
            txt.text = "Install";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 24;
            txt.color = Color.white;
            var tRect = btnTxt.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            
            // Check if installed
            bool isInstalled = false;
            if (Rooster.UpdateChecker.MatchedPackages != null)
            {
                 foreach(var installedPkg in Rooster.UpdateChecker.MatchedPackages.Values)
                 {
                     if (installedPkg.full_name == pkg.full_name)
                     {
                         isInstalled = true;
                         break;
                     }
                 }
            }
            
            if (isInstalled)
            {
                txt.text = "Installed";
                btn.interactable = false;
                btnImg.color = Color.gray;
            }
            else
            {
                btn.onClick.AddListener(() => {
                    // Trigger Install
                    txt.text = "Installing...";
                    btn.interactable = false;
                    
                     string url = pkg.latest.download_url;
                     string zipName = $"{pkg.name}.zip";
                     string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), zipName);
                     
                     RoosterPlugin.Instance.StartCoroutine(UpdateDownloader.DownloadFile(url, tempPath, (success, err) => {
                         if (success)
                         {
                             UpdateInstaller.InstallPackage(tempPath, pkg.name, (installSuccess, installErr) => {
                                 if (installSuccess)
                                 {
                                     txt.text = "Installed!";
                                     btnImg.color = Color.gray;
                                 }
                                 else
                                 {
                                     txt.text = "Error";
                                     RoosterPlugin.LogError("Install error: " + installErr);
                                 }
                             });
                         }
                         else
                         {
                             txt.text = "Failed";
                             btn.interactable = true;
                         }
                     }));
                });
            }
            
            // Back Button Override
            // We need to hook into the main back button to close DETAILS and go back to LIST
             _currentModal.okButton.OnClick.RemoveAllListeners();
             _currentModal.okButton.OnClick.AddListener((cursor) => {
                 UnityEngine.Object.Destroy(detailsObj);
                 _viewportObj.SetActive(true);
                 if (_scrollbarObj != null) _scrollbarObj.SetActive(true);
                  foreach(var b in _itemButtons) b.SetActive(true);
                  
                  // Restore original back listener
                 _currentModal.okButton.OnClick.RemoveAllListeners();
                 _currentModal.okButton.OnClick.AddListener((c) => {
                     Close();
                     ModMenuUI.ShowModMenu();
                 });
             });
        }
        
        private static void AddText(Transform parent, string content, int fontSize, bool bold, Color color)
        {
            var obj = new GameObject("Text", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var txt = obj.AddComponent<Text>();
            txt.text = bold ? $"<b>{content}</b>" : content;
            txt.supportRichText = true;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow; 
            
            // layout
            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 10;
            layout.flexibleWidth = 1;
        }

        public static void DestroyUI(TabletModalOverlay modal)
        {
            if (_viewportObj != null) UnityEngine.Object.Destroy(_viewportObj);
            _viewportObj = null;
             if (_scrollbarObj != null) UnityEngine.Object.Destroy(_scrollbarObj);
            _scrollbarObj = null;
            foreach(var b in _itemButtons) if(b!=null) UnityEngine.Object.Destroy(b);
            _itemButtons.Clear();
            _refreshButton = null;
            
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
        }
    }
}
