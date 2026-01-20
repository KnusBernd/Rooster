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

        private static Dictionary<ModUpdateInfo, Text> _statusLabels = new Dictionary<ModUpdateInfo, Text>();


        public static void ShowUpdateMenu()
        {
            if (Tablet.clickEventReceiver == null || Tablet.clickEventReceiver.modalOverlay == null) return;

            var modal = Tablet.clickEventReceiver.modalOverlay;
            var count = UpdateChecker.PendingUpdates.Count;

            UIHelpers.SetupModal(modal, new Vector2(1200, 800), $"{count} Update{(count == 1 ? "" : "s")} Pending", null);

            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.UpdateMenu;

            modal.okButtonContainer.gameObject.SetActive(false);
            modal.onOffContainer.gameObject.SetActive(false);

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
            DestroyUI();

            var container = modal.simpleMessageContainer;
            if (container == null) return;

            var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
            UIHelpers.CleanContainer(container.gameObject, textObj);

            var layout = UIHelpers.CreateScrollLayout(container.gameObject, "UpdateMenu", 20, 100, 20, 60, 10);

            var contentObj = layout.Content.gameObject;
            var vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.spacing = 5;

            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var update in UpdateChecker.PendingUpdates)
            {
                CreateUpdateRow(layout.Content, update);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(layout.Content);

            // Explicitly reset scroll to top
            if (layout.ScrollRect != null) layout.ScrollRect.verticalNormalizedPosition = 1f;

            CreateActionButtons(container.GetComponent<RectTransform>());
        }

        private static void CreateUpdateRow(RectTransform parent, ModUpdateInfo info)
        {
            var rowObj = new GameObject($"Row_{info.ModName}");
            rowObj.transform.SetParent(parent, false);

            var le = rowObj.AddComponent<LayoutElement>();
            le.minHeight = 60;
            le.preferredHeight = 60;
            le.flexibleWidth = 1;

            var hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childControlWidth = true;
            hLayout.childForceExpandWidth = false;
            hLayout.spacing = 15;
            hLayout.padding = new RectOffset(20, 20, 0, 0);

            // Mod Name (Left) 
            var nameObj = UIHelpers.CreateText(rowObj.transform, info.ModName, 32, TextAnchor.MiddleLeft, Color.white, HorizontalWrapMode.Overflow);
            var nameLe = nameObj.AddComponent<LayoutElement>();
            nameLe.preferredWidth = 600;
            nameLe.minWidth = 400; // Ensure it doesn't shrink to zero
            nameLe.flexibleWidth = 1; // Allow flexibility if space permits

            // Version (Center-Left)
            string verText = $"<color=grey>v{info.PluginInfo?.Metadata?.Version?.ToString() ?? "?"}</color> -> <color=green>v{info.Version}</color>";
            var verObj = UIHelpers.CreateText(rowObj.transform, verText, 28, TextAnchor.MiddleLeft, Color.white, HorizontalWrapMode.Overflow);
            var verLe = verObj.AddComponent<LayoutElement>();
            verLe.preferredWidth = 300;
            verLe.minWidth = 200;

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(rowObj.transform, false);
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 10; // Push status to right

            // Status (Right)
            var statusObj = UIHelpers.CreateText(rowObj.transform, "Waiting", 28, TextAnchor.MiddleRight, Color.grey, HorizontalWrapMode.Overflow);
            var statusLe = statusObj.AddComponent<LayoutElement>();
            statusLe.preferredWidth = 250;
            statusLe.minWidth = 150;

            var statusText = statusObj.GetComponent<Text>();
            _statusLabels[info] = statusText;
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

            UIHelpers.ApplyTheme(updateBtn, UIHelpers.Themes.Success);

            var updateLabel = updateBtn.GetComponentInChildren<TabletTextLabel>();
            if (updateLabel)
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
            updateBtn.OnClick.AddListener((cursor) =>
            {
                updateBtn.SetInteractable(false);
                updateLabel.text = "Processing...";

                UpdateChecker.UpdateAll(
                    (info, status) =>
                    {
                        if (info != null && _statusLabels.TryGetValue(info, out var label))
                        {
                            label.text = status;
                            if (status == "Ready") label.color = UIHelpers.Themes.Success.Normal;
                            else if (status.Contains("Failed") || status.Contains("Skipped")) label.color = UIHelpers.Themes.Danger.Normal;
                            else if (status == "Downloading..." || status == "Installing...") label.color = UIHelpers.Themes.Warning.Normal;
                            else label.color = Color.white;
                        }
                    },
                    () =>
                    {
                        // Transform button to "Restart Game"
                        updateLabel.text = "Restart Game";
                        updateBtn.SetInteractable(true);

                        UIHelpers.ApplyTheme(updateBtn, UIHelpers.Themes.Success);

                        updateBtn.OnClick = new TabletButtonEvent();
                        updateBtn.OnClick.AddListener((c) =>
                        {
                            Application.Quit();
                        });
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

            UIHelpers.ApplyTheme(dismissBtn, UIHelpers.Themes.Danger);

            var dismissLabel = dismissBtn.GetComponentInChildren<TabletTextLabel>();
            if (dismissLabel)
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
            dismissBtn.OnClick.AddListener((cursor) =>
            {
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

            foreach (var btn in _modButtons)
            {
                if (btn != null) UnityEngine.Object.Destroy(btn);
            }
            _modButtons.Clear();
            _statusLabels.Clear();

        }
    }
}
