using System;
using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Systems.Selection;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.UI;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    public enum RenderMode { View2D, View3D }

    /// <summary>
    /// Main game controller.
    /// Manages game loop: tick simulation, render board.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Match3Bridge _bridge;
        [SerializeField] private InputController _inputController;
        [SerializeField] private EffectManager _effectManager;

        [Header("Rendering")]
        [SerializeField] private RenderMode _renderMode = RenderMode.View2D;
        private IBoardView _boardView;

        /// <summary>
        /// Set render mode before Initialize. Used by GameBootstrap.
        /// </summary>
        public RenderMode RenderMode
        {
            get => _renderMode;
            set => _renderMode = value;
        }

        [Header("UI")]
        [SerializeField] private bool _enableUI = true;
        private UIManager _uiManager;

        // Cached delegates for proper unsubscription
        private Action<float> _onSpeedChangedHandler;
        private Action _onPauseToggledHandler;
        private Action _onAutoPlayToggledHandler;
        private Action _onRestartClickedHandler;

        // Replay UI delegates
        private Action<float> _onReplaySpeedChangedHandler;
        private Action _onReplayPauseToggledHandler;
        private Action _onReplayExitHandler;
        private Action _onReplayRestartHandler;
        private Action _onReplayBookmarkToggledHandler;
        private Action<int, int> _onReplayBookmarkMovedHandler;

        // Current replay recording (for restart support)
        private GameRecording _currentReplayRecording;
        private bool _replayCompletedShown;

        // Remember level context for restoring after replay exit
        private string _currentLevelId;

        // Speed steps for keyboard control (both normal and replay modes)
        private static readonly float[] SpeedSteps = { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f, 5.0f };

        [Header("Auto Initialize")]
        [SerializeField] private bool _autoInitialize;

        private bool _initialized;
        private ObjectiveDisplayController _objectiveDisplay;
        private HintController _hintController;
        private int _lastHintedTileId = -1;
        private int _lastHintGeneration = -1;
        private readonly List<int> _hintOutlineTileIds = new();
        private float _hintLightIntensity;
        private float _hintLightTime;
        private const float HintLightMaxIntensity = 3f;
        private const float HintLightFadeSpeed = 16f;
        private static readonly int HintColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int HintColorPropFallback = Shader.PropertyToID("_Color");

        private readonly CameraShakeController _shakeController = new();

        /// <summary>
        /// Bridge instance for external access.
        /// </summary>
        public Match3Bridge Bridge => _bridge;

        /// <summary>
        /// UI manager instance for external access (e.g. GameFlowController).
        /// </summary>
        public UIManager UI => _uiManager;

        /// <summary>
        /// Board view instance.
        /// </summary>
        public IBoardView BoardView => _boardView;

        private void Awake()
        {
            // Auto-create components if not assigned
            if (_bridge == null)
            {
                _bridge = GetComponentInChildren<Match3Bridge>();
                if (_bridge == null)
                {
                    var bridgeGo = new GameObject("Match3Bridge");
                    bridgeGo.transform.SetParent(transform, false);
                    _bridge = bridgeGo.AddComponent<Match3Bridge>();
                }
            }

            if (_inputController == null)
            {
                _inputController = GetComponentInChildren<InputController>();
                if (_inputController == null)
                {
                    var inputGo = new GameObject("InputController");
                    inputGo.transform.SetParent(transform, false);
                    _inputController = inputGo.AddComponent<InputController>();
                }
            }

            if (_effectManager == null)
            {
                _effectManager = GetComponentInChildren<EffectManager>();
                if (_effectManager == null)
                {
                    var effectGo = new GameObject("EffectManager");
                    effectGo.transform.SetParent(transform, false);
                    _effectManager = effectGo.AddComponent<EffectManager>();
                }
            }
        }

        private void Start()
        {
            if (_autoInitialize)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Initialize the game.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Create board view if not yet created
            _boardView ??= CreateBoardView();

            // Initialize bridge
            _bridge.Initialize();

            // Initialize views
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);

            // Initialize objective display (3D mode only)
            InitializeObjectiveDisplay();

            // Initialize input
            _inputController.Initialize(_bridge);
            _inputController.OnUserInput += OnUserInput;

            // Initialize hint system
            _hintController = new HintController(new BridgeHintContext(_bridge));

            // Initialize UI
            if (_enableUI)
            {
                InitializeUI();
            }

            _initialized = true;

            // // 纯 View 匀速掉落对比测试（棋盘右侧）
            // SetupFallingTest();

            Debug.Log("GameController initialized");
        }

        private void InitializeObjectiveDisplay()
        {
            if (_renderMode != RenderMode.View3D) return;
            if (_boardView is not Board3DView board3D) return;

            if (_objectiveDisplay == null)
            {
                var go = new GameObject("ObjectiveDisplay");
                go.transform.SetParent(transform, false);
                _objectiveDisplay = go.AddComponent<ObjectiveDisplayController>();
            }

            _objectiveDisplay.Initialize(_bridge, board3D, _effectManager, _currentLevelId);
            board3D.ObjectiveDisplay = _objectiveDisplay;
        }

        private void InitializeUI()
        {
            // Create UIManager
            var uiGo = new GameObject("UIManager");
            uiGo.transform.SetParent(transform, false);
            _uiManager = uiGo.AddComponent<UIManager>();
            _uiManager.Initialize(_bridge);

            // Create cached delegates for proper cleanup
            _onSpeedChangedHandler = speed => _bridge.GameSpeed = speed;
            _onPauseToggledHandler = () =>
            {
                _bridge.IsPaused = !_bridge.IsPaused;
                _uiManager.SetPaused(_bridge.IsPaused);
            };
            _onAutoPlayToggledHandler = () =>
            {
                _bridge.IsAutoPlaying = !_bridge.IsAutoPlaying;
                _uiManager.SetAutoPlay(_bridge.IsAutoPlaying);
            };
            _onRestartClickedHandler = RestartGame;

            // Wire UI callbacks
            _uiManager.OnSpeedChanged += _onSpeedChangedHandler;
            _uiManager.OnPauseToggled += _onPauseToggledHandler;
            _uiManager.OnAutoPlayToggled += _onAutoPlayToggledHandler;
            _uiManager.OnRestartClicked += _onRestartClickedHandler;

            // Wire replay panel callbacks
            var replayPanel = _uiManager.ReplayPanel;
            if (replayPanel != null)
            {
                _onReplaySpeedChangedHandler = speed =>
                {
                    var ctrl = _bridge.ReplayCtrl;
                    if (ctrl != null) ctrl.PlaybackSpeed = speed;
                };
                _onReplayPauseToggledHandler = () =>
                {
                    var ctrl = _bridge.ReplayCtrl;
                    if (ctrl != null)
                    {
                        ctrl.TogglePause();
                        replayPanel.SetPaused(ctrl.State == ReplayState.Paused);
                    }
                };
                _onReplayExitHandler = ExitReplay;
                _onReplayRestartHandler = RestartReplay;
                _onReplayBookmarkToggledHandler = ToggleReplayBookmark;
                _onReplayBookmarkMovedHandler = MoveReplayBookmark;

                replayPanel.OnSpeedChanged += _onReplaySpeedChangedHandler;
                replayPanel.OnPauseToggled += _onReplayPauseToggledHandler;
                replayPanel.OnExitClicked += _onReplayExitHandler;
                replayPanel.OnRestartClicked += _onReplayRestartHandler;
                replayPanel.OnBookmarkToggled += _onReplayBookmarkToggledHandler;
                replayPanel.OnBookmarkMoved += _onReplayBookmarkMovedHandler;
            }
        }

        /// <summary>
        /// Initialize with specific parameters.
        /// </summary>
        public void Initialize(int width, int height, int seed)
        {
            if (_initialized)
            {
                Reset();
            }

            // Create board view if not yet created
            _boardView ??= CreateBoardView();

            // Initialize bridge with parameters
            _bridge.Initialize(width, height, seed);

            // Initialize views
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);

            // Initialize objective display (3D mode only)
            InitializeObjectiveDisplay();

            // Initialize input
            _inputController.Initialize(_bridge);
            _inputController.OnUserInput -= OnUserInput; // prevent double-subscribe
            _inputController.OnUserInput += OnUserInput;

            // Initialize hint system
            _hintController = new HintController(new BridgeHintContext(_bridge));

            // Initialize UI
            if (_enableUI && _uiManager == null)
            {
                InitializeUI();
            }
            else if (_uiManager != null)
            {
                _uiManager.ResubscribeToBridge(_bridge);
                _uiManager.HideResult();
            }

            _initialized = true;

            // // 纯 View 匀速掉落对比测试（棋盘右侧）
            // SetupFallingTest();
        }

        /// <summary>
        /// Initialize with a named level.
        /// </summary>
        public void InitializeWithLevel(string levelId, int seed)
        {
            if (_initialized)
            {
                Reset();
            }

            _currentLevelId = levelId;
            _boardView ??= CreateBoardView();

            _bridge.Initialize(seed, levelId);

            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);
            InitializeObjectiveDisplay();
            _inputController.Initialize(_bridge);
            _inputController.OnUserInput -= OnUserInput;
            _inputController.OnUserInput += OnUserInput;

            _hintController = new HintController(new BridgeHintContext(_bridge));

            if (_enableUI && _uiManager == null)
            {
                InitializeUI();
            }
            else if (_uiManager != null)
            {
                _uiManager.ResubscribeToBridge(_bridge);
                _uiManager.HideResult();
            }

            _initialized = true;

            // // 纯 View 匀速掉落对比测试（棋盘右侧）
            // SetupFallingTest();
        }

        /// <summary>
        /// Restart the game with the same parameters.
        /// </summary>
        public void RestartGame()
        {
            var width = _bridge.Width;
            var height = _bridge.Height;
            var newSeed = System.Environment.TickCount;

            Reset();
            Initialize(width, height, newSeed);

            // Re-frame camera in case board dimensions changed (e.g. after replay)
            var cameraSetup = FindObjectOfType<CameraSetup>();
            if (cameraSetup != null)
                cameraSetup.SetupCamera();

            Debug.Log($"Game restarted with seed: {newSeed}");
        }

        /// <summary>
        /// Restart the game, preserving level context if one was active.
        /// </summary>
        private void RestartWithLevel()
        {
            var newSeed = System.Environment.TickCount;

            if (_currentLevelId != null)
            {
                Reset();
                InitializeWithLevel(_currentLevelId, newSeed);
            }
            else
            {
                var width = _bridge.Width;
                var height = _bridge.Height;
                Reset();
                Initialize(width, height, newSeed);
            }

            var cameraSetup = FindObjectOfType<CameraSetup>();
            if (cameraSetup != null)
                cameraSetup.SetupCamera();

            Debug.Log($"Game restarted with seed: {newSeed}, level: {_currentLevelId ?? "(none)"}");
        }

        /// <summary>
        /// Start replay mode with a recording.
        /// Reuses existing BoardView and rendering pipeline.
        /// </summary>
        public void StartReplay(GameRecording recording)
        {
            if (recording == null) return;

            _currentReplayRecording = recording;
            _replayCompletedShown = false;

            // Disable input during replay
            if (_inputController != null)
                _inputController.enabled = false;

            _hintController?.SetEnabled(false);

            // Create board view if needed
            _boardView ??= CreateBoardView();

            // Clear stale tile views before starting new replay
            _boardView.Clear();

            // Start replay on bridge
            _bridge.StartReplay(recording);

            // Re-initialize views with new dimensions
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);

            // Re-frame camera for replay board dimensions
            var cameraSetup = FindObjectOfType<CameraSetup>();
            if (cameraSetup != null)
                cameraSetup.SetupCamera();

            // Subscribe to replay controller events
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl != null)
            {
                ctrl.BookmarkHit += OnBookmarkHit;
                ctrl.BookmarksChanged += RefreshReplayBookmarkMarkers;
            }

            // Enter replay UI mode
            _uiManager?.EnterReplayMode();
            _uiManager?.ReplayPanel?.SetBookmarks(
                ctrl?.Bookmarks ?? recording.Bookmarks,
                recording.DurationTicks);

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || !_bridge.IsInitialized) return;

            // F5: add bookmark during gameplay
            if (!_bridge.IsReplaying && Input.GetKeyDown(KeyCode.F5))
            {
                _bridge.AddBookmark();
            }

            // F6: replay latest recording
            if (!_bridge.IsReplaying && Input.GetKeyDown(KeyCode.F6))
            {
                ReplayLatest();
            }

            // Handle keyboard controls (both modes)
            if (_bridge.IsReplaying)
                HandleReplayInput();
            else
                HandleNormalKeyboardInput();

            // Restore camera before input processing (InputController.Update runs in same frame)
            _shakeController.Restore();

            // Tick simulation
            _bridge.Tick(Time.smoothDeltaTime);

            float viewDt = _bridge.ScaledDeltaTime;

            var state = _bridge.VisualState;
            if (state == null) return;

            // Render board
            _boardView.Render(state, viewDt);

            // Update hint system (disabled during replay)
            if (_hintController != null && !_bridge.IsReplaying)
            {
                bool gameInProgress = _bridge.CurrentState.LevelStatus == Core.Models.Enums.LevelStatus.InProgress;
                bool canHint = viewDt > 0f && !_bridge.IsAutoPlaying && gameInProgress;
                _hintController.SetEnabled(canHint);
                if (canHint)
                    _hintController.Update(viewDt);
                ApplyHintToView(_hintController.CurrentHint, viewDt);
            }

            // Update effects
            _effectManager.UpdateEffects(state);

            // Update fly-to-objective animations
            _objectiveDisplay?.UpdateFlies(viewDt);

            // Screen shake: only trigger on rising edge of effect count
            _shakeController.UpdateEffectCount(state);
        }

        private void HandleReplayInput()
        {
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl == null) return;

            // Space: toggle pause
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // If completed, restart instead of toggling pause
                if (ctrl.State == ReplayState.Completed)
                {
                    RestartReplay();
                    return;
                }
                ctrl.TogglePause();
                _uiManager?.ReplayPanel?.SetPaused(ctrl.State == ReplayState.Paused);
            }

            // F5: toggle bookmark at current tick
            if (Input.GetKeyDown(KeyCode.F5))
                ToggleReplayBookmark();

            // Delete: remove selected bookmark
            if (Input.GetKeyDown(KeyCode.Delete))
                DeleteSelectedBookmark();

            // Left/Right arrows: adjust speed
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                var newSpeed = StepSpeed(ctrl.PlaybackSpeed, +1);
                ctrl.PlaybackSpeed = newSpeed;
                _uiManager?.ReplayPanel?.SetSpeed(newSpeed);
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                var newSpeed = StepSpeed(ctrl.PlaybackSpeed, -1);
                ctrl.PlaybackSpeed = newSpeed;
                _uiManager?.ReplayPanel?.SetSpeed(newSpeed);
            }

            // Escape: exit replay
            if (Input.GetKeyDown(KeyCode.Escape))
                ExitReplay();

            // R: restart replay
            if (Input.GetKeyDown(KeyCode.R))
                RestartReplay();

            // Update replay panel progress
            var replayPanel = _uiManager?.ReplayPanel;
            if (replayPanel != null)
            {
                replayPanel.UpdateProgress(ctrl.Progress);

                // Show completed state once
                if (!_replayCompletedShown && ctrl.State == ReplayState.Completed)
                {
                    _replayCompletedShown = true;
                    replayPanel.SetCompleted();
                }
            }
        }

        private void HandleNormalKeyboardInput()
        {
            // Space: toggle pause
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _bridge.IsPaused = !_bridge.IsPaused;
                _uiManager?.SetPaused(_bridge.IsPaused);
            }

            // Left/Right arrows: adjust game speed
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                var newSpeed = StepSpeed(_bridge.GameSpeed, +1);
                _bridge.GameSpeed = newSpeed;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                var newSpeed = StepSpeed(_bridge.GameSpeed, -1);
                _bridge.GameSpeed = newSpeed;
            }
        }

        private void ExitReplay()
        {
            // Unsubscribe from replay controller events before stopping
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl != null)
            {
                ctrl.BookmarkHit -= OnBookmarkHit;
                ctrl.BookmarksChanged -= RefreshReplayBookmarkMarkers;
            }

            _bridge.StopReplay();
            _uiManager?.ExitReplayMode();
            if (_inputController != null)
                _inputController.enabled = true;
            _currentReplayRecording = null;
            Debug.Log("[Replay] Stopped by user. Restarting game...");
            RestartWithLevel();
        }

        private void RestartReplay()
        {
            if (_currentReplayRecording == null) return;
            Debug.Log("[Replay] Restarting...");
            _replayCompletedShown = false;

            // Preserve current speed and bookmarks before creating new controller
            var currentSpeed = _uiManager?.ReplayPanel?.GetSpeed() ?? 1.0f;
            var oldCtrl = _bridge.ReplayCtrl;
            List<int> preservedBookmarks = null;
            if (oldCtrl != null)
            {
                preservedBookmarks = new List<int>(oldCtrl.Bookmarks);
                oldCtrl.BookmarkHit -= OnBookmarkHit;
                oldCtrl.BookmarksChanged -= RefreshReplayBookmarkMarkers;
            }

            _boardView?.Clear();
            _bridge.StartReplay(_currentReplayRecording);
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);
            _uiManager?.ReplayPanel?.ResetState();

            // Restore speed and bookmarks to the new ReplayController
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl != null)
            {
                ctrl.PlaybackSpeed = currentSpeed;
                ctrl.BookmarkHit += OnBookmarkHit;
                ctrl.BookmarksChanged += RefreshReplayBookmarkMarkers;

                // Restore bookmarks from previous session
                if (preservedBookmarks != null)
                {
                    foreach (var tick in preservedBookmarks)
                        ctrl.ToggleBookmark(tick);
                }
            }

            RefreshReplayBookmarkMarkers();

            var cameraSetup = FindObjectOfType<CameraSetup>();
            if (cameraSetup != null)
                cameraSetup.SetupCamera();
        }

        private void ToggleReplayBookmark()
        {
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl == null) return;

            bool added = ctrl.ToggleBookmark(ctrl.CurrentTick);
            Debug.Log(added
                ? $"[Replay] Bookmark added at tick {ctrl.CurrentTick}"
                : $"[Replay] Bookmark removed near tick {ctrl.CurrentTick}");
        }

        private void MoveReplayBookmark(int oldTick, int newTick)
        {
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl == null) return;

            ctrl.MoveBookmark(oldTick, newTick);
            Debug.Log($"[Replay] Bookmark moved: tick {oldTick} → {newTick}");
        }

        private void DeleteSelectedBookmark()
        {
            var replayPanel = _uiManager?.ReplayPanel;
            if (replayPanel == null) return;

            int tick = replayPanel.SelectedBookmarkTick;
            if (tick < 0) return;

            var ctrl = _bridge.ReplayCtrl;
            if (ctrl == null) return;

            replayPanel.ClearBookmarkSelection();
            ctrl.RemoveBookmarkNear(tick);
            Debug.Log($"[Replay] Bookmark deleted at tick {tick}");
        }

        private void RefreshReplayBookmarkMarkers()
        {
            var ctrl = _bridge.ReplayCtrl;
            if (ctrl == null) return;
            _uiManager?.ReplayPanel?.SetBookmarks(ctrl.Bookmarks, ctrl.TotalTicks);

            // Persist updated bookmarks to disk
            PersistBookmarks(ctrl);
        }

        private void PersistBookmarks(ReplayController ctrl)
        {
            if (_currentReplayRecording == null || ctrl == null) return;

            var updatedBookmarks = new int[ctrl.Bookmarks.Count];
            for (int i = 0; i < ctrl.Bookmarks.Count; i++)
                updatedBookmarks[i] = ctrl.Bookmarks[i];

            _currentReplayRecording = _currentReplayRecording with { Bookmarks = updatedBookmarks };
            Match3Bridge.OverwriteLatestRecording(_currentReplayRecording);
        }

        private void OnBookmarkHit(int tick)
        {
            Debug.Log($"[Replay] Hit bookmark at tick {tick} — auto-paused");
            _uiManager?.ReplayPanel?.SetPaused(true);
        }

        private void ReplayLatest()
        {
            var recording = Match3Bridge.LoadLatestRecording();
            if (recording == null) { Debug.LogWarning("[Replay] No recordings found."); return; }
            StartReplay(recording);
        }

        private static float StepSpeed(float current, int direction)
        {
            if (direction > 0)
            {
                for (int i = 0; i < SpeedSteps.Length; i++)
                    if (SpeedSteps[i] > current + 0.01f)
                        return SpeedSteps[i];
                return SpeedSteps[SpeedSteps.Length - 1];
            }
            else
            {
                for (int i = SpeedSteps.Length - 1; i >= 0; i--)
                    if (SpeedSteps[i] < current - 0.01f)
                        return SpeedSteps[i];
                return SpeedSteps[0];
            }
        }

        private void LateUpdate()
        {
            // Apply shake in LateUpdate so it doesn't affect input raycasts in Update
            _shakeController.Apply();
        }

        #region Debug Overlay

        private void OnGUI()
        {
            if (!_initialized || !_bridge.IsInitialized) return;

            if (!_bridge.IsReplaying)
                DrawGameOverlay();
        }

        private void DrawGameOverlay()
        {
            // Bookmark button — top-right, below TopPanel
            var btnRect = new Rect(Screen.width - 110, 40, 100, 36);
            if (GUI.Button(btnRect, "Bookmark (F5)"))
            {
                _bridge.AddBookmark();
            }
        }

        #endregion

        /// <summary>
        /// Reset the game.
        /// </summary>
        public void Reset()
        {
            _shakeController.Reset();

            // Stop auto-play on reset
            _bridge.IsAutoPlaying = false;
            _uiManager?.SetAutoPlay(false);

            _hintController?.SetEnabled(false);
            _lastHintedTileId = -1;
            _lastHintGeneration = -1;
            _hintOutlineTileIds.Clear();
            _hintLightIntensity = 0f;
            _hintLightTime = 0f;

            _objectiveDisplay?.Clear();
            _boardView?.Clear();
            _effectManager?.Clear();
            _uiManager?.HideResult();

            // Restore input (may have been disabled by StartReplay)
            if (_inputController != null)
                _inputController.enabled = true;

            _initialized = false;
        }

        private Views.FallingTestView _fallingTest;

        private void SetupFallingTest()
        {
            // 清理旧的（Restart 时会重建）
            if (_fallingTest != null)
            {
                Destroy(_fallingTest);
                _fallingTest = null;
            }

            _fallingTest = gameObject.AddComponent<Views.FallingTestView>();
            _fallingTest.Setup(_bridge);
        }

        private IBoardView CreateBoardView()
        {
            switch (_renderMode)
            {
                case RenderMode.View3D:
                    var go3D = new GameObject("Board3DView");
                    go3D.transform.SetParent(transform, false);
                    return go3D.AddComponent<Board3DView>();
                default:
                    var go2D = new GameObject("BoardView");
                    go2D.transform.SetParent(transform, false);
                    return go2D.AddComponent<BoardView>();
            }
        }

        private void OnDestroy()
        {
            _shakeController.Restore();

            if (_inputController != null)
            {
                _inputController.OnUserInput -= OnUserInput;
            }

            if (_objectiveDisplay != null)
            {
                Destroy(_objectiveDisplay.gameObject);
                _objectiveDisplay = null;
            }

            // Unsubscribe from replay controller events
            var replayCtrl = _bridge?.ReplayCtrl;
            if (replayCtrl != null)
            {
                replayCtrl.BookmarkHit -= OnBookmarkHit;
                replayCtrl.BookmarksChanged -= RefreshReplayBookmarkMarkers;
            }

            // Unsubscribe from UI events to prevent memory leaks
            if (_uiManager != null)
            {
                _uiManager.OnSpeedChanged -= _onSpeedChangedHandler;
                _uiManager.OnPauseToggled -= _onPauseToggledHandler;
                _uiManager.OnAutoPlayToggled -= _onAutoPlayToggledHandler;
                _uiManager.OnRestartClicked -= _onRestartClickedHandler;

                var replayPanel = _uiManager.ReplayPanel;
                if (replayPanel != null)
                {
                    replayPanel.OnSpeedChanged -= _onReplaySpeedChangedHandler;
                    replayPanel.OnPauseToggled -= _onReplayPauseToggledHandler;
                    replayPanel.OnExitClicked -= _onReplayExitHandler;
                    replayPanel.OnRestartClicked -= _onReplayRestartHandler;
                    replayPanel.OnBookmarkToggled -= _onReplayBookmarkToggledHandler;
                    replayPanel.OnBookmarkMoved -= _onReplayBookmarkMovedHandler;
                }

                Destroy(_uiManager.gameObject);
                _uiManager = null;
            }
        }

        private void OnUserInput()
        {
            _hintController?.OnUserInput();
        }

        private void ApplyHintToView(HintResult hint, float dt)
        {
            var gen = _hintController.HintGeneration;
            bool hintChanged = gen != _lastHintGeneration;

            if (!hint.IsActive || hint.TileId < 0)
            {
                if (_lastHintedTileId >= 0)
                {
                    ClearHintOnTile(_lastHintedTileId);
                    _lastHintedTileId = -1;
                }
                ClearHintOutlines();
                _lastHintGeneration = gen;
                // Fade out hint light
                UpdateHintLight(null, hint, hintChanged, dt);
                return;
            }

            // On hint change: clear old tile, apply new hint (resets _hintTime)
            if (hintChanged)
            {
                if (_lastHintedTileId >= 0)
                    ClearHintOnTile(_lastHintedTileId);

                var nudgeDir = Vector2.zero;
                if (hint.Type == HintAnimationType.SwapNudge)
                {
                    nudgeDir = new Vector2(hint.To.X - hint.From.X, -(hint.To.Y - hint.From.Y));
                    if (nudgeDir.sqrMagnitude > 0.001f)
                        nudgeDir.Normalize();
                }

                if (_boardView is Board3DView b3d && b3d.TryGetTileView(hint.TileId, out var tile3D))
                    tile3D.SetHinted(true, hint.Type, nudgeDir);
                else if (_boardView is BoardView b2d && b2d.TryGetTileView(hint.TileId, out var tile2D))
                    tile2D.SetHinted(true, hint.Type, nudgeDir);

                _lastHintedTileId = hint.TileId;
                _lastHintGeneration = gen;

                // Outline both swap tiles
                ClearHintOutlines();
                if (hint.Type == HintAnimationType.SwapNudge)
                    ApplySwapOutlines(hint);
            }

            // Update hint light position every frame + fade in
            if (_boardView is Board3DView board3DView && board3DView.TryGetTileView(hint.TileId, out var tile))
            {
                UpdateHintLight(tile, hint, hintChanged, dt);
            }
        }

        private void UpdateHintLight(Tile3DView tile, HintResult hint, bool colorChanged, float dt)
        {
            if (!(_boardView is Board3DView board3DView)) return;
            var hintLight = board3DView.GetLightingController()?.HintLight;
            if (hintLight == null) return;

            bool active = hint.IsActive && hint.TileId >= 0;

            // Compute target intensity
            float target;
            if (active)
            {
                _hintLightTime += dt;
                if (hint.Type == HintAnimationType.BombPulse)
                {
                    var pulse = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(_hintLightTime * 2f * Mathf.PI * 2f) + 1f) * 0.5f);
                    target = HintLightMaxIntensity * pulse;
                }
                else
                {
                    target = HintLightMaxIntensity;
                }
            }
            else
            {
                target = 0f;
            }

            // Smooth tracking: fade in follows pulse, fade out decays from current value
            _hintLightIntensity = Mathf.Lerp(_hintLightIntensity, target, HintLightFadeSpeed * dt);
            if (_hintLightIntensity < 0.01f) { _hintLightIntensity = 0f; _hintLightTime = 0f; }

            var intensity = _hintLightIntensity;
            hintLight.intensity = intensity;

            if (intensity > 0.01f)
            {
                hintLight.enabled = true;
                if (tile != null)
                {
                    var tilePos = tile.transform.position;
                    hintLight.transform.position = new Vector3(tilePos.x, tilePos.y, tilePos.z - 1f);
                    if (colorChanged)
                    {
                        var mat = tile.GetComponent<MeshRenderer>().sharedMaterial;
                        hintLight.color = mat.HasProperty(HintColorProp)
                            ? mat.GetColor(HintColorProp)
                            : mat.GetColor(HintColorPropFallback);
                    }
                }
            }
            else
            {
                hintLight.enabled = false;
            }
        }

        private void ApplySwapOutlines(HintResult hint)
        {
            if (!(_boardView is Board3DView b3d)) return;

            // Get all positions in the match group (mapped to pre-swap coordinates)
            var positions = _bridge.GetHintMatchPositions(hint.From, hint.To);

            for (int i = 0; i < positions.Count; i++)
            {
                var tileId = _bridge.GetTileIdAt(positions[i]);
                if (tileId >= 0 && b3d.TryGetTileView(tileId, out var tile))
                {
                    tile.SetOutlined(true);
                    _hintOutlineTileIds.Add(tileId);
                }
            }
        }

        private void ClearHintOutlines()
        {
            if (_hintOutlineTileIds.Count == 0) return;
            if (!(_boardView is Board3DView b3d)) return;

            for (int i = 0; i < _hintOutlineTileIds.Count; i++)
            {
                if (b3d.TryGetTileView(_hintOutlineTileIds[i], out var tile))
                    tile.SetOutlined(false);
            }
            _hintOutlineTileIds.Clear();
        }

        private void ClearHintOnTile(int tileId)
        {
            if (_boardView is Board3DView b3d)
            {
                if (b3d.TryGetTileView(tileId, out var tile3D))
                    tile3D.SetHinted(false);
            }
            else if (_boardView is BoardView b2d)
            {
                if (b2d.TryGetTileView(tileId, out var tile2D))
                    tile2D.SetHinted(false);
            }
        }

        private sealed class BridgeHintContext : IHintContext
        {
            private readonly Match3Bridge _bridge;

            public BridgeHintContext(Match3Bridge bridge)
            {
                _bridge = bridge;
            }

            public bool IsIdle => _bridge.IsIdle();

            public bool HasSelection => _bridge.CurrentState.SelectedPosition != Position.Invalid;

            public void ClearSelection() => _bridge.ClearSelection();

            public bool TryGetHintMove(out int actionType, out Position from, out Position to)
            {
                if (_bridge.TryGetHintMove(out var action))
                {
                    actionType = (int)action.ActionType;
                    if (action.ActionType == MoveActionType.Swap)
                    {
                        var highlight = _bridge.GetHintHighlightPosition(action);
                        // Swap from/to so 'from' is always the highlighted tile
                        if (highlight == action.To)
                        {
                            from = action.To;
                            to = action.From;
                        }
                        else
                        {
                            from = action.From;
                            to = action.To;
                        }
                    }
                    else
                    {
                        from = action.From;
                        to = action.To;
                    }
                    return true;
                }
                actionType = 0;
                from = default;
                to = default;
                return false;
            }

            public int GetTileIdAt(Position pos) => _bridge.GetTileIdAt(pos);
        }
    }
}
