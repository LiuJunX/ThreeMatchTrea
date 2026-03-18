using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a ground element is spawned by an obstacle death effect.
/// Used by presentation layer to animate ground appearance (e.g., Grass spreading from Bush).
/// </summary>
public sealed record GroundSpawnedEvent : GameEvent
{
    /// <summary>Grid position where the ground was spawned.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the spawned ground.</summary>
    public GroundType Type { get; init; }

    /// <summary>Position of the obstacle whose death caused this spawn.</summary>
    public Position SourcePosition { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a ground element takes damage but survives (health > 0 after hit).
/// Used by presentation layer to animate damage feedback (e.g., Grass crack/color change).
/// </summary>
public sealed record GroundDamagedEvent : GameEvent
{
    /// <summary>Grid position where ground was damaged.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the damaged ground.</summary>
    public GroundType Type { get; init; }

    /// <summary>Remaining health after damage.</summary>
    public byte RemainingHealth { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a cover element is destroyed.
/// </summary>
public sealed record CoverDestroyedEvent : GameEvent
{
    /// <summary>Grid position where cover was destroyed.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the destroyed cover.</summary>
    public CoverType Type { get; init; }

    /// <summary>Whether this cover was a level objective.</summary>
    public bool IsGoal { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a ground element is destroyed.
/// </summary>
public sealed record GroundDestroyedEvent : GameEvent
{
    /// <summary>Grid position where ground was destroyed.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the destroyed ground.</summary>
    public GroundType Type { get; init; }

    /// <summary>Whether this ground was a level objective.</summary>
    public bool IsGoal { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
