using System;
using Match3.Core.Models.Grid;
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

        [Header("Auto Initialize")]
        [SerializeField] private bool _autoInitialize;

        private bool _initialized;
        private ObjectiveDisplayController _objectiveDisplay;
        private HintController _hintController;
        private int _lastHintedTileId = -1;
        private int _lastHintGeneration = -1;
        private float _hintLightIntensity;
        private float _hintLightTime;
        private const float HintLightMaxIntensity = 3f;
        private const float HintLightFadeSpeed = 16f;
        private static readonly int HintColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int HintColorPropFallback = Shader.PropertyToID("_Color");

        // Screen shake state
        private Camera _cachedCamera;
        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector3 _cameraOriginalPos;
        private bool _shakeApplied;
        private int _lastShakeEffectCount;

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

            _objectiveDisplay.Initialize(_bridge, board3D, _effectManager);
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

            Debug.Log($"Game restarted with seed: {newSeed}");
        }

        private void Update()
        {
            if (!_initialized || !_bridge.IsInitialized) return;

            // Restore camera before input processing (InputController.Update runs in same frame)
            RestoreShake();

            // Tick simulation
            _bridge.Tick(Time.deltaTime);

            var state = _bridge.VisualState;
            if (state == null) return;

            // Render board
            _boardView.Render(state);

            // Update hint system
            if (_hintController != null)
            {
                bool gameInProgress = _bridge.CurrentState.LevelStatus == Core.Models.Enums.LevelStatus.InProgress;
                bool canHint = !_bridge.IsPaused && !_bridge.IsAutoPlaying && gameInProgress;
                _hintController.SetEnabled(canHint);
                if (canHint)
                    _hintController.Update(Time.deltaTime);
                ApplyHintToView(_hintController.CurrentHint);
            }

            // Update effects
            _effectManager.UpdateEffects(state);

            // Update fly-to-objective animations
            _objectiveDisplay?.UpdateFlies(Time.deltaTime);

            // Screen shake: only trigger on rising edge of effect count
            int effectCount = CountDestructionEffects(state);
            if (effectCount > _lastShakeEffectCount)
            {
                if (effectCount >= 10)
                    TriggerShake(0.2f, 0.15f);
                else if (effectCount >= 5)
                    TriggerShake(0.1f, 0.08f);
            }
            _lastShakeEffectCount = effectCount;
        }

        private void LateUpdate()
        {
            // Apply shake in LateUpdate so it doesn't affect input raycasts in Update
            ApplyShake();
        }

        private static int CountDestructionEffects(VisualState state)
        {
            int count = 0;
            foreach (var effect in state.Effects)
            {
                if (effect.EffectType == "explosion" || effect.EffectType == "bomb_explosion")
                {
                    count++;
                }
            }
            return count;
        }

        private Camera GetCamera()
        {
            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        private void TriggerShake(float duration, float intensity)
        {
            // Only upgrade to a stronger shake
            if (_shakeTimer > 0f && intensity <= _shakeIntensity) return;

            _shakeDuration = duration;
            _shakeIntensity = intensity;
            _shakeTimer = duration;

            // Capture original position only if not already shaking
            // (if shaking, _cameraOriginalPos already holds the true position)
            if (!_shakeApplied)
            {
                var cam = GetCamera();
                if (cam != null)
                    _cameraOriginalPos = cam.transform.position;
            }
        }

        private void RestoreShake()
        {
            if (!_shakeApplied) return;

            var cam = GetCamera();
            if (cam != null)
                cam.transform.position = _cameraOriginalPos;
            _shakeApplied = false;
        }

        private void ApplyShake()
        {
            if (_shakeTimer <= 0f) return;

            var cam = GetCamera();
            if (cam == null) return;

            _shakeTimer -= Time.deltaTime;

            if (_shakeTimer <= 0f)
            {
                _shakeTimer = 0f;
                // Don't apply offset, camera is already restored from RestoreShake
                return;
            }

            // Damped random offset
            var decay = _shakeTimer / _shakeDuration;
            var offsetX = UnityEngine.Random.Range(-1f, 1f) * _shakeIntensity * decay;
            var offsetY = UnityEngine.Random.Range(-1f, 1f) * _shakeIntensity * decay;
            cam.transform.position = _cameraOriginalPos + new Vector3(offsetX, offsetY, 0f);
            _shakeApplied = true;
        }

        /// <summary>
        /// Reset the game.
        /// </summary>
        public void Reset()
        {
            RestoreShake();
            _shakeTimer = 0f;
            _lastShakeEffectCount = 0;

            _hintController?.SetEnabled(false);
            _lastHintedTileId = -1;
            _lastHintGeneration = -1;
            _hintLightIntensity = 0f;
            _hintLightTime = 0f;

            _objectiveDisplay?.Clear();
            _boardView?.Clear();
            _effectManager?.Clear();
            _uiManager?.HideResult();
            _initialized = false;
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
            RestoreShake();

            if (_inputController != null)
            {
                _inputController.OnUserInput -= OnUserInput;
            }

            if (_objectiveDisplay != null)
            {
                Destroy(_objectiveDisplay.gameObject);
                _objectiveDisplay = null;
            }

            // Unsubscribe from UI events to prevent memory leaks
            if (_uiManager != null)
            {
                _uiManager.OnSpeedChanged -= _onSpeedChangedHandler;
                _uiManager.OnPauseToggled -= _onPauseToggledHandler;
                _uiManager.OnAutoPlayToggled -= _onAutoPlayToggledHandler;
                _uiManager.OnRestartClicked -= _onRestartClickedHandler;

                Destroy(_uiManager.gameObject);
                _uiManager = null;
            }
        }

        private void OnUserInput()
        {
            _hintController?.OnUserInput();
        }

        private void ApplyHintToView(HintResult hint)
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
                _lastHintGeneration = gen;
                // Fade out hint light
                UpdateHintLight(null, hint, hintChanged);
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
            }

            // Update hint light position every frame + fade in
            if (_boardView is Board3DView board3DView && board3DView.TryGetTileView(hint.TileId, out var tile))
            {
                UpdateHintLight(tile, hint, hintChanged);
            }
        }

        private void UpdateHintLight(Tile3DView tile, HintResult hint, bool colorChanged)
        {
            if (!(_boardView is Board3DView board3DView)) return;
            var hintLight = board3DView.GetLightingController()?.HintLight;
            if (hintLight == null) return;

            bool active = hint.IsActive && hint.TileId >= 0;

            // Compute target intensity
            float target;
            if (active)
            {
                _hintLightTime += Time.deltaTime;
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
            _hintLightIntensity = Mathf.Lerp(_hintLightIntensity, target, HintLightFadeSpeed * Time.deltaTime);
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
