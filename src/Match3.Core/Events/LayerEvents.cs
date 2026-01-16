using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a cover element is destroyed.
/// </summary>
public sealed record CoverDestroyedEvent : GameEvent
{
    /// <summary>Grid position where cover was destroyed.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the destroyed cover.</summary>
    public CoverType Type { get; init; }
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
}
