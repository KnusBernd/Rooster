using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Rooster.Patches
{
    /// <summary>
    /// Patches the PickCursor to enable mouse scrolling support for any in-game scrollbar.
    /// Also handles D-Pad/Right Stick scrolling emulation.
    /// </summary>
    [HarmonyPatch(typeof(PickCursor))]
    public static class PickCursorScrollPatch
    {
        private static Scrollbar capturedScrollbar;
        private static float captureStartValue;
        private static float captureStartCursorY;
        private static float captureStartCursorX;

        [HarmonyPatch("ReceiveEvent")]
        [HarmonyPrefix]
        public static bool ReceiveEventPrefix(PickCursor __instance, InputEvent e)
        {
            if (e.Key == InputEvent.InputKey.Up2 || e.Key == InputEvent.InputKey.Down2 ||
                e.Key == InputEvent.InputKey.RotateLeft || e.Key == InputEvent.InputKey.RotateRight)
            {
                if (Mathf.Abs(e.Valuef) > 0.001f)
                {
                    ScrollRect scrollRect = GetHoveredScrollRect(__instance);
                    if (scrollRect != null && scrollRect.content != null)
                    {
                        float direction = (e.Key == InputEvent.InputKey.Up2 || e.Key == InputEvent.InputKey.RotateRight) ? 1f : -1f;
                        float sensitivity = 0.1f;

                        if (scrollRect.vertical)
                        {
                            scrollRect.verticalNormalizedPosition += direction * sensitivity * e.Valuef;
                            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                        }
                    }
                }
            }

            if (e.Key != InputEvent.InputKey.Jump && e.Key != InputEvent.InputKey.Accept)
                return true;

            if (e.Valueb && e.Changed)
            {
                PointerEventData ped = new PointerEventData(EventSystem.current);

                Camera uiCam = null;
                if (__instance.InventoryBookMenu != null && __instance.InventoryBookMenu.TabletPage != null)
                {
                    var canvas = __instance.InventoryBookMenu.TabletPage.GetComponentInChildren<Canvas>();
                    if (canvas != null) uiCam = canvas.worldCamera;
                }

                if (uiCam == null) uiCam = Camera.main;

                ped.position = RectTransformUtility.WorldToScreenPoint(uiCam, __instance.cursorPoint.position);

                // Manual raycast to detect non-focused scrollbars
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);

                foreach (var result in results)
                {
                    var sb = result.gameObject.GetComponentInParent<Scrollbar>();
                    if (sb != null && sb.interactable)
                    {
                        capturedScrollbar = sb;
                        captureStartValue = sb.value;
                        if (__instance.cursorPoint != null)
                        {
                            captureStartCursorY = __instance.cursorPoint.position.y;
                            captureStartCursorX = __instance.cursorPoint.position.x;
                        }
                        return false;
                    }
                }
            }
            else if (!e.Valueb && e.Changed)
            {
                if (capturedScrollbar != null)
                {
                    capturedScrollbar = null;
                    return false;
                }
            }

            return true;
        }

        private static ScrollRect GetHoveredScrollRect(PickCursor cursor)
        {
            PointerEventData ped = new PointerEventData(EventSystem.current);
            Camera uiCam = null;
            if (cursor.InventoryBookMenu != null && cursor.InventoryBookMenu.TabletPage != null)
            {
                var canvas = cursor.InventoryBookMenu.TabletPage.GetComponentInChildren<Canvas>();
                if (canvas != null) uiCam = canvas.worldCamera;
            }
            if (uiCam == null) uiCam = Camera.main;

            ped.position = RectTransformUtility.WorldToScreenPoint(uiCam, cursor.cursorPoint.position);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);

            foreach (var result in results)
            {
                var sr = result.gameObject.GetComponentInParent<ScrollRect>();
                if (sr != null && sr.isActiveAndEnabled) return sr;
            }
            return null;
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        public static void FixedUpdatePostfix(PickCursor __instance)
        {
            if (capturedScrollbar == null) return;
            if (__instance.cursorPoint == null) return;
            if (capturedScrollbar.gameObject == null)
            {
                capturedScrollbar = null;
                return;
            }

            RectTransform sbRect = capturedScrollbar.GetComponent<RectTransform>();

            Vector3[] corners = new Vector3[4];
            sbRect.GetWorldCorners(corners);

            float trackHeight = corners[1].y - corners[0].y;
            float trackWidth = corners[2].x - corners[0].x;

            float handleHeight = 0f;
            float handleWidth = 0f;

            if (capturedScrollbar.handleRect != null)
            {
                Vector3[] hCorners = new Vector3[4];
                capturedScrollbar.handleRect.GetWorldCorners(hCorners);
                handleHeight = hCorners[1].y - hCorners[0].y;
                handleWidth = hCorners[2].x - hCorners[0].x;
            }

            if (capturedScrollbar.direction == Scrollbar.Direction.BottomToTop || capturedScrollbar.direction == Scrollbar.Direction.TopToBottom)
            {
                float worldDeltaY = __instance.cursorPoint.position.y - captureStartCursorY;

                float usableHeight = trackHeight - handleHeight;
                if (usableHeight < 0.001f) usableHeight = trackHeight * 0.1f;

                float normalizedDelta = worldDeltaY / usableHeight;

                float sign = (capturedScrollbar.direction == Scrollbar.Direction.TopToBottom) ? -1f : 1f;

                float val = captureStartValue + (normalizedDelta * sign);

                if (val > 1.0f)
                {
                    float excess = val - 1.0f;
                    captureStartCursorY += (excess * usableHeight) / sign;
                    val = 1.0f;
                }
                else if (val < 0.0f)
                {
                    float excess = val - 0.0f;
                    captureStartCursorY += (excess * usableHeight) / sign;
                    val = 0.0f;
                }

                capturedScrollbar.value = val;
            }
            else
            {
                float worldDeltaX = __instance.cursorPoint.position.x - captureStartCursorX;

                float usableWidth = trackWidth - handleWidth;
                if (usableWidth < 0.001f) usableWidth = trackWidth * 0.1f;

                float normalizedDelta = worldDeltaX / usableWidth;
                float sign = (capturedScrollbar.direction == Scrollbar.Direction.RightToLeft) ? -1f : 1f;

                float val = captureStartValue + (normalizedDelta * sign);

                // Elastic drag at bounds
                if (val > 1.0f)
                {
                    float excess = val - 1.0f;
                    captureStartCursorX += (excess * usableWidth) / sign;
                    val = 1.0f;
                }
                else if (val < 0.0f)
                {
                    float excess = val - 0.0f;
                    captureStartCursorX += (excess * usableWidth) / sign;
                    val = 0.0f;
                }

                capturedScrollbar.value = val;
            }
        }
    }
}
