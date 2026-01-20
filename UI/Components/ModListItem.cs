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
                if (hyphenIdx > 0)
                {
                    author = pkg.FullName.Substring(0, hyphenIdx);
                }
            }

            string categoryStr = (pkg.Categories != null && pkg.Categories.Count > 0)
                                    ? string.Join(", ", pkg.Categories)
                                    : "Mod";

            label.text = $"{pkg.Name.Replace('_', ' ')} v{pkg.Latest.VersionNumber}\n<i><size=18>by {author} | {categoryStr}</size></i>";
            label.labelType = TabletTextLabel.LabelType.SmallText;

            var uiText = label.GetComponent<Text>();
            if (uiText != null)
            {
                uiText.supportRichText = true;
                uiText.verticalOverflow = VerticalWrapMode.Overflow;
            }
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
