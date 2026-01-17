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
using Match3.Core.Utility.Pools;
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

    // UI random for auto-play feature
    private IRandom? _uiRandom;

    // Match finder for auto-play validation
    private IMatchFinder? _matchFinder;

    // Auto-play weight constants
    private const int WeightNormal = 10;
    private const int WeightUfo = 20;
    private const int WeightLine = 20;      // Horizontal/Vertical
    private const int WeightCross = 30;     // Square5x5
    private const int WeightRainbow = 40;   // Color

    // Auto-play action structure
    private readonly struct AutoPlayAction
    {
        public Position From { get; init; }
        public Position To { get; init; }
        public bool IsTap { get; init; }  // true=点击炸弹, false=交换
        public int Weight { get; init; }
    }

    // Reusable collections to avoid per-frame allocations
    private readonly List<AutoPlayAction> _candidateActions = new();

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
        _uiRandom = seedManager.GetRandom(RandomDomain.Main);

        _config = new Match3Config(Width, Height, 6);
        var config = _config;

        // Core systems
        var spawnModel = new RuleBasedSpawnModel(seedManager.GetRandom(RandomDomain.Refill));
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        _matchFinder = matchFinder;
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
        if (_simulationEngine == null || _matchFinder == null) return;

        var state = _simulationEngine.State;
        _candidateActions.Clear();

        // 1. 搜索所有有效交换（水平）
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                TryAddSwapAction(ref state, new Position(x, y), new Position(x + 1, y));
            }
        }

        // 2. 搜索所有有效交换（垂直）
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                TryAddSwapAction(ref state, new Position(x, y), new Position(x, y + 1));
            }
        }

        // 3. 搜索可点击的炸弹
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var pos = new Position(x, y);
                var tile = state.GetTile(x, y);
                if (IsTappableBomb(in tile) && state.CanInteract(pos) && !tile.IsFalling)
                {
                    _candidateActions.Add(new AutoPlayAction
                    {
                        From = pos,
                        To = default,
                        IsTap = true,
                        Weight = GetTileWeight(in tile)
                    });
                }
            }
        }

        // 4. 加权随机选择并执行
        if (_candidateActions.Count > 0 && _uiRandom != null)
        {
            var action = WeightedRandomSelect(_candidateActions, _uiRandom);
            if (action.IsTap)
            {
                _simulationEngine.HandleTap(action.From);
            }
            else
            {
                _simulationEngine.ApplyMove(action.From, action.To);
            }
        }
    }

    private void TryAddSwapAction(ref GameState state, Position from, Position to)
    {
        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);

        // 基础有效性检查
        if (tileA.Type == TileType.None || tileB.Type == TileType.None) return;
        if (tileA.IsFalling || tileB.IsFalling) return;
        if (!state.CanInteract(from) || !state.CanInteract(to)) return;

        // 计算权重
        int weightA = GetTileWeight(in tileA);
        int weightB = GetTileWeight(in tileB);
        bool isBombA = tileA.Bomb != BombType.None;
        bool isBombB = tileB.Bomb != BombType.None;

        int weight;
        if (isBombA && isBombB)
        {
            // 炸弹+炸弹：相乘（直接触发组合，不生成新炸弹）
            weight = weightA * weightB;
        }
        else
        {
            // 普通消除或炸弹+普通：检查匹配并计算新炸弹权重
            GridUtility.SwapTilesForCheck(ref state, from, to);

            // 获取匹配结果，包含将生成的炸弹信息
            var foci = new[] { from, to };
            var matchGroups = _matchFinder!.FindMatchGroups(in state, foci);

            GridUtility.SwapTilesForCheck(ref state, from, to); // 交换回来

            if (matchGroups.Count == 0)
            {
                ClassicMatchFinder.ReleaseGroups(matchGroups);
                return; // 无匹配
            }

            // 基础权重
            weight = isBombA || isBombB ? weightA + weightB : WeightNormal;

            // 加上将生成的新炸弹权重
            foreach (var group in matchGroups)
            {
                if (group.SpawnBombType != BombType.None)
                {
                    weight += GetBombWeight(group.SpawnBombType);
                }
            }

            ClassicMatchFinder.ReleaseGroups(matchGroups);
        }

        _candidateActions.Add(new AutoPlayAction
        {
            From = from,
            To = to,
            IsTap = false,
            Weight = weight
        });
    }

    private static int GetTileWeight(in Tile tile)
    {
        return GetBombWeight(tile.Bomb);
    }

    private static int GetBombWeight(BombType bomb)
    {
        return bomb switch
        {
            BombType.None => WeightNormal,
            BombType.Ufo => WeightUfo,
            BombType.Horizontal => WeightLine,
            BombType.Vertical => WeightLine,
            BombType.Square5x5 => WeightCross,
            BombType.Color => WeightRainbow,
            _ => WeightNormal
        };
    }

    private static bool IsTappableBomb(in Tile tile)
    {
        return tile.Bomb != BombType.None;
    }

    private static AutoPlayAction WeightedRandomSelect(List<AutoPlayAction> actions, IRandom random)
    {
        int totalWeight = 0;
        foreach (var action in actions)
        {
            totalWeight += action.Weight;
        }

        int randomValue = random.Next(0, totalWeight);
        int cumulative = 0;
        foreach (var action in actions)
        {
            cumulative += action.Weight;
            if (randomValue < cumulative)
            {
                return action;
            }
        }

        return actions[actions.Count - 1]; // fallback
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
