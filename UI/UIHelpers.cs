using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rooster.UI
{
    public static class UIHelpers
    {
        private static Texture2D _whiteTexture;
        private static Sprite _whiteSprite;

        public struct ButtonTheme
        {
            public Color Normal;
            public Color Hover;
            public Color Disabled;

            public ButtonTheme(Color normal, Color hover, Color disabled)
            {
                Normal = normal;
                Hover = hover;
                Disabled = disabled;
            }
        }

        public static class Themes
        {
            public static readonly ButtonTheme Success = new ButtonTheme(
                new Color(0.2f, 0.7f, 0.3f), 
                new Color(0.3f, 0.8f, 0.4f), 
                new Color(0.2f, 0.2f, 0.2f));

            public static readonly ButtonTheme Danger = new ButtonTheme(
                new Color(0.6f, 0.2f, 0.2f), 
                new Color(0.7f, 0.3f, 0.3f), 
                new Color(0.2f, 0.2f, 0.2f));

            public static readonly ButtonTheme Warning = new ButtonTheme(
                new Color(0.8f, 0.6f, 0.2f), 
                new Color(0.9f, 0.7f, 0.3f), 
                new Color(0.5f, 0.5f, 0.5f));

            public static readonly ButtonTheme Neutral = new ButtonTheme(
                new Color(0.25f, 0.25f, 0.25f), 
                new Color(0.35f, 0.35f, 0.35f), 
                new Color(0.15f, 0.15f, 0.15f));

             public static readonly ButtonTheme Action = new ButtonTheme(
                new Color(0.2f, 0.6f, 1f), 
                new Color(0.3f, 0.7f, 1f), 
                new Color(0.5f, 0.5f, 0.5f));
        }

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
             scrollRect.verticalNormalizedPosition = 1f; // Always start at top
             
             // Scrollbar
             var scrollbarObj = CreateScrollbar(parent.GetComponent<RectTransform>(), scrollRect, namePrefix);
             scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
             
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
            CreateText(parent, content, fontSize, bold ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft, color);
        }

        public static GameObject CreateText(Transform parent, string content, int fontSize, TextAnchor alignment, Color color, HorizontalWrapMode wrapMode = HorizontalWrapMode.Wrap)
        {
            var obj = new GameObject("Text", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var txt = obj.AddComponent<Text>();
            txt.text = content;
            txt.supportRichText = true;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = wrapMode;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            var layout = obj.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            
            return obj;
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
            
            var rt = btnObj.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(width, height);

            // Label
            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                label.text = text;
                label.transform.localScale = new Vector3(0.5f, 0.5f, 1f); 
                label.labelType = TabletTextLabel.LabelType.Normal;
                
                var txt = label.GetComponent<Text>();
                if (txt != null)
                {
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.alignment = TextAnchor.MiddleCenter;
                }
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
        public static void ApplyTheme(TabletButton btn, ButtonTheme theme)
        {
            ApplyButtonStyle(btn, theme.Normal, theme.Hover, theme.Disabled);
        }

        public static void ApplyButtonStyle(TabletButton btn, Color normal, Color hover, Color disabled)
        {
            if (btn == null) return;

            var newScheme = CloneColorScheme(btn.colorScheme ?? btn.gameObject.GetComponentInParent<TabletButton>()?.colorScheme, btn.gameObject);
            
            newScheme.buttonBgColor = normal;
            newScheme.buttonBgColor_Hover = hover;
            newScheme.buttonBgColor_Disabled = disabled;
            newScheme.buttonBgColor_TransparentHighlight = hover; 

            btn.colorScheme = newScheme;
            btn.ResetStyles();
        
            if (btn.background != null) 
            {
                btn.background.color = normal;
                btn.background.raycastTarget = true;
                btn.background.enabled = true;
            }
        }

        public static void SetupModal(TabletModalOverlay modal, Vector2 size, string title, Action onBack)
        {
            // 1. Reset standard elements
            modal.ShowSimpleMessage(title, "", () => { });
            modal.simpleMessageText.gameObject.SetActive(false);
            modal.onOffContainer.gameObject.SetActive(false);
            
            // 2. Setup Back Button
            modal.okButtonContainer.gameObject.SetActive(true);
            var okLabel = modal.okButton.GetComponentInChildren<TabletTextLabel>();
            if (okLabel != null) okLabel.text = "Back";

            modal.okButton.OnClick = new TabletButtonEvent();
            modal.okButton.OnClick.AddListener((cursor) => onBack?.Invoke());

            // 3. Clean Container
            // Preserve the simpleMessageText if it happens to be inside this container
            var textObj = modal.simpleMessageText != null ? modal.simpleMessageText.gameObject : null;
            CleanContainer(modal.simpleMessageContainer.gameObject, textObj);

            // 4. Setup Container Styling
            var container = modal.simpleMessageContainer;
            var rect = container.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var bgImg = container.gameObject.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            bgImg.color = Color.clear;
            bgImg.raycastTarget = true;
        }

        public static void CleanContainer(GameObject container, GameObject exclude = null)
        {
            // Destroy all child objects (buttons, rows, etc.)
            // Destroy all child objects safely (reverse loop)
            int childCount = container.transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = container.transform.GetChild(i).gameObject;
                if (exclude != null && child == exclude) continue;
                UnityEngine.Object.DestroyImmediate(child);
            }

            // Remove layouts
            foreach(var layout in container.GetComponents<LayoutGroup>())
                UnityEngine.Object.DestroyImmediate(layout);
                
            foreach(var fitter in container.GetComponents<ContentSizeFitter>())
                UnityEngine.Object.DestroyImmediate(fitter);

            // Remove ScrollRects to reset scrolling state
            foreach(var scrollRect in container.GetComponents<ScrollRect>())
                UnityEngine.Object.DestroyImmediate(scrollRect);

            // Ensure LayoutElement for parent compatibility
            var le = container.GetComponent<LayoutElement>() ?? container.AddComponent<LayoutElement>();
            le.preferredWidth = 1000f; // Default max
            le.preferredHeight = 900f;
            le.minWidth = 100f;
            le.minHeight = 100f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }
        public static void CreateToggleRow(Transform parent, TabletModalOverlay modal, string labelText,
            bool initialValue, Action<bool> onToggle, 
            out TabletButton onBtn, out TabletButton offBtn)
        {
            int layer = parent.gameObject.layer;

            var rowObj = new GameObject($"Row_{labelText.Replace(" ", "")}", typeof(RectTransform));
            rowObj.layer = layer;
            rowObj.transform.SetParent(parent, false);
            
            var rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(700, 70);

            var hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 20f;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 70f;
            rowLE.flexibleWidth = 1f;

            var labelObj = UnityEngine.Object.Instantiate(modal.titleText.gameObject, rowObj.transform);
            labelObj.name = "Label";
            labelObj.transform.localScale = Vector3.one;
            
            var labelTxt = labelObj.GetComponent<TabletTextLabel>();
            if (labelTxt != null)
            {
                labelTxt.text = labelText;
                labelTxt.labelType = TabletTextLabel.LabelType.Normal;
            }

            var labelLE = labelObj.GetComponent<LayoutElement>() ?? labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 350f;
            labelLE.flexibleWidth = 1f;

            TabletButton btnOnLocal = null;
            TabletButton btnOffLocal = null;

            var onBtnObj = UnityEngine.Object.Instantiate(modal.onButton.gameObject, rowObj.transform);
            onBtnObj.name = "OnBtn";
            onBtnObj.transform.localScale = Vector3.one;
            
            btnOnLocal = onBtnObj.GetComponent<TabletButton>();
            if (btnOnLocal != null)
            {
                var onLabel = onBtnObj.GetComponentInChildren<TabletTextLabel>();
                if (onLabel != null) 
                {
                    onLabel.text = "On";
                    onLabel.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                    
                    var txt = onLabel.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.alignment = TextAnchor.MiddleCenter;
                    }
                }
                
                btnOnLocal.OnClick = new TabletButtonEvent();
                // We will re-add listener later to capture correct chain
                btnOnLocal.SetDisabled(false);
                btnOnLocal.SetInteractable(true);
            }

            var onLE = onBtnObj.GetComponent<LayoutElement>() ?? onBtnObj.AddComponent<LayoutElement>();
            onLE.preferredWidth = 120f;
            onLE.preferredHeight = 70f;
            onLE.minWidth = 120f;
            onLE.minHeight = 70f;

            var offBtnObj = UnityEngine.Object.Instantiate(modal.offButton.gameObject, rowObj.transform);
            offBtnObj.name = "OffBtn";
            offBtnObj.transform.localScale = Vector3.one;
            
            btnOffLocal = offBtnObj.GetComponent<TabletButton>();
            if (btnOffLocal != null)
            {
                var offLabel = offBtnObj.GetComponentInChildren<TabletTextLabel>();
                if (offLabel != null)
                {
                    offLabel.text = "Off";
                    offLabel.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

                    var txt = offLabel.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.alignment = TextAnchor.MiddleCenter;
                    }
                }
                
                btnOffLocal.OnClick = new TabletButtonEvent();
                // We will re-add listener later to capture correct chain
                btnOffLocal.SetDisabled(false);
                btnOffLocal.SetInteractable(true);
            }

            var offLE = offBtnObj.GetComponent<LayoutElement>() ?? offBtnObj.AddComponent<LayoutElement>();
            offLE.preferredWidth = 120f;
            offLE.preferredHeight = 70f;
            offLE.minWidth = 120f;
            offLE.minHeight = 70f;

            onBtnObj.SetActive(true);
            offBtnObj.SetActive(true);

            // Assign out parameters now that objects are created
            onBtn = btnOnLocal;
            offBtn = btnOffLocal;

            // Use Local variables for Lambda Capture to avoid CS1628
            TabletButton capOn = btnOnLocal;
            TabletButton capOff = btnOffLocal;

            Action<bool> updateStyles = (isOn) => {
                 if (capOn != null)
                 {
                    capOn.buttonType = isOn ? TabletButton.ButtonType.Simple : TabletButton.ButtonType.Transparent;
                    capOn.ResetStyles();
                 }
                 if (capOff != null)
                 {
                    capOff.buttonType = isOn ? TabletButton.ButtonType.Transparent : TabletButton.ButtonType.Simple;
                    capOff.ResetStyles();
                 }
            };
            
            Action<bool> chainToggle = onToggle;
            Action<bool> wrapperToggle = (val) => {
                updateStyles(val);
                chainToggle(val);
            };
            
            // Re-bind listeners with style update
            if (capOn != null)
            {
                capOn.OnClick.AddListener((c) => wrapperToggle(true));
            }
            if (capOff != null)
            {
                capOff.OnClick.AddListener((c) => wrapperToggle(false));
            }

            // Assign the wrapper back to onToggle if caller needs it? 
            // The signature is Action<bool> onToggle (input), we don't return it.
            // But we already hooked up the listeners to call the wrapper.
            // But we DID mutate the input parameter 'onToggle' in previous code: `onToggle = (val) => ...`
            // Modifying the input parameter locally doesn't affect the caller unless it's ref. 
            // The previous code `onToggle = ...` was only effective for subsequent uses INSIDE this method if any.
            // But there were none. The listeners were hooked up to the NEW `onToggle` lambda.
            
            updateStyles(initialValue);
        }

        public static void ShowUninstallConfirmation(TabletModalOverlay modal, BepInEx.PluginInfo plugin, GameObject toHide, Action onCancelled, Action<bool> onConfirmed)
        {
            if (toHide != null) toHide.SetActive(false);

            var scope = Services.ModUninstaller.GetUninstallScope(plugin);
            string title = "Confirm Uninstall";
            string desc = "";
            bool deleteConfig = true;

            switch (scope)
            {
                case Services.UninstallScope.Manual:
                    desc = "This mod was installed manually. Rooster will attempt to remove the DLL and its configuration.";
                    break;
                case Services.UninstallScope.Discovered:
                    desc = "This mod was discovered locally but lacks metadata. Rooster will attempt to remove the DLL and its configuration.";
                    break;
                case Services.UninstallScope.Tracked:
                    desc = "This mod is fully managed. Rooster will remove all associated files in its folder and its configuration.";
                    break;
            }

            SetupModal(modal, new Vector2(1000, 700), title, () => {
                if (toHide != null) toHide.SetActive(true);
                onCancelled?.Invoke();
            });

            var container = modal.simpleMessageContainer;
            var layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 30f;
            layout.padding = new RectOffset(40, 40, 20, 20);

            // Description UI
            var descObj = UnityEngine.Object.Instantiate(modal.simpleMessageText.gameObject, container.transform);
            descObj.SetActive(true);
            var descTxt = descObj.GetComponent<Text>();
            descTxt.text = desc;
            descTxt.fontSize = 28;
            descTxt.alignment = TextAnchor.MiddleCenter;
            var descLE = descObj.GetComponent<LayoutElement>() ?? descObj.AddComponent<LayoutElement>();
            descLE.preferredHeight = 150f;

            // Toggle Config UI
            TabletButton onB, offB;
            CreateToggleRow(container.GetComponent<RectTransform>(), modal, "Delete Configuration", deleteConfig, (val) => {
                deleteConfig = val;
            }, out onB, out offB);

            // Action Row
            var actionRow = new GameObject("ActionRow", typeof(RectTransform));
            actionRow.transform.SetParent(container.transform, false);
            var actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 40f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childForceExpandWidth = false;

            var confirmBtn = CreateButton(actionRow.transform, modal.okButton, "Confirm Uninstall", 400, 80);
            ApplyTheme(confirmBtn, Themes.Danger);
            confirmBtn.OnClick = new TabletButtonEvent();
            confirmBtn.OnClick.AddListener((c) => {
                 UnityEngine.Debug.Log($"[UI Debug] Confirm Button Clicked. Invoking onConfirmed with {deleteConfig}");
                 onConfirmed?.Invoke(deleteConfig);
            });

            var cancelBtn = CreateButton(actionRow.transform, modal.okButton, "Cancel", 400, 80);
            cancelBtn.OnClick = new TabletButtonEvent();
            cancelBtn.OnClick.AddListener((c) => {
                if (toHide != null) toHide.SetActive(true);
                onCancelled?.Invoke();
            });
        }

        public static void ShowRestartPrompt(TabletModalOverlay modal, string title, string message, Action onLater)
        {
            RoosterPlugin.LogInfo($"[UI] ShowRestartPrompt called. Title: {title}");
            SetupModal(modal, new Vector2(1000, 500), title, null);
            
            // Explicitly hide the standard OK button since we provide custom ones
            if(modal.okButtonContainer != null) 
            {
                modal.okButtonContainer.gameObject.SetActive(false);
            }

            var container = modal.simpleMessageContainer;

            // Ensure the container itself has a layout handler if needed
            var layout = container.gameObject.GetComponent<VerticalLayoutGroup>() ?? container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 30f;
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            // Message
            var msgObj = CreateText(container.transform, message, 34, TextAnchor.MiddleCenter, Color.white);
            msgObj.name = "CustomMessageText";
            
            var msgLe = msgObj.GetComponent<LayoutElement>();
            if (msgLe) {
                msgLe.preferredHeight = 200; 
                msgLe.minHeight = 100;
            }

            // Buttons Row
            var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
            btnRow.transform.SetParent(container.transform, false);
            
            var rowLe = btnRow.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 100f;
            rowLe.minHeight = 80f;
            rowLe.flexibleWidth = 1f;
            
            var hLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 40f;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false; // CRITICAL: Prevent vertical stretching
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            
            // Restart Button
            var restartBtn = CreateButton(btnRow.transform, modal.okButton, "Quit to Desktop", 350, 70);
            ApplyTheme(restartBtn, Themes.Success);
            
            var lb = restartBtn.GetComponentInChildren<TabletTextLabel>();
            if(lb) {
                    var t = lb.GetComponent<Text>();
                    if(t) t.fontSize = 28;
            }

            restartBtn.OnClick = new TabletButtonEvent();
            restartBtn.OnClick.AddListener((c) => Application.Quit());
            restartBtn.SetDisabled(false);
            restartBtn.SetInteractable(true);

            // Later Button
            var laterBtn = CreateButton(btnRow.transform, modal.okButton, "Later", 350, 70);
            ApplyTheme(laterBtn, Themes.Neutral);
            
                var lb2 = laterBtn.GetComponentInChildren<TabletTextLabel>();
            if(lb2) {
                    var t = lb2.GetComponent<Text>();
                    if(t) t.fontSize = 24;
            }

            laterBtn.OnClick = new TabletButtonEvent();
            laterBtn.OnClick.AddListener((c) => onLater?.Invoke());
            laterBtn.SetDisabled(false);
            laterBtn.SetInteractable(true);
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
        }
    }
}
