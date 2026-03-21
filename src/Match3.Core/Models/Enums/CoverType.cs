using Match3.Core.Attributes;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the type of cover element above a tile.
/// Cover elements protect the tile from being destroyed.
/// </summary>
public enum CoverType : byte
{
    /// <summary>
    /// No cover element at this position.
    /// </summary>
    None = 0,

    /// <summary>
    /// Cage that blocks matching and swap. Static.
    /// </summary>
    [AIMapping(0, "Cage")]
    Cage = 1,

    /// <summary>
    /// Chain that blocks swap but allows matching. Static.
    /// </summary>
    [AIMapping(1, "Chain")]
    Chain = 2,

    /// <summary>
    /// Bubble that moves with the tile. Dynamic.
    /// </summary>
    [AIMapping(2, "Bubble")]
    Bubble = 3,

    /// <summary>
    /// Honey that blocks match/swap/movement. Destroyed by adjacent elimination.
    /// </summary>
    [AIMapping(3, "Honey")]
    Honey = 4,

    /// <summary>
    /// Frost that freezes tile in place. Allows matching but blocks swap/movement.
    /// Destroyed by direct hit or adjacent elimination. 1 HP.
    /// </summary>
    [AIMapping(4, "Frost")]
    Frost = 5,
}
