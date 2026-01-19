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
                string currentVer = updateInfo.PluginInfo.Metadata.Version.ToString();
                lines.Add($"<b>{updateInfo.ModName}</b>");
                lines.Add($"   <color=grey>v{currentVer}</color> -> <color=green>v{updateInfo.Version}</color>");
                lines.Add("");
            }

            string fullText = string.Join("\n", lines.ToArray());
            
            int count = UpdateChecker.PendingUpdates.Count;
            string title = $"{count} Update{(count == 1 ? "" : "s")} Available";
            modal.ShowSimpleMessage(title, fullText, null);

            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.UpdateMenu;

            modal.okButtonContainer.gameObject.SetActive(false);
            modal.onOffContainer.gameObject.SetActive(true);
            
            var onLabel = modal.onButton.GetComponentInChildren<TabletTextLabel>();
            if (onLabel != null) onLabel.text = "Update All"; 
            
            var offLabel = modal.offButton.GetComponentInChildren<TabletTextLabel>();
            if (offLabel != null) offLabel.text = "Dismiss";
            
            try
            {
                ApplyStyling(modal);
            }
            catch (Exception ex)
            {
                RoosterPlugin.LogError($"Failed to apply update menu styling: {ex}");
            }
        }

        private static void ApplyStyling(TabletModalOverlay modal)
        {
            var textObj = modal.simpleMessageText.gameObject;
            var textRect = textObj.GetComponent<RectTransform>();
            var container = modal.simpleMessageContainer;
            if (container == null) return;
            var containerRect = container.GetComponent<RectTransform>();

            containerRect.sizeDelta = new Vector2(900, 700);
            
            var bgImg = container.gameObject.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            bgImg.color = Color.clear; 
            bgImg.raycastTarget = true;

            DestroyUI();

            var tabletLabel = textObj.GetComponent<TabletTextLabel>();
            if (tabletLabel != null)
            {
                foreach (var textComp in textObj.GetComponentsInChildren<Text>(true))
                {
                    if (baseFontSize < 0) baseFontSize = textComp.fontSize;
                    textComp.fontSize = (int)(baseFontSize * 0.8f);
                    textComp.supportRichText = true;
                }
            }

            // Unified Scroll Layout
            // Top: 10
            // Bottom: 0
            // Side: 0 -> Results in Right Margin 50 (40 scrollbar + 10 padding)
            var layout = UIHelpers.CreateScrollLayout(container.gameObject, "UpdateMenu", 10, 0, 0, 40, 10);
            
            var contentObj = layout.Content.gameObject;
            var vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            
            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            textObj.transform.SetParent(layout.Content, false);
            // Ensure textObj expands
            var le = textObj.GetComponent<LayoutElement>() ?? textObj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            
            // Text object itself might need Fitter removal if handled by Parent Layout?
            // Original code added ContentSizeFitter to textObj.
            // If Text is child of VerticalLayout with ControlHeight, we don't need Fitter on Text?
            // Actually Text needs to report its preferred height. Text does that natively.
            // But we might need ContentSizeFitter on Text if we want it to limit itself?
            // VerticalLayoutGroup on Content with ChildControlHeight=true will force Text height.
            // If Text has large content, it effectively asks for height.
            // So we should strictly NOT have Fitter on textObj if VLG controls it, OR have it if VLG doesn't.
            // Let's rely on VLG.
            
            var textFitter = textObj.GetComponent<ContentSizeFitter>();
            if (textFitter != null) UnityEngine.Object.DestroyImmediate(textFitter);

            textRect.pivot = new Vector2(0f, 1f);
            
            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout.Content);
        }

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
        }
    }
}
