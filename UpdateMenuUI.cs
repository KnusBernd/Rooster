using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Patches;
using Rooster.Models;
using Rooster.UI;

namespace Rooster
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

            var viewportObj = new GameObject("UpdateMenuViewport", typeof(RectTransform));
            viewportObj.layer = container.gameObject.layer;
            viewportObj.transform.SetParent(container, false);
            var viewportRect = viewportObj.GetComponent<RectTransform>();
            
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero; 
            viewportRect.offsetMax = new Vector2(-50, -10);

            var vpImg = viewportObj.AddComponent<Image>();
            vpImg.color = Color.white; 
            var mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var layoutElement = viewportObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 600f;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 1f;

            textObj.transform.SetParent(viewportRect, false);
            var fitter = textObj.GetComponent<ContentSizeFitter>() ?? textObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(20, 0);
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var scrollRect = container.gameObject.GetComponent<ScrollRect>() ?? container.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = textRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.enabled = true;

            UIHelpers.CreateScrollbar(container, scrollRect, "UpdateMenu");

            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        }

        public static void DestroyUI()
        {
            if (Tablet.clickEventReceiver?.modalOverlay?.simpleMessageContainer == null) return;
            var container = Tablet.clickEventReceiver.modalOverlay.simpleMessageContainer;
            var modal = Tablet.clickEventReceiver.modalOverlay;

            var vp = container.Find("UpdateMenuViewport");
            if (vp != null)
            {
                
                if (modal.simpleMessageText != null && modal.simpleMessageText.transform.parent == vp)
                {
                    modal.simpleMessageText.transform.SetParent(container, false);
                    
                  
                    var textRect = modal.simpleMessageText.GetComponent<RectTransform>();
                    if (textRect != null)
                    {
                        
                        var fitter = modal.simpleMessageText.GetComponent<ContentSizeFitter>();
                        if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
                    }
                }
                UnityEngine.Object.DestroyImmediate(vp.gameObject);
            }
            
            var sb = container.Find("UpdateMenuScrollbar");
            if (sb != null) UnityEngine.Object.DestroyImmediate(sb.gameObject);
        }
    }
}
