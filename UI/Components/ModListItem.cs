using System;
using UnityEngine;
using UnityEngine.UI;
using Rooster.Models;
using Rooster.Services;

namespace Rooster.UI.Components
{
    public static class ModListItem
    {
        public static GameObject Create(
            TabletButton template,
            RectTransform parent,
            ThunderstorePackage pkg,
            Action<ThunderstorePackage> onClick)
        {
            if (template == null) return null;

            var btnObj = UnityEngine.Object.Instantiate(template.gameObject, parent);
            btnObj.name = "Pkg_" + pkg.Name;

            SetupLabel(btnObj, pkg);
            SetupIcon(btnObj, pkg);
            SetupCornerStats(btnObj, pkg);
            SetupButton(btnObj, template, pkg, onClick);
            SetupLayout(btnObj);

            btnObj.SetActive(true);
            return btnObj;
        }

        private static void SetupLabel(GameObject btnObj, ThunderstorePackage pkg)
        {
            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label == null) return;

            // Extract author/namespace from full_name (e.g., "Author-ModName")
            string author = "Unknown";
            if (!string.IsNullOrEmpty(pkg.FullName))
            {
                int hyphenIdx = pkg.FullName.IndexOf('-');
                if (hyphenIdx > 0) author = pkg.FullName.Substring(0, hyphenIdx);
            }

            if (!string.IsNullOrEmpty(pkg.SecondaryAuthor))
            {
                author = $"{pkg.SecondaryAuthor} (Fork: {author})";
            }

            string categoryStr = (pkg.Categories != null && pkg.Categories.Count > 0)
                                    ? string.Join(", ", pkg.Categories)
                                    : "Mod";

            label.text = $"<size=22>{pkg.Name.Replace('_', ' ')} v{pkg.Latest.VersionNumber}</size>\n<i><size=18>by {author} | {categoryStr}</size></i>";
            label.labelType = TabletTextLabel.LabelType.SmallText;

            var uiText = label.GetComponent<Text>();
            if (uiText != null)
            {
                uiText.supportRichText = true;
                uiText.verticalOverflow = VerticalWrapMode.Overflow;
                uiText.alignment = TextAnchor.MiddleLeft;
            }

            var labelRect = label.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(95, 0); 
                labelRect.offsetMax = new Vector2(-10, 0);
            }
        }

        private static void SetupIcon(GameObject btnObj, ThunderstorePackage pkg)
        {
            var iconObj = new GameObject("ModIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);
            iconObj.layer = btnObj.layer;

            var rect = iconObj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(75, 75);
            rect.anchoredPosition = new Vector2(10, 0);

            var img = iconObj.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.1f);

            IconService.Instance.GetIcon(pkg, (sprite) =>
            {
                if (img == null || sprite == null) return;
                img.sprite = sprite;
                img.color = Color.white;
            });
        }

        private static void SetupCornerStats(GameObject btnObj, ThunderstorePackage pkg)
        {
            // Top Right: ★ {likes} / ↓ {downloads}
            var topRight = new GameObject("StatsTop", typeof(RectTransform), typeof(Text));
            topRight.transform.SetParent(btnObj.transform, false);
            var trRt = topRight.GetComponent<RectTransform>();
            trRt.anchorMin = trRt.anchorMax = trRt.pivot = new Vector2(1, 1);
            trRt.anchoredPosition = new Vector2(-15, -10);
            trRt.sizeDelta = new Vector2(200, 30);

            var trText = topRight.GetComponent<Text>();
            trText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            trText.alignment = TextAnchor.UpperRight;
            trText.fontSize = 18;
            trText.color = Color.white;
            trText.supportRichText = true;
            trText.text = $"★ {pkg.Likes}  |  ↓ {FormatNumber(pkg.Downloads)}";

            // Bottom Right: {size} MB / updated: {date}
            var bottomRight = new GameObject("StatsBottom", typeof(RectTransform), typeof(Text));
            bottomRight.transform.SetParent(btnObj.transform, false);
            var brRt = bottomRight.GetComponent<RectTransform>();
            brRt.anchorMin = brRt.anchorMax = brRt.pivot = new Vector2(1, 0);
            brRt.anchoredPosition = new Vector2(-15, 10);
            brRt.sizeDelta = new Vector2(300, 30);

            var brText = bottomRight.GetComponent<Text>();
            brText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            brText.alignment = TextAnchor.LowerRight;
            brText.fontSize = 16;
            brText.color = new Color(1f, 1f, 1f, 0.9f);
            brText.supportRichText = true;

            string sizeStr = pkg.Latest != null ? FormatSize(pkg.Latest.FileSize) : "?? MB";
            string dateStr = pkg.DateUpdated;
            if (DateTime.TryParse(dateStr, out var dt)) dateStr = dt.ToString("yyyy-MM-dd");

            brText.text = $"{sizeStr}  /  <i>updated: {dateStr}</i>";
        }

        private static string FormatNumber(int num)
        {
            if (num >= 1000000) return (num / 1000000f).ToString("F1") + "M";
            if (num >= 1000) return (num / 1000f).ToString("F1") + "k";
            return num.ToString();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 100) // Less than 100 KB
            {
                float kb = bytes / 1024f;
                return kb.ToString("F1") + " KB";
            }
            float mb = bytes / (1024f * 1024f);
            return mb.ToString("F1") + " MB";
        }

        private static void SetupButton(GameObject btnObj, TabletButton template, ThunderstorePackage pkg, Action<ThunderstorePackage> onClick)
        {
            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn == null) return;

            if (tabletBtn.colorScheme == null) tabletBtn.colorScheme = template.colorScheme;

            tabletBtn.OnClick = new TabletButtonEvent();
            tabletBtn.OnClick.AddListener((cursor) =>
            {
                onClick?.Invoke(pkg);
            });

            tabletBtn.SetDisabled(false);
            tabletBtn.SetInteractable(true);
            tabletBtn.ResetStyles();

            // Apply Theme based on status
            UIHelpers.ButtonTheme theme = GetThemeForPackage(pkg);
            UIHelpers.ApplyTheme(tabletBtn, theme);
        }

        private static UIHelpers.ButtonTheme GetThemeForPackage(ThunderstorePackage pkg)
        {
            if (UpdateChecker.IsPendingUninstall(pkg.FullName))
            {
                return UIHelpers.Themes.Danger;
            }
            if (UpdateChecker.IsPackageInstalled(pkg.FullName))
            {
                return UIHelpers.Themes.Success;
            }
            if (UpdateChecker.PendingInstalls.Contains(pkg.FullName))
            {
                return UIHelpers.Themes.Warning;
            }
            return UIHelpers.Themes.Neutral;
        }

        private static void SetupLayout(GameObject btnObj)
        {
            var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 70f;
            le.flexibleWidth = 1f;
            le.minHeight = 70f;
        }
    }
}
