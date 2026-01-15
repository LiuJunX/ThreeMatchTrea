using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Utility;
using Match3.Core.View;
using Match3.Presentation;
using Match3.Random;
using Microsoft.Extensions.Logging;

namespace Match3.Web.Services;

public class Match3GameService : IDisposable
{
    private readonly ILogger<Match3GameService> _appLogger;
    private Match3Config? _config;
    private StandardInputSystem? _inputSystem;
    private bool _isAutoPlaying;
    private float _gameSpeed = 1.0f;
    private bool _disposed;
    private CancellationTokenSource? _loopCts;

    // Simulation Engine (new)
    private SimulationEngine? _simulationEngine;

    // Presentation Layer
    private BufferedEventCollector? _eventCollector;
    private EventInterpreter? _eventInterpreter;
    private AnimationTimeline? _animationTimeline;
    private VisualState? _visualState;

    public event Action? OnChange;

    public Match3GameService(ILogger<Match3GameService> appLogger)
    {
        _appLogger = appLogger;
    }

    public SimulationEngine? SimulationEngine => _simulationEngine;
    public Match3Config? Config => _config;
    public VisualState? VisualState => _visualState;
    public bool IsAutoPlaying => _isAutoPlaying;

    public string StatusMessage
    {
        get
        {
            if (_simulationEngine == null) return "Loading...";
            if (_animationTimeline?.HasActiveAnimations == true) return "Animating...";
            if (!_simulationEngine.IsStable()) return "Processing...";
            return "Ready";
        }
    }
    public float GameSpeed
    {
        get => _gameSpeed;
        set => _gameSpeed = Math.Clamp(value, 0.1f, 5.0f);
    }
    public int LastMatchesCount { get; private set; }

    public int Width { get; private set; } = 8;
    public int Height { get; private set; } = 8;
    public const int CellSize = 66; // Exposed for UI

    public void StartNewGame(LevelConfig? levelConfig = null)
    {
        StopLoop();

        if (levelConfig != null)
        {
            Width = levelConfig.Width;
            Height = levelConfig.Height;
        }

        var rngSeed = Environment.TickCount;
        var seedManager = new SeedManager(rngSeed);
        var rng = seedManager.GetRandom(RandomDomain.Main);

        _config = new Match3Config(Width, Height, 6);
        var config = _config;

        // Core systems
        var spawnModel = new RuleBasedSpawnModel(seedManager.GetRandom(RandomDomain.Refill));
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StandardScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, bombRegistry);
        var powerUpHandler = new PowerUpHandler(scoreSystem);
        var physics = new RealtimeGravitySystem(config, seedManager.GetRandom(RandomDomain.Physics));
        var refill = new RealtimeRefillSystem(spawnModel);
        var projectileSystem = new ProjectileSystem();

        // Create initial game state
        var tileGenerator = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));
        var initialState = CreateInitialState(Width, Height, rng, tileGenerator);

        // Presentation layer
        _eventCollector = new BufferedEventCollector();
        _visualState = new VisualState();
        _animationTimeline = new AnimationTimeline();
        _eventInterpreter = new EventInterpreter(_animationTimeline, _visualState);

        // Create simulation engine with event collector
        _simulationEngine = new SimulationEngine(
            initialState,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            _eventCollector
        );

        // Sync visual state from initial game state
        _visualState.SyncFromGameState(_simulationEngine.State);

        // Input system
        _inputSystem = new StandardInputSystem();
        _inputSystem.Configure(CellSize);
        _inputSystem.TapDetected += OnInputTap;
        _inputSystem.SwipeDetected += OnInputSwipe;

        LastMatchesCount = 0;
        _isAutoPlaying = false;

        StartLoop();
        NotifyStateChanged();
    }

    private GameState CreateInitialState(int width, int height, IRandom rng, StandardTileGenerator tileGenerator)
    {
        var state = new GameState(width, height, 6, rng);

        // Generate initial tiles without matches
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var type = tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
            }
        }

        return state;
    }

    private void OnInputTap(Position p)
    {
        // Tap handling could be used for special tile activation
        // For now, just log it
    }

    private void OnInputSwipe(Position from, Direction dir)
    {
        if (_simulationEngine == null) return;

        var offset = dir switch
        {
            Direction.Up => new Position(0, -1),
            Direction.Down => new Position(0, 1),
            Direction.Left => new Position(-1, 0),
            Direction.Right => new Position(1, 0),
            _ => new Position(0, 0)
        };
        var to = new Position(from.X + offset.X, from.Y + offset.Y);
        _simulationEngine.ApplyMove(from, to);
    }

    public void ResetGame()
    {
        if (_inputSystem != null)
        {
            _inputSystem.TapDetected -= OnInputTap;
            _inputSystem.SwipeDetected -= OnInputSwipe;
        }
        StartNewGame();
    }

    private void StartLoop()
    {
        _loopCts = new CancellationTokenSource();
        _ = GameLoopAsync(_loopCts.Token);
    }

    private void StopLoop()
    {
        _loopCts?.Cancel();
        _loopCts = null;
    }

    private async Task GameLoopAsync(CancellationToken token)
    {
        const int TargetFps = 60;
        const int FrameMs = 1000 / TargetFps;

        while (!token.IsCancellationRequested && !_disposed)
        {
            if (_simulationEngine != null && _eventCollector != null &&
                _eventInterpreter != null && _animationTimeline != null && _visualState != null)
            {
                float dt = (FrameMs / 1000.0f) * _gameSpeed;

                // Tick the simulation
                _simulationEngine.Tick(dt);

                // Process events into animations
                var events = _eventCollector.DrainEvents();
                if (events.Count > 0)
                {
                    _eventInterpreter.InterpretEvents(events);
                }

                // Update animation timeline
                _animationTimeline.Update(dt, _visualState);

                // Sync new tiles from game state (for spawned tiles)
                SyncNewTilesFromGameState();

                // Auto-play: make random move when stable
                if (_isAutoPlaying && _simulationEngine.IsStable() && !_animationTimeline.HasActiveAnimations)
                {
                    TryMakeRandomMove();
                }

                NotifyStateChanged();
            }

            try
            {
                await Task.Delay(FrameMs, token);
            }
            catch (TaskCanceledException) { break; }
        }
    }

    private void SyncNewTilesFromGameState()
    {
        if (_simulationEngine == null || _visualState == null || _animationTimeline == null) return;

        var state = _simulationEngine.State;

        // Collect all tile IDs currently in game state (O(w*h))
        var gameStateTileIds = new HashSet<long>();
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == TileType.None) continue;

                gameStateTileIds.Add(tile.Id);

                // Add tile to visual state if not present
                if (_visualState.GetTile(tile.Id) == null)
                {
                    // Spawn from above with animation
                    var startPos = new System.Numerics.Vector2(x, y - 1);
                    var endPos = new System.Numerics.Vector2(x, y);

                    _visualState.AddTile(
                        tile.Id,
                        tile.Type,
                        tile.Bomb,
                        new Position(x, y),
                        startPos
                    );

                    // Wait for any destroy animations in this column to complete before falling
                    float startTime = _animationTimeline.GetDestroyEndTimeForColumn(x, y);

                    // Create spawn/fall animation (delayed if needed)
                    var animation = new Match3.Presentation.Animations.TileMoveAnimation(
                        _animationTimeline.GenerateAnimationId(),
                        tile.Id,
                        startPos,
                        endPos,
                        startTime,
                        0.15f
                    );
                    _animationTimeline.AddAnimation(animation);
                }
                else
                {
                    // Update position for gravity movement - only if this tile has no active animation
                    if (!_animationTimeline.HasAnimationForTile(tile.Id))
                    {
                        var visual = _visualState.GetTile(tile.Id);
                        var currentPos = visual?.Position ?? new System.Numerics.Vector2(x, y);
                        var targetPos = new System.Numerics.Vector2(x, y);

                        // Check if position actually changed (gravity moved the tile)
                        if (visual != null && (int)currentPos.Y != y)
                        {
                            // Wait for any destroy animations in this column to complete
                            float startTime = _animationTimeline.GetDestroyEndTimeForColumn(x, y);

                            // Create fall animation
                            var animation = new Match3.Presentation.Animations.TileMoveAnimation(
                                _animationTimeline.GenerateAnimationId(),
                                tile.Id,
                                currentPos,
                                targetPos,
                                startTime,
                                0.15f
                            );
                            _animationTimeline.AddAnimation(animation);
                        }
                        else
                        {
                            // No movement needed, just sync position
                            _visualState.SetTilePosition(tile.Id, targetPos);
                        }
                    }
                }
            }
        }

        // Remove tiles from visual state that are no longer in game state (O(n))
        var tileIdsToRemove = new List<long>();
        foreach (var kvp in _visualState.Tiles)
        {
            // Only remove if not in game state AND alpha is near zero (animation complete)
            if (!gameStateTileIds.Contains(kvp.Key) && kvp.Value.Alpha < 0.01f)
            {
                tileIdsToRemove.Add(kvp.Key);
            }
        }

        foreach (var id in tileIdsToRemove)
        {
            _visualState.RemoveTile(id);
        }
    }

    private void TryMakeRandomMove()
    {
        if (_simulationEngine == null) return;

        var state = _simulationEngine.State;
        var validMoves = new List<(Position from, Position to)>();

        // Find all valid horizontal swaps
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);
                if (IsValidSwap(state, from, to))
                {
                    validMoves.Add((from, to));
                }
            }
        }

        // Find all valid vertical swaps
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);
                if (IsValidSwap(state, from, to))
                {
                    validMoves.Add((from, to));
                }
            }
        }

        if (validMoves.Count > 0)
        {
            var random = new System.Random();
            var (from, to) = validMoves[random.Next(validMoves.Count)];
            _simulationEngine.ApplyMove(from, to);
        }
    }

    private bool IsValidSwap(in GameState state, Position from, Position to)
    {
        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);
        return tileA.Type != TileType.None && tileB.Type != TileType.None;
    }

    public void ToggleAutoPlay()
    {
        _isAutoPlaying = !_isAutoPlaying;
    }

    public void HandlePointerDown(int gx, int gy, double sx, double sy)
    {
        _inputSystem?.OnPointerDown(gx, gy, sx, sy);
    }

    public void HandlePointerUp(double sx, double sy)
    {
        _inputSystem?.OnPointerUp(sx, sy);
    }

    /// <summary>
    /// Manually process one frame update. For testing purposes only.
    /// </summary>
    public void ManualUpdate(float dt = 1f / 60f)
    {
        _simulationEngine?.Tick(dt);
    }

    public void SetLastMatches(int count)
    {
        LastMatchesCount = count;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _disposed = true;
        if (_inputSystem != null)
        {
            _inputSystem.TapDetected -= OnInputTap;
            _inputSystem.SwipeDetected -= OnInputSwipe;
        }
        StopLoop();
        _simulationEngine?.Dispose();
    }
}
