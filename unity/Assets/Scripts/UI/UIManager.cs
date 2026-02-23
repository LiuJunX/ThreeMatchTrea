using System;
using Match3.Unity.Bridge;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Manages all game UI panels.
    /// Creates and coordinates TopPanel, BottomPanel, and ResultPanel.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private Canvas _canvas;
        private TopPanel _topPanel;
        private BottomPanel _bottomPanel;
        private ResultPanel _resultPanel;

        private Match3Bridge _bridge;
        private bool _initialized;

        // Cached delegates for proper unsubscription
        private Action<float> _onSpeedChangedHandler;
        private Action _onPauseToggledHandler;
        private Action _onAutoPlayToggledHandler;
        private Action _onRestartClickedHandler;

        /// <summary>
        /// Event fired when game speed is changed via UI.
        /// </summary>
        public event Action<float> OnSpeedChanged;

        /// <summary>
        /// Event fired when pause is toggled via UI.
        /// </summary>
        public event Action OnPauseToggled;

        /// <summary>
        /// Event fired when auto-play is toggled via UI.
        /// </summary>
        public event Action OnAutoPlayToggled;

        /// <summary>
        /// Event fired when restart is clicked via UI.
        /// </summary>
        public event Action OnRestartClicked;

        /// <summary>
        /// Initialize the UI manager.
        /// </summary>
        public void Initialize(Match3Bridge bridge)
        {
            if (_initialized) return;

            _bridge = bridge;

            CreateUI();
            SubscribeToBridgeEvents();

            _initialized = true;

            Debug.Log("UIManager initialized");
        }

        private void CreateUI()
        {
            // Ensure EventSystem exists (required for UI interaction)
            EnsureEventSystem();

            // Create main canvas
            _canvas = UIFactory.CreateCanvas("GameUI");
            _canvas.transform.SetParent(transform, false);

            var canvasRect = _canvas.GetComponent<RectTransform>();

            // Create top panel
            var topPanelGo = new GameObject("TopPanel");
            topPanelGo.transform.SetParent(_canvas.transform, false);
            _topPanel = topPanelGo.AddComponent<TopPanel>();
            _topPanel.Initialize();

            // Create bottom panel
            var bottomPanelGo = new GameObject("BottomPanel");
            bottomPanelGo.transform.SetParent(_canvas.transform, false);
            _bottomPanel = bottomPanelGo.AddComponent<BottomPanel>();
            _bottomPanel.Initialize();

            // Wire bottom panel events (save delegates for proper cleanup)
            _onSpeedChangedHandler = speed => OnSpeedChanged?.Invoke(speed);
            _onPauseToggledHandler = () => OnPauseToggled?.Invoke();
            _onAutoPlayToggledHandler = () => OnAutoPlayToggled?.Invoke();

            _bottomPanel.OnSpeedChanged += _onSpeedChangedHandler;
            _bottomPanel.OnPauseToggled += _onPauseToggledHandler;
            _bottomPanel.OnAutoPlayToggled += _onAutoPlayToggledHandler;

            // Create result panel (hidden by default)
            var resultPanelGo = new GameObject("ResultPanel");
            resultPanelGo.transform.SetParent(_canvas.transform, false);
            _resultPanel = resultPanelGo.AddComponent<ResultPanel>();
            _resultPanel.Initialize();

            _onRestartClickedHandler = () => OnRestartClicked?.Invoke();
            _resultPanel.OnRestartClicked += _onRestartClickedHandler;
            _resultPanel.Hide();
        }

        private void SubscribeToBridgeEvents()
        {
            if (_bridge == null) return;

            _bridge.OnMovesChanged += UpdateMoves;
            _bridge.OnScoreChanged += UpdateScore;
            _bridge.OnGameEnded += ShowResult;
        }

        private void UnsubscribeFromBridgeEvents()
        {
            if (_bridge == null) return;

            _bridge.OnMovesChanged -= UpdateMoves;
            _bridge.OnScoreChanged -= UpdateScore;
            _bridge.OnGameEnded -= ShowResult;
        }

        /// <summary>
        /// Update moves remaining display.
        /// </summary>
        public void UpdateMoves(int remaining)
        {
            _topPanel?.UpdateMoves(remaining);
        }

        /// <summary>
        /// Update score display.
        /// </summary>
        public void UpdateScore(int score)
        {
            _topPanel?.UpdateScore(score);
        }

        /// <summary>
        /// Show game result panel.
        /// </summary>
        public void ShowResult(bool isVictory, int score)
        {
            _resultPanel?.Show(isVictory, score);
        }

        /// <summary>
        /// Hide result panel.
        /// </summary>
        public void HideResult()
        {
            _resultPanel?.Hide();
        }

        /// <summary>
        /// Update pause button state.
        /// </summary>
        public void SetPaused(bool isPaused)
        {
            _bottomPanel?.SetPaused(isPaused);
        }

        /// <summary>
        /// Update auto-play button state.
        /// </summary>
        public void SetAutoPlay(bool isAutoPlaying)
        {
            _bottomPanel?.SetAutoPlay(isAutoPlaying);
        }

        private void EnsureEventSystem()
        {
            // Check if EventSystem already exists
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            // Create EventSystem
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            Debug.Log("EventSystem created for UI interaction");
        }

        private void OnDestroy()
        {
            UnsubscribeFromBridgeEvents();

            // Unsubscribe from panel events
            if (_bottomPanel != null)
            {
                _bottomPanel.OnSpeedChanged -= _onSpeedChangedHandler;
                _bottomPanel.OnPauseToggled -= _onPauseToggledHandler;
                _bottomPanel.OnAutoPlayToggled -= _onAutoPlayToggledHandler;
            }

            if (_resultPanel != null)
            {
                _resultPanel.OnRestartClicked -= _onRestartClickedHandler;
            }

            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }
    }

    /// <summary>
    /// Represents progress of a game objective.
    /// </summary>
    public struct ObjectiveProgress
    {
        public string Type;
        public int Current;
        public int Target;
        public Color Color;

        public bool IsCompleted => Current >= Target;
    }
}
