using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// 表示单个棋子的类型变化
/// </summary>
public readonly record struct TileTypeChange(int TileId, Position Position, ElementType FromType, ElementType ToType);

/// <summary>
/// 洗牌中每个参与 tile 的信息（含未变类型的 tile，供动画使用）
/// </summary>
public readonly record struct ShuffledTileInfo(int TileId, Position Position, ElementType NewType);

/// <summary>
/// 事件：检测到死锁（棋盘无可行移动）
/// </summary>
public sealed record DeadlockDetectedEvent : GameEvent
{
    /// <summary>检测到死锁时的分数</summary>
    public int Score { get; init; }

    /// <summary>检测到死锁时的移动次数</summary>
    public int MoveCount { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// 事件：棋盘已洗牌
/// </summary>
public sealed record BoardShuffledEvent : GameEvent
{
    /// <summary>洗牌尝试次数（如果多次洗牌直到有解）</summary>
    public int AttemptCount { get; init; } = 1;

    /// <summary>洗牌前的分数</summary>
    public int ScoreBefore { get; init; }

    /// <summary>所有改变类型的棋子列表</summary>
    public IReadOnlyList<TileTypeChange> Changes { get; init; } = Array.Empty<TileTypeChange>();

    /// <summary>所有参与洗牌的 tile（含未变类型的），供动画使用</summary>
    public IReadOnlyList<ShuffledTileInfo> AllShuffledTiles { get; init; } = Array.Empty<ShuffledTileInfo>();

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
