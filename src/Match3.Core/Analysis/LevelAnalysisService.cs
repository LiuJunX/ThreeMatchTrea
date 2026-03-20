using System;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Match3.Core.Systems.Spawning;
using Match3.Core.Models.Enums;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// 关卡分析服务实现
/// </summary>
public sealed class LevelAnalysisService : ILevelAnalysisService
{
    /// <summary>
    /// ThreadLocal 缓存模拟组件，避免重复创建
    /// </summary>
    private static readonly ThreadLocal<SimulationContext> _contextCache =
        new(() => new SimulationContext(), trackAllValues: false);

    public async Task<LevelAnalysisResult> AnalyzeAsync(
        LevelData levelData,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new AnalysisConfig();

        // 在后台线程执行
        return await Task.Run(() => RunAnalysis(levelData, config, progress, cancellationToken),
            cancellationToken);
    }

    public async Task<LevelAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new AnalysisConfig();

        // 在后台线程执行
        return await Task.Run(() => RunAnalysisFromConfig(levelConfig, config, progress, cancellationToken),
            cancellationToken);
    }

    private LevelAnalysisResult RunAnalysisFromConfig(
        LevelConfig levelConfig,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var initialState = CreateInitialStateFromConfig(levelConfig);
        return RunAnalysisCore(initialState, config, progress, cancellationToken);
    }

    private LevelAnalysisResult RunAnalysis(
        LevelData levelData,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var initialState = CreateInitialState(levelData);
        return RunAnalysisCore(initialState, config, progress, cancellationToken);
    }

    private LevelAnalysisResult RunAnalysisCore(
        GameState initialState,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Shared aggregation state — mutated under lock by the runner
        int winCount = 0;
        int deadlockCount = 0;
        int outOfMovesCount = 0;
        long totalMovesUsed = 0;
        long totalScore = 0;
        float totalLossCompletionRate = 0;
        long totalWinRemainingMoves = 0;
        long totalWinRemainingMovesSquared = 0;
        long totalTilesSpawned = 0;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;

        var runner = new AnalysisSimulationRunner<int, SingleGameResult, LevelAnalysisResult>(
            simulate: i => SimulateSingleGame(initialState, AnalysisSeedDerivation.FromSimulationIndex(i)),
            aggregate: result =>
            {
                totalMovesUsed += result.MovesUsed;
                totalScore += result.Score;
                totalTilesSpawned += result.TilesSpawned;

                switch (result.EndReason)
                {
                    case GameEndReason.Win:
                        winCount++;
                        int remaining = moveLimit - result.MovesUsed;
                        totalWinRemainingMoves += remaining;
                        totalWinRemainingMovesSquared += (long)remaining * remaining;
                        break;
                    case GameEndReason.Deadlock:
                        deadlockCount++;
                        totalLossCompletionRate += result.ObjectiveCompletionRate;
                        break;
                    case GameEndReason.OutOfMoves:
                        outOfMovesCount++;
                        totalLossCompletionRate += result.ObjectiveCompletionRate;
                        break;
                }
            },
            buildResult: (completed, total, elapsedMs, wasCancelled) =>
            {
                int lossCount = deadlockCount + outOfMovesCount;
                return new LevelAnalysisResult
                {
                    TotalSimulations = completed,
                    WinCount = winCount,
                    DeadlockCount = deadlockCount,
                    OutOfMovesCount = outOfMovesCount,
                    AverageMovesUsed = completed > 0 ? (float)totalMovesUsed / completed : 0,
                    AverageScore = completed > 0 ? (float)totalScore / completed : 0,
                    ElapsedMs = elapsedMs,
                    WasCancelled = wasCancelled,
                    AvgLossObjectiveCompletion = lossCount > 0 ? totalLossCompletionRate / lossCount : 0,
                    AvgWinRemainingMoves = winCount > 0 ? (float)totalWinRemainingMoves / winCount : 0,
                    StdDevWinRemainingMoves = winCount > 1
                        ? (float)System.Math.Sqrt(System.Math.Max(0,
                            (double)totalWinRemainingMovesSquared / winCount
                            - System.Math.Pow((double)totalWinRemainingMoves / winCount, 2)))
                        : 0,
                    AvgScorePerMove = totalMovesUsed > 0 ? (float)totalTilesSpawned / totalMovesUsed : 0
                };
            },
            reportProgress: progress != null
                ? (completed, total) => progress.Report(new SimulationProgress
                {
                    CompletedCount = completed,
                    TotalCount = total,
                    WinCount = winCount,
                    DeadlockCount = deadlockCount
                })
                : (Action<int, int>?)null,
            progressReportInterval: config.ProgressReportInterval);

        return runner.Run(
            config.SimulationCount,
            i => i,
            config.UseParallel,
            cancellationToken);
    }

    private GameState CreateInitialState(LevelData levelData)
    {
        var random = new XorShift64(12345);
        var state = new GameState(levelData.Width, levelData.Height, levelData.TileTypesCount, random)
        {
            MoveLimit = levelData.MoveLimit
        };

        // 初始化棋盘(无初始匹配)
        var types = GetTileTypes(levelData.TileTypesCount);
        for (int y = 0; y < levelData.Height; y++)
        {
            for (int x = 0; x < levelData.Width; x++)
            {
                int idx = y * levelData.Width + x;
                Models.Enums.ElementType type;
                do
                {
                    type = types[random.Next(types.Length)];
                } while (WouldCreateMatch(state, x, y, type));

                state.SetTile(x, y, new Tile(idx + 1, type, x, y));
            }
        }

        return state;
    }

    private GameState CreateInitialStateFromConfig(LevelConfig levelConfig)
    {
        // 统计关卡中使用的不同颜色数量
        int tileTypesCount = CountDistinctTileTypes(levelConfig.Grid);
        if (tileTypesCount == 0) tileTypesCount = 6; // 默认值

        var random = new XorShift64(12345);
        var state = new GameState(levelConfig.Width, levelConfig.Height, tileTypesCount, random)
        {
            MoveLimit = levelConfig.MoveLimit,
            TargetDifficulty = levelConfig.TargetDifficulty
        };

        // 从 LevelConfig 初始化棋盘
        for (int y = 0; y < levelConfig.Height; y++)
        {
            for (int x = 0; x < levelConfig.Width; x++)
            {
                int idx = y * levelConfig.Width + x;

                // Tile 层
                var type = levelConfig.Grid[idx];

                if (type == Models.Enums.ElementType.None)
                {
                    // 如果是 None 或 Random，生成随机颜色
                    var types = GetTileTypes(tileTypesCount);
                    do
                    {
                        type = types[random.Next(types.Length)];
                    } while (WouldCreateMatch(state, x, y, type));
                }

                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));

                // Ground 层
                if (levelConfig.Grounds != null && idx < levelConfig.Grounds.Length)
                {
                    var groundType = levelConfig.Grounds[idx];
                    if (groundType != Models.Enums.GroundType.None)
                    {
                        byte health = GroundRules.GetDefaultHealth(groundType);
                        if (levelConfig.GroundHealths != null && idx < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[idx] > 0)
                        {
                            health = (byte)levelConfig.GroundHealths[idx];
                        }
                        state.SetGround(x, y, new Ground(groundType, health));
                    }
                }

                // Cover 层
                if (levelConfig.Covers != null && idx < levelConfig.Covers.Length)
                {
                    var coverType = levelConfig.Covers[idx];
                    if (coverType != Models.Enums.CoverType.None)
                    {
                        byte health = CoverRules.GetDefaultHealth(coverType);
                        if (levelConfig.CoverHealths != null && idx < levelConfig.CoverHealths.Length && levelConfig.CoverHealths[idx] > 0)
                        {
                            health = (byte)levelConfig.CoverHealths[idx];
                        }
                        bool isDynamic = CoverRules.IsDynamicType(coverType);
                        state.SetCover(x, y, new Cover(coverType, health, isDynamic));
                    }
                }
            }
        }

        // 初始化关卡目标
        var objectiveSystem = new LevelObjectiveSystem();
        objectiveSystem.Initialize(ref state, levelConfig);

        return state;
    }

    private static int CountDistinctTileTypes(Models.Enums.ElementType[] grid)
    {
        if (grid == null || grid.Length == 0) return 0;

        var seen = new System.Collections.Generic.HashSet<Models.Enums.ElementType>();
        foreach (var type in grid)
        {
            if (type != Models.Enums.ElementType.None && type != Models.Enums.ElementType.ColorBomb)
            {
                seen.Add(type);
            }
        }
        return seen.Count;
    }

    private static Models.Enums.ElementType[] GetTileTypes(int count)
    {
        var allTypes = new[]
        {
            Models.Enums.ElementType.Item1,
            Models.Enums.ElementType.Item2,
            Models.Enums.ElementType.Item3,
            Models.Enums.ElementType.Item4,
            Models.Enums.ElementType.Item5,
            Models.Enums.ElementType.Item6
        };
        var result = new Models.Enums.ElementType[Math.Min(count, allTypes.Length)];
        Array.Copy(allTypes, result, result.Length);
        return result;
    }

    private static bool WouldCreateMatch(in GameState state, int x, int y, Models.Enums.ElementType type)
    {
        // 水平检查
        if (x >= 2 &&
            state.GetType(x - 1, y) == type &&
            state.GetType(x - 2, y) == type)
            return true;

        // 垂直检查
        if (y >= 2 &&
            state.GetType(x, y - 1) == type &&
            state.GetType(x, y - 2) == type)
            return true;

        return false;
    }

    private SingleGameResult SimulateSingleGame(GameState initialState, ulong seed)
    {
        var ctx = _contextCache.Value!;
        ctx.ResetForSimulation(seed, initialState.TileTypesCount);

        var state = initialState.Clone(ctx.StateRandom);

        var physics = ctx.GetPhysics();
        var refill = ctx.GetRefill(state.TileTypesCount);
        var matchFinder = ctx.GetMatchFinder();
        var matchProcessor = ctx.GetMatchProcessor();
        var powerUpHandler = ctx.GetPowerUpHandler();
        var objectiveSystem = ctx.GetObjectiveSystem();

        // 创建 ExplosionSystem 并传入 objectiveSystem 以追踪目标进度
        var coverSystem = new Systems.Layers.CoverSystem(objectiveSystem);
        var groundSystem = new Systems.Layers.GroundSystem(objectiveSystem);
        var explosionSystem = new ExplosionSystem(coverSystem, groundSystem, objectiveSystem);
        var obstacleSystem = new ObstacleSystem(objectiveSystem);
        var cellEliminator = new Systems.Elimination.CellEliminator(coverSystem, groundSystem, objectiveSystem, obstacleSystem);

        using var engine = new SimulationEngine(
            state,
            SimulationConfig.ForAI(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            null,
            NullEventCollector.Instance,
            explosionSystem,
            null, // deadlockDetector
            null, // shuffleSystem
            objectiveSystem,
            cellEliminator: cellEliminator);

        int movesUsed = 0;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;
        int initialTileId = engine.State.NextTileId;
        var endReason = GameEndReason.OutOfMoves;

        while (movesUsed < moveLimit)
        {
            var currentState = engine.State;
            var validMoves = ValidMoveDetector.FindAllValidMoves(in currentState, matchFinder);

            if (validMoves.Count == 0)
            {
                endReason = GameEndReason.Deadlock;
                Utility.Pools.Pools.Release(validMoves);
                break;
            }

            int moveIndex = ctx.MoveRandom.Next(0, validMoves.Count);
            var move = validMoves[moveIndex];

            engine.ApplyMove(move.From, move.To);
            engine.RunUntilStable();

            Utility.Pools.Pools.Release(validMoves);
            movesUsed++;

            // 检查胜利条件
            var stateAfterMove = engine.State;
            if (objectiveSystem.IsLevelComplete(in stateAfterMove))
            {
                endReason = GameEndReason.Win;
                break;
            }
        }

        // Calculate objective completion rate
        float completionRate = 0f;
        int activeObjectives = 0;
        var finalState = engine.State;
        for (int i = 0; i < 4; i++)
        {
            var prog = finalState.ObjectiveProgress[i];
            if (prog.IsActive)
            {
                completionRate += prog.TargetCount > 0
                    ? System.Math.Min(1f, (float)prog.CurrentCount / prog.TargetCount)
                    : 1f;
                activeObjectives++;
            }
        }
        if (activeObjectives > 0) completionRate /= activeObjectives;

        int tilesSpawned = finalState.NextTileId - initialTileId;

        return new SingleGameResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            Score = finalState.Score,
            ObjectiveCompletionRate = completionRate,
            TilesSpawned = tilesSpawned
        };
    }

    private enum GameEndReason
    {
        Win,
        Deadlock,
        OutOfMoves
    }

    private readonly struct SingleGameResult
    {
        public GameEndReason EndReason { get; init; }
        public int MovesUsed { get; init; }
        public long Score { get; init; }
        public float ObjectiveCompletionRate { get; init; }
        public int TilesSpawned { get; init; }
    }

    /// <summary>
    /// 模拟上下文缓存
    /// </summary>
    private sealed class SimulationContext
    {
        public readonly Match3Config Config = new();
        public readonly XorShift64 StateRandom = new();
        public readonly XorShift64 MoveRandom = new();
        public readonly BombGenerator BombGenerator = new();
        public readonly SimpleScoreSystem ScoreSystem = new();
        public readonly BombEffectRegistry BombEffects = BombEffectRegistry.CreateDefault();

        private RealtimeGravitySystem? _physics;
        private RandomSpawnModel? _spawnModel;
        private RealtimeRefillSystem? _refill;
        private ClassicMatchFinder? _matchFinder;
        private StandardMatchProcessor? _matchProcessor;
        private BombResolution? _powerUpHandler;
        private LevelObjectiveSystem? _objectiveSystem;

        public RealtimeGravitySystem GetPhysics() =>
            _physics ??= new RealtimeGravitySystem(Config, MoveRandom);

        public LevelObjectiveSystem GetObjectiveSystem() =>
            _objectiveSystem ??= new LevelObjectiveSystem();

        public RandomSpawnModel GetSpawnModel(int tileTypesCount)
        {
            _spawnModel ??= new RandomSpawnModel(StateRandom, tileTypesCount);
            return _spawnModel;
        }

        public RealtimeRefillSystem GetRefill(int tileTypesCount) =>
            _refill ??= new RealtimeRefillSystem(GetSpawnModel(tileTypesCount));

        public ClassicMatchFinder GetMatchFinder() =>
            _matchFinder ??= new ClassicMatchFinder(BombGenerator);

        public StandardMatchProcessor GetMatchProcessor()
        {
            if (_matchProcessor == null)
            {
                var objSys = GetObjectiveSystem();
                var coverSys = new CoverSystem(objSys);
                var groundSys = new GroundSystem(objSys);
                _matchProcessor = new StandardMatchProcessor(ScoreSystem, coverSys, groundSys, BombEffects);
            }
            return _matchProcessor;
        }

        public BombResolution GetPowerUpHandler() =>
            _powerUpHandler ??= new BombResolution(ScoreSystem);

        public void ResetForSimulation(ulong seed, int tileTypesCount)
        {
            StateRandom.SetState(seed);
            MoveRandom.SetState(seed + 1);
            // Reset spawn model with new tile types count if needed
            _spawnModel = new RandomSpawnModel(StateRandom, tileTypesCount);
        }
    }

    private class RandomSpawnModel : ISpawnModel
    {
        private readonly IRandom _rng;
        private readonly int _tileTypesCount;
        private static readonly ElementType[] Colors = new[]
        {
            ElementType.Item1, ElementType.Item2, ElementType.Item3,
            ElementType.Item4, ElementType.Item5, ElementType.Item6
        };

        public RandomSpawnModel(IRandom rng, int tileTypesCount)
        {
            _rng = rng;
            _tileTypesCount = tileTypesCount;
        }

        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            int idx = _rng.Next(0, _tileTypesCount);
            return Colors[idx];
        }
    }

    private sealed class SimpleScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Models.Gameplay.MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(Models.Enums.ElementType t1, Models.Enums.ElementType t2) => 100;
    }
}
