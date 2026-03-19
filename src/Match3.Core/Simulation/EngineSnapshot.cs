using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Simulation;

/// <summary>
/// Mutable value-type snapshot of engine state at a tick boundary.
/// Used by <see cref="GameEngine"/> ring buffer for speculative execution.
/// Contains reference-type fields (Events list) — ownership is transferred
/// between slots, not copied. See <see cref="GameEngine.TickAndStore"/>.
/// </summary>
public struct EngineSnapshot
{
    /// <summary>Deep copy of the game state (all 6 layers + metadata).</summary>
    public GameState State;

    /// <summary>XorShift64 internal state for deterministic RNG restoration.</summary>
    public ulong RngState;

    /// <summary>Simulation tick at capture time.</summary>
    public int Tick;

    /// <summary>Elapsed simulation time at capture time.</summary>
    public float ElapsedTime;

    /// <summary>Whether this slot has been filled with valid data.</summary>
    public bool IsValid;

    /// <summary>Events produced during the tick that led to this state.</summary>
    public List<GameEvent>? Events;

    /// <summary>Hash of State at capture time, for debug divergence detection.</summary>
    public uint StateHash;

    /// <summary>Whether the simulation was stable at capture time.</summary>
    public bool IsStable;

    /// <summary>
    /// Ensures the Events list is allocated and cleared.
    /// </summary>
    public void PrepareEvents()
    {
        if (Events == null)
            Events = new List<GameEvent>();
        else
            Events.Clear();
    }

    /// <summary>
    /// Invalidates this slot.
    /// </summary>
    public void Invalidate()
    {
        IsValid = false;
        Events?.Clear();
    }
}
