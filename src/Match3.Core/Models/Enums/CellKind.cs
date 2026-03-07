using System;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the structural role of a grid slot.
/// This is the "Container" or "Foundation" layer.
/// These properties are generally static and do not fall with gravity.
/// </summary>
public enum CellKind : byte
{
    /// <summary>
    /// No grid slot exists here (Void). 
    /// Items cannot exist here, and it's invisible to gameplay.
    /// Previously represented by TileType.Hole or TileType.None.
    /// </summary>
    Void = 0,

    /// <summary>
    /// Standard playable slot. Items can rest here.
    /// </summary>
    Slot = 1,

    /// <summary>
    /// Solid structure blocking movement. Items cannot enter.
    /// Previously represented by TileType.Wall.
    /// </summary>
    Wall = 2,

    /// <summary>
    /// Entry point for new items (Spawners).
    /// Functions as a Slot but also generates new items.
    /// Previously represented by TileType.Spawner.
    /// </summary>
    Spawner = 3,

    /// <summary>
    /// Exit point for collection goals.
    /// Items that fall here are collected/removed.
    /// Previously represented by TileType.Sink.
    /// </summary>
    Sink = 4
}
