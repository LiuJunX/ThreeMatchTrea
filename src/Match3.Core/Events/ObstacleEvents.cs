using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when an obstacle takes damage but survives.
/// </summary>
public sealed record ObstacleDamagedEvent : GameEvent
{
    /// <summary>Grid position of the obstacle.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the obstacle.</summary>
    public ObstacleType Type { get; init; }

    /// <summary>Remaining stage/HP after damage.</summary>
    public byte RemainingStage { get; init; }

    /// <summary>Whether this obstacle is a level objective.</summary>
    public bool IsGoal { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when an obstacle is fully destroyed (stage reached 0).
/// </summary>
public sealed record ObstacleDestroyedEvent : GameEvent
{
    /// <summary>Grid position of the obstacle.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the destroyed obstacle.</summary>
    public ObstacleType Type { get; init; }

    /// <summary>Whether this obstacle is a level objective.</summary>
    public bool IsGoal { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
