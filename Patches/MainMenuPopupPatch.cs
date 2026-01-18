using HarmonyLib;
using Rooster.UI;

namespace Rooster.Patches
{
    /// <summary>
    /// Intercepts main menu controller events to manage the custom popup queue.
    /// Hijacks the tablet modal system to display Rooster's mod menu and notifications.
    /// </summary>
    public static class MainMenuPopupPatch
    {
        public enum MenuState { None, ModMenu, ModSettings, UpdateMenu, BetaWarning, RestartRequired }

        public static MenuState CurrentMenuState = MenuState.None;



        [HarmonyPatch(typeof(MainMenuControl), "JoinControllerToMainMenu")]
        [HarmonyPostfix]
        public static void Postfix()
        {
            PopupController.ShowNextPopup();
        }

        public static void ShowPopupIfNeeded()
        {
            PopupController.ShowNextPopup();
        }

        [HarmonyPatch(typeof(TabletModalOverlay), "OnSelectChoice")]
        [HarmonyPrefix]
        public static bool OnSelectChoicePrefix(TabletModalOverlay __instance, int idx)
        {
            return PopupController.HandleChoice(__instance, idx);
        }

        [HarmonyPatch(typeof(TabletModalOverlay), "Close")]
        [HarmonyPrefix]
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
