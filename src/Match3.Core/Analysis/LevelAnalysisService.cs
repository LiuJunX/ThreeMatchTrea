using System;
using System.Diagnostics;
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
        var sw = Stopwatch.StartNew();

        int winCount = 0;
        int deadlockCount = 0;
        int outOfMovesCount = 0;
        long totalMovesUsed = 0;
        long totalScore = 0;
        int completedCount = 0;

        int total = config.SimulationCount;

        if (config.UseParallel)
        {
            // 并行执行
            int localWins = 0, localDeadlocks = 0, localOutOfMoves = 0;
            long localMoves = 0, localScores = 0;
            int localCompleted = 0;
            object lockObj = new object();
            int lastReportedCount = 0;

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.For(0, total, options, i =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = SimulateSingleGame(initialState, (ulong)(i * 7919 + 12345));

                    // 更新统计并检查是否需要报告进度
                    bool shouldReport = false;
                    int currentCompleted;
                    int currentWins, currentDeadlocks;

                    lock (lockObj)
                    {
                        localCompleted++;
                        currentCompleted = localCompleted;
                        localMoves += result.MovesUsed;
                        localScores += result.Score;

                        switch (result.EndReason)
                        {
                            case GameEndReason.Win:
                                localWins++;
                                break;
                            case GameEndReason.Deadlock:
                                localDeadlocks++;
                                break;
                            case GameEndReason.OutOfMoves:
                                localOutOfMoves++;
                                break;
                        }

                        currentWins = localWins;
                        currentDeadlocks = localDeadlocks;

                        // 检查是否需要报告进度
                        if (progress != null && currentCompleted - lastReportedCount >= config.ProgressReportInterval)
                        {
                            lastReportedCount = currentCompleted;
                            shouldReport = true;
                        }
                    }

                    // 在 lock 外部报告进度，避免阻塞其他线程
                    if (shouldReport)
                    {
                        progress!.Report(new SimulationProgress
                        {
                            CompletedCount = currentCompleted,
                            TotalCount = total,
                            WinCount = currentWins,
                            DeadlockCount = currentDeadlocks
                        });
                    }
                });

                winCount = localWins;
                deadlockCount = localDeadlocks;
                outOfMovesCount = localOutOfMoves;
                totalMovesUsed = localMoves;
                totalScore = localScores;
                completedCount = localCompleted;
            }
            catch (OperationCanceledException)
            {
                winCount = localWins;
                deadlockCount = localDeadlocks;
                outOfMovesCount = localOutOfMoves;
                totalMovesUsed = localMoves;
                totalScore = localScores;
                completedCount = localCompleted;

                sw.Stop();
                return new LevelAnalysisResult
                {
                    TotalSimulations = completedCount,
                    WinCount = winCount,
                    DeadlockCount = deadlockCount,
                    OutOfMovesCount = outOfMovesCount,
                    AverageMovesUsed = completedCount > 0 ? (float)totalMovesUsed / completedCount : 0,
                    AverageScore = completedCount > 0 ? (float)totalScore / completedCount : 0,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    WasCancelled = true
                };
            }
        }
        else
        {
            // 顺序执行
            for (int i = 0; i < total; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return new LevelAnalysisResult
                    {
                        TotalSimulations = completedCount,
                        WinCount = winCount,
                        DeadlockCount = deadlockCount,
                        OutOfMovesCount = outOfMovesCount,
                        AverageMovesUsed = completedCount > 0 ? (float)totalMovesUsed / completedCount : 0,
                        AverageScore = completedCount > 0 ? (float)totalScore / completedCount : 0,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                        WasCancelled = true
                    };
                }

                var result = SimulateSingleGame(initialState, (ulong)(i * 7919 + 12345));
                completedCount++;
                totalMovesUsed += result.MovesUsed;
                totalScore += result.Score;

                switch (result.EndReason)
                {
                    case GameEndReason.Win:
                        winCount++;
                        break;
                    case GameEndReason.Deadlock:
                        deadlockCount++;
                        break;
                    case GameEndReason.OutOfMoves:
                        outOfMovesCount++;
                        break;
                }

                // 报告进度
                if (progress != null && completedCount % config.ProgressReportInterval == 0)
                {
                    progress.Report(new SimulationProgress
                    {
                        CompletedCount = completedCount,
                        TotalCount = total,
                        WinCount = winCount,
                        DeadlockCount = deadlockCount
                    });
                }
            }
        }

        sw.Stop();

        // 最终进度报告
        progress?.Report(new SimulationProgress
        {
            CompletedCount = completedCount,
            TotalCount = total,
            WinCount = winCount,
            DeadlockCount = deadlockCount
        });

        return new LevelAnalysisResult
        {
            TotalSimulations = completedCount,
            WinCount = winCount,
            DeadlockCount = deadlockCount,
            OutOfMovesCount = outOfMovesCount,
            AverageMovesUsed = completedCount > 0 ? (float)totalMovesUsed / completedCount : 0,
            AverageScore = completedCount > 0 ? (float)totalScore / completedCount : 0,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            WasCancelled = false
        };
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
                Models.Enums.TileType type;
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
                var bomb = Models.Enums.BombType.None;
                if (levelConfig.Bombs != null && idx < levelConfig.Bombs.Length)
                {
                    bomb = levelConfig.Bombs[idx];
                }

                // 如果是 None 或 Random，生成随机颜色
                if (type == Models.Enums.TileType.None)
                {
                    var types = GetTileTypes(tileTypesCount);
                    do
                    {
                        type = types[random.Next(types.Length)];
                    } while (WouldCreateMatch(state, x, y, type));
                }

                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y, bomb));

                // Ground 层
                if (levelConfig.Grounds != null && idx < levelConfig.Grounds.Length)
                {
                    var groundType = levelConfig.Grounds[idx];
                    if (groundType != Models.Enums.GroundType.None)
                    {
                        byte health = GroundRules.GetDefaultHealth(groundType);
                        if (levelConfig.GroundHealths != null && idx < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[idx] > 0)
                        {
                            health = levelConfig.GroundHealths[idx];
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
                            health = levelConfig.CoverHealths[idx];
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

    private static int CountDistinctTileTypes(Models.Enums.TileType[] grid)
    {
        if (grid == null || grid.Length == 0) return 0;

        var seen = new System.Collections.Generic.HashSet<Models.Enums.TileType>();
        foreach (var type in grid)
        {
            if (type != Models.Enums.TileType.None && type != Models.Enums.TileType.Rainbow)
            {
                seen.Add(type);
            }
        }
        return seen.Count;
    }

    private static Models.Enums.TileType[] GetTileTypes(int count)
    {
        var allTypes = new[]
        {
            Models.Enums.TileType.Red,
            Models.Enums.TileType.Blue,
            Models.Enums.TileType.Green,
            Models.Enums.TileType.Yellow,
            Models.Enums.TileType.Purple,
            Models.Enums.TileType.Orange
        };
        var result = new Models.Enums.TileType[Math.Min(count, allTypes.Length)];
        Array.Copy(allTypes, result, result.Length);
        return result;
    }

    private static bool WouldCreateMatch(in GameState state, int x, int y, Models.Enums.TileType type)
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

        var state = initialState.Clone();
        state.Random = ctx.StateRandom;

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
            objectiveSystem);

        int movesUsed = 0;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;
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

        return new SingleGameResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            Score = engine.State.Score
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
        private PowerUpHandler? _powerUpHandler;
        private LevelObjectiveSystem? _objectiveSystem;

        public RealtimeGravitySystem GetPhysics() =>
            _physics ??= new RealtimeGravitySystem(Config, MoveRandom);

        public LevelObjectiveSystem GetObjectiveSystem() =>
            _objectiveSystem ??= new LevelObjectiveSystem();

        public RandomSpawnModel GetSpawnModel(int tileTypesCount)
        {
            _spawnModel ??= new RandomSpawnModel(tileTypesCount);
            return _spawnModel;
        }

        public RealtimeRefillSystem GetRefill(int tileTypesCount) =>
            _refill ??= new RealtimeRefillSystem(GetSpawnModel(tileTypesCount));

        public ClassicMatchFinder GetMatchFinder() =>
            _matchFinder ??= new ClassicMatchFinder(BombGenerator);

        public StandardMatchProcessor GetMatchProcessor() =>
            _matchProcessor ??= new StandardMatchProcessor(ScoreSystem, BombEffects);

        public PowerUpHandler GetPowerUpHandler() =>
            _powerUpHandler ??= new PowerUpHandler(ScoreSystem);

        public void ResetForSimulation(ulong seed, int tileTypesCount)
        {
            StateRandom.SetState(seed);
            MoveRandom.SetState(seed + 1);
            _spawnModel?.Reset(tileTypesCount);
        }
    }

    private sealed class RandomSpawnModel : Systems.Spawning.ISpawnModel
    {
        private int _typeCount;
        private int _counter;
        private static readonly Models.Enums.TileType[] AllTypes =
        {
            Models.Enums.TileType.Red, Models.Enums.TileType.Blue, Models.Enums.TileType.Green,
            Models.Enums.TileType.Yellow, Models.Enums.TileType.Purple, Models.Enums.TileType.Orange
        };

        public RandomSpawnModel(int typeCount) => _typeCount = Math.Min(typeCount, AllTypes.Length);

        public void Reset(int typeCount)
        {
            _typeCount = Math.Min(typeCount, AllTypes.Length);
            _counter = 0;
        }

        public Models.Enums.TileType Predict(ref GameState state, int spawnX, in Systems.Spawning.SpawnContext context)
        {
            int idx = (_counter++ + spawnX) % _typeCount;
            return AllTypes[idx];
        }
    }

    private sealed class SimpleScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Models.Gameplay.MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(Models.Enums.TileType t1, Models.Enums.BombType b1,
            Models.Enums.TileType t2, Models.Enums.BombType b2) => 100;
    }
}
