using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core.Analysis.MCTS;

/// <summary>
/// MCTS 配置
/// </summary>
public sealed class MCTSConfig
{
    /// <summary>每次决策的模拟次数</summary>
    public int SimulationsPerMove { get; set; } = 100;

    /// <summary>UCB1 探索常数</summary>
    public float ExplorationConstant { get; set; } = 1.414f;

    /// <summary>Rollout 最大深度</summary>
    public int MaxRolloutDepth { get; set; } = 30;

    /// <summary>是否使用策略引导 rollout</summary>
    public bool UseGuidedRollout { get; set; } = true;

    /// <summary>Rollout 策略的技能水平</summary>
    public float RolloutSkillLevel { get; set; } = 0.7f;

    /// <summary>完整分析的总局数</summary>
    public int TotalGames { get; set; } = 50;

    /// <summary>是否输出详细信息</summary>
    public bool Verbose { get; set; } = false;
}

/// <summary>
/// MCTS 分析结果
/// </summary>
public sealed class MCTSAnalysisResult
{
    /// <summary>理论最优胜率</summary>
    public float OptimalWinRate { get; init; }

    /// <summary>胜利局数</summary>
    public int WinCount { get; init; }

    /// <summary>总局数</summary>
    public int TotalGames { get; init; }

    /// <summary>平均使用步数（仅胜利局）</summary>
    public float AverageMovesUsedOnWin { get; init; }

    /// <summary>最少通关步数</summary>
    public int MinMovesToWin { get; init; }

    /// <summary>平均得分</summary>
    public float AverageScore { get; init; }

    /// <summary>关键决策点（需要特别注意的步骤）</summary>
    public List<CriticalMove>? CriticalMoves { get; init; }

    /// <summary>死锁次数</summary>
    public int DeadlockCount { get; init; }

    /// <summary>步数用尽次数</summary>
    public int OutOfMovesCount { get; init; }

    /// <summary>分析耗时(毫秒)</summary>
    public double ElapsedMs { get; init; }
}

/// <summary>
/// 关键决策点
/// </summary>
public sealed class CriticalMove
{
    /// <summary>步数（第几步）</summary>
    public int MoveNumber { get; init; }

    /// <summary>最佳移动</summary>
    public ValidMove BestMove { get; init; }

    /// <summary>最佳移动的胜率</summary>
    public float BestMoveWinRate { get; init; }

    /// <summary>次优移动的胜率</summary>
    public float SecondBestWinRate { get; init; }

    /// <summary>胜率差距（体现决策重要性）</summary>
    public float WinRateGap => BestMoveWinRate - SecondBestWinRate;
}

/// <summary>
/// MCTS 关卡分析器
/// </summary>
public sealed class MCTSAnalyzer
{
    private readonly MCTSConfig _config;
    private readonly Match3Config _gameConfig = new();

    public MCTSAnalyzer(MCTSConfig? config = null)
    {
        _config = config ?? new MCTSConfig();
    }

    /// <summary>
    /// 分析关卡的理论难度
    /// </summary>
    public async Task<MCTSAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RunAnalysis(levelConfig, progress, cancellationToken), cancellationToken);
    }

    private MCTSAnalysisResult RunAnalysis(
        LevelConfig levelConfig,
        IProgress<float>? progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        // 使用并行执行多局游戏
        var results = new ConcurrentBag<SingleGameMCTSResult>();
        int completedGames = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        Parallel.For(0, _config.TotalGames, parallelOptions, game =>
        {
            // 每个线程使用独立的种子
            var result = PlaySingleGameWithMCTS(levelConfig, (ulong)(game * 7919 + 12345));
            results.Add(result);

            // 报告进度（线程安全的递增）
            var completed = Interlocked.Increment(ref completedGames);
            progress?.Report((float)completed / _config.TotalGames);
        });

        sw.Stop();

        // 聚合结果
        int wins = 0;
        int deadlocks = 0;
        int outOfMoves = 0;
        long totalScore = 0;
        int totalMovesOnWin = 0;
        int minMovesToWin = int.MaxValue;
        var allCriticalMoves = new List<CriticalMove>();

        foreach (var result in results)
        {
            totalScore += result.Score;

            switch (result.EndReason)
            {
                case TerminalState.Win:
                    wins++;
                    totalMovesOnWin += result.MovesUsed;
                    minMovesToWin = Math.Min(minMovesToWin, result.MovesUsed);
                    break;
                case TerminalState.Deadlock:
                    deadlocks++;
                    break;
                case TerminalState.OutOfMoves:
                    outOfMoves++;
                    break;
            }

            if (result.CriticalMoves != null)
            {
                allCriticalMoves.AddRange(result.CriticalMoves);
            }
        }

        // 聚合关键决策点（按步数分组，取平均）
        var aggregatedCritical = AggregateCriticalMoves(allCriticalMoves);

        return new MCTSAnalysisResult
        {
            OptimalWinRate = (float)wins / _config.TotalGames,
            WinCount = wins,
            TotalGames = _config.TotalGames,
            AverageMovesUsedOnWin = wins > 0 ? (float)totalMovesOnWin / wins : 0,
            MinMovesToWin = minMovesToWin == int.MaxValue ? 0 : minMovesToWin,
            AverageScore = (float)totalScore / _config.TotalGames,
            CriticalMoves = aggregatedCritical,
            DeadlockCount = deadlocks,
            OutOfMovesCount = outOfMoves,
            ElapsedMs = sw.Elapsed.TotalMilliseconds
        };
    }

    private SingleGameMCTSResult PlaySingleGameWithMCTS(LevelConfig levelConfig, ulong seed)
    {
        var random = new XorShift64(seed);
        var state = CreateInitialState(levelConfig, random);
        var criticalMoves = new List<CriticalMove>();

        int moveLimit = levelConfig.MoveLimit > 0 ? levelConfig.MoveLimit : 20;
        int movesUsed = 0;
        var endReason = TerminalState.OutOfMoves;

        // 创建模拟组件
        var context = new SimulationContext(random, state.TileTypesCount);

        while (movesUsed < moveLimit)
        {
            var matchFinder = context.GetMatchFinder();
            var validMoves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

            if (validMoves.Count == 0)
            {
                endReason = TerminalState.Deadlock;
                Utility.Pools.Pools.Release(validMoves);
                break;
            }

            // 使用 MCTS 选择最佳移动
            var (bestMove, moveStats) = SelectBestMoveWithMCTS(state, validMoves, context, moveLimit - movesUsed);

            // 检查是否为关键决策点
            if (moveStats.Count >= 2)
            {
                var sorted = new List<(ValidMove move, float winRate)>(moveStats);
                sorted.Sort((a, b) => b.winRate.CompareTo(a.winRate));

                if (sorted[0].winRate - sorted[1].winRate > 0.1f) // 胜率差距超过 10%
                {
                    criticalMoves.Add(new CriticalMove
                    {
                        MoveNumber = movesUsed + 1,
                        BestMove = sorted[0].move,
                        BestMoveWinRate = sorted[0].winRate,
                        SecondBestWinRate = sorted[1].winRate
                    });
                }
            }

            // 执行移动
            state = ApplyMoveAndGetNewState(state, bestMove, context);

            Utility.Pools.Pools.Release(validMoves);
            movesUsed++;

            // 检查胜利
            if (context.GetObjectiveSystem().IsLevelComplete(in state))
            {
                endReason = TerminalState.Win;
                break;
            }
        }

        return new SingleGameMCTSResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            Score = state.Score,
            CriticalMoves = criticalMoves
        };
    }

    private (ValidMove bestMove, List<(ValidMove move, float winRate)> stats) SelectBestMoveWithMCTS(
        GameState state,
        List<ValidMove> validMoves,
        SimulationContext context,
        int remainingMoves)
    {
        var root = new MCTSNode();
        root.InitializeUntriedMoves(validMoves);

        for (int sim = 0; sim < _config.SimulationsPerMove; sim++)
        {
            // 1. Selection + Expansion
            var node = root;
            var currentState = state.Clone();

            while (!node.IsTerminal && node.IsFullyExpanded && node.HasChildren)
            {
                node = node.SelectBestChild(_config.ExplorationConstant);
                if (node.Move.HasValue)
                {
                    currentState = ApplyMoveAndGetNewState(currentState, node.Move.Value, context);
                }
            }

            // 2. Expansion
            if (!node.IsTerminal && !node.IsFullyExpanded && node.UntriedMoves != null && node.UntriedMoves.Count > 0)
            {
                int moveIndex = context.Random.Next(0, node.UntriedMoves.Count);
                node = node.Expand(moveIndex);

                if (node.Move.HasValue)
                {
                    currentState = ApplyMoveAndGetNewState(currentState, node.Move.Value, context);
                }

                // 检查新节点是否为终端
                var matchFinder = context.GetMatchFinder();
                var newValidMoves = ValidMoveDetector.FindAllValidMoves(in currentState, matchFinder);

                if (context.GetObjectiveSystem().IsLevelComplete(in currentState))
                {
                    node.MarkAsTerminal(TerminalState.Win);
                }
                else if (newValidMoves.Count == 0)
                {
                    node.MarkAsTerminal(TerminalState.Deadlock);
                }
                else if (node.Depth >= remainingMoves)
                {
                    node.MarkAsTerminal(TerminalState.OutOfMoves);
                }
                else
                {
                    node.InitializeUntriedMoves(newValidMoves);
                }

                Utility.Pools.Pools.Release(newValidMoves);
            }

            // 3. Rollout
            float reward;
            if (node.IsTerminal)
            {
                reward = node.TerminalType == TerminalState.Win ? 1f : 0f;
            }
            else
            {
                reward = Rollout(currentState, context, remainingMoves - node.Depth);
            }

            // 4. Backpropagation
            node.Backpropagate(reward);
        }

        // 选择访问次数最多的移动
        var bestChild = root.SelectMostVisitedChild();
        var bestMove = bestChild.Move!.Value;

        // 收集统计信息
        var stats = new List<(ValidMove move, float winRate)>();
        if (root.Children != null)
        {
            foreach (var child in root.Children)
            {
                if (child.Move.HasValue && child.VisitCount > 0)
                {
                    stats.Add((child.Move.Value, child.AverageReward));
                }
            }
        }

        return (bestMove, stats);
    }

    private float Rollout(GameState state, SimulationContext context, int maxDepth)
    {
        var currentState = state.Clone();
        int depth = 0;

        while (depth < maxDepth && depth < _config.MaxRolloutDepth)
        {
            var matchFinder = context.GetMatchFinder();
            var validMoves = ValidMoveDetector.FindAllValidMoves(in currentState, matchFinder);

            if (validMoves.Count == 0)
            {
                Utility.Pools.Pools.Release(validMoves);
                return 0f; // Deadlock
            }

            ValidMove selectedMove;
            if (_config.UseGuidedRollout && context.Random.NextFloat() > 0.1f) // 90% 用快速启发式，10% 随机
            {
                selectedMove = SelectMoveWithFastScorer(in currentState, validMoves, _config.RolloutSkillLevel);
            }
            else
            {
                selectedMove = validMoves[context.Random.Next(0, validMoves.Count)];
            }

            currentState = ApplyMoveAndGetNewState(currentState, selectedMove, context);
            Utility.Pools.Pools.Release(validMoves);

            // 检查胜利
            if (context.GetObjectiveSystem().IsLevelComplete(in currentState))
            {
                return 1f;
            }

            depth++;
        }

        // 未在限制内完成，根据进度给予部分奖励
        float progress = CalculateObjectiveProgress(in currentState);
        return progress * 0.5f; // 部分奖励
    }

    /// <summary>
    /// 使用快速启发式评分选择移动（无需完整模拟）
    /// </summary>
    private static ValidMove SelectMoveWithFastScorer(
        in GameState state,
        List<ValidMove> validMoves,
        float skillLevel)
    {
        ValidMove best = validMoves[0];
        float bestScore = float.MinValue;

        foreach (var vm in validMoves)
        {
            float score = FastMoveScorer.ScoreMove(in state, vm, skillLevel);

            if (score > bestScore)
            {
                bestScore = score;
                best = vm;
            }
        }

        return best;
    }

    private GameState ApplyMoveAndGetNewState(GameState state, ValidMove move, SimulationContext context)
    {
        var newState = state.Clone();
        newState.Random = context.Random;

        using var engine = CreateEngine(newState, context);
        engine.ApplyMove(move.From, move.To);
        engine.RunUntilStable();

        return engine.State;
    }

    private SimulationEngine CreateEngine(GameState state, SimulationContext context)
    {
        var physics = context.GetPhysics();
        var refill = context.GetRefill(state.TileTypesCount);
        var matchFinder = context.GetMatchFinder();
        var matchProcessor = context.GetMatchProcessor();
        var powerUpHandler = context.GetPowerUpHandler();
        var objectiveSystem = context.GetObjectiveSystem();
        var explosionSystem = context.GetExplosionSystem();

        return new SimulationEngine(
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
            null,
            null,
            objectiveSystem);
    }

    private static float CalculateObjectiveProgress(in GameState state) =>
        AnalysisUtility.CalculateObjectiveProgress(in state);

    private List<CriticalMove> AggregateCriticalMoves(List<CriticalMove> allMoves)
    {
        if (allMoves.Count == 0) return new List<CriticalMove>();

        // 按步数分组
        var byMoveNumber = new Dictionary<int, List<CriticalMove>>();
        foreach (var cm in allMoves)
        {
            if (!byMoveNumber.TryGetValue(cm.MoveNumber, out var list))
            {
                list = new List<CriticalMove>();
                byMoveNumber[cm.MoveNumber] = list;
            }
            list.Add(cm);
        }

        // 取出现次数最多的关键步骤
        var result = new List<CriticalMove>();
        foreach (var (moveNum, moves) in byMoveNumber)
        {
            if (moves.Count >= _config.TotalGames * 0.2f) // 至少 20% 的局中出现
            {
                float avgBestWinRate = 0;
                float avgSecondWinRate = 0;
                foreach (var m in moves)
                {
                    avgBestWinRate += m.BestMoveWinRate;
                    avgSecondWinRate += m.SecondBestWinRate;
                }
                avgBestWinRate /= moves.Count;
                avgSecondWinRate /= moves.Count;

                result.Add(new CriticalMove
                {
                    MoveNumber = moveNum,
                    BestMove = moves[0].BestMove,
                    BestMoveWinRate = avgBestWinRate,
                    SecondBestWinRate = avgSecondWinRate
                });
            }
        }

        result.Sort((a, b) => a.MoveNumber.CompareTo(b.MoveNumber));
        return result;
    }

    private static GameState CreateInitialState(LevelConfig levelConfig, XorShift64 random) =>
        AnalysisUtility.CreateInitialStateFromConfig(levelConfig, random);

    // === Internal Types ===

    private readonly struct SingleGameMCTSResult
    {
        public TerminalState EndReason { get; init; }
        public int MovesUsed { get; init; }
        public long Score { get; init; }
        public List<CriticalMove>? CriticalMoves { get; init; }
    }

    private sealed class SimulationContext
    {
        public readonly XorShift64 Random;
        public readonly Match3Config Config = new();
        public readonly BombGenerator BombGenerator = new();
        public readonly SimpleScoreSystem ScoreSystem = new();
        public readonly BombEffectRegistry BombEffects = BombEffectRegistry.CreateDefault();

        private readonly int _tileTypesCount;
        private RealtimeGravitySystem? _physics;
        private RandomSpawnModel? _spawnModel;
        private RealtimeRefillSystem? _refill;
        private ClassicMatchFinder? _matchFinder;
        private StandardMatchProcessor? _matchProcessor;
        private BombResolution? _powerUpHandler;
        private LevelObjectiveSystem? _objectiveSystem;

        // 缓存的层系统对象（避免重复创建）
        private CoverSystem? _coverSystem;
        private GroundSystem? _groundSystem;
        private ExplosionSystem? _explosionSystem;

        public SimulationContext(XorShift64 random, int tileTypesCount)
        {
            Random = random;
            _tileTypesCount = tileTypesCount;
        }

        public RealtimeGravitySystem GetPhysics() =>
            _physics ??= new RealtimeGravitySystem(Config, Random);

        /// <summary>
        /// 获取 ObjectiveSystem（每次游戏需要重置）
        /// </summary>
        public LevelObjectiveSystem GetObjectiveSystem() =>
            _objectiveSystem ??= new LevelObjectiveSystem();

        /// <summary>
        /// 为新游戏创建新的 ObjectiveSystem
        /// </summary>
        public LevelObjectiveSystem CreateNewObjectiveSystem()
        {
            _objectiveSystem = new LevelObjectiveSystem();
            // 重置依赖 ObjectiveSystem 的系统
            _coverSystem = null;
            _groundSystem = null;
            _explosionSystem = null;
            return _objectiveSystem;
        }

        public RandomSpawnModel GetSpawnModel()
        {
            _spawnModel ??= new RandomSpawnModel(Random, _tileTypesCount);
            return _spawnModel;
        }

        public RealtimeRefillSystem GetRefill(int tileTypesCount) =>
            _refill ??= new RealtimeRefillSystem(GetSpawnModel());

        public ClassicMatchFinder GetMatchFinder() =>
            _matchFinder ??= new ClassicMatchFinder(BombGenerator);

        public StandardMatchProcessor GetMatchProcessor()
        {
            if (_matchProcessor == null)
            {
                var objSys = GetObjectiveSystem();
                _coverSystem ??= new CoverSystem(objSys);
                _groundSystem ??= new GroundSystem(objSys);
                _matchProcessor = new StandardMatchProcessor(ScoreSystem, _coverSystem, _groundSystem, BombEffects);
            }
            return _matchProcessor;
        }

        public BombResolution GetPowerUpHandler() =>
            _powerUpHandler ??= new BombResolution(ScoreSystem);

        /// <summary>
        /// 获取缓存的 ExplosionSystem（复用以减少对象分配）
        /// </summary>
        public ExplosionSystem GetExplosionSystem()
        {
            if (_explosionSystem == null)
            {
                var objSys = GetObjectiveSystem();
                _coverSystem = new CoverSystem(objSys);
                _groundSystem = new GroundSystem(objSys);
                _explosionSystem = new ExplosionSystem(_coverSystem, _groundSystem, objSys);
            }
            return _explosionSystem;
        }
    }

    private class RandomSpawnModel : Systems.Spawning.ISpawnModel
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

        public ElementType Predict(ref GameState state, int spawnX, in Systems.Spawning.SpawnContext context)
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
