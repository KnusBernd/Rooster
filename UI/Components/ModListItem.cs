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
