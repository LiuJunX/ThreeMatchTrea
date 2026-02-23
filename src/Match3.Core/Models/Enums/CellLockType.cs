using System;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Flags enum for cell-level locks. Each flag represents a different lock type.
/// Multiple locks can be combined: CellLockType.Drop | CellLockType.Receive
/// Each lock type supports reference counting (max 15) via bit-packed uint storage.
/// </summary>
[Flags]
public enum CellLockType : byte
{
    None           = 0,
    /// <summary>Tile cannot drop out (gravity skips this cell).</summary>
    Drop           = 1 << 0,
    /// <summary>Cell refuses incoming tiles (gravity/refill won't fill here).</summary>
    Receive        = 1 << 1,
    /// <summary>Tile cannot be swapped by player input.</summary>
    Swap           = 1 << 2,
    /// <summary>Tile cannot participate in match detection.</summary>
    Matching       = 1 << 3,
    /// <summary>Tile cannot be destroyed by explosions or matches.</summary>
    Indestructible = 1 << 4,
    /// <summary>Cell cannot be selected as a target (e.g. by UFO).</summary>
    Targeting     = 1 << 5,
}
