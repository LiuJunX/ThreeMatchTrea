using System;
using System.Collections.Generic;
using Match3.Core.Choreography;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Selection;
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

        // Per-cell lock tracking: each entry has its own timer
        private readonly struct ActiveLock
        {
            public readonly LockToken Token;
            public readonly float Duration;

            public ActiveLock(LockToken token, float duration)
            {
                Token = token;
                Duration = duration;
            }
        }
        private readonly List<ActiveLock> _activeLocks = new();
        private readonly List<float> _lockTimers = new();

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
            public TileType TileType;
            public Position SourceGridPosition;
            /// <summary>null = direct fly (3-connect), non-null = bomb merge position.</summary>
            public Position? MergeTarget;
            public float FlyDelay;
            public int NewCount;
            public int TargetCount;
        }

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

            _initialized = true;

            // Reset UI state tracking
            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;
            ReleaseAllLocks();

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
                if (OnObjectiveCollected != null)
                    ScanForObjectiveCollections(events);

                var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                _player.Append(commands);

                // Acquire per-cell locks from Choreographer's lock schedule
                AcquireLocksFromSchedule();
            }

            // Tick the animation player
            _player.Tick(scaledDelta);

            // Tick visual effects (advance elapsed time, remove expired)
            _player.VisualState.UpdateEffects(scaledDelta);

            // Tick per-cell lock timers, release expired ones
            TickLockTimers(scaledDelta);

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

        /// <summary>
        /// Read Choreographer.LockEntries, adjust durations based on context, acquire locks.
        /// </summary>
        private void AcquireLocksFromSchedule()
        {
            var entries = _choreographer.LockEntries;
            if (entries.Count == 0) return;

            // Build set of positions that will fly to objectives (for duration adjustment)
            _flyPositionKeys.Clear();
            foreach (var fly in _pendingFlies)
            {
                if (fly.MergeTarget == null)
                    _flyPositionKeys.Add(PackGridKey(fly.SourceGridPosition.X, fly.SourceGridPosition.Y));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float duration = entry.Duration;

                if (entry.IsMerge)
                {
                    // Merge locks: slightly shorter to let gravity start sooner
                    duration -= 0.03f;
                }
                else
                {
                    // Match locks: adjust based on whether tile flies to objective
                    var key = PackGridKey(entry.Position.X, entry.Position.Y);
                    duration += _flyPositionKeys.Contains(key) ? 0.05f : -0.05f;
                }

                duration = Mathf.Max(duration, 0.01f);
                var token = _session.Engine.AcquireLock(entry.Position, entry.LockType);
                _activeLocks.Add(new ActiveLock(token, duration));
                _lockTimers.Add(duration);
            }
        }
        private readonly HashSet<long> _flyPositionKeys = new();

        /// <summary>
        /// Tick all active lock timers, release expired ones.
        /// </summary>
        private void TickLockTimers(float deltaTime)
        {
            for (int i = _lockTimers.Count - 1; i >= 0; i--)
            {
                _lockTimers[i] -= deltaTime;
                if (_lockTimers[i] <= 0f)
                {
                    _session.Engine.ReleaseLock(_activeLocks[i].Token);
                    _activeLocks.RemoveAt(i);
                    _lockTimers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Release all active locks immediately.
        /// </summary>
        private void ReleaseAllLocks()
        {
            for (int i = 0; i < _activeLocks.Count; i++)
                _session.Engine.ReleaseLock(_activeLocks[i].Token);
            _activeLocks.Clear();
            _lockTimers.Clear();
        }

        // Reusable collections for ScanForObjectiveCollections (avoid GC)
        private readonly Dictionary<float, Dictionary<TileType, Queue<(int TileId, Position Pos, Position? MergeTarget)>>>
            _destroyedBySimTime = new();
        private readonly List<FlyCollectionRequest> _pendingFlies = new();
        private int _lastObjectiveHash = -1;

        private void ScanForObjectiveCollections(IReadOnlyList<GameEvent> events)
        {
            _destroyedBySimTime.Clear();
            _pendingFlies.Clear();

            var state = _session.Engine.State;

            foreach (var evt in events)
            {
                if (evt is TileDestroyedEvent tde && tde.Reason == DestroyReason.Match)
                {
                    if (!_destroyedBySimTime.TryGetValue(tde.SimulationTime, out var byType))
                    {
                        byType = new Dictionary<TileType, Queue<(int, Position, Position?)>>();
                        _destroyedBySimTime[tde.SimulationTime] = byType;
                    }
                    if (!byType.TryGetValue(tde.Type, out var queue))
                    {
                        queue = new Queue<(int, Position, Position?)>();
                        byType[tde.Type] = queue;
                    }
                    queue.Enqueue((tde.TileId, tde.GridPosition, tde.MergeTarget));
                }
                else if (evt is ObjectiveProgressEvent ope)
                {
                    if (ope.ObjectiveIndex < 0 || ope.ObjectiveIndex >= state.ObjectiveProgress.Length)
                        continue;

                    var objProg = state.ObjectiveProgress[ope.ObjectiveIndex];
                    if (objProg.TargetLayer != ObjectiveTargetLayer.Tile)
                        continue;

                    var targetType = (TileType)objProg.ElementType;

                    if (_destroyedBySimTime.TryGetValue(ope.SimulationTime, out var byType)
                        && byType.TryGetValue(targetType, out var queue)
                        && queue.Count > 0)
                    {
                        var (tileId, pos, mergeTarget) = queue.Dequeue();
                        _pendingFlies.Add(new FlyCollectionRequest
                        {
                            TileId = tileId,
                            ObjectiveIndex = ope.ObjectiveIndex,
                            TileType = targetType,
                            SourceGridPosition = pos,
                            MergeTarget = mergeTarget,
                            FlyDelay = 0f,
                            NewCount = ope.CurrentCount,
                            TargetCount = ope.TargetCount
                        });
                    }
                }
            }

            // Post-process: assign stagger delays for merge groups
            if (_pendingFlies.Count > 0)
                AssignMergeDelays();

            // Fire events
            foreach (var fly in _pendingFlies)
                OnObjectiveCollected?.Invoke(fly);
        }

        private void AssignMergeDelays()
        {
            // Group merge flies by MergeTarget, sort by distance, assign delays
            var mergeGroups = new Dictionary<Position, List<int>>();

            for (int i = 0; i < _pendingFlies.Count; i++)
            {
                var fly = _pendingFlies[i];
                if (fly.MergeTarget == null) continue;

                var target = fly.MergeTarget.Value;
                if (!mergeGroups.TryGetValue(target, out var indices))
                {
                    indices = new List<int>();
                    mergeGroups[target] = indices;
                }
                indices.Add(i);
            }

            const float stagger = 0.08f;
            foreach (var kvp in mergeGroups)
            {
                var target = kvp.Key;
                var indices = kvp.Value;
                if (indices.Count <= 1) continue;

                // Sort by distance to merge target
                indices.Sort((a, b) =>
                {
                    var da = GridDistance(_pendingFlies[a].SourceGridPosition, target);
                    var db = GridDistance(_pendingFlies[b].SourceGridPosition, target);
                    return da.CompareTo(db);
                });

                for (int j = 0; j < indices.Count; j++)
                {
                    var fly = _pendingFlies[indices[j]];
                    fly.FlyDelay = j * stagger;
                    _pendingFlies[indices[j]] = fly;
                }
            }
        }

        private static float GridDistance(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
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

            // Check objectives changed
            CheckObjectiveChanges(in state);
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

                var tileType = p.TargetLayer == ObjectiveTargetLayer.Tile
                    ? (TileType)p.ElementType
                    : TileType.None;

                uiObjectives[slot++] = new ObjectiveProgress
                {
                    Type = p.TargetLayer.ToString(),
                    Current = p.CurrentCount,
                    Target = p.TargetCount,
                    Color = tileType != TileType.None
                        ? SpriteFactory.GetTileColor(tileType)
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
            if (_session != null)
            {
                ReleaseAllLocks();
            }
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
            OnObjectiveCollected = null;
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
