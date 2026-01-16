namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the type of ground element beneath a tile.
/// Ground elements are affected when the tile above is destroyed.
/// </summary>
public enum GroundType : byte
{
    /// <summary>
    /// No ground element at this position.
    /// </summary>
    None = 0,

    /// <summary>
    /// Ice layer that can be broken.
    /// </summary>
    Ice = 1,

    /// <summary>
    /// Jelly that spreads or needs multiple hits.
    /// </summary>
    Jelly = 2,

    /// <summary>
    /// Honey that may have special spreading behavior.
    /// </summary>
    Honey = 3,
}
