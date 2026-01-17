namespace Match3.Core.Events;

/// <summary>
/// Base record for all game events.
/// Events are immutable snapshots of state changes during simulation.
/// Using record for value-based equality and immutability.
/// </summary>
public abstract record GameEvent
{
    /// <summary>
    /// Simulation tick at which this event occurred.
    /// </summary>
    public long Tick { get; init; }

    /// <summary>
    /// Elapsed simulation time in seconds when this event occurred.
    /// Used by Presentation layer for animation timing.
    /// </summary>
    public float SimulationTime { get; init; }

    /// <summary>
    /// Accept a visitor for strong-typed dispatch.
    /// Each concrete event overrides this method.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public abstract void Accept(IEventVisitor visitor);
}
