using System.Numerics;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a tile changes position during simulation.
/// </summary>
public sealed record TileMovedEvent : GameEvent
{
    /// <summary>Unique identifier of the tile.</summary>
    public long TileId { get; init; }

    /// <summary>Position before movement.</summary>
    public Vector2 FromPosition { get; init; }

    /// <summary>Position after movement.</summary>
    public Vector2 ToPosition { get; init; }

    /// <summary>Reason for the movement.</summary>
    public MoveReason Reason { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a tile is destroyed (cleared from the grid).
/// </summary>
public sealed record TileDestroyedEvent : GameEvent
{
    /// <summary>Unique identifier of the destroyed tile.</summary>
    public long TileId { get; init; }

    /// <summary>Grid position where tile was destroyed.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the destroyed tile.</summary>
    public TileType Type { get; init; }

    /// <summary>Bomb type if tile had a bomb attached.</summary>
    public BombType Bomb { get; init; }

    /// <summary>Reason for destruction.</summary>
    public DestroyReason Reason { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a new tile is spawned (refill).
/// </summary>
public sealed record TileSpawnedEvent : GameEvent
{
    /// <summary>Unique identifier of the new tile.</summary>
    public long TileId { get; init; }

    /// <summary>Grid position where tile was spawned.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the spawned tile.</summary>
    public TileType Type { get; init; }

    /// <summary>Bomb type if tile was spawned with a bomb.</summary>
    public BombType Bomb { get; init; }

    /// <summary>Initial spawn position (may be above grid for animation).</summary>
    public Vector2 SpawnPosition { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when two tiles are swapped.
/// </summary>
public sealed record TilesSwappedEvent : GameEvent
{
    /// <summary>First tile identifier.</summary>
    public long TileAId { get; init; }

    /// <summary>Second tile identifier.</summary>
    public long TileBId { get; init; }

    /// <summary>Position of first tile.</summary>
    public Position PositionA { get; init; }

    /// <summary>Position of second tile.</summary>
    public Position PositionB { get; init; }

    /// <summary>Whether this is a revert (invalid swap).</summary>
    public bool IsRevert { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
