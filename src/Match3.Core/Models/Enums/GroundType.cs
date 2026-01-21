using Match3.Core.Attributes;

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
    [AIMapping(0, "Ice")]
    Ice = 1,
}
