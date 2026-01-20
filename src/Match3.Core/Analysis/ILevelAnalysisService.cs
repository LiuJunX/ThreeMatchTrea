using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Config;

namespace Match3.Core.Analysis;

/// <summary>
/// 模拟进度报告
/// </summary>
public readonly struct SimulationProgress
{
    /// <summary>已完成的模拟次数</summary>
    public int CompletedCount { get; init; }

    /// <summary>总模拟次数</summary>
    public int TotalCount { get; init; }

    /// <summary>当前胜利次数</summary>
    public int WinCount { get; init; }

    /// <summary>当前死锁次数</summary>
    public int DeadlockCount { get; init; }

    /// <summary>当前通过率 (0-1)</summary>
    public float WinRate => TotalCount > 0 ? (float)WinCount / CompletedCount : 0;

    /// <summary>当前死锁率 (0-1)</summary>
    public float DeadlockRate => TotalCount > 0 ? (float)DeadlockCount / CompletedCount : 0;

    /// <summary>进度百分比 (0-1)</summary>
    public float Progress => TotalCount > 0 ? (float)CompletedCount / TotalCount : 0;

    /// <summary>是否已完成</summary>
    public bool IsCompleted => CompletedCount >= TotalCount;
}

/// <summary>
/// 关卡分析结果
/// </summary>
public sealed class LevelAnalysisResult
{
    /// <summary>总模拟次数</summary>
    public int TotalSimulations { get; init; }

    /// <summary>胜利次数</summary>
    public int WinCount { get; init; }

    /// <summary>死锁次数</summary>
    public int DeadlockCount { get; init; }

    /// <summary>步数用尽次数</summary>
    public int OutOfMovesCount { get; init; }

    /// <summary>通过率 (0-1)</summary>
    public float WinRate => TotalSimulations > 0 ? (float)WinCount / TotalSimulations : 0;

    /// <summary>死锁率 (0-1)</summary>
    public float DeadlockRate => TotalSimulations > 0 ? (float)DeadlockCount / TotalSimulations : 0;

    /// <summary>平均使用步数</summary>
    public float AverageMovesUsed { get; init; }

    /// <summary>平均得分</summary>
    public float AverageScore { get; init; }

    /// <summary>分析耗时(毫秒)</summary>
    public double ElapsedMs { get; init; }

    /// <summary>是否被取消</summary>
    public bool WasCancelled { get; init; }

    /// <summary>难度评级</summary>
    public DifficultyRating DifficultyRating => WinRate switch
    {
        >= 0.90f => DifficultyRating.VeryEasy,
        >= 0.70f => DifficultyRating.Easy,
        >= 0.40f => DifficultyRating.Medium,
        >= 0.10f => DifficultyRating.Hard,
        _ => DifficultyRating.VeryHard
    };

    /// <summary>
    /// 分层玩家统计结果（当使用 PlayerPopulation 模式时）
    /// </summary>
    public PlayerTierResult[]? TierResults { get; init; }

    /// <summary>
    /// 阶段进度分布（记录每 N 步的平均进度）
    /// </summary>
    public StageProgressDistribution? ProgressDistribution { get; init; }
}

/// <summary>
/// 分层玩家统计结果
/// </summary>
public sealed class PlayerTierResult
{
    /// <summary>分层名称</summary>
    public string TierName { get; init; } = "";

    /// <summary>该分层的模拟次数</summary>
    public int SimulationCount { get; init; }

    /// <summary>胜利次数</summary>
    public int WinCount { get; init; }

    /// <summary>死锁次数</summary>
    public int DeadlockCount { get; init; }

    /// <summary>步数用尽次数</summary>
    public int OutOfMovesCount { get; init; }

    /// <summary>通过率</summary>
    public float WinRate => SimulationCount > 0 ? (float)WinCount / SimulationCount : 0;

    /// <summary>平均使用步数</summary>
    public float AverageMovesUsed { get; init; }

    /// <summary>平均得分</summary>
    public float AverageScore { get; init; }
}

/// <summary>
/// 阶段进度分布
/// </summary>
public sealed class StageProgressDistribution
{
    /// <summary>
    /// 每个阶段的平均进度 (index = 步数, value = 平均完成度 0-1)
    /// </summary>
    public float[] AverageProgressByMove { get; init; } = Array.Empty<float>();

    /// <summary>
    /// 剩余步数分布 (key = 剩余步数区间, value = 占比)
    /// </summary>
    public Dictionary<string, float>? RemainingMovesDistribution { get; init; }

    /// <summary>
    /// 失败原因分布
    /// </summary>
    public Dictionary<string, float>? FailureReasonDistribution { get; init; }
}

/// <summary>
/// 难度评级
/// </summary>
public enum DifficultyRating
{
    VeryEasy,
    Easy,
    Medium,
    Hard,
    VeryHard
}

/// <summary>
/// 关卡分析配置
/// </summary>
public sealed class AnalysisConfig
{
    /// <summary>模拟次数，默认1000</summary>
    public int SimulationCount { get; set; } = 1000;

    /// <summary>进度报告间隔(模拟次数)，默认50</summary>
    public int ProgressReportInterval { get; set; } = 50;

    /// <summary>是否使用并行，默认true</summary>
    public bool UseParallel { get; set; } = true;

    /// <summary>
    /// 模拟策略模式
    /// </summary>
    public SimulationMode Mode { get; set; } = SimulationMode.Random;

    /// <summary>
    /// 玩家群体配置（当 Mode = PlayerPopulation 时使用）
    /// 如果为 null，使用默认群体配置
    /// </summary>
    public PlayerPopulationConfig? PopulationConfig { get; set; }
}

/// <summary>
/// 模拟策略模式
/// </summary>
public enum SimulationMode
{
    /// <summary>纯随机选择（当前默认行为）</summary>
    Random,

    /// <summary>使用玩家群体模型模拟</summary>
    PlayerPopulation,

    /// <summary>使用贪心策略</summary>
    Greedy,

    /// <summary>使用炸弹优先策略</summary>
    BombPriority
}

/// <summary>
/// 玩家群体配置
/// </summary>
public sealed class PlayerPopulationConfig
{
    /// <summary>
    /// 玩家分层配置列表
    /// </summary>
    public PlayerTierConfig[] Tiers { get; set; } = DefaultTiers;

    /// <summary>
    /// 是否输出分层统计结果
    /// </summary>
    public bool OutputTierResults { get; set; } = true;

    /// <summary>
    /// 默认玩家分层配置
    /// </summary>
    public static PlayerTierConfig[] DefaultTiers => new[]
    {
        new PlayerTierConfig { Name = "Novice", SkillLevel = 0.2f, BombPreference = 1.2f, ObjectiveFocus = 0.3f, Weight = 0.15f },
        new PlayerTierConfig { Name = "Casual", SkillLevel = 0.5f, BombPreference = 1.0f, ObjectiveFocus = 0.7f, Weight = 0.50f },
        new PlayerTierConfig { Name = "Core", SkillLevel = 0.75f, BombPreference = 0.9f, ObjectiveFocus = 1.2f, Weight = 0.30f },
        new PlayerTierConfig { Name = "Expert", SkillLevel = 0.95f, BombPreference = 0.8f, ObjectiveFocus = 1.5f, Weight = 0.05f }
    };
}

/// <summary>
/// 玩家分层配置
/// </summary>
public sealed class PlayerTierConfig
{
    /// <summary>分层名称</summary>
    public string Name { get; set; } = "Default";

    /// <summary>技能水平 (0.0 ~ 1.0)</summary>
    public float SkillLevel { get; set; } = 0.5f;

    /// <summary>炸弹偏好 (0.0 ~ 2.0)</summary>
    public float BombPreference { get; set; } = 1.0f;

    /// <summary>目标关注度 (0.0 ~ 2.0)</summary>
    public float ObjectiveFocus { get; set; } = 1.0f;

    /// <summary>权重占比 (0.0 ~ 1.0)</summary>
    public float Weight { get; set; } = 1.0f;
}

/// <summary>
/// 关卡分析服务接口
/// </summary>
public interface ILevelAnalysisService
{
    /// <summary>
    /// 异步分析关卡难度（使用简化参数，生成随机初始棋盘）
    /// </summary>
    Task<LevelAnalysisResult> AnalyzeAsync(
        LevelData levelData,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步分析关卡难度（使用完整关卡配置）
    /// </summary>
    /// <param name="levelConfig">完整关卡配置</param>
    /// <param name="config">分析配置</param>
    /// <param name="progress">进度回调(在后台线程调用)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分析结果</returns>
    Task<LevelAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 关卡数据(简化版，根据实际项目调整)
/// </summary>
public sealed class LevelData
{
    public int Width { get; init; } = 8;
    public int Height { get; init; } = 8;
    public int MoveLimit { get; init; } = 20;
    public int TileTypesCount { get; init; } = 5;
    // TODO: 添加目标、特殊格子等
}
