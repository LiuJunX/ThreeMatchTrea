using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when score is added.
/// </summary>
public sealed record ScoreAddedEvent : GameEvent
{
    /// <summary>Points added.</summary>
    public int Points { get; init; }

    /// <summary>Total score after addition.</summary>
    public long TotalScore { get; init; }

    /// <summary>Reason for the score.</summary>
    public ScoreReason Reason { get; init; }

    /// <summary>Source position (for visual feedback).</summary>
    public Position? SourcePosition { get; init; }

    /// <summary>Current combo level (if applicable).</summary>
    public int ComboLevel { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when combo level changes.
/// </summary>
public sealed record ComboChangedEvent : GameEvent
{
    /// <summary>Previous combo level.</summary>
    public int PreviousLevel { get; init; }

    /// <summary>New combo level.</summary>
    public int NewLevel { get; init; }

    /// <summary>Combo multiplier being applied.</summary>
    public float Multiplier { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when a move is completed.
/// </summary>
public sealed record MoveCompletedEvent : GameEvent
{
    /// <summary>Total moves made so far.</summary>
    public int TotalMoves { get; init; }

    /// <summary>Remaining moves (if limited).</summary>
    public int? RemainingMoves { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
