using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a ColorBomb session starts (new multi-tick mechanism).
/// Triggers charge-up animation in the Choreographer.
/// </summary>
public sealed record ColorBombSessionStartEvent : GameEvent
{
    /// <summary>Tile ID of the ColorBomb.</summary>
    public int TileId { get; init; }

    /// <summary>Grid position of the ColorBomb.</summary>
    public Position Position { get; init; }

    /// <summary>The color this ColorBomb is targeting.</summary>
    public ElementType TargetColor { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a ColorBomb beam is launched toward a target.
/// </summary>
public sealed record ColorBombBeamLaunchedEvent : GameEvent
{
    /// <summary>Tile ID of the source ColorBomb.</summary>
    public int BombTileId { get; init; }

    /// <summary>World-space origin of the beam (bomb position).</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Target grid position.</summary>
    public Position TargetPosition { get; init; }

    /// <summary>Tile ID of the target (for validation on arrival).</summary>
    public int TargetTileId { get; init; }

    /// <summary>Total flight duration (seconds).</summary>
    public float FlightDuration { get; init; }

    /// <summary>Zero-based beam index within this session.</summary>
    public int BeamIndex { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when all beams have arrived and batch destruction executes.
/// </summary>
public sealed record ColorBombBatchDestroyEvent : GameEvent
{
    /// <summary>Tile ID of the ColorBomb being removed.</summary>
    public int BombTileId { get; init; }

    /// <summary>Grid position of the ColorBomb.</summary>
    public Position BombPosition { get; init; }

    /// <summary>Positions destroyed in this batch (only those still alive at destroy time).</summary>
    public IReadOnlyList<Position> DestroyedPositions { get; init; } = Array.Empty<Position>();

    /// <summary>Tile IDs of destroyed tiles (parallel to DestroyedPositions).</summary>
    public IReadOnlyList<int> DestroyedTileIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
