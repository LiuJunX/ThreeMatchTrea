using System;
using System.Collections.Generic;
using Match3.Core.Choreography;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Selection;
using Match3.Core.Utility;
using Match3.Presentation;
using Match3.Random;
using Match3.Unity.Pools;
using Match3.Unity.Services;
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

        private CellLockManager _lockManager;
        private ObjectiveCollectionProcessor _objectiveCollector;

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
        /// Grid layout mask: layout[row, col] = true if cell is playable.
        /// Returns null if not initialized.
        /// </summary>
        public bool[,] GridLayout
        {
            get
            {
                if (_session == null) return null;
                var state = _session.Engine.State;
                var layout = new bool[state.Height, state.Width];
                for (int y = 0; y < state.Height; y++)
                    for (int x = 0; x < state.Width; x++)
                        layout[y, x] = state.Grid[y * state.Width + x].Type != Core.Models.Enums.ElementType.None;
                return layout;
            }
        }

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
        private bool _gameEndFired;

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

        /// <summary>
        /// Event fired when a tile should fly to an objective icon.
        /// </summary>
        public event Action<FlyCollectionRequest> OnObjectiveCollected;

        #endregion

        /// <summary>
        /// Data for a tile-to-objective fly animation request.
        /// </summary>
        public struct FlyCollectionRequest
        {
            public int TileId;
            public int ObjectiveIndex;
            public ElementType ElementType;
            public Position SourceGridPosition;
            /// <summary>null = direct fly (3-connect), non-null = bomb merge position.</summary>
            public Position? MergeTarget;
            public float FlyDelay;
            public int NewCount;
            public int TargetCount;
        }

        /// <summary>
        /// Current move limit (for star calculation).
        /// </summary>
        public int MoveLimit => _session?.Engine.State.MoveLimit ?? 0;

        /// <summary>
        /// Current moves remaining.
        /// </summary>
        public int MovesRemaining
        {
            get
            {
                if (_session == null) return 0;
                var s = _session.Engine.State;
                return s.MoveLimit - s.MoveCount;
            }
        }

        /// <summary>
        /// Initialize the bridge with default or serialized parameters.
        /// </summary>
        public void Initialize()
        {
            Initialize(_width, _height, _seed != 0 ? _seed : System.Environment.TickCount);
        }

        /// <summary>
        /// Initialize the bridge with a specific level config.
        /// </summary>
        public void Initialize(int seed, string levelId)
        {
            if (_initialized)
            {
                Cleanup();
            }

            _seed = seed;

            _factory = new GameServiceBuilder()
                .UseDefaultServices()
                .Build();

            LevelConfig levelConfig = null;
            try
            {
                levelConfig = UnityConfigProvider.Instance.GetLevelConfig(levelId);
                if (levelConfig != null)
                {
                    _width = levelConfig.Width;
                    _height = levelConfig.Height;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Match3Bridge] Failed to load level config '{levelId}': {ex.Message}");
            }

            var config = new GameServiceConfiguration
            {
                Width = _width,
                Height = _height,
                RngSeed = seed,
                EnableEventCollection = true
            };
            _session = _factory.CreateGameSession(config, levelConfig);

            _choreographer = new Choreographer();
            _player = new Player();

            var matchFinder = new ClassicMatchFinder(new BombGenerator());
            var uiRandom = _session.SeedManager.GetRandom(RandomDomain.Main);
            _autoPlaySelector = new WeightedMoveSelector(matchFinder, uiRandom);

            var state = _session.Engine.State;
            _player.SyncFromGameState(in state);

            _lockManager = new CellLockManager();
            _objectiveCollector = new ObjectiveCollectionProcessor();
            _initialized = true;

            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;
            _gameEndFired = false;
            _lastObjectiveHash = -1;

            Debug.Log($"Match3Bridge initialized: {_width}x{_height}, seed={seed}, level={levelId}");
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

            // Load level config (provides objectives, move limit, etc.)
            LevelConfig levelConfig = null;
            try
            {
                levelConfig = UnityConfigProvider.Instance.GetLevelConfig("level_001");
                // Override width/height/moves from level config
                if (levelConfig != null)
                {
                    _width = width = levelConfig.Width;
                    _height = height = levelConfig.Height;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Match3Bridge] Failed to load level config: {ex.Message}");
            }

            // Create game session
            var config = new GameServiceConfiguration
            {
                Width = width,
                Height = height,
                RngSeed = seed,
                EnableEventCollection = true
            };
            _session = _factory.CreateGameSession(config, levelConfig);

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

            _lockManager = new CellLockManager();
            _objectiveCollector = new ObjectiveCollectionProcessor();
            _initialized = true;

            // Reset UI state tracking
            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;
            _gameEndFired = false;
            _lastObjectiveHash = -1;

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
                // Scan for objective collections before choreography
                // Always call Process() so PendingFlies is fresh for lock duration adjustment
                _objectiveCollector.Process(events, _session.Engine.State, OnObjectiveCollected);

                var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                _player.Append(commands);

                // Acquire per-cell locks from Choreographer's lock schedule
                _lockManager.AcquireFromSchedule(
                    _choreographer.LockEntries,
                    _objectiveCollector.PendingFlies,
                    _session.Engine.AcquireLock);
            }

            // Tick the animation player
            _player.Tick(scaledDelta);

            // Tick visual effects (advance elapsed time, remove expired)
            _player.VisualState.UpdateEffects(scaledDelta);

            // Tick per-cell lock timers, release expired ones
            _lockManager.Tick(scaledDelta, _session.Engine.ReleaseLock);

            // Sync falling tiles from game state (physics-driven positions)
            {
                var state = _session.Engine.State;
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
                    Debug.Log($"[AutoPlay] waiting: stable={_session.Engine.IsStable()} anim={HasActiveAnimations} moves={st.MoveCount}/{st.MoveLimit} | {_player.GetAnimationDiagnostics()}");
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

        private int _lastObjectiveHash = -1;

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

            // Check objectives changed
            CheckObjectiveChanges(in state);

            // Check level completed (one-shot)
            if (!_gameEndFired && state.LevelStatus != LevelStatus.InProgress)
            {
                _gameEndFired = true;
                bool isVictory = state.LevelStatus == LevelStatus.Victory;
                OnGameEnded?.Invoke(isVictory, state.Score);
            }
        }

        private void CheckObjectiveChanges(in GameState state)
        {
            if (state.ObjectiveProgress == null) return;

            // Quick hash to detect changes
            int hash = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                var p = state.ObjectiveProgress[i];
                if (!p.IsActive) continue;
                hash = hash * 397 + p.CurrentCount;
                hash = hash * 397 + p.TargetCount;
            }

            if (hash == _lastObjectiveHash) return;
            _lastObjectiveHash = hash;

            // Build UI-friendly objective array (sequential, active only)
            int activeCount = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                if (state.ObjectiveProgress[i].IsActive) activeCount++;
            }

            var uiObjectives = new ObjectiveProgress[activeCount];
            int slot = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                var p = state.ObjectiveProgress[i];
                if (!p.IsActive) continue;

                var elementType = p.TargetLayer == ObjectiveTargetLayer.Tile
                    ? (ElementType)p.ElementType
                    : ElementType.None;

                uiObjectives[slot++] = new ObjectiveProgress
                {
                    Type = p.TargetLayer.ToString(),
                    Current = p.CurrentCount,
                    Target = p.TargetCount,
                    Color = elementType != ElementType.None
                        ? SpriteFactory.GetTileColor(elementType)
                        : Color.gray
                };
            }

            OnObjectivesUpdated?.Invoke(uiObjectives);
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
        /// Try to get a hint move (best available move for the player).
        /// Returns false if no valid moves available.
        /// </summary>
        public bool TryGetHintMove(out MoveAction action)
        {
            action = default;
            if (!_initialized || _autoPlaySelector == null) return false;

            var state = _session.Engine.State;
            _autoPlaySelector.InvalidateCache();
            return _autoPlaySelector.TryGetMove(in state, out action);
        }

        /// <summary>
        /// For a swap move, determine which tile to highlight:
        /// the one that will be eliminated after the swap.
        /// If both match, prefer the one forming a bomb.
        /// </summary>
        public Position GetHintHighlightPosition(MoveAction action)
        {
            if (action.ActionType != MoveActionType.Swap) return action.From;
            if (!_initialized) return action.From;

            var state = _session.Engine.State;
            var from = action.From;
            var to = action.To;

            // Both bombs → always highlight from
            var tileA = state.GetTile(from.X, from.Y);
            var tileB = state.GetTile(to.X, to.Y);
            if (tileA.Bomb != BombType.None && tileB.Bomb != BombType.None) return from;

            var matchFinder = new ClassicMatchFinder(new BombGenerator());

            GridUtility.SwapTilesForCheck(ref state, from, to);

            bool fromPosMatches = matchFinder.HasMatchAt(in state, from);
            bool toPosMatches = matchFinder.HasMatchAt(in state, to);

            var result = from; // default: highlight the tile at 'from'

            if (fromPosMatches && toPosMatches)
            {
                // Both match → check bomb formation, prefer the side forming a bomb
                var foci = new[] { from, to };
                var groups = matchFinder.FindMatchGroups(in state, foci);

                bool fromPosBomb = false;
                foreach (var g in groups)
                {
                    if (g.SpawnBombType == BombType.None) continue;
                    foreach (var pos in g.Positions)
                    {
                        if (pos == from) { fromPosBomb = true; break; }
                    }
                    if (fromPosBomb) break;
                }
                ClassicMatchFinder.ReleaseGroups(groups);

                // fromPosBomb: tile at 'from' after swap (originally at 'to') forms bomb → highlight 'to'
                if (fromPosBomb) result = to;
            }
            else if (fromPosMatches && !toPosMatches)
            {
                // Only the tile now at 'from' (originally at 'to') matches → highlight 'to'
                result = to;
            }

            GridUtility.SwapTilesForCheck(ref state, from, to); // swap back

            return result;
        }

        /// <summary>
        /// Get all pre-swap positions of tiles that would match with the highlighted tile after swap.
        /// hintFrom = highlighted tile's current position, hintTo = swap destination.
        /// Returns positions mapped back to current (pre-swap) coordinates.
        /// </summary>
        private readonly List<Position> _hintMatchPositions = new();
        public IReadOnlyList<Position> GetHintMatchPositions(Position hintFrom, Position hintTo)
        {
            _hintMatchPositions.Clear();
            if (!_initialized) return _hintMatchPositions;

            var state = _session.Engine.State;
            var matchFinder = new ClassicMatchFinder(new BombGenerator());

            GridUtility.SwapTilesForCheck(ref state, hintFrom, hintTo);

            // Find match groups at hintTo (where highlighted tile lands after swap)
            var foci = new[] { hintTo };
            var groups = matchFinder.FindMatchGroups(in state, foci);

            foreach (var g in groups)
            {
                foreach (var pos in g.Positions)
                {
                    // Map post-swap positions back to pre-swap (current) positions
                    if (pos == hintTo)
                        _hintMatchPositions.Add(hintFrom);
                    else if (pos == hintFrom)
                        _hintMatchPositions.Add(hintTo);
                    else
                        _hintMatchPositions.Add(pos);
                }
            }
            ClassicMatchFinder.ReleaseGroups(groups);

            GridUtility.SwapTilesForCheck(ref state, hintFrom, hintTo); // swap back

            return _hintMatchPositions;
        }

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        public void ClearSelection()
        {
            if (!_initialized) return;
            _session.Engine.SetSelectedPosition(Position.Invalid);
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
            return tile.Type != Core.Models.Enums.ElementType.None ? tile.Id : -1;
        }

        /// <summary>
        /// Get the hole zone for a column (entryY, exitY).
        /// Returns null if the column has no holes.
        /// </summary>
        public (int entryY, int exitY)? GetColumnHoleZone(int column)
        {
            if (!_initialized || _session == null) return null;

            var state = _session.Engine.State;
            if (column < 0 || column >= state.Width) return null;

            for (int y = 0; y < state.Height; y++)
            {
                if (state.IsHole(column, y))
                {
                    int exitY = Core.Systems.Physics.GravityTargetResolver.FindHoleZoneExit(in state, column, y);
                    return (y, exitY);
                }
            }

            return null;
        }

        private static bool AreAdjacent(Position a, Position b)
        {
            int dx = System.Math.Abs(a.X - b.X);
            int dy = System.Math.Abs(a.Y - b.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        private void Cleanup()
        {
            if (_session != null)
            {
                _lockManager?.ReleaseAll(_session.Engine.ReleaseLock);
            }
            _session?.Dispose();
            _session = null;
            _player = null;
            _choreographer = null;
            _autoPlaySelector = null;
            _initialized = false;

            _lastObjectiveHash = -1;

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
