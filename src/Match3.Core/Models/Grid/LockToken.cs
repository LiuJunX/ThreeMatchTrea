using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Opaque handle returned by LockScheduler. Pass to Release to decrement ref-counts.
/// </summary>
public readonly struct LockToken
{
    public readonly int Id;
    public readonly int CellIndex;
    public readonly CellLockType Types;

    public LockToken(int id, int cellIndex, CellLockType types)
    {
        Id = id;
        CellIndex = cellIndex;
        Types = types;
    }

    /// <summary>
    /// A token with Id > 0 is valid. Default-constructed tokens are invalid.
    /// </summary>
    public bool IsValid => Id > 0;

    /// <summary>
    /// Default invalid token (Id = 0).
    /// </summary>
    public static readonly LockToken Invalid = default;
}
