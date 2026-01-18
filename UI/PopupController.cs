using UnityEngine;

namespace Rooster.UI
{
    /// <summary>
    /// Central controller for managing the sequence of popups shown at the main menu.
    /// Handles Beta Warnings, Restart Prompts, and Update Notifications.
    /// </summary>
    public static class PopupController
    {
        private static bool _betaWarningShown = false;
        private static bool _updateShownThisSession = false;
        private static bool _isUpdating = false;

        public static void ShowNextPopup()
        {
            if (!_betaWarningShown && RoosterConfig.ShowBetaWarning.Value)
            {
                ShowBetaWarning();
                return;
            }

            if (UpdateChecker.RestartRequired)
            {
                ShowRestartRequired();
                return;
            }

            if (!_updateShownThisSession && 
                (UpdateChecker.UpdatesAvailable.Count > 0 || UpdateChecker.PendingUpdates.Count > 0))
            {
                UpdateMenuUI.ShowUpdateMenu();
                _updateShownThisSession = true;
            }
        }

        private static void ShowBetaWarning()
        {
            if (Tablet.clickEventReceiver?.modalOverlay == null) return;
            var modal = Tablet.clickEventReceiver.modalOverlay;

            Patches.MainMenuPopupPatch.CurrentMenuState = Patches.MainMenuPopupPatch.MenuState.BetaWarning;

            modal.ShowSimpleMessage(
                "Welcome to the Rooster Beta!",
                "This mod is in active development. Features are experimental, so bugs may appear. Creating a backup of your BepInEx/plugins folder is highly recommended.",
                null
            );

            modal.okButtonContainer.gameObject.SetActive(false);
            modal.onOffContainer.gameObject.SetActive(true);

            var onLabel = modal.onButton.GetComponentInChildren<TabletTextLabel>();
            if (onLabel != null) onLabel.text = "OK";

            var offLabel = modal.offButton.GetComponentInChildren<TabletTextLabel>();
            if (offLabel != null) offLabel.text = "Never show again";

            var buttonRect = modal.onOffContainer.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.SetAsLastSibling();
                buttonRect.anchoredPosition = new Vector2(0f, -140f);
            }

            _betaWarningShown = true;
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
                case Patches.MainMenuPopupPatch.MenuState.BetaWarning:
                    return HandleBetaWarningChoice(modal, idx);

                case Patches.MainMenuPopupPatch.MenuState.UpdateMenu:
                    return HandleUpdateMenuChoice(modal, idx);

                case Patches.MainMenuPopupPatch.MenuState.RestartRequired:
                    return HandleRestartChoice(modal, idx);

                default:
                    return true; 
            }
        }

        private static bool HandleBetaWarningChoice(TabletModalOverlay modal, int idx)
        {
            if (idx == 0) 
            {
                RoosterConfig.ShowBetaWarning.Value = false;
                RoosterConfig.SaveConfig();
            }

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
                    (status) => {
                        if (modal?.simpleMessageText != null)
                            modal.simpleMessageText.text = status;
                    },
                    () => {
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
