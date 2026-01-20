using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Gameplay;

/// <summary>
/// Tracks runtime progress of a single level objective.
/// </summary>
public struct ObjectiveProgress
{
    /// <summary>The layer the target element belongs to.</summary>
    public ObjectiveTargetLayer TargetLayer;

    /// <summary>The element type value (TileType, CoverType, or GroundType cast to int).</summary>
    public int ElementType;

    /// <summary>Number of elements required to clear.</summary>
    public int TargetCount;

    /// <summary>Number of elements cleared so far.</summary>
    public int CurrentCount;

    /// <summary>Whether this objective has been completed.</summary>
    public readonly bool IsCompleted => CurrentCount >= TargetCount;

    /// <summary>Whether this objective slot is active (has a target).</summary>
    public readonly bool IsActive => TargetLayer != ObjectiveTargetLayer.None;
}
