using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Three-dimensional counter for controlling spawn rate of a specific element.
/// Tracks per-round (move), total, and on-board limits.
/// </summary>
public struct SpawnCounter
{
    /// <summary>Maximum spawns per player move. 0 = unlimited.</summary>
    public int MaxPerRound;

    /// <summary>Maximum total spawns across the entire game. 0 = unlimited.</summary>
    public int MaxTotal;

    /// <summary>Maximum simultaneous instances on the board. 0 = unlimited.</summary>
    public int MaxOnBoard;

    /// <summary>Spawns generated in the current round.</summary>
    public int CountThisRound;

    /// <summary>Total spawns generated across the entire game.</summary>
    public int CountTotal;

    /// <summary>Last observed move count, used to detect new rounds.</summary>
    private int _lastMoveCount;

    /// <summary>
    /// Detects a new round (player move) and resets per-round counter.
    /// Call once per spawn cycle before any CanSpawn checks.
    /// </summary>
    public void UpdateRound(int moveCount)
    {
        if (moveCount != _lastMoveCount)
        {
            CountThisRound = 0;
            _lastMoveCount = moveCount;
        }
    }

    /// <summary>
    /// Checks whether spawning is allowed given current counter state.
    /// Pure query — call <see cref="UpdateRound"/> first to sync round state.
    /// </summary>
    /// <param name="onBoardCount">Current number of this element on the board.</param>
    public readonly bool CanSpawn(int onBoardCount)
    {
        if (MaxPerRound > 0 && CountThisRound >= MaxPerRound)
            return false;

        if (MaxTotal > 0 && CountTotal >= MaxTotal)
            return false;

        if (MaxOnBoard > 0 && onBoardCount >= MaxOnBoard)
            return false;

        return true;
    }

    /// <summary>
    /// Records a successful spawn.
    /// </summary>
    public void OnSpawned()
    {
        CountThisRound++;
        CountTotal++;
    }

}
