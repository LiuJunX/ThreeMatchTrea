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
        private ReplayPanel _replayPanel;

        private Match3Bridge _bridge;
        private bool _initialized;
        private bool _quitWasVisible;
        private bool _navWasVisible;

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

            // Create replay panel (hidden by default)
            var replayPanelGo = new GameObject("ReplayPanel");
            replayPanelGo.transform.SetParent(_canvas.transform, false);
            _replayPanel = replayPanelGo.AddComponent<ReplayPanel>();
            _replayPanel.Initialize();

            // Create result panel (hidden by default)
            var resultPanelGo = new GameObject("ResultPanel");
            resultPanelGo.transform.SetParent(_canvas.transform, false);
            _resultPanel = resultPanelGo.AddComponent<ResultPanel>();
            _resultPanel.Initialize();

            _onRestartClickedHandler = () => OnRestartClicked?.Invoke();
            _resultPanel.OnRestartClicked += _onRestartClickedHandler;
            _resultPanel.Hide();
        }

        private bool _autoResultEnabled = true;

        private void SubscribeToBridgeEvents()
        {
            if (_bridge == null) return;

            _bridge.OnMovesChanged += UpdateMoves;
            _bridge.OnScoreChanged += UpdateScore;
            if (_autoResultEnabled)
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
        /// Disable UIManager's automatic result display (flow mode handles it).
        /// Must be called before Initialize or ResubscribeToBridge.
        /// </summary>
        public void SetAutoResultEnabled(bool enabled)
        {
            if (_autoResultEnabled == enabled) return;
            _autoResultEnabled = enabled;

            if (_bridge == null) return;
            if (!enabled)
                _bridge.OnGameEnded -= ShowResult;
            else
                _bridge.OnGameEnded += ShowResult;
        }

        /// <summary>
        /// Re-subscribe to a (potentially new) bridge instance after re-init.
        /// </summary>
        public void ResubscribeToBridge(Match3Bridge bridge)
        {
            UnsubscribeFromBridgeEvents();
            _bridge = bridge;
            SubscribeToBridgeEvents();
        }

        /// <summary>
        /// Replace the Restart button action (e.g. to return to level select).
        /// </summary>
        public void SetRestartAction(Action action)
        {
            if (_resultPanel != null)
            {
                _resultPanel.OnRestartClicked -= _onRestartClickedHandler;
                _onRestartClickedHandler = () => action?.Invoke();
                _resultPanel.OnRestartClicked += _onRestartClickedHandler;
            }
        }

        /// <summary>
        /// Access the result panel for external wiring (e.g. Next/LevelSelect buttons).
        /// </summary>
        public ResultPanel ResultPanel => _resultPanel;

        /// <summary>
        /// Access the top panel for external wiring (e.g. Quit button).
        /// </summary>
        public TopPanel TopPanel => _topPanel;

        /// <summary>
        /// Access the replay panel for external wiring.
        /// </summary>
        public ReplayPanel ReplayPanel => _replayPanel;

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
        /// Show game result panel with star rating (flow mode).
        /// </summary>
        public void ShowResult(bool isVictory, int score, int stars)
        {
            _resultPanel?.Show(isVictory, score, stars);
        }

        /// <summary>
        /// Hide result panel.
        /// </summary>
        public void HideResult()
        {
            _resultPanel?.Hide();
        }

        /// <summary>
        /// Enter replay mode: hide game UI, show replay UI.
        /// </summary>
        public void EnterReplayMode()
        {
            _quitWasVisible = _topPanel != null && _topPanel.IsQuitButtonVisible;
            _navWasVisible = _topPanel != null && _topPanel.IsNavButtonsVisible;
            _bottomPanel?.gameObject.SetActive(false);
            _topPanel?.SetQuitButtonVisible(false);
            _topPanel?.SetNavButtonsVisible(false);
            _replayPanel?.ResetState();
            _replayPanel?.Show();
        }

        /// <summary>
        /// Exit replay mode: restore game UI, hide replay UI.
        /// </summary>
        public void ExitReplayMode()
        {
            _replayPanel?.Hide();
            _bottomPanel?.gameObject.SetActive(true);
            _topPanel?.SetQuitButtonVisible(_quitWasVisible);
            _topPanel?.SetNavButtonsVisible(_navWasVisible);
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
