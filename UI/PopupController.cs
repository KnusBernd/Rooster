using UnityEngine;

namespace Rooster.UI
{
    /// <summary>
    /// Central controller for managing the sequence of popups shown at the main menu.
    /// Handles Disclaimers, Restart Prompts, and Update Notifications.
    /// </summary>
    public static class PopupController
    {
        private static bool _updateShownThisSession = false;
        private static bool _isUpdating = false;

        public static void ShowNextPopup()
        {
            if (!RoosterConfig.DisclaimerAccepted.Value)
            {
                ShowDisclaimer();
                return;
            }


            if (UpdateChecker.RestartRequired)
            {
                ShowRestartRequired();
                return;
            }

            // Only show update popup after check is complete and no other popup is active
            if (!_updateShownThisSession && UpdateChecker.CheckComplete && UpdateChecker.PendingUpdates.Count > 0 &&
                Patches.MainMenuPopupPatch.CurrentMenuState == Patches.MainMenuPopupPatch.MenuState.None)
            {
                UpdateMenuUI.ShowUpdateMenu();
                _updateShownThisSession = true;
            }
        }

        private static void ShowDisclaimer()
        {
            if (Tablet.clickEventReceiver?.modalOverlay == null) return;
            var modal = Tablet.clickEventReceiver.modalOverlay;

            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.Disclaimer;

            // Prepare Modal
            UIHelpers.SetupModal(modal, new Vector2(1100, 750), "Disclaimer", () => {
                HandleDisclaimerChoice(modal, 0);
            });

            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "I Accept the Risks";

            // Cleanup any existing custom UI
            var container = modal.simpleMessageContainer.gameObject;
            
            // Setup Scrollable Content
            var scroll = UIHelpers.CreateScrollLayout(container, "Rooster Disclaimer", 20, 20, 40);
            
            // Allow content to grow
            var fitter = scroll.Content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            
            var layout = scroll.Content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(20, 20, 20, 20);

            string disclaimer = "This mod manager is an unofficial, third-party tool and is NOT affiliated with, endorsed by, or supported by Clever Endeavour Games.\n\n" +
                    "Be aware that some mods may give unfair advantages in online games or be disruptive to other players. Using such mods constitutes hacking and cheating and can get you permanently banned from the game. You should always be careful about what you install, as clandestine software can easily introduce Trojan horses and spyware to your machine.\n\n" +
                    "While Clever Endeavour tolerates local cosmetic modifications (such as character skins), any mods that affect online gameplay are strictly prohibited and may result in a permanent IP ban from the game. Online altering modifications should only be used in private lobbies.\n\n" +
                    "You assume all risks associated with using this tool. The developers of Rooster are not responsible for any damage to your game, computer, save files, or account bans resulting from its use.";

            UIHelpers.CreateText(scroll.Content, disclaimer, 32, TextAnchor.UpperLeft, Color.white);
            
            // Reset scroll position to top
            scroll.ScrollRect.verticalNormalizedPosition = 1f;
        }


        private static void ShowRestartRequired()
        {
            if (Tablet.clickEventReceiver?.modalOverlay == null) return;
            var modal = Tablet.clickEventReceiver.modalOverlay;

            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.RestartRequired;

            modal.ShowSimpleMessage("Updates Installed", "Updates have been installed successfully.\nPlease restart the game.", null);
            modal.titleText.text = "Update Complete";

            modal.onOffContainer.gameObject.SetActive(true);
            modal.okButtonContainer.gameObject.SetActive(false);

            var onLabel = modal.onButton.GetComponentInChildren<TabletTextLabel>();
            if (onLabel != null) onLabel.text = "Quit Game";

            modal.onButton.OnClick = new TabletButtonEvent();
            modal.onButton.OnClick.AddListener((c) => { Application.Quit(); });

            var offLabel = modal.offButton.GetComponentInChildren<TabletTextLabel>();
            if (offLabel != null) offLabel.text = "Later";

            modal.offButton.OnClick = new TabletButtonEvent();
            modal.offButton.OnClick.AddListener((c) => { modal.Close(); });

            _updateShownThisSession = true;
        }

        public static bool HandleChoice(TabletModalOverlay modal, int idx)
        {
            var state = Patches.MainMenuPopupPatch.CurrentMenuState;

            switch (state)
            {
                case Patches.MainMenuPopupPatch.MenuState.Disclaimer:
                    return HandleDisclaimerChoice(modal, idx);


                case Patches.MainMenuPopupPatch.MenuState.UpdateMenu:
                    return HandleUpdateMenuChoice(modal, idx);

                case Patches.MainMenuPopupPatch.MenuState.RestartRequired:
                    return HandleRestartChoice(modal, idx);

                default:
                    return true;
            }
        }

        private static bool HandleDisclaimerChoice(TabletModalOverlay modal, int idx)
        {
            RoosterConfig.DisclaimerAccepted.Value = true;
            RoosterConfig.SaveConfig();

            modal.Close();
            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.None;
            ShowNextPopup();
            return false;
        }


        private static bool HandleUpdateMenuChoice(TabletModalOverlay modal, int idx)
        {
            if (idx == 1 && !_isUpdating)
            {
                _isUpdating = true;
                modal.simpleMessageText.text = "Initializing Update...";
                modal.onOffContainer.gameObject.SetActive(false);

                UpdateChecker.UpdateAll(
                    (info, status) =>
                    {
                        if (modal?.simpleMessageText != null)
                        {
                            if (info != null) modal.simpleMessageText.text = $"{info.ModName}: {status}";
                            else modal.simpleMessageText.text = status;
                        }
                    },
                    () =>
                    {
                        _isUpdating = false;
                        if (modal?.simpleMessageText != null)
                            modal.simpleMessageText.text = "Updates Complete!\nPlease restart manually.";

                        if (modal != null)
                        {
                            modal.onOffContainer.gameObject.SetActive(true);
                            modal.titleText.text = "Update Complete";

                            var restartLabel = modal.onButton.GetComponentInChildren<TabletTextLabel>();
                            if (restartLabel != null) restartLabel.text = "Quit Game";

                            var laterLabel = modal.offButton.GetComponentInChildren<TabletTextLabel>();
                            if (laterLabel != null) laterLabel.text = "Later";

                            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.RestartRequired;
                        }
                    }
                );
                return false;
            }
            else if (idx == 0 && !_isUpdating)
            {
                modal.Close();
                return false;
            }
            return true;
        }

        private static bool HandleRestartChoice(TabletModalOverlay modal, int idx)
        {
            if (idx == 1)
            {
                Application.Quit();
                return false;
            }
            else
            {
                modal.Close();
                return false;
            }
        }
    }
}
