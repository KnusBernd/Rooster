using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Patches;
using Rooster.Models;
using Rooster.UI;

namespace Rooster.UI
{
    /// <summary>
    /// Manages the UI for the Update Menu, which notifies users of available mod updates.
    /// Hijacks the tablet modal to show a list of pending updates.
    /// </summary>
    public class UpdateMenuUI
    {
        private static float baseFontSize = -1f;

        public static void ShowUpdateMenu()
        {
            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null) return;

            var modal = Tablet.clickEventReceiver.modalOverlay;
            
            var lines = new List<string>();
            foreach(var updateInfo in UpdateChecker.PendingUpdates)
            {
                string currentVer = updateInfo.PluginInfo?.Metadata?.Version?.ToString() ?? "0.0.0";
                lines.Add($"<b>{updateInfo.ModName}</b>");
                lines.Add($"   <color=grey>v{currentVer}</color> -> <color=green>v{updateInfo.Version}</color>");
                lines.Add("");
            }

            string fullText = string.Join("\n", lines.ToArray());
            
            var count = UpdateChecker.PendingUpdates.Count;
            
            UIHelpers.SetupModal(modal, new Vector2(900, 700), $"{count} Update{(count == 1 ? "" : "s")} Pending", null);
            
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.UpdateMenu;

            modal.okButtonContainer.gameObject.SetActive(false);
            modal.onOffContainer.gameObject.SetActive(false); // Hide default buttons
            
            try
            {
                ApplyStyling(modal, fullText);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to apply update menu styling: {ex}");
            }
        }

        private static void ApplyStyling(TabletModalOverlay modal, string content)
        {
            DestroyUI();
            
            var container = modal.simpleMessageContainer;
            if (container == null) return;

            UIHelpers.CleanContainer(container.gameObject);

            // Unified Scroll Layout
            // Top: 20
            // Bottom: 100 (Leave room for buttons)
            // Side: 20 -> Results in Right Margin 60 (40 scrollbar + 20 padding)
            var layout = UIHelpers.CreateScrollLayout(container.gameObject, "UpdateMenu", 20, 100, 20, 40, 10);
            
            var contentObj = layout.Content.gameObject;
            var vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            
            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Add text to content
            int fontSize = (int)(baseFontSize > 0 ? baseFontSize * 0.8f : 36);
            UIHelpers.AddText(layout.Content, content, fontSize, false, Color.white);
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout.Content);
            
            CreateActionButtons(container.GetComponent<RectTransform>());
        }

        private static void CreateActionButtons(RectTransform parent)
        {
            var modal = Tablet.clickEventReceiver.modalOverlay;
            var template = modal.okButton;

            // Update All Button
            var updateBtn = UIHelpers.CreateButton(parent, template, "Update All", 300, 60);
            var updateRect = updateBtn.GetComponent<RectTransform>();
            updateRect.anchorMin = new Vector2(0.3f, 0); // Left side
            updateRect.anchorMax = new Vector2(0.3f, 0);
            updateRect.pivot = new Vector2(0.5f, 0);
            updateRect.pivot = new Vector2(0.5f, 0);
            updateRect.anchoredPosition = new Vector2(0, 20);
            updateRect.sizeDelta = new Vector2(300, 60);
            
            // Remove LayoutElement to prevent layout group interference if any (though we are manual here)
            var le1 = updateBtn.GetComponent<LayoutElement>();
            if (le1 != null) UnityEngine.Object.Destroy(le1);
            
            UIHelpers.ApplyButtonStyle(updateBtn, 
                new Color(0.2f, 0.7f, 0.3f), // Green
                new Color(0.3f, 0.8f, 0.4f), 
                new Color(0.2f, 0.2f, 0.2f)
            );
            
            var updateLabel = updateBtn.GetComponentInChildren<TabletTextLabel>();
            if(updateLabel)
            {
                var txt = updateLabel.GetComponent<Text>() ?? updateLabel.GetComponentInChildren<Text>();
                if (txt != null) 
                {
                    txt.fontSize = 24;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.alignment = TextAnchor.MiddleCenter;
                }
                
                var lblRect = updateLabel.GetComponent<RectTransform>();
                if (lblRect != null)
                {
                    lblRect.anchorMin = Vector2.zero;
                    lblRect.anchorMax = Vector2.one;
                    lblRect.sizeDelta = Vector2.zero;
                    lblRect.anchoredPosition = Vector2.zero;
                }

                // Defensive: Remove layout components from label
                var le = updateLabel.GetComponent<LayoutElement>();
                if (le) UnityEngine.Object.Destroy(le);
                var csf = updateLabel.GetComponent<ContentSizeFitter>();
                if (csf) UnityEngine.Object.Destroy(csf);
            }

            updateBtn.OnClick = new TabletButtonEvent();
            updateBtn.OnClick.AddListener((cursor) => {
                updateBtn.SetInteractable(false);
                updateLabel.text = "Updating...";
                UpdateChecker.UpdateAll(
                    (status) => { /* Update status text if we had a status bar */ },
                    () => {
                        // Transform button to "Restart Game"
                        updateLabel.text = "Restart Game";
                        updateBtn.SetInteractable(true);
                        
                        // Change style to "Success/Action" style (Green -> Lighter Green)
                        // This indicates completion and encourages the user to proceed.
                        UIHelpers.ApplyButtonStyle(updateBtn, 
                            new Color(0.2f, 0.8f, 0.2f), // Green (Normal)
                            new Color(0.3f, 0.9f, 0.3f), // Lighter Green (Hover)
                            new Color(0.5f, 0.5f, 0.5f) // Grey (Disabled)
                        );
                        
                        // Rebind click to Quit
                        updateBtn.OnClick = new TabletButtonEvent();
                        updateBtn.OnClick.AddListener((c) => {
                            Application.Quit();
                        });

                        // Show toast
                        if (UserMessageManager.Instance != null)
                             UserMessageManager.Instance.UserMessage("Updates Complete! Please Restart.", 5.0f, UserMessageManager.UserMsgPriority.hi, false);
                    }
                );
            });
            updateBtn.SetDisabled(false);
            updateBtn.SetInteractable(true);


            // Dismiss Button
            var dismissBtn = UIHelpers.CreateButton(parent, template, "Dismiss", 300, 60);
            var dismissRect = dismissBtn.GetComponent<RectTransform>();
            dismissRect.anchorMin = new Vector2(0.7f, 0); // Right side
            dismissRect.anchorMax = new Vector2(0.7f, 0);
            dismissRect.pivot = new Vector2(0.5f, 0);
            dismissRect.pivot = new Vector2(0.5f, 0);
            dismissRect.anchoredPosition = new Vector2(0, 20);
            dismissRect.sizeDelta = new Vector2(300, 60);
            
            var le2 = dismissBtn.GetComponent<LayoutElement>();
            if (le2 != null) UnityEngine.Object.Destroy(le2);

            UIHelpers.ApplyButtonStyle(dismissBtn, 
                new Color(0.6f, 0.2f, 0.2f), // Redish
                new Color(0.7f, 0.3f, 0.3f), 
                new Color(0.2f, 0.2f, 0.2f)
            );
            
            var dismissLabel = dismissBtn.GetComponentInChildren<TabletTextLabel>();
            if(dismissLabel) 
            {
                var txt = dismissLabel.GetComponent<Text>() ?? dismissLabel.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.fontSize = 24;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.alignment = TextAnchor.MiddleCenter;
                }
                
                var lblRect = dismissLabel.GetComponent<RectTransform>();
                if (lblRect != null)
                {
                    lblRect.anchorMin = Vector2.zero;
                    lblRect.anchorMax = Vector2.one;
                    lblRect.sizeDelta = Vector2.zero;
                    lblRect.anchoredPosition = Vector2.zero;
                }

                var le = dismissLabel.GetComponent<LayoutElement>();
                if (le) UnityEngine.Object.Destroy(le);
                var csf = dismissLabel.GetComponent<ContentSizeFitter>();
                if (csf) UnityEngine.Object.Destroy(csf);
            }

            dismissBtn.OnClick = new TabletButtonEvent();
            dismissBtn.OnClick.AddListener((cursor) => {
                // Just close
                // Revert state?
                 modal.Close();
                 DestroyUI();
            });
            dismissBtn.SetDisabled(false);
            dismissBtn.SetInteractable(true);
            
            // Register buttons for cleanup
            _modButtons.Add(updateBtn.gameObject);
            _modButtons.Add(dismissBtn.gameObject);
        }
        
        // Track created buttons for cleanup
        private static List<GameObject> _modButtons = new List<GameObject>();

        public static void DestroyUI()
        {
            if (Tablet.clickEventReceiver?.modalOverlay?.simpleMessageContainer == null) return;
            var container = Tablet.clickEventReceiver.modalOverlay.simpleMessageContainer;
            var modal = Tablet.clickEventReceiver.modalOverlay;

            // Find Content where we hid the text
             var content = container.Find("UpdateMenuViewport/UpdateMenuContent");
             // Fallback to Viewport if refactor fails or partial
             if (content == null) content = container.Find("UpdateMenuViewport");
             
             if (content != null)
             {
                 if (modal.simpleMessageText != null && modal.simpleMessageText.transform.IsChildOf(content))
                 {
                     modal.simpleMessageText.transform.SetParent(container, false);
                     modal.simpleMessageText.gameObject.SetActive(false);
                     
                     var textRect = modal.simpleMessageText.GetComponent<RectTransform>();
                     if (textRect != null)
                     {
                         var fitter = modal.simpleMessageText.GetComponent<ContentSizeFitter>();
                         if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
                         
                         var le = modal.simpleMessageText.GetComponent<LayoutElement>();
                         if (le != null) UnityEngine.Object.DestroyImmediate(le);
                     }
                 }
                 // Destroy Viewport (parent of content)
                 var vp = container.Find("UpdateMenuViewport");
                 if (vp != null) UnityEngine.Object.DestroyImmediate(vp.gameObject);
             }
            
            var sb = container.Find("UpdateMenuScrollbar");
            if (sb != null) UnityEngine.Object.DestroyImmediate(sb.gameObject);
            
            foreach(var btn in _modButtons)
            {
                if (btn != null) UnityEngine.Object.Destroy(btn);
            }
            _modButtons.Clear();
        }
    }
}
