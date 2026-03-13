using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Bottom UI panel for replay mode.
    /// Layout: thin progress bar on top, controls row below.
    /// [Exit] [Speed: 1.0x ===slider===] [Tick/Total] [Pause] [Restart]
    /// </summary>
    public sealed class ReplayPanel : MonoBehaviour
    {
        private const float PanelHeight = 70f;
        private const float ProgressBarHeight = 8f;
        private const float MinSpeed = 0.1f;
        private const float MaxSpeed = 5.0f;
        private const float DefaultSpeed = 1.0f;
        private const int FontSize = 22;

        private RectTransform _rect;
        private RectTransform _progressBgRect;
        private RectTransform _progressFillRect;
        private readonly List<GameObject> _bookmarkMarkers = new();
        private Slider _speedSlider;
        private TextMeshProUGUI _speedLabel;
        private Button _pauseButton;
        private TextMeshProUGUI _pauseButtonText;
        private Button _exitButton;
        private Button _restartButton;

        public event Action<float> OnSpeedChanged;
        public event Action OnPauseToggled;
        public event Action OnExitClicked;
        public event Action OnRestartClicked;

        public void Initialize()
        {
            _rect = gameObject.AddComponent<RectTransform>();
            UIFactory.SetAnchors(_rect, AnchorPreset.BottomStretch);
            _rect.sizeDelta = new Vector2(0, PanelHeight);
            _rect.anchoredPosition = Vector2.zero;

            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            // Progress bar background (full width, top of panel)
            var progressBgGo = new GameObject("ProgressBg");
            _progressBgRect = progressBgGo.AddComponent<RectTransform>();
            _progressBgRect.SetParent(transform, false);
            _progressBgRect.anchorMin = new Vector2(0, 1);
            _progressBgRect.anchorMax = new Vector2(1, 1);
            _progressBgRect.pivot = new Vector2(0.5f, 1);
            _progressBgRect.sizeDelta = new Vector2(0, ProgressBarHeight);
            _progressBgRect.anchoredPosition = Vector2.zero;
            var progressBg = progressBgGo.AddComponent<Image>();
            progressBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Progress bar fill
            var progressFillGo = new GameObject("ProgressFill");
            _progressFillRect = progressFillGo.AddComponent<RectTransform>();
            _progressFillRect.SetParent(_progressBgRect, false);
            _progressFillRect.anchorMin = Vector2.zero;
            _progressFillRect.anchorMax = new Vector2(0, 1);
            _progressFillRect.pivot = new Vector2(0, 0.5f);
            _progressFillRect.offsetMin = Vector2.zero;
            _progressFillRect.offsetMax = Vector2.zero;
            var progressFill = progressFillGo.AddComponent<Image>();
            progressFill.color = new Color(0.3f, 0.8f, 1f, 1f);

            // Controls row (below progress bar)
            var controlsGo = new GameObject("Controls");
            var controlsRect = controlsGo.AddComponent<RectTransform>();
            controlsRect.SetParent(transform, false);
            controlsRect.anchorMin = Vector2.zero;
            controlsRect.anchorMax = new Vector2(1, 1);
            controlsRect.offsetMin = Vector2.zero;
            controlsRect.offsetMax = new Vector2(0, -ProgressBarHeight);

            var layout = controlsGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Exit button
            _exitButton = UIFactory.CreateButton(
                controlsRect, "Exit",
                () => OnExitClicked?.Invoke(),
                new Color(0.6f, 0.25f, 0.25f, 1f), Color.white, FontSize, "ExitButton");
            var exitLayout = _exitButton.gameObject.AddComponent<LayoutElement>();
            exitLayout.preferredWidth = 70;
            exitLayout.preferredHeight = 40;

            // Speed label
            _speedLabel = UIFactory.CreateText(
                controlsRect, "1.0x", FontSize, Color.white,
                TextAlignmentOptions.MidlineRight, "SpeedLabel");
            var speedLabelLayout = _speedLabel.gameObject.AddComponent<LayoutElement>();
            speedLabelLayout.preferredWidth = 60;

            // Speed slider
            _speedSlider = UIFactory.CreateSlider(controlsRect, MinSpeed, MaxSpeed, DefaultSpeed, "SpeedSlider");
            var sliderLayout = _speedSlider.gameObject.AddComponent<LayoutElement>();
            sliderLayout.preferredWidth = 140;
            sliderLayout.preferredHeight = 26;
            _speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.AddComponent<RectTransform>().SetParent(controlsRect, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Pause button
            _pauseButton = UIFactory.CreateButton(
                controlsRect, "Pause",
                () => OnPauseToggled?.Invoke(),
                new Color(0.25f, 0.35f, 0.6f, 1f), Color.white, FontSize, "PauseButton");
            var pauseLayout = _pauseButton.gameObject.AddComponent<LayoutElement>();
            pauseLayout.preferredWidth = 80;
            pauseLayout.preferredHeight = 40;
            _pauseButtonText = _pauseButton.GetComponentInChildren<TextMeshProUGUI>();

            // Replay button (repeat playback)
            _restartButton = UIFactory.CreateButton(
                controlsRect, "Replay",
                () => OnRestartClicked?.Invoke(),
                new Color(0.2f, 0.55f, 0.35f, 1f), Color.white, FontSize, "ReplayButton");
            var restartLayout = _restartButton.gameObject.AddComponent<LayoutElement>();
            restartLayout.preferredWidth = 80;
            restartLayout.preferredHeight = 40;

            // Hidden by default
            gameObject.SetActive(false);
        }

        private void OnSpeedSliderChanged(float value)
        {
            var rounded = Mathf.Round(value * 10f) / 10f;
            _speedLabel.text = $"{rounded:F1}x";
            OnSpeedChanged?.Invoke(rounded);
        }

        public void SetPaused(bool isPaused)
        {
            if (_pauseButtonText != null)
                _pauseButtonText.text = isPaused ? "Play" : "Pause";

            if (_pauseButton != null)
            {
                var colors = _pauseButton.colors;
                colors.normalColor = isPaused
                    ? new Color(0.6f, 0.25f, 0.25f, 1f)
                    : new Color(0.25f, 0.35f, 0.6f, 1f);
                _pauseButton.colors = colors;

                var image = _pauseButton.GetComponent<Image>();
                if (image != null) image.color = colors.normalColor;
            }
        }

        public void SetCompleted()
        {
            if (_pauseButtonText != null)
                _pauseButtonText.text = "Done";

            if (_pauseButton != null)
            {
                _pauseButton.interactable = false;
                var image = _pauseButton.GetComponent<Image>();
                if (image != null) image.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            }

            // Highlight replay button
            if (_restartButton != null)
            {
                var colors = _restartButton.colors;
                colors.normalColor = new Color(0.15f, 0.7f, 0.4f, 1f);
                _restartButton.colors = colors;
                var image = _restartButton.GetComponent<Image>();
                if (image != null) image.color = colors.normalColor;
            }
        }

        public void ResetState()
        {
            SetPaused(false);

            if (_pauseButton != null)
                _pauseButton.interactable = true;

            if (_restartButton != null)
            {
                var colors = _restartButton.colors;
                colors.normalColor = new Color(0.2f, 0.55f, 0.35f, 1f);
                _restartButton.colors = colors;
                var image = _restartButton.GetComponent<Image>();
                if (image != null) image.color = colors.normalColor;
            }
        }

        /// <summary>
        /// Place bookmark markers on the progress bar.
        /// </summary>
        public void SetBookmarks(IReadOnlyList<int> bookmarkTicks, int totalTicks)
        {
            // Clear old markers
            for (int i = 0; i < _bookmarkMarkers.Count; i++)
                Destroy(_bookmarkMarkers[i]);
            _bookmarkMarkers.Clear();

            if (totalTicks <= 0 || bookmarkTicks == null || bookmarkTicks.Count == 0) return;

            for (int i = 0; i < bookmarkTicks.Count; i++)
            {
                float t = (float)bookmarkTicks[i] / totalTicks;

                var marker = new GameObject($"Bookmark_{i}");
                var rect = marker.AddComponent<RectTransform>();
                rect.SetParent(_progressBgRect, false);
                rect.anchorMin = new Vector2(t, 0);
                rect.anchorMax = new Vector2(t, 1);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(3f, 0); // 3px wide vertical line

                var img = marker.AddComponent<Image>();
                img.color = new Color(1f, 0.85f, 0.2f, 1f); // yellow marker

                _bookmarkMarkers.Add(marker);
            }
        }

        public void UpdateProgress(float progress)
        {
            if (_progressFillRect != null)
                _progressFillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        }

        public void SetSpeed(float speed)
        {
            if (_speedSlider != null)
                _speedSlider.value = Mathf.Clamp(speed, MinSpeed, MaxSpeed);
        }

        public float GetSpeed()
        {
            return _speedSlider != null ? _speedSlider.value : DefaultSpeed;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_speedSlider != null)
                _speedSlider.onValueChanged.RemoveListener(OnSpeedSliderChanged);
        }
    }
}
