using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Common lock combinations for typical gameplay scenarios.
/// </summary>
public static class LockPreset
{
    /// <summary>Completely frozen: cannot drop, receive, swap, or match.</summary>
    public const CellLockType Frozen = CellLockType.Drop | CellLockType.Receive | CellLockType.Swap | CellLockType.Matching;

    /// <summary>Anchored in place: cannot drop out or receive new tiles.</summary>
    public const CellLockType Anchored = CellLockType.Drop | CellLockType.Receive;

    /// <summary>Protected: cannot be destroyed or targeted.</summary>
    public const CellLockType Protected = CellLockType.Indestructible | CellLockType.Targeting;
}
