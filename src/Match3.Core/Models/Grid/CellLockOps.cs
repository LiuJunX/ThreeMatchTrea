using System.Diagnostics;
using System.Runtime.CompilerServices;
using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Bit-packing operations for cell locks.
/// Each CellLockType occupies a 4-bit nibble in a uint, supporting ref-count 0–15.
/// Layout: bits [3:0]=Drop, [7:4]=Receive, [11:8]=Swap, [15:12]=Matching,
///         [19:16]=Indestructible, [23:20]=Targeting.
/// </summary>
public static class CellLockOps
{
    private const int BitsPerLock = 4;
    private const uint NibbleMask = 0xF;
    private const uint MaxCount = 15;

    /// <summary>
    /// Increment the ref-count for each flagged lock type. Saturates at 15.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Lock(uint packed, CellLockType types)
    {
        int flags = (int)types;
        for (int bit = 0; flags != 0; bit++, flags >>= 1)
        {
            if ((flags & 1) == 0) continue;
            int shift = bit * BitsPerLock;
            uint count = (packed >> shift) & NibbleMask;
            if (count < MaxCount)
            {
                packed += 1u << shift;
            }
        }
        return packed;
    }

    /// <summary>
    /// Decrement the ref-count for each flagged lock type. Clamps at 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Unlock(uint packed, CellLockType types)
    {
        int flags = (int)types;
        for (int bit = 0; flags != 0; bit++, flags >>= 1)
        {
            if ((flags & 1) == 0) continue;
            int shift = bit * BitsPerLock;
            uint count = (packed >> shift) & NibbleMask;
            if (count > 0)
            {
                packed -= 1u << shift;
            }
        }
        return packed;
    }

    /// <summary>
    /// Returns true if the ref-count for the given single lock type is > 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLocked(uint packed, CellLockType type)
    {
        int bit = BitIndex(type);
        int shift = bit * BitsPerLock;
        return ((packed >> shift) & NibbleMask) > 0;
    }

    /// <summary>
    /// Returns the ref-count for a single lock type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCount(uint packed, CellLockType type)
    {
        int bit = BitIndex(type);
        int shift = bit * BitsPerLock;
        return (int)((packed >> shift) & NibbleMask);
    }

    /// <summary>
    /// Returns the bit index (0-based) for a single-flag CellLockType.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitIndex(CellLockType type)
    {
        Debug.Assert(type != CellLockType.None && (type & (type - 1)) == 0,
            $"BitIndex expects a single-flag CellLockType, got {type}");
        int v = (int)type;
        int index = 0;
        while (v > 1) { v >>= 1; index++; }
        return index;
    }
}
