using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

/// <summary>
/// 移动选择系统的配置类
/// 包含权重、策略参数等可调节的配置
/// </summary>
public class MoveSelectionConfig
{
    /// <summary>
    /// 默认配置
    /// </summary>
    public static MoveSelectionConfig Default { get; } = new();

    /// <summary>
    /// 权重配置
    /// </summary>
    public WeightConfig Weights { get; init; } = new();

    /// <summary>
    /// 随机选择器配置
    /// </summary>
    public RandomSelectorConfig RandomSelector { get; init; } = new();

    /// <summary>
    /// 权重选择器配置
    /// </summary>
    public WeightedSelectorConfig WeightedSelector { get; init; } = new();

    /// <summary>
    /// 权重配置类
    /// </summary>
    public class WeightConfig
    {
        /// <summary>
        /// 普通方块权重
        /// </summary>
        public int Normal { get; init; } = 10;

        /// <summary>
        /// UFO 炸弹权重
        /// </summary>
        public int Ufo { get; init; } = 20;

        /// <summary>
        /// 条形炸弹权重（水平/垂直）
        /// </summary>
        public int Line { get; init; } = 20;

        /// <summary>
        /// 方形炸弹权重（5x5）
        /// </summary>
        public int Cross { get; init; } = 30;

        /// <summary>
        /// 彩球权重
        /// </summary>
        public int Rainbow { get; init; } = 40;

        /// <summary>
        /// 根据炸弹类型获取权重
        /// </summary>
        public int GetWeight(BombType bombType)
        {
            return bombType switch
            {
                BombType.None => Normal,
                BombType.Ufo => Ufo,
                BombType.Horizontal => Line,
                BombType.Vertical => Line,
                BombType.Square5x5 => Cross,
                BombType.Color => Rainbow,
                _ => Normal
            };
        }
    }

    /// <summary>
    /// 随机选择器配置
    /// </summary>
    public class RandomSelectorConfig
    {
        /// <summary>
        /// 最大尝试次数
        /// </summary>
        public int MaxAttempts { get; init; } = 20;
    }

    /// <summary>
    /// 加权选择器配置
    /// </summary>
    public class WeightedSelectorConfig
    {
        /// <summary>
        /// 是否支持点击炸弹
        /// </summary>
        public bool EnableTapBombs { get; init; } = true;

        /// <summary>
        /// 炸弹组合是否使用乘法权重（true=相乘，false=相加）
        /// </summary>
        public bool UseBombMultiplier { get; init; } = true;

        /// <summary>
        /// 是否启用候选缓存
        /// </summary>
        public bool EnableCaching { get; init; } = true;
    }
}
