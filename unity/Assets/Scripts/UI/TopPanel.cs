using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Top UI panel displaying moves remaining and score.
    /// Objectives are shown in world space via ObjectiveDisplayController.
    /// </summary>
    public sealed class TopPanel : MonoBehaviour
    {
        private const float PanelHeight = 60f;
        private const int FontSize = 24;
        private const int ScoreFontSize = 28;

        private RectTransform _rect;
        private HorizontalLayoutGroup _layout;
        private TextMeshProUGUI _movesText;
        private TextMeshProUGUI _scoreText;
        private Button _quitButton;
        private Button _prevButton;
        private Button _replayButton;
        private Button _nextButton;

        private int _currentMoves;
        private int _currentScore;

        public event Action OnQuitClicked;
        public event Action OnPrevLevelClicked;
        public event Action OnReplayClicked;
        public event Action OnNextLevelClicked;

        /// <summary>
        /// Initialize the top panel.
        /// </summary>
        public void Initialize()
        {
            _rect = gameObject.AddComponent<RectTransform>();
            UIFactory.SetAnchors(_rect, AnchorPreset.TopStretch);
            _rect.sizeDelta = new Vector2(0, PanelHeight);
            _rect.anchoredPosition = new Vector2(0, 0);

            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // Main layout
            _layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            _layout.spacing = 20f;
            _layout.padding = new RectOffset(20, 20, 10, 10);
            _layout.childAlignment = TextAnchor.MiddleLeft;
            _layout.childControlWidth = false;
            _layout.childControlHeight = true;
            _layout.childForceExpandWidth = false;
            _layout.childForceExpandHeight = false;

            // Quit button (hidden by default, shown in flow mode)
            _quitButton = UIFactory.CreateButton(
                transform, "< Quit", () => OnQuitClicked?.Invoke(),
                new Color(0.4f, 0.4f, 0.45f), Color.white, 18, "QuitButton");
            var quitLayout = _quitButton.gameObject.AddComponent<LayoutElement>();
            quitLayout.preferredWidth = 80;
            quitLayout.preferredHeight = 40;
            _quitButton.gameObject.SetActive(false);

            // Nav buttons: Prev | Replay | Next (hidden by default, shown in flow mode)
            _prevButton = UIFactory.CreateButton(
                transform, "< Prev", () => OnPrevLevelClicked?.Invoke(),
                new Color(0.35f, 0.35f, 0.4f), Color.white, 16, "PrevButton");
            var prevLayout = _prevButton.gameObject.AddComponent<LayoutElement>();
            prevLayout.preferredWidth = 70;
            prevLayout.preferredHeight = 36;
            _prevButton.gameObject.SetActive(false);

            _replayButton = UIFactory.CreateButton(
                transform, "Replay", () => OnReplayClicked?.Invoke(),
                new Color(0.3f, 0.4f, 0.5f), Color.white, 16, "ReplayButton");
            var replayLayout = _replayButton.gameObject.AddComponent<LayoutElement>();
            replayLayout.preferredWidth = 75;
            replayLayout.preferredHeight = 36;
            _replayButton.gameObject.SetActive(false);

            _nextButton = UIFactory.CreateButton(
                transform, "Next >", () => OnNextLevelClicked?.Invoke(),
                new Color(0.35f, 0.35f, 0.4f), Color.white, 16, "NextButton");
            var nextLayout = _nextButton.gameObject.AddComponent<LayoutElement>();
            nextLayout.preferredWidth = 70;
            nextLayout.preferredHeight = 36;
            _nextButton.gameObject.SetActive(false);

            // Spacer to push moves/score to right
            var spacer = new GameObject("Spacer");
            var spacerRect = spacer.AddComponent<RectTransform>();
            spacerRect.SetParent(transform, false);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1;

            // Moves display
            _movesText = UIFactory.CreateText(
                transform,
                "Moves: --",
                FontSize,
                Color.white,
                TextAlignmentOptions.MidlineRight,
                "MovesText");
            var movesLayout = _movesText.gameObject.AddComponent<LayoutElement>();
            movesLayout.preferredWidth = 120;

            // Score display
            _scoreText = UIFactory.CreateText(
                transform,
                "Score: 0",
                ScoreFontSize,
                new Color(1f, 0.85f, 0.3f),
                TextAlignmentOptions.MidlineRight,
                "ScoreText");
            var scoreLayout = _scoreText.gameObject.AddComponent<LayoutElement>();
            scoreLayout.preferredWidth = 150;
        }

        /// <summary>
        /// Update moves remaining display.
        /// </summary>
        public void UpdateMoves(int remaining)
        {
            if (_currentMoves == remaining) return;
            _currentMoves = remaining;
            _movesText.text = $"Moves: {remaining}";

            // Warning color when low on moves
            if (remaining <= 3)
            {
                _movesText.color = new Color(1f, 0.3f, 0.3f);
            }
            else if (remaining <= 5)
            {
                _movesText.color = new Color(1f, 0.7f, 0.3f);
            }
            else
            {
                _movesText.color = Color.white;
            }
        }

        /// <summary>
        /// Update score display.
        /// </summary>
        public void UpdateScore(int score)
        {
            if (_currentScore == score) return;
            _currentScore = score;
            _scoreText.text = $"Score: {score:N0}";
        }

        /// <summary>
        /// Whether the quit button is currently visible.
        /// </summary>
        public bool IsQuitButtonVisible => _quitButton != null && _quitButton.gameObject.activeSelf;

        /// <summary>
        /// Whether the nav buttons are currently visible.
        /// </summary>
        public bool IsNavButtonsVisible => _replayButton != null && _replayButton.gameObject.activeSelf;

        /// <summary>
        /// Show or hide the quit button (used by flow mode).
        /// </summary>
        public void SetQuitButtonVisible(bool visible)
        {
            _quitButton.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Show or hide the nav buttons (Prev, Replay, Next). Used by flow mode.
        /// </summary>
        public void SetNavButtonsVisible(bool visible)
        {
            _prevButton.gameObject.SetActive(visible);
            _replayButton.gameObject.SetActive(visible);
            _nextButton.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Enable/disable Prev button (disabled at first level).
        /// </summary>
        public void SetPrevEnabled(bool enabled)
        {
            _prevButton.interactable = enabled;
        }

        /// <summary>
        /// Enable/disable Next button (disabled at last level).
        /// </summary>
        public void SetNextEnabled(bool enabled)
        {
            _nextButton.interactable = enabled;
        }
    }
}
