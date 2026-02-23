using System;
using Match3.Core.Choreography;
using Match3.Core.DependencyInjection;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Selection;
using Match3.Presentation;
using Match3.Random;
using Match3.Unity.Pools;
using Match3.Unity.UI;
using UnityEngine;

namespace Match3.Unity.Bridge
{
    /// <summary>
    /// Bridge between Match3 Core DLL and Unity.
    /// Manages GameSession, Choreographer, and Player.
    /// </summary>
    public sealed class Match3Bridge : MonoBehaviour
    {
        [Header("Board Configuration")]
        [SerializeField] private int _width = 8;
        [SerializeField] private int _height = 8;
        [SerializeField] private int _seed = 0;

        [Header("Visual Configuration")]
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector2 _boardOrigin = Vector2.zero;

        private IGameServiceFactory _factory;
        private GameSession _session;
        private Choreographer _choreographer;
        private Player _player;
        private WeightedMoveSelector _autoPlaySelector;

        private bool _initialized;
        private float _mergeSuppressionTimer;

        /// <summary>
        /// Cell size in world units.
        /// </summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Board origin in world space.
        /// </summary>
        public Vector2 BoardOrigin => _boardOrigin;

        /// <summary>
        /// Board width in cells.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Board height in cells.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Current visual state for rendering.
        /// </summary>
        public VisualState VisualState => _player?.VisualState;

        /// <summary>
        /// Whether there are active animations.
        /// </summary>
        public bool HasActiveAnimations => _player?.HasActiveAnimations ?? false;

        /// <summary>
        /// Whether the bridge is initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Current game state reference. Returns default if not initialized.
        /// </summary>
        public GameState CurrentState => _session?.Engine.State ?? default;

        #region UI Properties

        private float _gameSpeed = 1.0f;
        private bool _isPaused;
        private bool _isAutoPlaying;
        private int _lastMovesRemaining = -1;
        private int _lastScore = -1;

        /// <summary>
        /// Game simulation speed multiplier (0.1x - 5.0x).
        /// </summary>
        public float GameSpeed
        {
            get => _gameSpeed;
            set => _gameSpeed = Mathf.Clamp(value, 0.1f, 5.0f);
        }

        /// <summary>
        /// Whether the game is paused.
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                _session?.Engine.SetPaused(value);
            }
        }

        /// <summary>
        /// Whether auto-play mode is active.
        /// </summary>
        public bool IsAutoPlaying
        {
            get => _isAutoPlaying;
            set => _isAutoPlaying = value;
        }

        /// <summary>
        /// Event fired when moves remaining changes.
        /// </summary>
        public event Action<int> OnMovesChanged;

        /// <summary>
        /// Event fired when score changes.
        /// </summary>
        public event Action<int> OnScoreChanged;

        /// <summary>
        /// Event fired when game ends (victory, score).
        /// </summary>
        public event Action<bool, int> OnGameEnded;

        /// <summary>
        /// Event fired when objectives are updated.
        /// </summary>
        public event Action<ObjectiveProgress[]> OnObjectivesUpdated;

        #endregion

        /// <summary>
        /// Initialize the bridge with default or serialized parameters.
        /// </summary>
        public void Initialize()
        {
            Initialize(_width, _height, _seed != 0 ? _seed : System.Environment.TickCount);
        }

        /// <summary>
        /// Initialize the bridge with explicit parameters.
        /// </summary>
        public void Initialize(int width, int height, int seed)
        {
            if (_initialized)
            {
                Cleanup();
            }

            _width = width;
            _height = height;
            _seed = seed;

            // Create factory with default services
            _factory = new GameServiceBuilder()
                .UseDefaultServices()
                .Build();

            // Create game session
            var config = new GameServiceConfiguration
            {
                Width = width,
                Height = height,
                RngSeed = seed,
                EnableEventCollection = true
            };
            _session = _factory.CreateGameSession(config);

            // Create choreographer and player
            _choreographer = new Choreographer();
            _player = new Player();

            // Create auto-play selector (same as Web version)
            var matchFinder = new ClassicMatchFinder(new BombGenerator());
            var uiRandom = _session.SeedManager.GetRandom(RandomDomain.Main);
            _autoPlaySelector = new WeightedMoveSelector(matchFinder, uiRandom);

            // Sync initial state
            var state = _session.Engine.State;
            _player.SyncFromGameState(in state);

            _initialized = true;

            // Reset UI state tracking
            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;

            Debug.Log($"Match3Bridge initialized: {width}x{height}, seed={seed}");
        }

        /// <summary>
        /// Update the bridge by one frame.
        /// Processes simulation, choreography, and animations.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized || _session == null) return;
            if (_isPaused) return;

            // Apply game speed
            var scaledDelta = deltaTime * _gameSpeed;

            // Tick the simulation engine
            _session.Engine.Tick(scaledDelta);

            // Drain events and convert to render commands
            var events = _session.DrainEvents();
            if (events.Count > 0)
            {
                var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                _player.Append(commands);

                // Start merge suppression timer (only suppresses for MergeDuration)
                if (_choreographer.LastBatchHadMerge)
                    _mergeSuppressionTimer = _choreographer.MergeDuration;
            }

            // Tick the animation player
            _player.Tick(scaledDelta);

            // Tick visual effects (advance elapsed time, remove expired)
            _player.VisualState.UpdateEffects(scaledDelta);

            // Countdown merge suppression — expires exactly when merge animation ends,
            // not when all animations (including effects) finish.
            _mergeSuppressionTimer = Mathf.Max(0f, _mergeSuppressionTimer - scaledDelta);

            // Sync falling tiles from game state (physics-driven positions)
            // Only suppress during the merge window so gravity waits for merge to finish.
            // Normal matches let physics sync run normally.
            {
                var state = _session.Engine.State;
                _player.VisualState.SuppressNewTileSync = _mergeSuppressionTimer > 0f;
                _player.VisualState.SyncFallingTilesFromGameState(in state);
            }

            // Check for UI state changes
            CheckStateChanges();

            // Handle auto-play (same logic as Web version)
            if (_isAutoPlaying)
            {
                if (_session.Engine.IsStable() && !HasActiveAnimations)
                {
                    TryMakeAutoMove();
                }
                else if (Time.frameCount % 120 == 0)
                {
                    var st = _session.Engine.State;
                    Debug.Log($"[AutoPlay] waiting: stable={_session.Engine.IsStable()} anim={HasActiveAnimations} moves={st.MoveCount}/{st.MoveLimit}");
                }
            }
        }

        private void TryMakeAutoMove()
        {
            if (_autoPlaySelector == null) return;

            var state = _session.Engine.State;
            if (state.MoveCount >= state.MoveLimit)
            {
                Debug.Log($"[AutoPlay] game over: {state.MoveCount}/{state.MoveLimit}");
                _isAutoPlaying = false;
                return;
            }

            // Invalidate cache after board changes
            _autoPlaySelector.InvalidateCache();

            // Use Core's weighted move selector (same as Web)
            if (_autoPlaySelector.TryGetMove(in state, out var action))
            {
                Debug.Log($"[AutoPlay] move #{state.MoveCount+1}: {action.ActionType} ({action.From.X},{action.From.Y})->({action.To.X},{action.To.Y})");
                if (action.ActionType == MoveActionType.Tap)
                {
                    _session.Engine.HandleTap(action.From);
                }
                else
                {
                    _session.Engine.ApplyMove(action.From, action.To);
                }
            }
            else
            {
                Debug.Log($"[AutoPlay] no valid move found, moves={state.MoveCount}/{state.MoveLimit}");
            }
        }

        private void CheckStateChanges()
        {
            var state = _session.Engine.State;

            // Check moves changed (MovesRemaining = MoveLimit - MoveCount)
            var currentMoves = state.MoveLimit - state.MoveCount;
            if (currentMoves != _lastMovesRemaining)
            {
                _lastMovesRemaining = currentMoves;
                OnMovesChanged?.Invoke(currentMoves);
            }

            // Check score changed
            var currentScore = state.Score;
            if (currentScore != _lastScore)
            {
                _lastScore = currentScore;
                OnScoreChanged?.Invoke(currentScore);
            }
        }

        /// <summary>
        /// Notify UI that the game has ended.
        /// Call this from GameController when game ends.
        /// </summary>
        public void NotifyGameEnded(bool isVictory, int finalScore)
        {
            OnGameEnded?.Invoke(isVictory, finalScore);
        }

        /// <summary>
        /// Update objectives and notify UI.
        /// Call this when objectives change.
        /// </summary>
        public void NotifyObjectivesUpdated(ObjectiveProgress[] objectives)
        {
            OnObjectivesUpdated?.Invoke(objectives);
        }

        /// <summary>
        /// Apply a move from position A to position B.
        /// Core's CanInteract handles per-tile checks (cover, falling, suspended).
        /// </summary>
        public bool ApplyMove(Position from, Position to)
        {
            if (!_initialized) return false;

            // Check if positions are adjacent
            if (!AreAdjacent(from, to))
            {
                return false;
            }

            // Apply swap through simulation engine
            // Core's ApplyMove checks CanInteract (cover, falling, suspended, None)
            return _session.Engine.ApplyMove(from, to);
        }

        /// <summary>
        /// Check if the game is idle (no active moves or animations).
        /// </summary>
        public bool IsIdle()
        {
            return _initialized && !HasActiveAnimations && _session.Engine.IsStable();
        }

        /// <summary>
        /// Handle a tap at the specified grid position.
        /// Delegates to Core's SimulationEngine.HandleTap for selection/bomb activation logic.
        /// Core's CanInteract handles per-tile checks (cover, falling, suspended).
        /// </summary>
        public void HandleTap(Position pos)
        {
            if (!_initialized) return;

            _session.Engine.HandleTap(pos);
        }

        /// <summary>
        /// Get tile ID at grid position.
        /// Returns -1 if no tile at position.
        /// </summary>
        public int GetTileIdAt(Position pos)
        {
            if (!_initialized) return -1;

            var state = _session.Engine.State;
            if (pos.X < 0 || pos.X >= state.Width || pos.Y < 0 || pos.Y >= state.Height)
                return -1;

            var tile = state.GetTile(pos.X, pos.Y);
            return tile.Type != Core.Models.Enums.TileType.None ? tile.Id : -1;
        }

        private static bool AreAdjacent(Position a, Position b)
        {
            int dx = System.Math.Abs(a.X - b.X);
            int dy = System.Math.Abs(a.Y - b.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        private void Cleanup()
        {
            _session?.Dispose();
            _session = null;
            _player = null;
            _choreographer = null;
            _autoPlaySelector = null;
            _initialized = false;

            // Clear events to prevent memory leaks
            OnMovesChanged = null;
            OnScoreChanged = null;
            OnGameEnded = null;
            OnObjectivesUpdated = null;

            // Clear static caches to prevent stale references
            SpriteFactory.ClearCache();
            MeshFactory.ClearCache();
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
