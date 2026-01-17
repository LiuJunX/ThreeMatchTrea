using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Selection;

/// <summary>
/// 移动操作，表示一次交换或点击
/// </summary>
public readonly struct MoveAction
{
    /// <summary>
    /// 起始位置（交换的第一个方块，或点击的位置）
    /// </summary>
    public Position From { get; init; }

    /// <summary>
    /// 目标位置（交换的第二个方块，点击时为 default）
    /// </summary>
    public Position To { get; init; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public MoveActionType ActionType { get; init; }

    /// <summary>
    /// 权重（用于加权随机选择）
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// 创建交换操作
    /// </summary>
    public static MoveAction Swap(Position from, Position to, int weight = 10)
    {
        return new MoveAction
        {
            From = from,
            To = to,
            ActionType = MoveActionType.Swap,
            Weight = weight
        };
    }

    /// <summary>
    /// 创建点击操作
    /// </summary>
    public static MoveAction Tap(Position position, int weight = 10)
    {
        return new MoveAction
        {
            From = position,
            To = default,
            ActionType = MoveActionType.Tap,
            Weight = weight
        };
    }

    /// <summary>
    /// 转换为 Move（用于兼容现有 API）
    /// </summary>
    public Move ToMove() => new(From, To);
}

/// <summary>
/// 移动操作类型
/// </summary>
public enum MoveActionType
{
    /// <summary>
    /// 交换两个方块
    /// </summary>
    Swap,

    /// <summary>
    /// 点击单个方块（激活炸弹）
    /// </summary>
    Tap
}
