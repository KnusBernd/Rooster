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

        public class ScrollLayout
        {
            public ScrollRect ScrollRect;
            public RectTransform Viewport;
            public RectTransform Content;
            public GameObject ScrollbarObj;
        }

        public static ScrollLayout CreateScrollLayout(GameObject parent, string namePrefix, float topMargin, float bottomMargin, float sideMargin = 0f, float scrollbarWidth = 40f, float scrollbarPadding = 10f)
        {

             var viewportObj = new GameObject($"{namePrefix}Viewport", typeof(RectTransform));
             viewportObj.layer = parent.layer;
             viewportObj.transform.SetParent(parent.transform, false);
             var viewportRect = viewportObj.GetComponent<RectTransform>();
             
             // Fill parent but respect margins
             viewportRect.anchorMin = Vector2.zero;
             viewportRect.anchorMax = Vector2.one;
             viewportRect.pivot = new Vector2(0, 1);
             
             // Offsets: 
             // Make room for scrollbar on right
             viewportRect.offsetMin = new Vector2(sideMargin, bottomMargin);
             viewportRect.offsetMax = new Vector2(-(sideMargin + scrollbarWidth + scrollbarPadding), -topMargin);
             
             var vpImg = viewportObj.AddComponent<Image>();
             vpImg.sprite = GetWhiteSprite();
             vpImg.color = new Color(1, 1, 1, 0.05f);
             vpImg.color = Color.white;
             
             var mask = viewportObj.AddComponent<Mask>();
             mask.showMaskGraphic = false;

             var contentObj = new GameObject($"{namePrefix}Content", typeof(RectTransform));
             contentObj.layer = parent.layer;
             contentObj.transform.SetParent(viewportRect, false);
             var contentRect = contentObj.GetComponent<RectTransform>();
             
             contentRect.anchorMin = new Vector2(0, 1);
             contentRect.anchorMax = new Vector2(1, 1); 
             contentRect.pivot = new Vector2(0.5f, 1);
             contentRect.anchoredPosition = Vector2.zero;
             contentRect.sizeDelta = Vector2.zero; // height controlled by fitter
             
             
             var scrollRect = parent.GetComponent<ScrollRect>() ?? parent.AddComponent<ScrollRect>();
             scrollRect.content = contentRect;
             scrollRect.viewport = viewportRect;
             scrollRect.horizontal = false;
             scrollRect.vertical = true;
             scrollRect.movementType = ScrollRect.MovementType.Clamped;
             scrollRect.scrollSensitivity = 30f;
             
             // Scrollbar
             var scrollbarObj = CreateScrollbar(parent.GetComponent<RectTransform>(), scrollRect, namePrefix);
             var sbRect = scrollbarObj.GetComponent<RectTransform>();
             
             sbRect.anchorMin = new Vector2(1, 0);
             sbRect.anchorMax = new Vector2(1, 1);
             sbRect.pivot = new Vector2(1, 1);
             
             sbRect.offsetMin = new Vector2(-scrollbarWidth - sideMargin, bottomMargin);
             sbRect.offsetMax = new Vector2(-sideMargin, -topMargin);
             sbRect.sizeDelta = new Vector2(scrollbarWidth, sbRect.sizeDelta.y);
             
             return new ScrollLayout 
             {
                 ScrollRect = scrollRect,
                 Viewport = viewportRect,
                 Content = contentRect,
                 ScrollbarObj = scrollbarObj
             };
        }
        public static void AddText(Transform parent, string content, int fontSize, bool bold, Color color)
        {
            var obj = new GameObject("Text", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var txt = obj.AddComponent<Text>();
            txt.text = bold ? $"<b>{content}</b>" : content;
            txt.supportRichText = true;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 10;
            layout.flexibleWidth = 1;
        }

        public static TabletButton CreateButton(Transform parent, TabletButton template, string text, float width, float height)
        {
            var btnObj = UnityEngine.Object.Instantiate(template.gameObject, parent);
            btnObj.name = "Btn_" + text.Replace(" ", "");
            
            // Layout
            var oldLe = btnObj.GetComponent<LayoutElement>();
            if (oldLe != null) UnityEngine.Object.Destroy(oldLe);
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = width;

            // Label
            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                label.text = text;
                label.transform.localScale = new Vector3(0.5f, 0.5f, 1f); 
                label.labelType = TabletTextLabel.LabelType.Normal;
            }

            // Button Component
            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                // Clone scheme by default so it has its own instance
                tabletBtn.colorScheme = CloneColorScheme(template.colorScheme, btnObj);
                tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                tabletBtn.ResetStyles();
            }

            btnObj.SetActive(true);
            return tabletBtn;
        }
    }
}
