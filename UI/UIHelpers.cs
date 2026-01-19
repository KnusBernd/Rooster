using UnityEngine;
using UnityEngine.UI;

namespace Rooster.UI
{
    /// <summary>
    /// Provides utility methods for creating and managing common UI elements.
    /// Handles texture generation and scrollbar instantiation.
    /// </summary>
    public static class UIHelpers
    {
        private static Texture2D _whiteTexture;
        private static Sprite _whiteSprite;

        public static Texture2D GetWhiteTexture()
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(4, 4);
                Color[] pixels = new Color[4 * 4];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                _whiteTexture.SetPixels(pixels);
                _whiteTexture.Apply();
            }
            return _whiteTexture;
        }

        public static Sprite GetWhiteSprite()
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(GetWhiteTexture(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            return _whiteSprite;
        }

        /// <summary>Creates a styled scrollbar and attaches it to a ScrollRect.</summary>
        public static GameObject CreateScrollbar(RectTransform container, ScrollRect scrollRect, string name)
        {
            Sprite whiteSprite = GetWhiteSprite();
            int layer = container.gameObject.layer;

            var sbObj = new GameObject($"{name}Scrollbar", typeof(RectTransform), typeof(Scrollbar), typeof(Image));
            sbObj.layer = layer;
            sbObj.transform.SetParent(container, false);
            
            var le = sbObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var sbRect = sbObj.GetComponent<RectTransform>();
            var sb = sbObj.GetComponent<Scrollbar>();
            var sbImg = sbObj.GetComponent<Image>();

            sbRect.anchorMin = new Vector2(1f, 0f); 
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            sbRect.sizeDelta = new Vector2(40, -20);
            sbRect.anchoredPosition = new Vector2(-10, 0);

            sbImg.sprite = whiteSprite;
            sbImg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            sbImg.type = Image.Type.Sliced;

            var handleArea = new GameObject("Handle Area", typeof(RectTransform));
            handleArea.layer = layer;
            handleArea.transform.SetParent(sbRect, false);
            var haRect = handleArea.GetComponent<RectTransform>();
            haRect.anchorMin = Vector2.zero;
            haRect.anchorMax = Vector2.one;
            haRect.sizeDelta = new Vector2(-4, -4);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.layer = layer;
            handle.transform.SetParent(haRect, false);
            var hRect = handle.GetComponent<RectTransform>();
            var hImg = handle.GetComponent<Image>();
            
            hImg.sprite = whiteSprite;
            hImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            hImg.type = Image.Type.Sliced;
            hRect.sizeDelta = Vector2.zero;

            sb.handleRect = hRect;
            sb.targetGraphic = hImg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            scrollRect.verticalScrollbar = sb;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            return sbObj;
        }
        public static TabletColorScheme CloneColorScheme(TabletColorScheme source, GameObject target)
        {
            var newScheme = target.AddComponent<TabletColorScheme>();
            newScheme.bgColor = source.bgColor;
            newScheme.groupBgColor = source.groupBgColor;
            newScheme.buttonBgColor = source.buttonBgColor;
            newScheme.buttonBgColor_Hover = source.buttonBgColor_Hover;
            newScheme.buttonBgColor_Disabled = source.buttonBgColor_Disabled;
            newScheme.buttonBgColor_TransparentHighlight = source.buttonBgColor_TransparentHighlight;
            newScheme.buttonBgColor_Dangerous = source.buttonBgColor_Dangerous;
            newScheme.buttonBgColor_Dangerous_Disabled = source.buttonBgColor_Dangerous_Disabled;
            newScheme.buttonBgColor_Dangerous_Hover = source.buttonBgColor_Dangerous_Hover;
            newScheme.mainTextColor = source.mainTextColor;
            newScheme.mainTextColor_Disabled = source.mainTextColor_Disabled;
            newScheme.mainTextColor_Modified = source.mainTextColor_Modified;
            newScheme.mainTextColor_Modified_Disabled = source.mainTextColor_Modified_Disabled;
            newScheme.subtitleColor = source.subtitleColor;
            newScheme.subtitleColor_Disabled = source.subtitleColor_Disabled;
            
            newScheme.mainTextSize = source.mainTextSize;
            newScheme.mainTextTinySize = source.mainTextTinySize;
            newScheme.mainTextSmallSize = source.mainTextSmallSize;
            newScheme.mainTextLargeSize = source.mainTextLargeSize;
            newScheme.titleTextSize = source.titleTextSize;
            newScheme.subtitleTextSize = source.subtitleTextSize;
            
            return newScheme;
        }
    }
}
