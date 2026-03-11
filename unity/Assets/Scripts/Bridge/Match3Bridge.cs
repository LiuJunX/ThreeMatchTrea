using System;
using System.Collections.Generic;
using System.IO;
using Match3.Core.Choreography;
using Match3.Core.Commands;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Simulation;
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
        private float _timeAccumulator;

        // Recording (always-on during gameplay)
        private GameRecorder _recorder;

        // Replay mode
        private ReplayController _replayController;
        private bool _isReplaying;

        private ObjectiveCollectionProcessor _objectiveCollector;
        private readonly List<GameEvent> _eventBuffer = new();
        private readonly ClassicMatchFinder _hintMatchFinder = new(new BombGenerator());

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
                if (!_initialized) return null;
                var state = CurrentState;
                if (state.Grid == null) return null;
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
        /// Whether the bridge is in replay mode.
        /// </summary>
        public bool IsReplaying => _isReplaying;

        /// <summary>
        /// Current game state reference. Returns default if not initialized.
        /// </summary>
        public GameState CurrentState =>
            _isReplaying ? (_replayController?.Engine?.State ?? default)
                         : (_session?.Engine.State ?? default);

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
        public int MoveLimit => CurrentState.MoveLimit;

        /// <summary>
        /// Current moves remaining.
        /// </summary>
        public int MovesRemaining
        {
            get
            {
                var s = CurrentState;
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

            var matchFinder = _hintMatchFinder;
            var uiRandom = _session.SeedManager.GetRandom(RandomDomain.Main);
            _autoPlaySelector = new WeightedMoveSelector(matchFinder, uiRandom);

            var state = _session.Engine.State;
            _player.SyncFromGameState(in state);

            _objectiveCollector = new ObjectiveCollectionProcessor();
            _initialized = true;

            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;
            _gameEndFired = false;
            _lastObjectiveHash = -1;
            _timeAccumulator = 0f;
            _isReplaying = false;

            // Start recording
            _recorder = new GameRecorder(in state, seed);

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
            var matchFinder = _hintMatchFinder;
            var uiRandom = _session.SeedManager.GetRandom(RandomDomain.Main);
            _autoPlaySelector = new WeightedMoveSelector(matchFinder, uiRandom);

            // Sync initial state
            var state = _session.Engine.State;
            _player.SyncFromGameState(in state);

            _objectiveCollector = new ObjectiveCollectionProcessor();
            _initialized = true;

            // Reset UI state tracking
            _lastMovesRemaining = -1;
            _lastScore = -1;
            _isPaused = false;
            _isAutoPlaying = false;
            _gameEndFired = false;
            _lastObjectiveHash = -1;
            _timeAccumulator = 0f;
            _isReplaying = false;

            // Start recording
            _recorder = new GameRecorder(in state, seed);

            Debug.Log($"Match3Bridge initialized: {width}x{height}, seed={seed}");
        }

        /// <summary>
        /// Update the bridge by one frame.
        /// Processes simulation, choreography, and animations.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (_isPaused) return;

            if (_isReplaying)
            {
                // Replay has its own speed control (ReplayController.PlaybackSpeed),
                // bypass _gameSpeed to avoid double-scaling.
                TickReplay(deltaTime);
            }
            else
            {
                var scaledDelta = deltaTime * _gameSpeed;
                TickNormal(scaledDelta);
            }
        }

        private void TickNormal(float scaledDelta)
        {
            if (_session == null) return;

            // Fixed timestep accumulator: ensures simulation uses identical dt
            // to replay (1/60f), making recorded games deterministically reproducible.
            const float fixedStep = SimulationConfig.DefaultFixedDeltaTime;
            _timeAccumulator += scaledDelta;
            while (_timeAccumulator >= fixedStep)
            {
                _timeAccumulator -= fixedStep;
                _session.Engine.Tick(fixedStep);
            }

            // Drain events accumulated from all fixed ticks
            _session.DrainEventsTo(_eventBuffer);
            ProcessEventsAndAnimate(scaledDelta, _session.Engine.State);

            // Check for UI state changes
            CheckStateChanges();

            // Auto-save recording on game end
            if (_gameEndFired && _recorder != null && _recorder.IsRecording)
            {
                AutoSaveRecording();
            }

            // Handle auto-play
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

        private bool _replayCompletedFired;

        private void TickReplay(float deltaTime)
        {
            if (_replayController == null) return;

            _replayController.Tick(deltaTime);

            // Drain events from the replay engine
            _eventBuffer.Clear();
            if (_replayController.Engine?.EventCollector is BufferedEventCollector buffered)
            {
                buffered.DrainEventsTo(_eventBuffer);
            }

            // Animation speed must match replay speed so events don't pile up
            var effectiveDelta = deltaTime * _replayController.PlaybackSpeed;
            ProcessEventsAndAnimate(effectiveDelta, _replayController.Engine?.State ?? default);

            // Update UI (score, moves, objectives) during replay
            CheckStateChanges();

            // One-shot completion log
            if (!_replayCompletedFired && _replayController.State == ReplayState.Completed)
            {
                _replayCompletedFired = true;
                Debug.Log("[Replay] Playback completed.");
            }
        }

        private void ProcessEventsAndAnimate(float scaledDelta, GameState state)
        {
            var events = (IReadOnlyList<GameEvent>)_eventBuffer;
            if (_eventBuffer.Count > 0)
            {
                _objectiveCollector.Process(events, state, OnObjectiveCollected);

                var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                _player.Append(commands);
            }

            _player.Tick(scaledDelta);
            _player.VisualState.UpdateEffects(scaledDelta);
            _player.VisualState.SyncFallingTilesFromGameState(in state);
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
            // Route through public methods so auto-play moves are also recorded
            if (_autoPlaySelector.TryGetMove(in state, out var action))
            {
                Debug.Log($"[AutoPlay] move #{state.MoveCount+1}: {action.ActionType} ({action.From.X},{action.From.Y})->({action.To.X},{action.To.Y})");
                if (action.ActionType == MoveActionType.Tap)
                {
                    HandleTap(action.From);
                }
                else
                {
                    ApplyMove(action.From, action.To);
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
            var state = CurrentState;

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
        /// Creates a SwapCommand, records it, and executes through the engine.
        /// </summary>
        public bool ApplyMove(Position from, Position to)
        {
            if (!_initialized || _session == null) return false;

            if (!AreAdjacent(from, to))
                return false;

            var cmd = new SwapCommand
            {
                From = from,
                To = to,
                IssuedAtTick = _session.Engine.CurrentTick
            };

            if (cmd.Execute(_session.Engine))
            {
                _recorder?.RecordCommand(cmd);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if the game is idle (no active moves or animations).
        /// </summary>
        public bool IsIdle()
        {
            if (!_initialized) return false;
            if (_isReplaying) return false;
            return !HasActiveAnimations && _session != null && _session.Engine.IsStable();
        }

        /// <summary>
        /// Try to get a hint move (best available move for the player).
        /// Returns false if no valid moves available.
        /// </summary>
        public bool TryGetHintMove(out MoveAction action)
        {
            action = default;
            if (!_initialized || _session == null || _autoPlaySelector == null) return false;

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
            if (!_initialized || _session == null) return action.From;

            var state = _session.Engine.State;
            var from = action.From;
            var to = action.To;

            // Both bombs → always highlight from
            var tileA = state.GetTile(from.X, from.Y);
            var tileB = state.GetTile(to.X, to.Y);
            if (tileA.Type.IsBomb() && tileB.Type.IsBomb()) return from;

            var matchFinder = _hintMatchFinder;

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
                    if (g.SpawnBombType == ElementType.None) continue;
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
            if (!_initialized || _session == null) return _hintMatchPositions;

            var state = _session.Engine.State;
            var matchFinder = _hintMatchFinder;

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
            if (!_initialized || _session == null) return;
            _session.Engine.SetSelectedPosition(Position.Invalid);
        }

        /// <summary>
        /// Handle a tap at the specified grid position.
        /// Creates a TapCommand, records it, and executes through the engine.
        /// </summary>
        public void HandleTap(Position pos)
        {
            if (!_initialized || _session == null) return;

            var cmd = new TapCommand
            {
                Position = pos,
                IssuedAtTick = _session.Engine.CurrentTick
            };

            if (cmd.Execute(_session.Engine))
                _recorder?.RecordCommand(cmd);
        }

        /// <summary>
        /// Get tile ID at grid position.
        /// Returns -1 if no tile at position.
        /// </summary>
        public int GetTileIdAt(Position pos)
        {
            if (!_initialized) return -1;

            var state = CurrentState;
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
            if (!_initialized) return null;

            var state = CurrentState;
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

        #region Recording & Replay

        private const int RingBufferSize = 5;
        private const string RecordingDir = "Recordings";

        private static string RecordingBasePath =>
            Path.Combine(Application.persistentDataPath, RecordingDir);

        /// <summary>
        /// Saves the current recording to the ring buffer (auto-called on game end).
        /// Also callable manually from debug menu.
        /// </summary>
        public string SaveRecording()
        {
            if (_recorder == null || _session == null) return null;

            var state = _session.Engine.State;
            var recording = _recorder.Complete(
                _session.Engine.CurrentTick,
                state.Score,
                state.MoveCount);

            var path = SaveToRingBuffer(recording);
            Debug.Log($"[Recording] Saved: {path} ({recording.TotalMoves} moves, {recording.Commands.Count} commands)");

            // Create a fresh recorder for continued play
            _recorder = new GameRecorder(in state, _seed);

            return path;
        }

        /// <summary>
        /// Saves a named recording (for archiving a specific bug).
        /// </summary>
        public string SaveRecordingAs(string name)
        {
            if (_recorder == null || _session == null) return null;

            var state = _session.Engine.State;
            var recording = _recorder.Complete(
                _session.Engine.CurrentTick,
                state.Score,
                state.MoveCount);

            var dir = RecordingBasePath;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{name}.json");
            File.WriteAllText(path, GameRecordingSerializer.ToJson(recording));
            Debug.Log($"[Recording] Saved as: {path}");

            _recorder = new GameRecorder(in state, _seed);
            return path;
        }

        private void AutoSaveRecording()
        {
            if (_recorder == null || !_recorder.IsRecording || _session == null) return;

            var state = _session.Engine.State;
            var recording = _recorder.Complete(
                _session.Engine.CurrentTick,
                state.Score,
                state.MoveCount);

            var path = SaveToRingBuffer(recording);
            Debug.Log($"[Recording] Auto-saved: {path}");
        }

        private static string SaveToRingBuffer(GameRecording recording)
        {
            var dir = RecordingBasePath;
            Directory.CreateDirectory(dir);

            var indexPath = Path.Combine(dir, "index.txt");
            int slot = 0;
            if (File.Exists(indexPath))
                int.TryParse(File.ReadAllText(indexPath).Trim(), out slot);

            var filePath = Path.Combine(dir, $"recording_{slot}.json");
            File.WriteAllText(filePath, GameRecordingSerializer.ToJson(recording));
            File.WriteAllText(indexPath, ((slot + 1) % RingBufferSize).ToString());

            return filePath;
        }

        /// <summary>
        /// Starts replay mode from a recording file.
        /// </summary>
        public void StartReplay(GameRecording recording)
        {
            if (recording == null) return;

            // Auto-save current game recording before entering replay
            if (_recorder != null && _recorder.IsRecording && _session != null)
            {
                AutoSaveRecording();
            }

            StopReplay();

            // Dispose the previous game session to free resources
            _recorder?.Dispose();
            _recorder = null;
            _session?.Dispose();
            _session = null;

            _factory ??= new GameServiceBuilder().UseDefaultServices().Build();
            _replayController = new ReplayController(recording, _factory);

            // Reset presentation
            _choreographer = new Choreographer();
            _player = new Player();
            _objectiveCollector = new ObjectiveCollectionProcessor();

            // Sync initial visual state from recording
            var seedManager = new SeedManager(recording.RandomSeed);
            var mainRng = seedManager.GetRandom(RandomDomain.Main);
            var initialState = recording.InitialState.ToState(mainRng);
            _player.SyncFromGameState(in initialState);

            _width = recording.InitialState.Width;
            _height = recording.InitialState.Height;

            _isReplaying = true;
            _initialized = true;
            _isPaused = false;
            _isAutoPlaying = false;
            _gameEndFired = false;
            _replayCompletedFired = false;
            _timeAccumulator = 0f;
            _lastMovesRemaining = -1;
            _lastScore = -1;
            _lastObjectiveHash = -1;

            _replayController.Play();

            Debug.Log($"[Replay] Started: {recording.TotalMoves} moves, {recording.DurationTicks} ticks, seed={recording.RandomSeed}");
        }

        /// <summary>
        /// Stops replay mode.
        /// </summary>
        public void StopReplay()
        {
            _replayController?.Dispose();
            _replayController = null;
            _isReplaying = false;
            // Session was disposed when entering replay, so bridge is uninitialized.
            // User must restart the game to resume playing.
            _initialized = false;
        }

        /// <summary>
        /// Adds a bookmark at the current tick.
        /// Press during gameplay to mark a point for later investigation.
        /// </summary>
        public void AddBookmark()
        {
            if (_recorder == null || _session == null) return;

            var tick = _session.Engine.CurrentTick;
            _recorder.AddBookmark(tick);
            Debug.Log($"[Bookmark] Added at tick {tick} (move #{_session.Engine.State.MoveCount})");
        }

        /// <summary>
        /// Returns the replay controller for external control (pause/seek/speed).
        /// Null when not in replay mode.
        /// </summary>
        public ReplayController ReplayCtrl => _replayController;

        /// <summary>
        /// Lists available recording files.
        /// </summary>
        public static string[] GetRecordingFiles()
        {
            var dir = RecordingBasePath;
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
        }

        #endregion

        private static bool AreAdjacent(Position a, Position b)
        {
            int dx = System.Math.Abs(a.X - b.X);
            int dy = System.Math.Abs(a.Y - b.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        private void Cleanup()
        {
            StopReplay();
            _recorder?.Dispose();
            _recorder = null;
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
