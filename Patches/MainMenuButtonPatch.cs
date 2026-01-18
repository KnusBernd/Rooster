using HarmonyLib;
using UnityEngine;

namespace Rooster.Patches
{
    /// <summary>
    /// Patches the main menu initialization to inject a "Mods" button.
    /// This button opens the custom Mod Menu UI.
    /// </summary>
    public static class MainMenuButtonPatch
    {
        /// <summary>
        /// Applies the manual postfix patch to TabletMainMenuHome.Initialize.
        /// </summary>
        /// <param name="harmony">The Harmony instance.</param>
        public static void ApplyPatch(Harmony harmony)
        {
            var original = typeof(TabletMainMenuHome).GetMethod(nameof(TabletMainMenuHome.Initialize));
            if (original != null)
            {
                var postfix = typeof(MainMenuButtonPatch).GetMethod(nameof(Postfix));
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                RoosterPlugin.LogInfo("MainMenuButtonPatch applied to TabletMainMenuHome.Initialize");
            }
            else
            {
                RoosterPlugin.LogError("Failed to find TabletMainMenuHome.Initialize for button patch");
            }
        }

        /// <summary>
        /// Postfix method that runs after the main menu is initialized.
        /// Clones an existing button to create the "Mods" button and sets up its click listener.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            GameObject existingBtn = FindButtonByLabel("Mods");
            if (existingBtn != null)
            {
                RoosterPlugin.LogInfo("Mods button already exists, skipping creation");
                return;
            }

            try
            {
                RoosterPlugin.LogInfo("Creating Mods button...");

                GameObject optionsButton = FindFirstAvailableButton(new[] { "OPTIONS", "Options", "PLAY", "Play", "Play Local", "Play Online" });
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

                GameObject quitButton = FindButtonByLabel("Quit");
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
                        RoosterPlugin.LogInfo("Mods button clicked!");
                        ModMenuUI.ShowModMenu();
                    });
                }

                RoosterPlugin.LogInfo("Mods button successfully created!");
            }
            catch (System.Exception ex)
            {
                RoosterPlugin.LogError($"Failed to create Mods button: {ex}");
            }
        }

        private static GameObject FindButtonByLabel(string label)
        {
            foreach (var btn in UnityEngine.Object.FindObjectsOfType<TabletButton>())
            {
                var txt = btn.gameObject.GetComponentInChildren<TabletTextLabel>();
                if (txt != null && txt.text != null && txt.text.Equals(label, System.StringComparison.OrdinalIgnoreCase))
                {
                    return btn.gameObject;
                }
            }
            return null;
        }

        private static GameObject FindFirstAvailableButton(string[] labels)
        {
            foreach (var lbl in labels)
            {
                var btn = FindButtonByLabel(lbl);
                if (btn != null)
                {
                    return btn;
                }
            }
            return null;
        }
    }
}
