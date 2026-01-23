using HarmonyLib;
using Rooster.UI;
using UnityEngine;

namespace Rooster.Patches
{
    /// <summary>
    /// Patches the main menu initialization to inject a "Mods" button.
    /// This button opens the custom Mod Menu UI.
    /// </summary>
    [HarmonyPatch(typeof(TabletMainMenuHome), "Initialize")]
    public static class MainMenuButtonPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            GameObject existingBtn = FindButton("Mods");
            if (existingBtn != null) return;

            try
            {
                GameObject optionsButton = FindButton("Options");
                if (optionsButton == null)
                {
                    RoosterPlugin.LogError("Could not find a suitable button to clone for Mods");
                    return;
                }

                var modsButton = Object.Instantiate<GameObject>(optionsButton);
                modsButton.name = "Mods";
                modsButton.transform.SetParent(optionsButton.transform.parent);
                modsButton.transform.localScale = Vector3.one;

                var textLabel = modsButton.transform.Find("Text Label").GetComponent<TabletTextLabel>();
                if (textLabel != null)
                {
                    textLabel.text = "Mods";
                }

                var iconImage = modsButton.transform.Find("Image");
                if (iconImage != null)
                {
                    iconImage.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    iconImage.transform.localPosition += new Vector3(0f, 30f, 0f);

                    var iconImage1 = Object.Instantiate<GameObject>(iconImage.gameObject, iconImage.transform.parent);
                    iconImage1.name = "Image1";
                    iconImage1.transform.localPosition = iconImage.transform.localPosition + new Vector3(-52f, -52f, 0f);
                    iconImage1.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    iconImage1.transform.localRotation = Quaternion.Euler(0f, 0f, 22.5f);

                    var iconImage2 = Object.Instantiate<GameObject>(iconImage.gameObject, iconImage.transform.parent);
                    iconImage2.name = "Image2";
                    iconImage2.transform.localPosition = iconImage.transform.localPosition + new Vector3(50f, -50f, 0f);
                    iconImage2.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    iconImage2.transform.localRotation = Quaternion.Euler(0f, 0f, -22.5f);
                }

                GameObject quitButton = FindButton("Quit");
                if (quitButton != null)
                {
                    int quitIndex = quitButton.transform.GetSiblingIndex();
                    modsButton.transform.SetSiblingIndex(quitIndex);
                }

                var tabletButton = modsButton.GetComponent<TabletButton>();
                if (tabletButton != null)
                {
                    tabletButton.OnClick = new TabletButtonEvent();
                    tabletButton.OnClick.AddListener((controller) =>
                    {
                        ModMenuUI.ShowModMenu();
                    });
                }
            }
            catch (System.Exception ex)
            {
                RoosterPlugin.LogError($"Failed to create Mods button: {ex}");
            }
        }

        private static GameObject FindButton(string identifier)
        {
            foreach (var btn in UnityEngine.Object.FindObjectsOfType<TabletButton>())
            {
                if (btn.name.Equals(identifier, System.StringComparison.OrdinalIgnoreCase))
                {
                    return btn.gameObject;
                }

                var txt = btn.gameObject.GetComponentInChildren<TabletTextLabel>();
                if (txt != null && txt.text != null && txt.text.Equals(identifier, System.StringComparison.OrdinalIgnoreCase))
                {
                    return btn.gameObject;
                }
            }
            return null;
        }
    }
}
