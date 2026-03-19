using System.Diagnostics;
using Match3.Core.Models.Grid;

namespace Match3.Core.Simulation;

/// <summary>
/// Fast hash of GameState for debug verification of speculative execution.
/// Uses FNV-1a over the Grid layer (tile type + ID) which catches most divergences.
/// </summary>
internal static class StateHasher
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>
    /// Compute a lightweight hash of the game state.
    /// Covers Grid layer (element types + tile IDs) and score/move metadata.
    /// </summary>
    public static uint ComputeHash(in GameState state)
    {
        uint hash = FnvOffsetBasis;

        var grid = state.Grid;
        if (grid != null)
        {
            for (int i = 0; i < grid.Length; i++)
            {
                hash ^= (uint)grid[i].Type;
                hash *= FnvPrime;
                hash ^= (uint)grid[i].Id;
                hash *= FnvPrime;
            }
        }

        hash ^= (uint)state.Score;
        hash *= FnvPrime;
        hash ^= (uint)state.MoveCount;
        hash *= FnvPrime;

        return hash;
    }

    /// <summary>
    /// Debug-only: assert that two states produce the same hash.
    /// Called by GameEngine to verify speculative execution consistency.
    /// </summary>
    [Conditional("DEBUG")]
    public static void AssertConsistent(in GameState expected, in GameState actual, int tick)
    {
        var expectedHash = ComputeHash(in expected);
        var actualHash = ComputeHash(in actual);
        Debug.Assert(expectedHash == actualHash,
            $"Speculative divergence at tick {tick}. " +
            $"Expected hash={expectedHash:X8}, actual hash={actualHash:X8}. " +
            "An external system may have modified state without InjectCommand().");
    }
}
