using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Main menu: title + PLAY button.
    /// Own Canvas (ScreenSpaceOverlay).
    /// </summary>
    public sealed class MainMenuPanel : MonoBehaviour
    {
        private Canvas _canvas;
        private Button _playButton;

        public event Action OnPlayClicked;

        public void Initialize()
        {
            _canvas = UIFactory.CreateCanvas("MainMenuCanvas");
            _canvas.sortingOrder = 200;
            _canvas.transform.SetParent(transform, false);

            var canvasRect = _canvas.GetComponent<RectTransform>();

            // Dark background
            var bg = UIFactory.CreatePanel(
                _canvas.transform,
                new Color(0.1f, 0.1f, 0.15f, 1f),
                "Background");
            UIFactory.SetAnchors(bg, AnchorPreset.Stretch);
            bg.sizeDelta = Vector2.zero;

            // Center layout
            var layout = UIFactory.CreateVerticalLayout(
                _canvas.transform, 40f, TextAnchor.MiddleCenter, "CenterLayout");
            var layoutRect = layout.GetComponent<RectTransform>();
            UIFactory.SetAnchors(layoutRect, AnchorPreset.Center);
            layoutRect.sizeDelta = new Vector2(400, 300);

            // Title
            var title = UIFactory.CreateText(
                layoutRect, "MATCH 3", 72,
                new Color(1f, 0.85f, 0.3f),
                TextAlignmentOptions.Center, "Title");
            title.fontStyle = FontStyles.Bold;
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 100;
            titleLayout.preferredWidth = 400;

            // PLAY button
            _playButton = UIFactory.CreateButton(
                layoutRect, "PLAY", () => OnPlayClicked?.Invoke(),
                new Color(0.3f, 0.6f, 0.3f), Color.white, 36, "PlayButton");
            var btnLayout = _playButton.gameObject.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = 200;
            btnLayout.preferredHeight = 70;
        }

        public void Show() => _canvas.gameObject.SetActive(true);
        public void Hide() => _canvas.gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
