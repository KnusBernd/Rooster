using HarmonyLib;
using Rooster.UI;

namespace Rooster.Patches
{
    /// <summary>
    /// Intercepts main menu controller events to manage the custom popup queue.
    /// Hijacks the tablet modal system to display Rooster's mod menu and notifications.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuControl), "JoinControllerToMainMenu")]
    public static class MainMenuPopupPatch
    {
        /// <summary>
        /// Enum representing the current state of the hijacked menu/modal.
        /// </summary>
        public enum MenuState { None, ModMenu, ModSettings, UpdateMenu, BetaWarning, RestartRequired }
        
        /// <summary>
        /// The current active state of the custom menu system.
        /// </summary>
        public static MenuState CurrentMenuState = MenuState.None;

        /// <summary>
        /// Applies patches to MainMenuControl and TabletModalOverlay.
        /// </summary>
        /// <param name="harmony">The Harmony instance.</param>
        public static void ApplyPatch(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(MainMenuControl), "JoinControllerToMainMenu");
            if (method != null)
            {
                var postfix = AccessTools.Method(typeof(MainMenuPopupPatch), nameof(Postfix));
                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
            }
            else
            {
                RoosterPlugin.LogError("Failed to find MainMenuControl.JoinControllerToMainMenu");
            }

            var choiceMethod = AccessTools.Method(typeof(TabletModalOverlay), "OnSelectChoice");
            if (choiceMethod != null)
            {
                var choicePrefix = AccessTools.Method(typeof(MainMenuPopupPatch), nameof(OnSelectChoicePrefix));
                harmony.Patch(choiceMethod, prefix: new HarmonyMethod(choicePrefix));
            }

            var closeMethod = AccessTools.Method(typeof(TabletModalOverlay), "Close");
            if (closeMethod != null)
            {
                var closePrefix = AccessTools.Method(typeof(MainMenuPopupPatch), nameof(OnClosePrefix));
                harmony.Patch(closeMethod, prefix: new HarmonyMethod(closePrefix));
            }
        }

        /// <summary>
        /// Triggers the popup queue check when the controller joins the main menu.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            PopupController.ShowNextPopup();
        }

        /// <summary>
        /// Helper to trigger the next popup in the queue if conditions are met.
        /// </summary>
        public static void ShowPopupIfNeeded()
        {
            PopupController.ShowNextPopup();
        }

        /// <summary>
        /// Prefix patch for TabletModalOverlay.OnSelectChoice.
        /// Redirects button clicks to PopupController when a custom menu is active.
        /// </summary>
        public static bool OnSelectChoicePrefix(TabletModalOverlay __instance, int idx)
        {
            return PopupController.HandleChoice(__instance, idx);
        }

        /// <summary>
        /// Prefix patch for TabletModalOverlay.Close.
        /// Intercepts close events to handle back navigation in custom menus (e.g., Settings -> Mod List).
        /// </summary>
        public static bool OnClosePrefix()
        {
            
            if (CurrentMenuState == MenuState.ModSettings)
            {
                ModSettingsUI.CleanupCustomUI();
                CurrentMenuState = MenuState.ModMenu;
                ModMenuUI.ShowModMenu();
                return false;
            }

            
            ModMenuUI.ScheduleCleanup();
            ModSettingsUI.CleanupCustomUI();
            UpdateMenuUI.DestroyUI();
            CurrentMenuState = MenuState.None;
            return true;
        }
    }
}
