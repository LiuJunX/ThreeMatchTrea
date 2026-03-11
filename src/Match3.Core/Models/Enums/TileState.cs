using System;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Represents the physics/lifecycle state of a tile.
/// Uses flags to allow combining multiple states if needed.
/// </summary>
[Flags]
public enum TileState : byte
{
    /// <summary>
    /// Default state - tile is stationary on the grid.
    /// </summary>
    None = 0,

    /// <summary>
    /// Tile is currently falling due to gravity.
    /// </summary>
    Falling = 1 << 0,

    /// <summary>
    /// Tile is being destroyed (for future animation sync).
    /// </summary>
    Destroying = 1 << 2,

    /// <summary>
    /// Position is reserved by another system (e.g., UFO target).
    /// </summary>
    Reserved = 1 << 3,
}
