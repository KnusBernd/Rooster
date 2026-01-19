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
        private static Coroutine _cleanupCoroutine;
        
        private static List<ThunderstorePackage> _thunderstoreMods = new List<ThunderstorePackage>();
        private static List<ThunderstorePackage> _curatedMods = new List<ThunderstorePackage>();
        private static bool _isThunderstoreTab = true;
        
        // Caching the modal for refreshing
        private static TabletModalOverlay _currentModal;

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
            
            modal.ShowSimpleMessage("Loading Mods...", "Please wait...", () => { });
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
            RoosterPlugin.Instance.StartCoroutine(FetchAndDisplay());
        }

        private static void Close()
        {
            DestroyUI(_currentModal);
        }

        private static IEnumerator FetchAndDisplay()
        {
            // Initial UI State
            if (_currentModal != null) _currentModal.ShowSimpleMessage("Loading Mod List...", "Fetching from Thunderstore...", () => {});
            
            // Fetch Thunderstore
            RoosterPlugin.Instance.StartCoroutine(ThunderstoreApi.FetchAllPackages((packages) => {
                _thunderstoreMods = packages;
                if (_isThunderstoreTab) 
                {
                    RefreshList();
                    // Hide loading text if it's still there
                     if (_currentModal != null && _currentModal.simpleMessageText != null) 
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }));

            // Fetch GitHub
            RoosterPlugin.Instance.StartCoroutine(GitHubApi.FetchCuratedList((packages) => {
                _curatedMods = packages;
                if (!_isThunderstoreTab) 
                {
                    RefreshList();
                     if (_currentModal != null && _currentModal.simpleMessageText != null) 
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }));
            
            yield break; 
        }

        private static void ApplyStyling(TabletModalOverlay modal)
        {
            DestroyUI(modal);
            
            var container = modal.simpleMessageContainer;
            var containerRect = container.GetComponent<RectTransform>();
            
            if (!_originalSize.HasValue) _originalSize = containerRect.sizeDelta;
            
            containerRect.sizeDelta = new Vector2(1000, 900);
            
            var bgImg = container.gameObject.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            bgImg.color = Color.clear;

            // Setup Viewport
             var viewportObj = new GameObject("BrowserViewport", typeof(RectTransform));
            _viewportObj = viewportObj;
            viewportObj.layer = container.gameObject.layer;
            viewportObj.transform.SetParent(container, false);
            var viewportRect = viewportObj.GetComponent<RectTransform>();
            
            // Reserve space for tabs at top
            // Reserve space for tabs at top
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(25, 0); // Side padding
            viewportRect.offsetMax = new Vector2(-25, -80); 

            var vpImg = viewportObj.AddComponent<Image>();
            vpImg.sprite = UIHelpers.GetWhiteSprite();
            vpImg.color = Color.white; 
            var mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            // Create Tabs
            CreateTabs(container);

            // Hide default text
            modal.simpleMessageText.gameObject.SetActive(false);
        }

        private static void CreateTabs(Transform parent)
        {
            // Simple buttons for tabs
            // This is a bit hacky, normally separate method
            // Position them above the list
            
            float tabWidth = 200;
            float tabHeight = 60;
            
            CreateTabButton(parent, "Thunderstore", -250, () => {
                if (!_isThunderstoreTab) {
                    _isThunderstoreTab = true;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, _isThunderstoreTab);

            CreateTabButton(parent, "GitHub", 250, () => {
                if (_isThunderstoreTab) {
                    _isThunderstoreTab = false;
                    RefreshList();
                    if (_currentModal != null && _currentModal.simpleMessageText != null)
                        _currentModal.simpleMessageText.gameObject.SetActive(false);
                }
            }, !_isThunderstoreTab);
        }
        
        private static void CreateTabButton(Transform parent, string text, float xOffset, Action onClick, bool active)
        {
             // Create a temporary object to hold the button
            var btnObj = new GameObject("Tab_" + text, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(400, 60);
            rect.anchoredPosition = new Vector2(xOffset, -10);

            // Use TabletButton template from somewhere? 
            // Since we don't have easy access to a template here without grabbing it from the modal...
            // We'll reuse the OK button template logic from ModMenuUI or just make a simple colored image with text.
            
            var img = btnObj.AddComponent<Image>();
            img.color = active ? new Color(0.2f, 0.6f, 1f) : new Color(0.5f, 0.5f, 0.5f);
             
            var layout = btnObj.AddComponent<LayoutElement>();
            
            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            
            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 24;
            rect = txtObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            
            _itemButtons.Add(btnObj); 
        }

        private static void RefreshList()
        {
             if (_viewportObj == null) return;
             
             // Clear existing content
             // We need to rebuild the ScrollRect content
             var scrollRect = _viewportObj.transform.parent.GetComponent<ScrollRect>() ?? _viewportObj.transform.parent.gameObject.AddComponent<ScrollRect>();
            
             // Destroy old content
             if (scrollRect.content != null) UnityEngine.Object.DestroyImmediate(scrollRect.content.gameObject);
             
             var contentObj = new GameObject("BrowserContent", typeof(RectTransform));
             contentObj.transform.SetParent(_viewportObj.transform, false);
             var contentRect = contentObj.GetComponent<RectTransform>();
             
             var layout = contentObj.AddComponent<VerticalLayoutGroup>();
             layout.childControlWidth = true;
             layout.childControlHeight = false;
             layout.childForceExpandWidth = true;
             layout.spacing = 10;
             layout.padding = new RectOffset(10, 10, 10, 10);
             
             var fitter = contentObj.AddComponent<ContentSizeFitter>();
             fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
             
             contentRect.anchorMin = new Vector2(0, 1);
             contentRect.anchorMax = new Vector2(1, 1);
             contentRect.pivot = new Vector2(0.5f, 1);
             
             scrollRect.content = contentRect;
             scrollRect.viewport = _viewportObj.GetComponent<RectTransform>();
             scrollRect.vertical = true;
             scrollRect.horizontal = false;
             
             var list = _isThunderstoreTab ? _thunderstoreMods : _curatedMods;
             
             foreach(var pkg in list)
             {
                 CreatePackageItem(contentRect, pkg);
             }
             
             // Scrollbar
             if (_scrollbarObj != null) UnityEngine.Object.DestroyImmediate(_scrollbarObj);
             _scrollbarObj = UIHelpers.CreateScrollbar((RectTransform)_viewportObj.transform.parent, scrollRect, "BrowserScroll");
             
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
                 // Assuming UpdateChecker.MatchedPackages contains full_name keys
                 if (UpdateChecker.MatchedPackages != null && UpdateChecker.MatchedPackages.ContainsKey(pkg.full_name))
                 {
                     isInstalled = true;
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
            AddText(detailsObj.transform, pkg.name, 40, true);
            AddText(detailsObj.transform, "by " + pkg.full_name?.Split('-')[0], 24, false);
            
            // Description
            AddText(detailsObj.transform, pkg.description, 20, false);
            
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
            bool isInstalled = Rooster.UpdateChecker.MatchedPackages.ContainsKey(pkg.full_name); // This assumes MatchedPackages is populated
            // Or better, check Chainloader
            // For now, if installed, disable? Or allow update?
            // User requirement: "installation button (which is not active when this mod is already downloaded)"
            
            // We need to check if ANY plugin matches this package.
            bool alreadyDownloaded = false; 
            if (Rooster.UpdateChecker.MatchedPackages != null) // This is map of GUID -> Package
            {
               // We need map Package -> Local 
               // Actually UpdateChecker.MatchedPackages keys are Plugin GUIDs. Values are Strings (Package FullName). // WAIT: Dictionary<string, ThunderstorePackage> MatchedPackages in UpdateChecker.cs?
               // Let's assume it is Dictionary<string, ThunderstorePackage> based on previous context.
               // IF MatchedPackages is Dictionary<string, ThunderstorePackage>, then Values are ThunderstorePackage objects.
               foreach(var kvp in Rooster.UpdateChecker.MatchedPackages)
               {
                   if (kvp.Value.full_name == pkg.full_name)
                   {
                       alreadyDownloaded = true;
                       break;
                   }
               }
            }

            if (alreadyDownloaded)
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
                    
                    // We need a path to download to. UpdateDownloader needs URL.
                     // ThunderstoreDownloadUrl
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
        
        private static void AddText(Transform parent, string content, int fontSize, bool bold)
        {
            var obj = new GameObject("Text", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var txt = obj.AddComponent<Text>();
            txt.text = bold ? $"<b>{content}</b>" : content;
            txt.supportRichText = true;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.color = Color.black;
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
