namespace Match3.Core.Choreography;

/// <summary>
/// Base class for all render commands.
/// Commands are executed by Player to update VisualState.
/// </summary>
public abstract record RenderCommand
{
    /// <summary>Start time in the timeline.</summary>
    public float StartTime { get; init; }

    /// <summary>Duration of the command (0 for instant commands).</summary>
    public float Duration { get; init; }

    /// <summary>Priority for command ordering (higher = executed later when same StartTime).</summary>
    public int Priority { get; init; }

    /// <summary>End time of the command.</summary>
    public float EndTime => StartTime + Duration;
}
