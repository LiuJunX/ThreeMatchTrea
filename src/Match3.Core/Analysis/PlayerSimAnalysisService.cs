using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.AI;
using Match3.Core.AI.Strategies;
using Match3.Core.Config;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// 策略驱动的关卡分析服务
/// 支持玩家群体模拟和分层统计
/// </summary>
public sealed class PlayerSimAnalysisService : ILevelAnalysisService
{
    private static readonly ThreadLocal<SharedSimulationContext> _contextCache =
        new(() => new SharedSimulationContext(), trackAllValues: false);


    public async Task<LevelAnalysisResult> AnalyzeAsync(
        LevelData levelData,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new AnalysisConfig();
        return await Task.Run(() =>
        {
            var random = new XorShift64(12345);
            var initialState = AnalysisUtility.CreateInitialState(levelData);
            return RunAnalysisCore(initialState, config, progress, cancellationToken);
        }, cancellationToken);
    }

    public async Task<LevelAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new AnalysisConfig();
        return await Task.Run(() =>
        {
            var random = new XorShift64(12345);
            var initialState = AnalysisUtility.CreateInitialStateFromConfig(levelConfig, random);
            var (initDeadlockRate, initShuffleRate) = RandomAnalysisService.MeasureInitQualityFromConfig(levelConfig);
            return RunAnalysisCore(initialState, config, progress, cancellationToken,
                initDeadlockRate, initShuffleRate);
        }, cancellationToken);
    }

    private LevelAnalysisResult RunAnalysisCore(
        GameState initialState,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken,
        float initDeadlockRate = 0f,
        float initShuffleRate = 0f)
    {
        if (config.Mode == SimulationMode.PlayerPopulation)
        {
            return RunPopulationAnalysis(initialState, config, progress, cancellationToken,
                initDeadlockRate, initShuffleRate);
        }
        else
        {
            return RunSingleStrategyAnalysis(initialState, config, progress, cancellationToken);
        }
    }

    private LevelAnalysisResult RunPopulationAnalysis(
        GameState initialState,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken,
        float initDeadlockRate = 0f,
        float initShuffleRate = 0f)
    {
        var popConfig = config.PopulationConfig ?? new PlayerPopulationConfig();
        var tiers = popConfig.Tiers;
        int total = config.SimulationCount;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;

        // 分配每个分层的模拟次数
        var tierSimCounts = DistributeSimulations(tiers, total);

        // 统计数据
        var tierStats = new TierStatistics[tiers.Length];
        for (int i = 0; i < tiers.Length; i++)
        {
            tierStats[i] = new TierStatistics { TierName = tiers[i].Name };
        }

        int globalWins = 0, globalDeadlocks = 0, globalOutOfMoves = 0;
        long globalMoves = 0, globalScores = 0;

        // 进度分布追踪
        var progressAccumulator = new float[moveLimit + 1];
        var progressCounts = new int[moveLimit + 1];

        // 剩余步数统计（用于胜利的情况）
        int remaining0to2 = 0, remaining3to5 = 0, remaining6to10 = 0, remaining10plus = 0;
        int totalWinsForRemaining = 0;

        // L1 diagnostics
        float totalLossCompletionRate = 0;
        long totalWinRemainingMoves = 0;
        long totalWinRemainingMovesSquared = 0;
        long totalTilesSpawned = 0;

        // 创建所有模拟任务（混合各分层）
        var simTasks = new List<(int tierIdx, int simIdx)>();
        int globalSimIdx = 0;
        for (int tierIdx = 0; tierIdx < tiers.Length; tierIdx++)
        {
            for (int i = 0; i < tierSimCounts[tierIdx]; i++)
            {
                simTasks.Add((tierIdx, globalSimIdx++));
            }
        }

        // 随机打乱任务顺序，使各分层交错执行
        ShuffleTasks(simTasks, 12345);

        var runner = new AnalysisSimulationRunner<(int tierIdx, int simIdx), SingleGameResult, LevelAnalysisResult>(
            simulate: task =>
            {
                var (tierIdx, simIdx) = task;
                var tierConfig = tiers[tierIdx];
                var result = SimulateSingleGameWithStrategy(
                    initialState,
                    AnalysisSeedDerivation.FromSimulationIndex(simIdx),
                    tierConfig,
                    moveLimit);
                // Tag the result with the tier index so aggregate can route it
                return new SingleGameResult
                {
                    EndReason = result.EndReason,
                    MovesUsed = result.MovesUsed,
                    Score = result.Score,
                    ProgressByMove = result.ProgressByMove,
                    ObjectiveCompletionRate = result.ObjectiveCompletionRate,
                    TilesSpawned = result.TilesSpawned,
                    TierIndex = tierIdx
                };
            },
            aggregate: result =>
            {
                UpdateStatistics(ref tierStats[result.TierIndex], result,
                    ref globalWins, ref globalDeadlocks, ref globalOutOfMoves,
                    ref globalMoves, ref globalScores,
                    progressAccumulator, progressCounts, moveLimit,
                    ref remaining0to2, ref remaining3to5, ref remaining6to10, ref remaining10plus,
                    ref totalWinsForRemaining,
                    ref totalLossCompletionRate, ref totalWinRemainingMoves,
                    ref totalWinRemainingMovesSquared, ref totalTilesSpawned);
            },
            buildResult: (completed, totalCount, elapsedMs, wasCancelled) =>
                BuildResult(completed, globalWins, globalDeadlocks, globalOutOfMoves,
                    globalMoves, globalScores, tierStats, progressAccumulator, progressCounts,
                    moveLimit, remaining0to2, remaining3to5, remaining6to10, remaining10plus, totalWinsForRemaining,
                    totalLossCompletionRate, totalWinRemainingMoves, totalWinRemainingMovesSquared, totalTilesSpawned,
                    elapsedMs, wasCancelled, popConfig.OutputTierResults,
                    initDeadlockRate, initShuffleRate),
            reportProgress: progress != null
                ? (completed, totalCount) => progress.Report(new SimulationProgress
                {
                    CompletedCount = completed,
                    TotalCount = totalCount,
                    WinCount = globalWins,
                    DeadlockCount = globalDeadlocks
                })
                : (Action<int, int>?)null,
            progressReportInterval: config.ProgressReportInterval);

        return runner.Run(simTasks, config.UseParallel, cancellationToken);
    }

    private LevelAnalysisResult RunSingleStrategyAnalysis(
        GameState initialState,
        AnalysisConfig config,
        IProgress<SimulationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tierConfig = config.Mode switch
        {
            SimulationMode.Greedy => new PlayerTierConfig
            {
                Name = "Greedy",
                SkillLevel = 1.0f,
                BombPreference = 1.0f,
                ObjectiveFocus = 1.0f,
                Weight = 1.0f
            },
            SimulationMode.BombPriority => new PlayerTierConfig
            {
                Name = "BombPriority",
                SkillLevel = 1.0f,
                BombPreference = 2.0f,
                ObjectiveFocus = 0.5f,
                Weight = 1.0f
            },
            _ => new PlayerTierConfig
            {
                Name = "Random",
                SkillLevel = 0.0f,
                BombPreference = 1.0f,
                ObjectiveFocus = 0.0f,
                Weight = 1.0f
            }
        };

        var modifiedConfig = new AnalysisConfig
        {
            SimulationCount = config.SimulationCount,
            ProgressReportInterval = config.ProgressReportInterval,
            UseParallel = config.UseParallel,
            Mode = SimulationMode.PlayerPopulation,
            PopulationConfig = new PlayerPopulationConfig
            {
                Tiers = new[] { tierConfig },
                OutputTierResults = false
            }
        };

        return RunPopulationAnalysis(initialState, modifiedConfig, progress, cancellationToken);
    }

    private static void ShuffleTasks(List<(int tierIdx, int simIdx)> tasks, int seed)
    {
        var rng = new XorShift64((ulong)seed);
        for (int i = tasks.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (tasks[i], tasks[j]) = (tasks[j], tasks[i]);
        }
    }

    private static void UpdateStatistics(
        ref TierStatistics tierStats,
        SingleGameResult result,
        ref int globalWins, ref int globalDeadlocks, ref int globalOutOfMoves,
        ref long globalMoves, ref long globalScores,
        float[] progressAccumulator, int[] progressCounts, int moveLimit,
        ref int remaining0to2, ref int remaining3to5, ref int remaining6to10, ref int remaining10plus,
        ref int totalWinsForRemaining,
        ref float totalLossCompletionRate, ref long totalWinRemainingMoves,
        ref long totalWinRemainingMovesSquared, ref long totalTilesSpawned)
    {
        tierStats.SimCount++;
        tierStats.TotalMoves += result.MovesUsed;
        tierStats.TotalScore += result.Score;
        globalMoves += result.MovesUsed;
        globalScores += result.Score;
        totalTilesSpawned += result.TilesSpawned;

        switch (result.EndReason)
        {
            case GameEndReason.Win:
                tierStats.Wins++;
                globalWins++;

                // 统计剩余步数分布
                int remaining = moveLimit - result.MovesUsed;
                totalWinsForRemaining++;
                totalWinRemainingMoves += remaining;
                totalWinRemainingMovesSquared += (long)remaining * remaining;
                if (remaining <= 2) remaining0to2++;
                else if (remaining <= 5) remaining3to5++;
                else if (remaining <= 10) remaining6to10++;
                else remaining10plus++;
                break;

            case GameEndReason.Deadlock:
                tierStats.Deadlocks++;
                globalDeadlocks++;
                totalLossCompletionRate += result.ObjectiveCompletionRate;
                break;

            case GameEndReason.OutOfMoves:
                tierStats.OutOfMoves++;
                globalOutOfMoves++;
                totalLossCompletionRate += result.ObjectiveCompletionRate;
                break;
        }

        // 累积进度分布
        if (result.ProgressByMove != null)
        {
            for (int m = 0; m < result.ProgressByMove.Length && m <= moveLimit; m++)
            {
                progressAccumulator[m] += result.ProgressByMove[m];
                progressCounts[m]++;
            }
        }
    }

    private static int[] DistributeSimulations(PlayerTierConfig[] tiers, int total)
    {
        var counts = new int[tiers.Length];
        float totalWeight = 0;
        foreach (var t in tiers) totalWeight += t.Weight;

        int assigned = 0;
        for (int i = 0; i < tiers.Length - 1; i++)
        {
            counts[i] = (int)(total * tiers[i].Weight / totalWeight);
            assigned += counts[i];
        }
        counts[tiers.Length - 1] = total - assigned;

        return counts;
    }

    private static LevelAnalysisResult BuildResult(
        int completedCount, int winCount, int deadlockCount, int outOfMovesCount,
        long totalMoves, long totalScores, TierStatistics[] tierStats,
        float[] progressAccumulator, int[] progressCounts, int moveLimit,
        int remaining0to2, int remaining3to5, int remaining6to10, int remaining10plus, int totalWinsForRemaining,
        float totalLossCompletionRate, long totalWinRemainingMoves,
        long totalWinRemainingMovesSquared, long totalTilesSpawned,
        double elapsedMs, bool wasCancelled, bool outputTierResults,
        float initDeadlockRate = 0f, float initShuffleRate = 0f)
    {
        // 构建分层结果
        PlayerTierResult[]? tierResults = null;
        if (outputTierResults && tierStats.Length > 1)
        {
            tierResults = new PlayerTierResult[tierStats.Length];
            for (int i = 0; i < tierStats.Length; i++)
            {
                var s = tierStats[i];
                tierResults[i] = new PlayerTierResult
                {
                    TierName = s.TierName,
                    SimulationCount = s.SimCount,
                    WinCount = s.Wins,
                    DeadlockCount = s.Deadlocks,
                    OutOfMovesCount = s.OutOfMoves,
                    AverageMovesUsed = s.SimCount > 0 ? (float)s.TotalMoves / s.SimCount : 0,
                    AverageScore = s.SimCount > 0 ? (float)s.TotalScore / s.SimCount : 0
                };
            }
        }

        // 构建进度分布
        var avgProgress = new float[moveLimit + 1];
        for (int i = 0; i <= moveLimit; i++)
        {
            avgProgress[i] = progressCounts[i] > 0 ? progressAccumulator[i] / progressCounts[i] : 0;
        }

        // 剩余步数分布（仅胜利局）
        Dictionary<string, float>? remainingMovesDistribution = null;
        if (totalWinsForRemaining > 0)
        {
            remainingMovesDistribution = new Dictionary<string, float>
            {
                ["0-2"] = (float)remaining0to2 / totalWinsForRemaining,
                ["3-5"] = (float)remaining3to5 / totalWinsForRemaining,
                ["6-10"] = (float)remaining6to10 / totalWinsForRemaining,
                ["10+"] = (float)remaining10plus / totalWinsForRemaining
            };
        }

        // 失败原因分布
        int totalFails = deadlockCount + outOfMovesCount;
        Dictionary<string, float>? failureDistribution = null;
        if (totalFails > 0)
        {
            failureDistribution = new Dictionary<string, float>
            {
                ["Deadlock"] = (float)deadlockCount / totalFails,
                ["OutOfMoves"] = (float)outOfMovesCount / totalFails
            };
        }

        int lossCount = deadlockCount + outOfMovesCount;

        return new LevelAnalysisResult
        {
            TotalSimulations = completedCount,
            WinCount = winCount,
            DeadlockCount = deadlockCount,
            OutOfMovesCount = outOfMovesCount,
            AverageMovesUsed = completedCount > 0 ? (float)totalMoves / completedCount : 0,
            AverageScore = completedCount > 0 ? (float)totalScores / completedCount : 0,
            ElapsedMs = elapsedMs,
            WasCancelled = wasCancelled,
            AvgLossObjectiveCompletion = lossCount > 0 ? totalLossCompletionRate / lossCount : 0,
            AvgWinRemainingMoves = winCount > 0 ? (float)totalWinRemainingMoves / winCount : 0,
            StdDevWinRemainingMoves = winCount > 1
                ? (float)System.Math.Sqrt(System.Math.Max(0,
                    (double)totalWinRemainingMovesSquared / winCount
                    - System.Math.Pow((double)totalWinRemainingMoves / winCount, 2)))
                : 0,
            AvgScorePerMove = totalMoves > 0 ? (float)totalTilesSpawned / totalMoves : 0,
            InitDeadlockRate = initDeadlockRate,
            InitShuffleRate = initShuffleRate,
            TierResults = tierResults,
            ProgressDistribution = new StageProgressDistribution
            {
                AverageProgressByMove = avgProgress,
                RemainingMovesDistribution = remainingMovesDistribution,
                FailureReasonDistribution = failureDistribution
            }
        };
    }

    private SingleGameResult SimulateSingleGameWithStrategy(
        GameState initialState,
        ulong seed,
        PlayerTierConfig tierConfig,
        int moveLimit)
    {
        var ctx = _contextCache.Value!;
        ctx.ResetForSimulation(seed, initialState.TileTypesCount);

        var state = initialState.Clone(ctx.StateRandom);

        // 每次模拟创建新的 ObjectiveSystem 以避免状态污染
        var objectiveSystem = ctx.CreateObjectiveSystem();

        // 创建玩家策略
        var profile = new PlayerProfile
        {
            Name = tierConfig.Name,
            SkillLevel = tierConfig.SkillLevel,
            BombPreference = tierConfig.BombPreference,
            ObjectiveFocus = tierConfig.ObjectiveFocus
        };
        var strategy = new SyntheticPlayerStrategy(profile, ctx.MoveRandom);

        using var engine = ctx.CreateEngine(state, objectiveSystem);

        int movesUsed = 0;
        int initialTileId = engine.State.NextTileId;
        var endReason = GameEndReason.OutOfMoves;
        var progressByMove = new float[moveLimit + 1];

        while (movesUsed < moveLimit)
        {
            var currentState = engine.State;
            progressByMove[movesUsed] = AnalysisUtility.CalculateObjectiveProgress(in currentState);

            var matchFinder = ctx.GetMatchFinder();
            var validMoves = ValidMoveDetector.FindAllValidMoves(in currentState, matchFinder);

            if (validMoves.Count == 0)
            {
                endReason = GameEndReason.Deadlock;
                Utility.Pools.Pools.Release(validMoves);
                break;
            }

            ValidMove bestMove;
            if (tierConfig.SkillLevel > 0.01f)
            {
                bestMove = SelectBestMoveWithStrategy(currentState, validMoves, strategy, ctx);
            }
            else
            {
                bestMove = validMoves[ctx.MoveRandom.Next(0, validMoves.Count)];
            }

            engine.ApplyMove(bestMove.From, bestMove.To);
            engine.RunUntilStable();

            Utility.Pools.Pools.Release(validMoves);
            movesUsed++;

            var stateAfterMove = engine.State;
            if (objectiveSystem.IsLevelComplete(in stateAfterMove))
            {
                progressByMove[movesUsed] = 1.0f;
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

        return new SingleGameResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            Score = finalState.Score,
            ProgressByMove = progressByMove,
            ObjectiveCompletionRate = completionRate,
            TilesSpawned = finalState.NextTileId - initialTileId,
            TierIndex = -1 // Will be set by caller context
        };
    }

    private static ValidMove SelectBestMoveWithStrategy(
        GameState currentState,
        List<ValidMove> validMoves,
        SyntheticPlayerStrategy strategy,
        SharedSimulationContext ctx)
    {
        ValidMove bestMove = validMoves[0];
        float bestScore = float.MinValue;

        foreach (var vm in validMoves)
        {
            var move = new Move { From = vm.From, To = vm.To };

            // 使用快速预览获取 move 信息
            var (scoreGained, tilesCleared, isValid) = ctx.QuickPreviewMove(in currentState, vm.From, vm.To);

            if (!isValid) continue;

            var preview = new MovePreview
            {
                Move = move,
                ScoreGained = scoreGained,
                TilesCleared = tilesCleared
            };

            float score = strategy.ScoreMove(in currentState, move, preview);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = vm;
            }
        }

        return bestMove;
    }

    // === Internal Types ===

    private enum GameEndReason { Win, Deadlock, OutOfMoves }

    private readonly struct SingleGameResult
    {
        public GameEndReason EndReason { get; init; }
        public int MovesUsed { get; init; }
        public long Score { get; init; }
        public float[]? ProgressByMove { get; init; }
        public float ObjectiveCompletionRate { get; init; }
        public int TilesSpawned { get; init; }
        /// <summary>
        /// Index into the tiers array, used by the aggregation step to route results.
        /// </summary>
        public int TierIndex { get; init; }
    }

    private struct TierStatistics
    {
        public string TierName;
        public int SimCount;
        public int Wins;
        public int Deadlocks;
        public int OutOfMoves;
        public long TotalMoves;
        public long TotalScore;
    }
}
