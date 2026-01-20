namespace Match3.Core.Models.Enums;

/// <summary>
/// Represents the current status of a level.
/// </summary>
public enum LevelStatus : byte
{
    /// <summary>Level is in progress.</summary>
    InProgress = 0,

    /// <summary>Level completed successfully (all objectives met).</summary>
    Victory = 1,

    /// <summary>Level failed (ran out of moves).</summary>
    Defeat = 2,
}
