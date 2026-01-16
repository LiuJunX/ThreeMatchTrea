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
    Cage = 1,

    /// <summary>
    /// Chain that blocks swap but allows matching. Static.
    /// </summary>
    Chain = 2,

    /// <summary>
    /// Bubble that moves with the tile. Dynamic.
    /// </summary>
    Bubble = 3,

    /// <summary>
    /// Ice cover that blocks swap. Static.
    /// </summary>
    IceCover = 4,
}
