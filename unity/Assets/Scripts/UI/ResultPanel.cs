using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Modal panel displaying game result (Victory/Defeat).
    /// Layout:
    /// ┌─────────────────────┐
    /// │      VICTORY!       │
    /// │     ★ ★ ★           │
    /// │    Score: 1250      │
    /// │ [Restart] [Next]    │
    /// │   [Level Select]    │
    /// └─────────────────────┘
    /// </summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        private const float PanelWidth = 400f;
        private const float PanelHeight = 350f;
        private const int TitleFontSize = 48;
        private const int StarsFontSize = 40;
        private const int ScoreFontSize = 32;
        private const int ButtonFontSize = 24;

        private RectTransform _rect;
        private Image _overlay;
        private RectTransform _modalRect;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _starsText;
        private TextMeshProUGUI _scoreText;
        private Button _restartButton;
        private Button _nextLevelButton;
        private Button _levelSelectButton;

        private bool _isVisible;

        public event Action OnRestartClicked;
        public event Action OnNextLevelClicked;
        public event Action OnLevelSelectClicked;

        public void Initialize()
        {
            _rect = gameObject.AddComponent<RectTransform>();
            UIFactory.SetAnchors(_rect, AnchorPreset.Stretch);
            _rect.sizeDelta = Vector2.zero;
            _rect.anchoredPosition = Vector2.zero;

            // Semi-transparent overlay
            _overlay = gameObject.AddComponent<Image>();
            _overlay.color = new Color(0, 0, 0, 0.6f);
            _overlay.raycastTarget = true;

            // Modal container
            var modalPanel = UIFactory.CreatePanel(
                transform,
                new Color(0.15f, 0.15f, 0.2f, 0.95f),
                "ModalPanel");
            _modalRect = modalPanel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(_modalRect, AnchorPreset.Center);
            _modalRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _modalRect.anchoredPosition = Vector2.zero;

            var outline = modalPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1, 1, 1, 0.3f);
            outline.effectDistance = new Vector2(2, -2);

            var layout = modalPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15f;
            layout.padding = new RectOffset(30, 30, 30, 25);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Title
            _titleText = UIFactory.CreateText(
                _modalRect, "VICTORY!", TitleFontSize,
                new Color(1f, 0.85f, 0.3f),
                TextAlignmentOptions.Center, "TitleText");
            _titleText.fontStyle = FontStyles.Bold;
            var titleLayout = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 55;

            // Stars
            _starsText = UIFactory.CreateText(
                _modalRect, "", StarsFontSize,
                new Color(1f, 0.85f, 0.2f),
                TextAlignmentOptions.Center, "StarsText");
            var starsLayout = _starsText.gameObject.AddComponent<LayoutElement>();
            starsLayout.preferredHeight = 45;

            // Score
            _scoreText = UIFactory.CreateText(
                _modalRect, "Score: 0", ScoreFontSize,
                Color.white, TextAlignmentOptions.Center, "ScoreText");
            var scoreLayout = _scoreText.gameObject.AddComponent<LayoutElement>();
            scoreLayout.preferredHeight = 40;

            // Button row (Restart + Next Level)
            var buttonRow = UIFactory.CreateHorizontalLayout(
                _modalRect, 15f, TextAnchor.MiddleCenter, "ButtonRow");
            var buttonRowRect = buttonRow.GetComponent<RectTransform>();
            var buttonRowLayout = buttonRow.gameObject.AddComponent<LayoutElement>();
            buttonRowLayout.preferredHeight = 50;

            _restartButton = UIFactory.CreateButton(
                buttonRowRect, "Restart", () => OnRestartClicked?.Invoke(),
                new Color(0.3f, 0.6f, 0.3f), Color.white, ButtonFontSize, "RestartButton");
            var restartLayout = _restartButton.gameObject.AddComponent<LayoutElement>();
            restartLayout.preferredWidth = 150;
            restartLayout.preferredHeight = 50;

            _nextLevelButton = UIFactory.CreateButton(
                buttonRowRect, "Next Level", () => OnNextLevelClicked?.Invoke(),
                new Color(0.3f, 0.5f, 0.8f), Color.white, ButtonFontSize, "NextLevelButton");
            var nextLayout = _nextLevelButton.gameObject.AddComponent<LayoutElement>();
            nextLayout.preferredWidth = 150;
            nextLayout.preferredHeight = 50;

            // Level Select button
            _levelSelectButton = UIFactory.CreateButton(
                _modalRect, "Level Select", () => OnLevelSelectClicked?.Invoke(),
                new Color(0.4f, 0.4f, 0.4f), Color.white, ButtonFontSize, "LevelSelectButton");
            var selectLayout = _levelSelectButton.gameObject.AddComponent<LayoutElement>();
            selectLayout.preferredWidth = 200;
            selectLayout.preferredHeight = 45;
        }

        /// <summary>
        /// Show result with star rating.
        /// </summary>
        public void Show(bool isVictory, int score, int stars = 0)
        {
            gameObject.SetActive(true);
            _isVisible = true;

            if (isVictory)
            {
                _titleText.text = "VICTORY!";
                _titleText.color = new Color(1f, 0.85f, 0.3f);
            }
            else
            {
                _titleText.text = "DEFEAT";
                _titleText.color = new Color(0.8f, 0.3f, 0.3f);
            }

            // Stars display
            if (isVictory && stars > 0)
            {
                var filled = new string('\u2605', stars);   // ★
                var empty = new string('\u2606', 3 - stars); // ☆
                _starsText.text = filled + empty;
                _starsText.gameObject.SetActive(true);
            }
            else
            {
                _starsText.gameObject.SetActive(false);
            }

            _scoreText.text = $"Score: {score:N0}";

            // Next Level only visible on victory
            _nextLevelButton.gameObject.SetActive(isVictory);
        }

        /// <summary>
        /// Show result without stars (backward compat for non-flow mode).
        /// </summary>
        public void Show(bool isVictory, int score)
        {
            Show(isVictory, score, 0);
            // Hide flow-specific buttons in non-flow mode
            _nextLevelButton.gameObject.SetActive(false);
            _levelSelectButton.gameObject.SetActive(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _isVisible = false;
        }

        /// <summary>
        /// Show or hide the flow-specific buttons (Next Level, Level Select).
        /// </summary>
        public void SetFlowButtonsVisible(bool visible)
        {
            _nextLevelButton.gameObject.SetActive(visible);
            _levelSelectButton.gameObject.SetActive(visible);
        }

        public bool IsVisible => _isVisible;
    }
}
