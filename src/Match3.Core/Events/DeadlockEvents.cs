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

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
