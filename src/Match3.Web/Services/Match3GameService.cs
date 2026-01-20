using System;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Choreography;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Selection;
using Match3.Core.View;
using Match3.Presentation;
using Match3.Random;
using Microsoft.Extensions.Logging;

namespace Match3.Web.Services;

public class Match3GameService : IDisposable
{
    private readonly ILogger<Match3GameService> _appLogger;
    private readonly IGameServiceFactory _gameServiceFactory;
    private Match3Config? _config;
    private StandardInputSystem? _inputSystem;
    private bool _isAutoPlaying;
    private float _gameSpeed = 1.0f;
    private bool _disposed;
    private CancellationTokenSource? _loopCts;

    // Game Session (from factory)
    private GameSession? _gameSession;

    // Presentation Layer (Pure Player Architecture)
    private Choreographer? _choreographer;
    private Player? _player;

    // Auto-play move selector (Core 层实现)
    private WeightedMoveSelector? _autoPlaySelector;

    public event Action? OnChange;

    public Match3GameService(ILogger<Match3GameService> appLogger, IGameServiceFactory gameServiceFactory)
    {
        _appLogger = appLogger;
        _gameServiceFactory = gameServiceFactory;
    }

    public SimulationEngine? SimulationEngine => _gameSession?.Engine;
    public Match3Config? Config => _config;
    public VisualState? VisualState => _player?.VisualState;
    public bool IsAutoPlaying => _isAutoPlaying;
    public bool IsPaused => _gameSession?.Engine.IsPaused ?? false;
    public bool HasActiveAnimations => _player?.HasActiveAnimations ?? false;

    public string StatusMessage
    {
        get
        {
            var engine = _gameSession?.Engine;
            if (engine == null) return "Loading...";
            if (engine.IsPaused) return "Paused";
            if (HasActiveAnimations) return "Animating...";
            if (!engine.IsStable()) return "Processing...";
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

    /// <summary>
    /// Current objective progress array (4 slots).
    /// </summary>
    public ObjectiveProgress[] Objectives => _gameSession?.Engine.State.ObjectiveProgress ?? Array.Empty<ObjectiveProgress>();

    /// <summary>
    /// Current level status.
    /// </summary>
    public LevelStatus LevelStatus => _gameSession?.Engine.State.LevelStatus ?? LevelStatus.InProgress;

    public void StartNewGame(LevelConfig? levelConfig = null)
    {
        StopLoop();

        if (levelConfig != null)
        {
            Width = levelConfig.Width;
            Height = levelConfig.Height;
        }

        // Create configuration for the game session
        var configuration = new GameServiceConfiguration
        {
            Width = Width,
            Height = Height,
            TileTypesCount = 6,
            RngSeed = Environment.TickCount,
            EnableEventCollection = true,
            SimulationConfig = SimulationConfig.ForHumanPlay()
        };

        // Dispose previous session if exists
        _gameSession?.Dispose();

        // Create game session using factory
        _gameSession = _gameServiceFactory.CreateGameSession(configuration, levelConfig);

        _config = new Match3Config(Width, Height, 6);

        // Presentation layer (Pure Player Architecture)
        _choreographer = new Choreographer();
        _player = new Player();
        _player.SyncFromGameState(_gameSession.Engine.State);

        // Auto-play selector - create a fresh match finder for the selector
        var matchFinder = new ClassicMatchFinder(new Core.Systems.Matching.Generation.BombGenerator());
        var uiRandom = _gameSession.SeedManager.GetRandom(RandomDomain.Main);
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

    private void OnInputTap(Position p)
    {
        var engine = _gameSession?.Engine;
        if (engine == null) return;

        // Use SimulationEngine's built-in tap handling
        engine.HandleTap(p);
    }

    private void OnInputSwipe(Position from, Direction dir)
    {
        var engine = _gameSession?.Engine;
        if (engine == null) return;

        var offset = dir switch
        {
            Direction.Up => new Position(0, -1),
            Direction.Down => new Position(0, 1),
            Direction.Left => new Position(-1, 0),
            Direction.Right => new Position(1, 0),
            _ => new Position(0, 0)
        };
        var to = new Position(from.X + offset.X, from.Y + offset.Y);
        engine.ApplyMove(from, to);
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
            var session = _gameSession;
            if (session != null && _player != null && _choreographer != null)
            {
                float dt = (FrameMs / 1000.0f) * _gameSpeed;

                // Tick the simulation
                session.Engine.Tick(dt);

                // Convert events to render commands and append to player
                var events = session.DrainEvents();
                if (events.Count > 0)
                {
                    var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                    _player.Append(commands);
                }

                // Update player (execute commands, update visual state)
                _player.Tick(dt);

                // Sync falling tile positions from physics simulation
                // This handles gravity-based movement that doesn't go through the event system
                _player.VisualState.SyncFallingTilesFromGameState(session.Engine.State);

                // Update visual effects
                _player.VisualState.UpdateEffects(dt);

                // Auto-play: make random move when stable
                if (_isAutoPlaying && session.Engine.IsStable() && !HasActiveAnimations)
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
        var engine = _gameSession?.Engine;
        if (engine == null || _autoPlaySelector == null) return;

        // 使棋盘变化后的缓存失效
        _autoPlaySelector.InvalidateCache();

        // 使用 Core 层的加权移动选择器
        var state = engine.State;
        if (_autoPlaySelector.TryGetMove(in state, out var action))
        {
            if (action.ActionType == MoveActionType.Tap)
            {
                engine.HandleTap(action.From);
            }
            else
            {
                engine.ApplyMove(action.From, action.To);
            }
        }
    }

    public void ToggleAutoPlay()
    {
        _isAutoPlaying = !_isAutoPlaying;
    }

    public void TogglePause()
    {
        _gameSession?.Engine.SetPaused(!IsPaused);
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
        _gameSession?.Engine.Tick(dt);
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
        _gameSession?.Dispose();
    }
}
