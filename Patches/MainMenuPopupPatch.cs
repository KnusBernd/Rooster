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
        public enum MenuState { None, ModMenu, ModSettings, UpdateMenu, BetaWarning, RestartRequired }

        public static MenuState CurrentMenuState = MenuState.None;

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

        [HarmonyPostfix]
        public static void Postfix()
        {
            PopupController.ShowNextPopup();
        }

        public static void ShowPopupIfNeeded()
        {
            PopupController.ShowNextPopup();
        }

        public static bool OnSelectChoicePrefix(TabletModalOverlay __instance, int idx)
        {
            return PopupController.HandleChoice(__instance, idx);
        }

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
