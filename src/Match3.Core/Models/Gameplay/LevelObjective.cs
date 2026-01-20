using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Gameplay;

/// <summary>
/// Defines a single level objective configuration.
/// </summary>
public struct LevelObjective
{
    /// <summary>The layer the target element belongs to.</summary>
    public ObjectiveTargetLayer TargetLayer;

    /// <summary>The element type value (TileType, CoverType, or GroundType cast to int).</summary>
    public int ElementType;

    /// <summary>Number of elements required to clear.</summary>
    public int TargetCount;
}
