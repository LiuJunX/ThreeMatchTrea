using Match3.Core.Models.Grid;

namespace Match3.Core.Undo;

/// <summary>
/// Immutable snapshot of the complete engine state before a player operation.
/// Used by <see cref="UndoSystem"/> to support multi-step undo.
/// </summary>
public sealed class UndoCheckpoint
{
    /// <summary>Deep copy of the game state (all 6 layers + metadata).</summary>
    public GameState SavedState { get; }

    /// <summary>XorShift64 internal state for deterministic RNG restoration.</summary>
    public ulong RandomState { get; }

    /// <summary>Simulation tick at the time of save.</summary>
    public int Tick { get; }

    /// <summary>Elapsed simulation time at the time of save.</summary>
    public float ElapsedTime { get; }

    /// <summary>
    /// Whether the board was stable (no falling, cascading, or pending effects)
    /// when this checkpoint was saved. Rush-moves (抢步) have this set to false.
    /// </summary>
    public bool WasBoardStable { get; }

    public UndoCheckpoint(
        GameState savedState,
        ulong randomState,
        int tick,
        float elapsedTime,
        bool wasBoardStable)
    {
        SavedState = savedState;
        RandomState = randomState;
        Tick = tick;
        ElapsedTime = elapsedTime;
        WasBoardStable = wasBoardStable;
    }
}
