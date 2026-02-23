using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Opaque handle returned by AcquireLock. Pass to ReleaseLock to decrement ref-counts.
/// </summary>
public readonly struct LockToken
{
    public readonly int CellIndex;
    public readonly CellLockType Types;

    public LockToken(int cellIndex, CellLockType types)
    {
        CellIndex = cellIndex;
        Types = types;
    }
}
