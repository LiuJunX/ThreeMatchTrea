using System;
using System.Collections.Generic;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a match is detected.
/// </summary>
public sealed record MatchDetectedEvent : GameEvent
{
    /// <summary>Type of tiles in the match.</summary>
    public TileType Type { get; init; }

    /// <summary>All positions in the match.</summary>
    public IReadOnlyCollection<Position> Positions { get; init; } = Array.Empty<Position>();

    /// <summary>Shape of the match.</summary>
    public MatchShape Shape { get; init; }

    /// <summary>Number of tiles in the match.</summary>
    public int TileCount { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a bomb is created from a match.
/// </summary>
public sealed record BombCreatedEvent : GameEvent
{
    /// <summary>Tile ID that became a bomb.</summary>
    public long TileId { get; init; }

    /// <summary>Position of the bomb.</summary>
    public Position Position { get; init; }

    /// <summary>Type of bomb created.</summary>
    public BombType BombType { get; init; }

    /// <summary>Base tile type of the bomb.</summary>
    public TileType BaseType { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a bomb is activated (explodes).
/// </summary>
public sealed record BombActivatedEvent : GameEvent
{
    /// <summary>Tile ID of the activated bomb.</summary>
    public long TileId { get; init; }

    /// <summary>Position of the bomb.</summary>
    public Position Position { get; init; }

    /// <summary>Type of bomb that was activated.</summary>
    public BombType BombType { get; init; }

    /// <summary>All positions affected by the explosion.</summary>
    public IReadOnlyCollection<Position> AffectedPositions { get; init; } = Array.Empty<Position>();

    /// <summary>Whether this was triggered by a chain reaction.</summary>
    public bool IsChainReaction { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a bomb combo is activated.
/// </summary>
public sealed record BombComboEvent : GameEvent
{
    /// <summary>First bomb type in the combo.</summary>
    public BombType BombTypeA { get; init; }

    /// <summary>Second bomb type in the combo.</summary>
    public BombType BombTypeB { get; init; }

    /// <summary>Position of first bomb.</summary>
    public Position PositionA { get; init; }

    /// <summary>Position of second bomb.</summary>
    public Position PositionB { get; init; }

    /// <summary>All positions affected by the combo.</summary>
    public IReadOnlyCollection<Position> AffectedPositions { get; init; } = Array.Empty<Position>();

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
