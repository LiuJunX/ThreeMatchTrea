using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis.MCTS;
using Match3.Core.Config;

namespace Match3.Core.Analysis;

/// <summary>
/// 综合关卡分析器
/// 结合 MCTS 理论分析和玩家群体模拟
/// </summary>
public sealed class ComprehensiveLevelAnalyzer
{
    private readonly StrategyDrivenAnalysisService _populationService = new();
    private readonly MCTSConfig _mctsConfig;

    public ComprehensiveLevelAnalyzer(MCTSConfig? mctsConfig = null)
    {
        _mctsConfig = mctsConfig ?? new MCTSConfig();
    }

    /// <summary>
    /// 执行综合分析
    /// </summary>
    public async Task<ComprehensiveAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        ComprehensiveAnalysisConfig? config = null,
        IProgress<ComprehensiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new ComprehensiveAnalysisConfig();
        var sw = Stopwatch.StartNew();

        // 阶段 1: MCTS 理论分析（可选）
        MCTSAnalysisResult? mctsResult = null;
        if (config.RunMCTSAnalysis)
        {
            progress?.Report(new ComprehensiveProgress
            {
                Phase = AnalysisPhase.MCTS,
                PhaseProgress = 0,
                Message = "Running MCTS analysis..."
            });

            var mctsProgress = new Progress<float>(p =>
            {
                progress?.Report(new ComprehensiveProgress
                {
                    Phase = AnalysisPhase.MCTS,
                    PhaseProgress = p,
                    Message = $"MCTS analysis: {p:P0}"
                });
            });

            // 使用配置中的 MCTSTotalGames 覆盖默认值
            var effectiveMctsConfig = new MCTSConfig
            {
                TotalGames = config.MCTSTotalGames,
                SimulationsPerMove = _mctsConfig.SimulationsPerMove,
                ExplorationConstant = _mctsConfig.ExplorationConstant,
                MaxRolloutDepth = _mctsConfig.MaxRolloutDepth,
                UseGuidedRollout = _mctsConfig.UseGuidedRollout,
                RolloutSkillLevel = _mctsConfig.RolloutSkillLevel,
                Verbose = _mctsConfig.Verbose
            };
            var mctsAnalyzer = new MCTSAnalyzer(effectiveMctsConfig);
            mctsResult = await mctsAnalyzer.AnalyzeAsync(levelConfig, mctsProgress, cancellationToken);
        }

        // 阶段 2: 玩家群体模拟
        progress?.Report(new ComprehensiveProgress
        {
            Phase = AnalysisPhase.PlayerPopulation,
            PhaseProgress = 0,
            Message = "Running player population simulation..."
        });

        var populationConfig = new AnalysisConfig
        {
            SimulationCount = config.PopulationSimulationCount,
            UseParallel = config.UseParallel,
            Mode = SimulationMode.PlayerPopulation,
            PopulationConfig = config.PopulationConfig ?? new PlayerPopulationConfig()
        };

        var popProgress = new Progress<SimulationProgress>(p =>
        {
            progress?.Report(new ComprehensiveProgress
            {
                Phase = AnalysisPhase.PlayerPopulation,
                PhaseProgress = p.Progress,
                Message = $"Population simulation: {p.CompletedCount}/{p.TotalCount}"
            });
        });

        var populationResult = await _populationService.AnalyzeAsync(
            levelConfig, populationConfig, popProgress, cancellationToken);

        sw.Stop();

        // 构建综合结果
        return BuildComprehensiveResult(mctsResult, populationResult, config.PopulationConfig, sw.Elapsed.TotalMilliseconds);
    }

    private ComprehensiveAnalysisResult BuildComprehensiveResult(
        MCTSAnalysisResult? mctsResult,
        LevelAnalysisResult populationResult,
        PlayerPopulationConfig? populationConfig,
        double totalElapsedMs)
    {
        // 计算难度差距（理论 vs 实际）
        float? difficultyGap = null;
        if (mctsResult != null)
        {
            difficultyGap = mctsResult.OptimalWinRate - populationResult.WinRate;
        }

        // 生成难度建议
        var suggestions = GenerateSuggestions(mctsResult, populationResult);

        // 获取加权平均胜率（基于玩家分层）
        float weightedWinRate = CalculateWeightedWinRate(populationResult, populationConfig);

        return new ComprehensiveAnalysisResult
        {
            // 理论分析结果
            TheoreticalOptimalWinRate = mctsResult?.OptimalWinRate,
            MinMovesToWin = mctsResult?.MinMovesToWin,
            CriticalMoves = mctsResult?.CriticalMoves,

            // 玩家模拟结果
            PopulationResult = populationResult,
            WeightedAverageWinRate = weightedWinRate,

            // 综合评估
            DifficultyGap = difficultyGap,
            OverallDifficultyRating = CalculateOverallDifficulty(mctsResult, populationResult),
            Suggestions = suggestions,

            TotalElapsedMs = totalElapsedMs
        };
    }

    private float CalculateWeightedWinRate(LevelAnalysisResult result, PlayerPopulationConfig? populationConfig)
    {
        if (result.TierResults == null || result.TierResults.Length == 0)
            return result.WinRate;

        // 使用配置中的权重，如果没有则使用默认权重
        float[] weights;
        if (populationConfig?.Tiers != null && populationConfig.Tiers.Length > 0)
        {
            weights = new float[populationConfig.Tiers.Length];
            for (int i = 0; i < populationConfig.Tiers.Length; i++)
            {
                weights[i] = populationConfig.Tiers[i].Weight;
            }
        }
        else
        {
            // 默认权重：Novice 15%, Casual 50%, Core 30%, Expert 5%
            weights = new[] { 0.15f, 0.50f, 0.30f, 0.05f };
        }

        float totalWeight = 0;
        float weightedSum = 0;

        for (int i = 0; i < result.TierResults.Length && i < weights.Length; i++)
        {
            weightedSum += result.TierResults[i].WinRate * weights[i];
            totalWeight += weights[i];
        }

        return totalWeight > 0 ? weightedSum / totalWeight : result.WinRate;
    }

    private OverallDifficulty CalculateOverallDifficulty(
        MCTSAnalysisResult? mcts,
        LevelAnalysisResult population)
    {
        // 基于普通玩家（Casual）的胜率判断
        float casualWinRate = population.WinRate;
        if (population.TierResults != null && population.TierResults.Length >= 2)
        {
            casualWinRate = population.TierResults[1].WinRate; // Casual tier
        }

        // 如果有 MCTS 结果，也考虑理论难度
        if (mcts != null)
        {
            // 如果理论胜率很低，说明关卡设计可能有问题
            if (mcts.OptimalWinRate < 0.5f)
            {
                return OverallDifficulty.PossiblyUnfair;
            }
        }

        return casualWinRate switch
        {
            >= 0.75f => OverallDifficulty.TooEasy,
            >= 0.60f => OverallDifficulty.Easy,
            >= 0.45f => OverallDifficulty.Balanced,
            >= 0.30f => OverallDifficulty.Challenging,
            >= 0.15f => OverallDifficulty.Hard,
            _ => OverallDifficulty.VeryHard
        };
    }

    private List<string> GenerateSuggestions(
        MCTSAnalysisResult? mcts,
        LevelAnalysisResult population)
    {
        var suggestions = new List<string>();

        // 基于整体胜率的建议
        if (population.WinRate < 0.3f)
        {
            suggestions.Add("关卡整体胜率较低，建议增加步数或降低目标数量");
        }
        else if (population.WinRate > 0.8f)
        {
            suggestions.Add("关卡整体胜率较高，可以适当增加难度");
        }

        // 基于分层差异的建议
        if (population.TierResults != null && population.TierResults.Length >= 2)
        {
            float noviceWinRate = population.TierResults[0].WinRate;
            float expertWinRate = population.TierResults[population.TierResults.Length - 1].WinRate;

            if (expertWinRate - noviceWinRate > 0.5f)
            {
                suggestions.Add("新手和高手胜率差距过大，考虑添加引导或提示");
            }

            if (noviceWinRate < 0.2f)
            {
                suggestions.Add("新手玩家胜率过低，可能需要简化前期目标");
            }
        }

        // 基于 MCTS 结果的建议
        if (mcts != null)
        {
            if (mcts.OptimalWinRate > 0.9f && population.WinRate < 0.5f)
            {
                suggestions.Add("理论胜率高但实际胜率低，存在优化空间（可通过引导提升玩家表现）");
            }

            if (mcts.CriticalMoves != null && mcts.CriticalMoves.Count > 3)
            {
                suggestions.Add($"存在 {mcts.CriticalMoves.Count} 个关键决策点，关卡策略深度较高");
            }

            if (mcts.DeadlockCount > mcts.TotalGames * 0.2f)
            {
                suggestions.Add("死锁发生率较高，建议检查棋盘布局或增加颜色种类");
            }
        }

        // 基于失败原因的建议
        if (population.DeadlockRate > 0.1f)
        {
            suggestions.Add("死锁率超过 10%，建议优化棋盘生成逻辑");
        }

        if (population.ProgressDistribution?.FailureReasonDistribution != null)
        {
            var failures = population.ProgressDistribution.FailureReasonDistribution;
            if (failures.TryGetValue("OutOfMoves", out float outOfMovesRate) && outOfMovesRate > 0.7f)
            {
                suggestions.Add("大部分失败是因为步数用尽，可考虑增加步数限制");
            }
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("关卡难度设计合理，无明显问题");
        }

        return suggestions;
    }
}

/// <summary>
/// 综合分析配置
/// </summary>
public sealed class ComprehensiveAnalysisConfig
{
    /// <summary>是否运行 MCTS 分析（较慢但提供理论上限）</summary>
    public bool RunMCTSAnalysis { get; set; } = true;

    /// <summary>MCTS 分析的局数</summary>
    public int MCTSTotalGames { get; set; } = 30;

    /// <summary>玩家群体模拟次数</summary>
    public int PopulationSimulationCount { get; set; } = 1000;

    /// <summary>是否使用并行</summary>
    public bool UseParallel { get; set; } = true;

    /// <summary>玩家群体配置</summary>
    public PlayerPopulationConfig? PopulationConfig { get; set; }
}

/// <summary>
/// 综合分析进度
/// </summary>
public sealed class ComprehensiveProgress
{
    public AnalysisPhase Phase { get; init; }
    public float PhaseProgress { get; init; }
    public string Message { get; init; } = "";

    public float OverallProgress => Phase switch
    {
        AnalysisPhase.MCTS => PhaseProgress * 0.3f,
        AnalysisPhase.PlayerPopulation => 0.3f + PhaseProgress * 0.7f,
        _ => PhaseProgress
    };
}

/// <summary>
/// 分析阶段
/// </summary>
public enum AnalysisPhase
{
    MCTS,
    PlayerPopulation
}

/// <summary>
/// 综合分析结果
/// </summary>
public sealed class ComprehensiveAnalysisResult
{
    // === 理论分析结果（MCTS） ===

    /// <summary>理论最优胜率（最强 AI 的胜率）</summary>
    public float? TheoreticalOptimalWinRate { get; init; }

    /// <summary>最少通关步数</summary>
    public int? MinMovesToWin { get; init; }

    /// <summary>关键决策点</summary>
    public List<CriticalMove>? CriticalMoves { get; init; }

    // === 玩家模拟结果 ===

    /// <summary>玩家群体模拟完整结果</summary>
    public LevelAnalysisResult PopulationResult { get; init; } = null!;

    /// <summary>加权平均胜率</summary>
    public float WeightedAverageWinRate { get; init; }

    // === 综合评估 ===

    /// <summary>难度差距（理论最优 - 实际玩家）</summary>
    public float? DifficultyGap { get; init; }

    /// <summary>整体难度评级</summary>
    public OverallDifficulty OverallDifficultyRating { get; init; }

    /// <summary>调整建议</summary>
    public List<string> Suggestions { get; init; } = new();

    /// <summary>总耗时（毫秒）</summary>
    public double TotalElapsedMs { get; init; }

    /// <summary>
    /// 生成简要报告
    /// </summary>
    public string GenerateSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 关卡难度分析报告 ===");
        sb.AppendLine();

        // 整体评级
        sb.AppendLine($"整体难度评级: {OverallDifficultyRating}");
        sb.AppendLine();

        // 理论分析
        if (TheoreticalOptimalWinRate.HasValue)
        {
            sb.AppendLine("【理论分析（MCTS）】");
            sb.AppendLine($"  理论最优胜率: {TheoreticalOptimalWinRate:P1}");
            if (MinMovesToWin.HasValue && MinMovesToWin > 0)
            {
                sb.AppendLine($"  最少通关步数: {MinMovesToWin}");
            }
            if (CriticalMoves != null && CriticalMoves.Count > 0)
            {
                sb.AppendLine($"  关键决策点: {CriticalMoves.Count} 处");
            }
            sb.AppendLine();
        }

        // 玩家模拟
        sb.AppendLine("【玩家群体模拟】");
        sb.AppendLine($"  整体胜率: {PopulationResult.WinRate:P1}");
        sb.AppendLine($"  加权胜率: {WeightedAverageWinRate:P1}");
        sb.AppendLine($"  平均步数: {PopulationResult.AverageMovesUsed:F1}");
        sb.AppendLine($"  死锁率: {PopulationResult.DeadlockRate:P1}");

        if (PopulationResult.TierResults != null)
        {
            sb.AppendLine("  分层胜率:");
            foreach (var tier in PopulationResult.TierResults)
            {
                sb.AppendLine($"    - {tier.TierName}: {tier.WinRate:P1}");
            }
        }
        sb.AppendLine();

        // 难度差距
        if (DifficultyGap.HasValue)
        {
            sb.AppendLine($"【难度差距】");
            sb.AppendLine($"  理论 vs 实际: {DifficultyGap:P1}");
            if (DifficultyGap > 0.3f)
            {
                sb.AppendLine("  → 存在较大优化空间");
            }
            sb.AppendLine();
        }

        // 建议
        sb.AppendLine("【调整建议】");
        foreach (var suggestion in Suggestions)
        {
            sb.AppendLine($"  • {suggestion}");
        }
        sb.AppendLine();

        sb.AppendLine($"分析耗时: {TotalElapsedMs:F0}ms");

        return sb.ToString();
    }
}

/// <summary>
/// 整体难度评级
/// </summary>
public enum OverallDifficulty
{
    /// <summary>过于简单</summary>
    TooEasy,

    /// <summary>简单</summary>
    Easy,

    /// <summary>平衡</summary>
    Balanced,

    /// <summary>有挑战</summary>
    Challenging,

    /// <summary>困难</summary>
    Hard,

    /// <summary>非常困难</summary>
    VeryHard,

    /// <summary>可能不公平（理论胜率都很低）</summary>
    PossiblyUnfair
}
