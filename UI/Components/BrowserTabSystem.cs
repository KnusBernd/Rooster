using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rooster.UI.Components
{
    public class BrowserTabSystem
    {
        private TabletButton _buttonTemplate;
        private TabletButton _refreshButton;
        private TabletTextLabel _refreshLabel;
        private GameObject _loadingSpinner;

        public event Action OnRefreshClicked;
        public event Action OnThunderstoreTabClicked;
        public event Action OnGitHubTabClicked;

        public BrowserTabSystem(TabletButton template)
        {
            _buttonTemplate = template;
        }

        public void CreateTabs(Transform parent, bool isThunderstoreActive)
        {
            CreateTabButton(parent, "Thunderstore", -315, () => OnThunderstoreTabClicked?.Invoke(), UIHelpers.Themes.Action);
            CreateTabButton(parent, "GitHub", 15, () => OnGitHubTabClicked?.Invoke(), UIHelpers.Themes.Neutral);
            CreateRefreshButton(parent);
        }

        public void SetRefreshState(bool isRefreshing)
        {
            if (_refreshButton != null)
            {
                _refreshButton.SetInteractable(!isRefreshing);
                _refreshButton.SetDisabled(isRefreshing);
            }

            if (_loadingSpinner != null)
            {
                _loadingSpinner.SetActive(isRefreshing);
            }

            if (_refreshLabel != null)
            {
                _refreshLabel.text = isRefreshing ? "" : "Refresh";
            }
        }

        private System.Collections.Generic.List<GameObject> _tabButtons = new System.Collections.Generic.List<GameObject>();

        public void SetVisible(bool visible)
        {
            foreach (var btn in _tabButtons)
            {
                if (btn != null) btn.SetActive(visible);
            }
            if (_loadingSpinner != null && !visible) _loadingSpinner.SetActive(false);
        }

        private void CreateTabButton(Transform parent, string text, float xOffset, Action onClick, UIHelpers.ButtonTheme theme)
        {
            if (_buttonTemplate == null) return;

            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "Tab_" + text;

            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(300, 80);
            rect.anchoredPosition = new Vector2(xOffset, -10);

            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                label.text = text;
                label.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                label.labelType = TabletTextLabel.LabelType.Normal;

                var t = label.GetComponent<Text>();
                if (t != null)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                UIHelpers.ApplyTheme(tabletBtn, theme);

                tabletBtn.OnClick = new TabletButtonEvent();
                tabletBtn.OnClick.AddListener((cursor) =>
                {
                    RoosterPlugin.LogInfo($"Tab Clicked: {text}");
                    onClick?.Invoke();
                });
                tabletBtn.SetDisabled(false);
                tabletBtn.SetInteractable(true);
                tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                tabletBtn.ResetStyles();
            }

            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);

            _tabButtons.Add(btnObj);
        }

        private void CreateRefreshButton(Transform parent)
        {
            if (_buttonTemplate == null) return;

            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            btnObj.name = "RefreshButton";

            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(200, 80);
            rect.anchoredPosition = new Vector2(300, -10);

            var label = btnObj.GetComponentInChildren<TabletTextLabel>();
            if (label != null)
            {
                _refreshLabel = label;
                label.text = "Refresh";
                label.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                label.labelType = TabletTextLabel.LabelType.Normal;

                var t = label.GetComponent<Text>();
                if (t != null)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                }

                var lblRect = label.GetComponent<RectTransform>();
                if (lblRect != null)
                {
                    lblRect.anchorMin = Vector2.zero;
                    lblRect.anchorMax = Vector2.one;
                    lblRect.sizeDelta = Vector2.zero;
                    lblRect.anchoredPosition = Vector2.zero;
                }
            }

            var tabletBtn = btnObj.GetComponent<TabletButton>();
            if (tabletBtn != null)
            {
                UIHelpers.ApplyTheme(tabletBtn, UIHelpers.Themes.Warning);

                tabletBtn.OnClick = new TabletButtonEvent();
                tabletBtn.OnClick.AddListener((cursor) =>
                {
                    OnRefreshClicked?.Invoke();
                });
                tabletBtn.SetDisabled(false);
                tabletBtn.SetInteractable(true);
                tabletBtn.buttonType = TabletButton.ButtonType.Simple;
                tabletBtn.ResetStyles();

                _refreshButton = tabletBtn;
            }

            var le = btnObj.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.Destroy(le);

            _tabButtons.Add(btnObj);

            // Loading Spinner Logic
            GameObject playOnline = GameObject.Find("main Buttons/Play Online");
            if (playOnline != null)
            {
                var originalSpinner = playOnline.transform.Find("LoadingSpinner");
                if (originalSpinner != null)
                {
                    _loadingSpinner = UnityEngine.Object.Instantiate(originalSpinner.gameObject, btnObj.transform);
                    _loadingSpinner.name = "RefreshSpinner";
                    _loadingSpinner.SetActive(false);

                    var spinRect = _loadingSpinner.GetComponent<RectTransform>();
                    if (spinRect != null)
                    {
                        spinRect.anchorMin = new Vector2(0.5f, 0.5f);
                        spinRect.anchorMax = new Vector2(0.5f, 0.5f);
                        spinRect.pivot = new Vector2(0.5f, 0.5f);
                        spinRect.anchoredPosition = Vector2.zero;
                        _loadingSpinner.transform.localScale = new Vector3(0.375f, 0.375f, 1f);
                    }
                }
            }
        }
    }
}
