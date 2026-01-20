namespace Match3.Core.Models.Enums;

/// <summary>
/// Specifies which layer the objective target belongs to.
/// </summary>
public enum ObjectiveTargetLayer : byte
{
    /// <summary>No target (inactive objective).</summary>
    None = 0,

    /// <summary>Target is a TileType.</summary>
    Tile = 1,

    /// <summary>Target is a CoverType.</summary>
    Cover = 2,

    /// <summary>Target is a GroundType.</summary>
    Ground = 3,
}
