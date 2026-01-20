namespace Match3.Core.Events;

/// <summary>
/// Event emitted when objective progress is updated.
/// </summary>
public sealed record ObjectiveProgressEvent : GameEvent
{
    /// <summary>Index of the objective (0-3).</summary>
    public int ObjectiveIndex { get; init; }

    /// <summary>Previous count before this update.</summary>
    public int PreviousCount { get; init; }

    /// <summary>Current count after this update.</summary>
    public int CurrentCount { get; init; }

    /// <summary>Target count required for completion.</summary>
    public int TargetCount { get; init; }

    /// <summary>Whether the objective is now completed.</summary>
    public bool IsCompleted { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Event emitted when level is completed (victory or defeat).
/// </summary>
public sealed record LevelCompletedEvent : GameEvent
{
    /// <summary>True if victory, false if defeat.</summary>
    public bool IsVictory { get; init; }

    /// <summary>Final score at level end.</summary>
    public long FinalScore { get; init; }

    /// <summary>Total moves used.</summary>
    public int MovesUsed { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
