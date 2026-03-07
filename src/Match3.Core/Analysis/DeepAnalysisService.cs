using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.AI;
using Match3.Core.AI.Strategies;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// Deep Analysis 服务 - 提供 7 个高级关卡评估指标
/// </summary>
public sealed class DeepAnalysisService
{
    private static readonly ThreadLocal<SharedSimulationContext> _contextCache =
        new(() => new SharedSimulationContext(), trackAllValues: false);

    // 心流权重配置
    private const float WeightObjectiveProgress = 1.0f;
    private const float WeightBombFormation = 0.5f;
    private const float WeightBombActivation = 0.8f;
    private const float WeightCascadeDepth = 0.3f;
    private const float WeightObstacleClear = 0.4f;

    /// <summary>
    /// 执行 Deep Analysis
    /// </summary>
    public async Task<DeepAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        int simulationsPerTier = 250,
        IProgress<DeepAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var initRandom = new XorShift64(12345);
            var initialState = AnalysisUtility.CreateInitialStateFromConfig(levelConfig, initRandom);
            return RunDeepAnalysis(initialState, simulationsPerTier, progress, cancellationToken);
        }, cancellationToken);
    }

    private DeepAnalysisResult RunDeepAnalysis(
        GameState initialState,
        int simulationsPerTier,
        IProgress<DeepAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var tiers = PlayerPopulationConfig.DefaultTiers;
        int totalSimulations = tiers.Length * simulationsPerTier;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;

        // === 统计数据收集器 ===
        var tierResults = new ConcurrentDictionary<string, TierStats>();
        foreach (var tier in tiers)
        {
            tierResults[tier.Name] = new TierStats();
        }

        // 心流曲线累积器 [moveIndex] = (sum, count)
        var flowAccumulator = new ConcurrentDictionary<int, (float sum, int count)>();

        // 瓶颈目标统计
        var bottleneckStats = new ConcurrentDictionary<int, int>(); // objectiveIndex -> failureCount

        // 运气依赖度计算：记录每个玩家每局的结果
        var playerResults = new ConcurrentBag<(string tier, bool[] outcomes)>();

        // P95 计算：记录每个玩家的通关尝试次数
        var clearAttempts = new ConcurrentBag<int>();

        int completedCount = 0;
        object lockObj = new();

        // === 并行模拟 ===
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        try
        {
            // 为每个分层模拟多局
            Parallel.ForEach(tiers, options, tierConfig =>
            {
                // 每个分层模拟多个"玩家"，每个玩家玩多局
                int playersPerTier = 50;
                int gamesPerPlayer = simulationsPerTier / playersPerTier;

                for (int playerIdx = 0; playerIdx < playersPerTier; playerIdx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var playerOutcomes = new bool[gamesPerPlayer];
                    int attemptsToWin = 0;
                    bool hasWon = false;

                    for (int gameIdx = 0; gameIdx < gamesPerPlayer; gameIdx++)
                    {
                        // 使用稳定的 seed 计算，避免 GetHashCode 负值问题
                        ulong tierSeed = (ulong)(Array.IndexOf(tiers, tierConfig) + 1);
                        ulong seed = tierSeed * 1000000UL + (ulong)playerIdx * 1000UL + (ulong)gameIdx;
                        var result = SimulateSingleGame(initialState, seed, tierConfig, moveLimit);

                        // 记录结果
                        playerOutcomes[gameIdx] = result.Won;

                        // P95 计算
                        if (!hasWon)
                        {
                            attemptsToWin++;
                            if (result.Won)
                            {
                                hasWon = true;
                                clearAttempts.Add(attemptsToWin);
                            }
                        }

                        // 更新分层统计
                        var stats = tierResults[tierConfig.Name];
                        lock (stats)
                        {
                            stats.TotalGames++;
                            if (result.Won) stats.Wins++;
                        }

                        // 累积心流曲线
                        if (result.FlowScores != null)
                        {
                            for (int m = 0; m < result.FlowScores.Length; m++)
                            {
                                flowAccumulator.AddOrUpdate(m,
                                    (result.FlowScores[m], 1),
                                    (_, old) => (old.sum + result.FlowScores[m], old.count + 1));
                            }
                        }

                        // 瓶颈目标统计
                        if (!result.Won && result.BottleneckObjectiveIndex >= 0)
                        {
                            bottleneckStats.AddOrUpdate(result.BottleneckObjectiveIndex, 1, (_, c) => c + 1);
                        }

                        // 进度报告
                        lock (lockObj)
                        {
                            completedCount++;
                            if (completedCount % 50 == 0)
                            {
                                progress?.Report(new DeepAnalysisProgress
                                {
                                    Progress = (float)completedCount / totalSimulations,
                                    CompletedCount = completedCount,
                                    TotalCount = totalSimulations,
                                    Stage = $"模拟 {tierConfig.Name}"
                                });
                            }
                        }
                    }

                    // 记录玩家结果用于运气依赖度计算
                    playerResults.Add((tierConfig.Name, playerOutcomes));

                    // 如果玩家没有通关，记录最大尝试次数
                    if (!hasWon)
                    {
                        clearAttempts.Add(gamesPerPlayer + 5); // 超出范围表示未通关
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new DeepAnalysisResult
            {
                WasCancelled = true,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                TotalSimulations = completedCount
            };
        }

        sw.Stop();

        // === 计算最终指标 ===

        // 1. 心流曲线
        var flowCurve = new float[moveLimit];
        float flowMin = float.MaxValue, flowMax = float.MinValue, flowSum = 0;
        int flowCount = 0;
        for (int i = 0; i < moveLimit; i++)
        {
            if (flowAccumulator.TryGetValue(i, out var acc) && acc.count > 0)
            {
                flowCurve[i] = acc.sum / acc.count;
                flowMin = Math.Min(flowMin, flowCurve[i]);
                flowMax = Math.Max(flowMax, flowCurve[i]);
                flowSum += flowCurve[i];
                flowCount++;
            }
        }
        if (flowMin == float.MaxValue) flowMin = 0;

        // 2. 分层胜率
        var tierWinRates = new Dictionary<string, float>();
        foreach (var kvp in tierResults)
        {
            var stats = kvp.Value;
            tierWinRates[kvp.Key] = stats.TotalGames > 0 ? (float)stats.Wins / stats.TotalGames : 0;
        }

        // 3. 瓶颈目标
        string bottleneckObjective = "";
        float bottleneckFailureRate = 0;
        if (bottleneckStats.Count > 0)
        {
            int totalFailures = bottleneckStats.Values.Sum();
            var worst = bottleneckStats.OrderByDescending(kvp => kvp.Value).First();
            bottleneckObjective = GetObjectiveName(initialState, worst.Key);
            bottleneckFailureRate = totalFailures > 0 ? (float)worst.Value / totalFailures : 0;
        }

        // 4. 技能敏感度
        float noviceWinRate = tierWinRates.GetValueOrDefault("Novice", 0);
        float expertWinRate = tierWinRates.GetValueOrDefault("Expert", 0);
        float skillSensitivity = expertWinRate > 0.01f
            ? Math.Max(0, (expertWinRate - noviceWinRate) / expertWinRate)
            : 0;

        // 5. 挫败风险 - 计算连续3局失败的玩家比例
        float frustrationRisk = CalculateFrustrationRisk(playerResults.ToList());

        // 6. 运气依赖度
        float luckDependency = CalculateLuckDependency(playerResults.ToList());

        // 7. P95 通关次数
        int p95ClearAttempts = CalculateP95(clearAttempts.ToList());

        progress?.Report(new DeepAnalysisProgress
        {
            Progress = 1.0f,
            CompletedCount = completedCount,
            TotalCount = totalSimulations,
            Stage = "完成"
        });

        return new DeepAnalysisResult
        {
            FlowCurve = flowCurve,
            FlowMin = flowMin,
            FlowMax = flowMax,
            FlowAverage = flowCount > 0 ? flowSum / flowCount : 0,
            TierWinRates = tierWinRates,
            BottleneckObjective = bottleneckObjective,
            BottleneckFailureRate = bottleneckFailureRate,
            SkillSensitivity = skillSensitivity,
            FrustrationRisk = frustrationRisk,
            LuckDependency = luckDependency,
            P95ClearAttempts = p95ClearAttempts,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TotalSimulations = completedCount,
            WasCancelled = false
        };
    }

    private SingleGameDeepResult SimulateSingleGame(
        GameState initialState,
        ulong seed,
        PlayerTierConfig tierConfig,
        int moveLimit)
    {
        var ctx = _contextCache.Value!;
        ctx.ResetForSimulation(seed, initialState.TileTypesCount);

        var state = initialState.Clone();
        state.Random = ctx.StateRandom;

        var objectiveSystem = ctx.CreateObjectiveSystem();

        var profile = new PlayerProfile
        {
            Name = tierConfig.Name,
            SkillLevel = tierConfig.SkillLevel,
            BombPreference = tierConfig.BombPreference,
            ObjectiveFocus = tierConfig.ObjectiveFocus
        };
        var strategy = new SyntheticPlayerStrategy(profile, ctx.MoveRandom);

        using var engine = ctx.CreateEngine(state, objectiveSystem);

        var flowScores = new float[moveLimit];
        int movesUsed = 0;
        bool won = false;

        while (movesUsed < moveLimit)
        {
            var currentState = engine.State;

            var matchFinder = ctx.GetMatchFinder();
            var validMoves = ValidMoveDetector.FindAllValidMoves(in currentState, matchFinder);

            if (validMoves.Count == 0)
            {
                Utility.Pools.Pools.Release(validMoves);
                break; // Deadlock
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

            // 记录移动前的状态
            long scoreBefore = currentState.Score;
            int bombsBefore = CountBombs(in currentState);
            var progressBefore = GetObjectiveProgress(in currentState);

            engine.ApplyMove(bestMove.From, bestMove.To);
            engine.RunUntilStable();

            Utility.Pools.Pools.Release(validMoves);

            // 计算心流分数
            var stateAfter = engine.State;
            long scoreGained = stateAfter.Score - scoreBefore;
            int bombsAfter = CountBombs(in stateAfter);
            var progressAfter = GetObjectiveProgress(in stateAfter);

            float objectiveProgressGain = (progressAfter - progressBefore);
            int bombsFormed = Math.Max(0, bombsAfter - bombsBefore);
            int bombsActivated = Math.Max(0, bombsBefore - bombsAfter);
            int cascadeDepth = (int)(scoreGained / 100); // 粗略估计连锁深度

            float flowScore = objectiveProgressGain * 100 * WeightObjectiveProgress
                      + bombsFormed * WeightBombFormation * 50
                      + bombsActivated * WeightBombActivation * 50
                      + cascadeDepth * WeightCascadeDepth * 30;

            flowScores[movesUsed] = flowScore;
            movesUsed++;

            if (objectiveSystem.IsLevelComplete(in stateAfter))
            {
                won = true;
                break;
            }
        }

        // 计算瓶颈目标
        int bottleneckObjectiveIndex = -1;
        if (!won)
        {
            bottleneckObjectiveIndex = FindBottleneckObjective(engine.State);
        }

        return new SingleGameDeepResult
        {
            Won = won,
            MovesUsed = movesUsed,
            FlowScores = flowScores,
            BottleneckObjectiveIndex = bottleneckObjectiveIndex
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

    private static int CountBombs(in GameState state)
    {
        int count = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Bomb != BombType.None) count++;
        }
        return count;
    }

    private static float GetObjectiveProgress(in GameState state)
    {
        float total = 0;
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            var prog = state.ObjectiveProgress[i];
            if (prog.TargetCount > 0)
            {
                total += (float)prog.CurrentCount / prog.TargetCount;
                count++;
            }
        }
        return count > 0 ? total / count : 0;
    }

    private static int FindBottleneckObjective(in GameState state)
    {
        int worstIndex = -1;
        float worstProgress = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            var prog = state.ObjectiveProgress[i];
            if (prog.TargetCount > 0)
            {
                float progress = (float)prog.CurrentCount / prog.TargetCount;
                if (progress < worstProgress)
                {
                    worstProgress = progress;
                    worstIndex = i;
                }
            }
        }

        return worstIndex;
    }

    private static string GetObjectiveName(in GameState state, int index)
    {
        if (index < 0 || index >= 4) return "Unknown";

        var prog = state.ObjectiveProgress[index];
        string layerName = prog.TargetLayer switch
        {
            ObjectiveTargetLayer.Tile => "Tile",
            ObjectiveTargetLayer.Cover => "Cover",
            ObjectiveTargetLayer.Ground => "Ground",
            _ => "Unknown"
        };

        if (prog.TargetLayer == ObjectiveTargetLayer.Tile)
        {
            var elementType = (ElementType)prog.ElementType;
            return $"{elementType}";
        }

        return $"{layerName}_{prog.ElementType}";
    }

    private static float CalculateFrustrationRisk(List<(string tier, bool[] outcomes)> playerResults)
    {
        if (playerResults.Count == 0) return 0;

        int frustatedPlayers = 0;
        foreach (var (_, outcomes) in playerResults)
        {
            // 检查是否有连续3局失败
            int consecutive = 0;
            foreach (var won in outcomes)
            {
                if (!won)
                {
                    consecutive++;
                    if (consecutive >= 3)
                    {
                        frustatedPlayers++;
                        break;
                    }
                }
                else
                {
                    consecutive = 0;
                }
            }
        }

        return (float)frustatedPlayers / playerResults.Count;
    }

    private static float CalculateLuckDependency(List<(string tier, bool[] outcomes)> playerResults)
    {
        if (playerResults.Count == 0) return 0;

        // 计算每个玩家的胜率方差（局间方差）
        var playerWinRates = new List<float>();
        foreach (var (_, outcomes) in playerResults)
        {
            if (outcomes.Length > 0)
            {
                float winRate = (float)outcomes.Count(w => w) / outcomes.Length;
                playerWinRates.Add(winRate);
            }
        }

        if (playerWinRates.Count < 2) return 0;

        // 总体方差
        float overallMean = playerWinRates.Average();
        float totalVariance = playerWinRates.Select(r => (r - overallMean) * (r - overallMean)).Average();

        // 局间方差 - 同一分层内的方差
        var tierGroups = playerResults.GroupBy(p => p.tier);
        float withinVariance = 0;
        int groupCount = 0;

        foreach (var group in tierGroups)
        {
            var rates = group.Select(p => (float)p.outcomes.Count(w => w) / Math.Max(1, p.outcomes.Length)).ToList();
            if (rates.Count > 1)
            {
                float mean = rates.Average();
                withinVariance += rates.Select(r => (r - mean) * (r - mean)).Average();
                groupCount++;
            }
        }

        if (groupCount > 0)
        {
            withinVariance /= groupCount;
        }

        // 运气依赖度 = 局间方差 / 总方差
        return totalVariance > 0.001f ? Math.Min(1f, withinVariance / totalVariance) : 0;
    }

    private static int CalculateP95(List<int> attempts)
    {
        if (attempts.Count == 0) return 10;

        var sorted = attempts.OrderBy(a => a).ToList();
        int p95Index = (int)(sorted.Count * 0.95);
        p95Index = Math.Min(p95Index, sorted.Count - 1);

        return sorted[p95Index];
    }

    // === Internal Types ===

    private sealed class TierStats
    {
        public int TotalGames;
        public int Wins;
    }

    private readonly struct SingleGameDeepResult
    {
        public bool Won { get; init; }
        public int MovesUsed { get; init; }
        public float[]? FlowScores { get; init; }
        public int BottleneckObjectiveIndex { get; init; }
    }
}
