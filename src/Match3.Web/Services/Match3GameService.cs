using System;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
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
using Match3.Core.Systems.Selection;
using Match3.Core.Systems.Spawning;
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
    private PresentationController? _presentationController;

    // Auto-play move selector (Core 层实现)
    private WeightedMoveSelector? _autoPlaySelector;

    public event Action? OnChange;

    public Match3GameService(ILogger<Match3GameService> appLogger)
    {
        _appLogger = appLogger;
    }

    public SimulationEngine? SimulationEngine => _simulationEngine;
    public Match3Config? Config => _config;
    public VisualState? VisualState => _presentationController?.VisualState;
    public bool IsAutoPlaying => _isAutoPlaying;
    public bool IsPaused => _simulationEngine?.IsPaused ?? false;

    public string StatusMessage
    {
        get
        {
            if (_simulationEngine == null) return "Loading...";
            if (_simulationEngine.IsPaused) return "Paused";
            if (_presentationController?.HasActiveAnimations == true) return "Animating...";
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
        var uiRandom = seedManager.GetRandom(RandomDomain.Main);

        _config = new Match3Config(Width, Height, 6);
        var config = _config;

        // Core systems
        var spawnModel = new RuleBasedSpawnModel(seedManager.GetRandom(RandomDomain.Refill));
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StandardScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, bombRegistry);
        var explosionSystem = new ExplosionSystem();
        var powerUpHandler = new PowerUpHandler(scoreSystem);
        var physics = new RealtimeGravitySystem(config, seedManager.GetRandom(RandomDomain.Physics));
        var refill = new RealtimeRefillSystem(spawnModel);
        var projectileSystem = new ProjectileSystem();

        // Create initial game state
        var tileGenerator = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));
        var initialState = CreateInitialState(Width, Height, rng, tileGenerator, levelConfig);

        // Presentation layer
        _eventCollector = new BufferedEventCollector();
        _presentationController = new PresentationController();

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
            _eventCollector,
            explosionSystem
        );

        // Initialize presentation from game state
        _presentationController.Initialize(_simulationEngine.State);

        // Auto-play selector (Core 层实现)
        _autoPlaySelector = new WeightedMoveSelector(matchFinder, uiRandom);

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

    private GameState CreateInitialState(int width, int height, IRandom rng,
        StandardTileGenerator tileGenerator, LevelConfig? levelConfig)
    {
        var state = new GameState(width, height, 6, rng);

        // If there's a valid LevelConfig with tiles, use BoardInitializer
        if (levelConfig?.Grid != null && HasValidTiles(levelConfig.Grid))
        {
            var initializer = new BoardInitializer(tileGenerator);
            initializer.Initialize(ref state, levelConfig);
        }
        else
        {
            // Generate initial tiles without matches (random)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var type = tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                }
            }
        }

        return state;
    }

    private static bool HasValidTiles(TileType[] grid)
    {
        // Check if there's at least one non-None tile
        foreach (var t in grid)
        {
            if (t != TileType.None) return true;
        }
        return false;
    }

    private void OnInputTap(Position p)
    {
        if (_simulationEngine == null) return;

        // Use SimulationEngine's built-in tap handling
        _simulationEngine.HandleTap(p);
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
            if (_simulationEngine != null && _eventCollector != null && _presentationController != null)
            {
                float dt = (FrameMs / 1000.0f) * _gameSpeed;

                // Tick the simulation
                _simulationEngine.Tick(dt);

                // Update presentation (events -> animations -> sync)
                var events = _eventCollector.DrainEvents();
                _presentationController.Update(dt, events, _simulationEngine.State);

                // Auto-play: make random move when stable
                if (_isAutoPlaying && _simulationEngine.IsStable() && !_presentationController.HasActiveAnimations)
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

    private void TryMakeRandomMove()
    {
        if (_simulationEngine == null || _autoPlaySelector == null) return;

        // 使棋盘变化后的缓存失效
        _autoPlaySelector.InvalidateCache();

        // 使用 Core 层的加权移动选择器
        var state = _simulationEngine.State;
        if (_autoPlaySelector.TryGetMove(in state, out var action))
        {
            if (action.ActionType == MoveActionType.Tap)
            {
                _simulationEngine.HandleTap(action.From);
            }
            else
            {
                _simulationEngine.ApplyMove(action.From, action.To);
            }
        }
    }

    public void ToggleAutoPlay()
    {
        _isAutoPlaying = !_isAutoPlaying;
    }

    public void TogglePause()
    {
        _simulationEngine?.SetPaused(!IsPaused);
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
