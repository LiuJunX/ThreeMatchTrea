using System;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.AI.Strategies;

/// <summary>
/// 合成玩家策略 - 模拟不同技能水平的真实玩家行为
/// </summary>
public sealed class SyntheticPlayerStrategy : IAIStrategy
{
    private readonly PlayerProfile _profile;
    private readonly XorShift64 _random;

    public SyntheticPlayerStrategy(PlayerProfile profile, XorShift64 random)
    {
        _profile = profile;
        _random = random;
    }

    public string Name => $"SyntheticPlayer_{_profile.Name}";

    public float ScoreMove(in GameState state, Move move, MovePreview preview)
    {
        if (!preview.IsValidMove) return -1000f;

        float score = 0f;

        // === 基础感知（所有玩家都能看到的） ===

        // 消除数量 - 视觉上明显的大消除
        score += preview.TilesCleared * 15f;

        // 炸弹激活 - 视觉反馈强烈
        score += preview.BombsActivated * 80f * _profile.BombPreference;

        // === 技能相关（需要经验才能判断） ===

        // 连锁预判 - 高手才会考虑
        score += preview.MaxCascadeDepth * 40f * _profile.SkillLevel;

        // 分数意识 - 有经验的玩家关注分数
        score += preview.ScoreGained * 0.1f * _profile.SkillLevel;

        // === 目标意识 ===

        // 高手会优先完成目标，新手可能只追求大消除
        float objectiveBonus = CalculateObjectiveBonus(state, preview);
        score += objectiveBonus * _profile.ObjectiveFocus;

        // === 位置偏好（人体工学因素） ===

        // 玩家倾向于点击屏幕下方和中间区域
        float positionBonus = CalculatePositionPreference(state, move);
        score += positionBonus * (1f - _profile.SkillLevel * 0.5f); // 高手受位置影响较小

        // === 视觉吸引力 ===

        // 大面积同色区域更容易被注意到
        if (preview.TilesCleared >= 4)
        {
            score += 30f * (1f - _profile.SkillLevel * 0.3f); // 新手更容易被吸引
        }

        // === 随机扰动（模拟"看走眼"和个人偏好差异） ===

        float noiseRange = 50f * (1f - _profile.SkillLevel);
        float noise = (_random.NextFloat() - 0.5f) * 2f * noiseRange;
        score += noise;

        return score;
    }

    private float CalculateObjectiveBonus(in GameState state, MovePreview preview)
    {
        // 简化实现：基于消除数量估算目标贡献
        // 实际可以根据 FinalState 的目标进度变化计算
        float bonus = 0f;

        // 如果有目标进度信息，计算进度贡献
        if (preview.FinalState.HasValue)
        {
            var finalState = preview.FinalState.Value;
            for (int i = 0; i < 4; i++)
            {
                var before = state.ObjectiveProgress[i];
                var after = finalState.ObjectiveProgress[i];

                if (before.TargetCount > 0)
                {
                    int progressGain = after.CurrentCount - before.CurrentCount;
                    if (progressGain > 0)
                    {
                        // 越接近完成，奖励越高
                        float completionRatio = (float)after.CurrentCount / after.TargetCount;
                        bonus += progressGain * 50f * (1f + completionRatio);
                    }
                }
            }
        }

        return bonus;
    }

    private float CalculatePositionPreference(in GameState state, Move move)
    {
        // 玩家倾向于操作屏幕下方和中间区域
        int centerX = state.Width / 2;
        int bottomY = state.Height - 1;

        float fromX = move.From.X;
        float fromY = move.From.Y;

        // 距离中心的水平距离（越近越好）
        float horizontalDistance = Math.Abs(fromX - centerX);
        float horizontalBonus = (state.Width / 2f - horizontalDistance) * 3f;

        // 距离底部的距离（越近越好，模拟手机握持习惯）
        float verticalBonus = fromY * 4f; // Y 越大越靠下

        return horizontalBonus + verticalBonus;
    }
}

/// <summary>
/// 玩家配置档案
/// </summary>
public sealed class PlayerProfile
{
    /// <summary>
    /// 配置名称
    /// </summary>
    public string Name { get; init; } = "Default";

    /// <summary>
    /// 技能水平 (0.0 ~ 1.0)
    /// 影响：连锁预判能力、分数意识、随机噪声
    /// </summary>
    public float SkillLevel { get; init; } = 0.5f;

    /// <summary>
    /// 炸弹偏好 (0.0 ~ 2.0)
    /// 1.0 = 正常，>1 = 更喜欢炸弹，<1 = 不太关注炸弹
    /// </summary>
    public float BombPreference { get; init; } = 1.0f;

    /// <summary>
    /// 目标关注度 (0.0 ~ 2.0)
    /// 1.0 = 正常，>1 = 更关注目标，<1 = 忽视目标
    /// </summary>
    public float ObjectiveFocus { get; init; } = 1.0f;

    /// <summary>
    /// 在模拟中的权重占比 (0.0 ~ 1.0)
    /// </summary>
    public float Weight { get; init; } = 1.0f;

    // === 预设配置 ===

    /// <summary>
    /// 新手玩家：随机性高，不关注目标和连锁
    /// </summary>
    public static PlayerProfile Novice => new()
    {
        Name = "Novice",
        SkillLevel = 0.2f,
        BombPreference = 1.2f,  // 喜欢炸弹（视觉吸引）
        ObjectiveFocus = 0.3f,  // 不太关注目标
        Weight = 0.15f
    };

    /// <summary>
    /// 休闲玩家：有基本策略，偶尔犯错
    /// </summary>
    public static PlayerProfile Casual => new()
    {
        Name = "Casual",
        SkillLevel = 0.5f,
        BombPreference = 1.0f,
        ObjectiveFocus = 0.7f,
        Weight = 0.50f
    };

    /// <summary>
    /// 核心玩家：策略较好，关注目标
    /// </summary>
    public static PlayerProfile Core => new()
    {
        Name = "Core",
        SkillLevel = 0.75f,
        BombPreference = 0.9f,  // 不盲目追求炸弹
        ObjectiveFocus = 1.2f,  // 更关注目标
        Weight = 0.30f
    };

    /// <summary>
    /// 高手玩家：接近最优决策
    /// </summary>
    public static PlayerProfile Expert => new()
    {
        Name = "Expert",
        SkillLevel = 0.95f,
        BombPreference = 0.8f,  // 理性使用炸弹
        ObjectiveFocus = 1.5f,  // 高度关注目标
        Weight = 0.05f
    };

    /// <summary>
    /// 获取默认玩家群体配置
    /// </summary>
    public static PlayerProfile[] DefaultPopulation => new[]
    {
        Novice,
        Casual,
        Core,
        Expert
    };
}
