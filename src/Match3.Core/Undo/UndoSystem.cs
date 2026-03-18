using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Random;

namespace Match3.Core.Undo;

/// <summary>
/// Manages a stack of <see cref="UndoCheckpoint"/>s for multi-step undo.
/// Saves board state before each player operation and restores on undo.
/// </summary>
/// <remarks>
/// Rush-move handling: if the player's last move was issued while the board was
/// still cascading (抢步), undo pops past all consecutive rush-move checkpoints
/// until it reaches the most recent stable checkpoint.
/// </remarks>
public sealed class UndoSystem
{
    private readonly Stack<UndoCheckpoint> _checkpoints = new();

    /// <summary>Number of checkpoints available for undo.</summary>
    public int CheckpointCount => _checkpoints.Count;

    /// <summary>Whether at least one undo step is available.</summary>
    public bool CanUndo => _checkpoints.Count > 0;

    /// <summary>
    /// Saves the current engine state as an undo checkpoint.
    /// Call this BEFORE executing each player command (swap/tap).
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void SaveCheckpoint(SimulationEngine engine)
    {
        if (engine == null) throw new System.ArgumentNullException(nameof(engine));

        var rng = engine.State.Random as XorShift64;
        ulong rngState = rng?.GetState() ?? 0;

        // Clone with a fresh RNG so the saved state is fully independent.
        // On restore, SavedState is passed directly (no re-clone needed).
        var clonedState = engine.State.Clone(new XorShift64(rngState));

        var checkpoint = new UndoCheckpoint(
            savedState: clonedState,
            randomState: rngState,
            tick: engine.CurrentTick,
            elapsedTime: engine.ElapsedTime,
            wasBoardStable: engine.IsStable());

        _checkpoints.Push(checkpoint);
    }

    /// <summary>
    /// Performs an undo: restores the engine to the most recent stable checkpoint.
    /// If the top checkpoint was a rush-move, keeps popping until a stable one is found.
    /// </summary>
    /// <remarks>
    /// Only stable checkpoints are restored. Unstable (rush-move) checkpoints cannot be
    /// safely restored because ProjectileSystem, ExplosionSystem, and ColorBombSessionManager
    /// hold active-effect state outside GameState that Clone() does not capture.
    /// </remarks>
    /// <returns>The checkpoint that was restored, or null if no stable checkpoint is available.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public UndoCheckpoint? Undo(SimulationEngine engine)
    {
        if (engine == null) throw new System.ArgumentNullException(nameof(engine));
        if (_checkpoints.Count == 0)
            return null;

        // Pop the top checkpoint (the state before the most recent move)
        var checkpoint = _checkpoints.Pop();

        // If it was a rush-move, keep popping until we find a stable checkpoint
        while (!checkpoint.WasBoardStable && _checkpoints.Count > 0)
        {
            checkpoint = _checkpoints.Pop();
        }

        // If we exhausted the stack without finding a stable checkpoint, bail out
        if (!checkpoint.WasBoardStable)
            return null;

        RestoreEngine(engine, checkpoint);
        return checkpoint;
    }

    /// <summary>
    /// Clears all checkpoints. Call when starting a new level.
    /// </summary>
    public void Clear()
    {
        _checkpoints.Clear();
    }

    /// <summary>
    /// Peeks at the top checkpoint without popping it.
    /// </summary>
    public UndoCheckpoint? Peek()
    {
        return _checkpoints.Count > 0 ? _checkpoints.Peek() : null;
    }

    private static void RestoreEngine(SimulationEngine engine, UndoCheckpoint checkpoint)
    {
        // SavedState already has a correctly initialized independent RNG from save time.
        // Pass the struct directly — struct copy gives the engine its own value fields,
        // while sharing array references with the (now-discarded) checkpoint.
        engine.RestoreState(checkpoint.SavedState, checkpoint.Tick, checkpoint.ElapsedTime);
    }
}
