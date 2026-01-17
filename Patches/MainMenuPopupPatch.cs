using HarmonyLib;
using UnityEngine;
using System.Linq;

namespace ThunderstoreUpdateChecker.Patches
{
    [HarmonyPatch(typeof(MainMenuControl), "JoinControllerToMainMenu")]
    public static class MainMenuPopupPatch
    {
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
                ThunderstoreUpdateCheckerPlugin.LogError("Failed to find MainMenuControl.JoinControllerToMainMenu");
            }
        }

        private static bool shownThisSession = false;

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (shownThisSession) return;
            if (UpdateChecker.UpdatesAvailable.Count == 0) return;

            if (Tablet.clickEventReceiver != null && Tablet.clickEventReceiver.modalOverlay != null)
            {
                var modal = Tablet.clickEventReceiver.modalOverlay;
                
                string title = "Mod Updates Available";
                string message = string.Join("\n", UpdateChecker.UpdatesAvailable.Take(5).ToArray());
                if (UpdateChecker.UpdatesAvailable.Count > 5) message += "\nAnd more...";

                // Show default message (Automatically uses OK button and closes on click)
                modal.ShowSimpleMessage(title, message, null);

                // Adjust Layout
                var textRect = modal.simpleMessageContainer;
                // Use OK button container instead of OnOff container
                var buttonRect = modal.okButtonContainer.GetComponent<RectTransform>();

                if (textRect != null && buttonRect != null)
                {
                    textRect.anchorMin = new Vector2(0.5f, 0.5f);
                    textRect.anchorMax = new Vector2(0.5f, 0.5f);
                    textRect.pivot = new Vector2(0.5f, 0.5f);

                    buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                    buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                    buttonRect.pivot = new Vector2(0.5f, 0.5f);

                    textRect.SetSiblingIndex(1);
                    buttonRect.SetAsLastSibling();

                    textRect.anchoredPosition = new UnityEngine.Vector2(0f, 80f);
                    buttonRect.anchoredPosition = new UnityEngine.Vector2(0f, -140f);
                }

                shownThisSession = true;
            }
        }
    }
}
