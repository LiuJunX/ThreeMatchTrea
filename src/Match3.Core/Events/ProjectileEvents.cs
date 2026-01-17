using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a projectile is launched.
/// </summary>
public sealed record ProjectileLaunchedEvent : GameEvent
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Type of projectile.</summary>
    public ProjectileType Type { get; init; }

    /// <summary>Launch origin position.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Target grid position (if targeting a cell).</summary>
    public Position? TargetPosition { get; init; }

    /// <summary>Target tile ID (if tracking a specific tile).</summary>
    public long? TargetTileId { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a projectile moves during flight.
/// </summary>
public sealed record ProjectileMovedEvent : GameEvent
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Position before movement.</summary>
    public Vector2 FromPosition { get; init; }

    /// <summary>Position after movement.</summary>
    public Vector2 ToPosition { get; init; }

    /// <summary>Current velocity for interpolation.</summary>
    public Vector2 Velocity { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a projectile changes its target.
/// </summary>
public sealed record ProjectileRetargetedEvent : GameEvent
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Previous target position.</summary>
    public Position OldTarget { get; init; }

    /// <summary>New target position.</summary>
    public Position NewTarget { get; init; }

    /// <summary>Reason for retargeting.</summary>
    public RetargetReason Reason { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a projectile impacts its target.
/// </summary>
public sealed record ProjectileImpactEvent : GameEvent
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Grid position of impact.</summary>
    public Position ImpactPosition { get; init; }

    /// <summary>Tile ID that was hit (if any).</summary>
    public long? HitTileId { get; init; }

    /// <summary>All positions affected by the impact.</summary>
    public IReadOnlyCollection<Position> AffectedPositions { get; init; } = Array.Empty<Position>();

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Reason for projectile retargeting.
/// </summary>
public enum RetargetReason
{
    /// <summary>Original target was destroyed.</summary>
    OriginalTargetDestroyed,

    /// <summary>Found a better target.</summary>
    BetterTargetFound,

    /// <summary>Original target moved out of range.</summary>
    TargetOutOfRange
}
