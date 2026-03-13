using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Shared mutable state for all choreography sub-processors.
/// Holds the command list, timing state, and cross-batch tracking dictionaries
/// that must be shared across domain-specific choreographers during a single
/// <see cref="Choreographer.Choreograph"/> call and across consecutive batches.
/// </summary>
internal sealed class ChoreographerContext
{
    /// <summary>
    /// Accumulated render commands for the current batch.
    /// Sub-processors append to this list; Choreographer returns it.
    /// </summary>
    internal readonly List<RenderCommand> Commands = new();

    /// <summary>
    /// Base timeline time for the current batch, set by <see cref="Choreographer.Choreograph"/>.
    /// </summary>
    internal float BaseTime;

    /// <summary>
    /// Minimum SimulationTime among all events in the current batch.
    /// Used to compute relative start times so the first event starts at <see cref="BaseTime"/>.
    /// </summary>
    internal float MinSimulationTime;

    /// <summary>
    /// Timing configuration for all choreography animations.
    /// </summary>
    internal ChoreographyConfig Config = new();

    /// <summary>
    /// Visual-only beam projectile IDs (negative to avoid collision with Core projectile IDs).
    /// Decremented for each new beam spawned.
    /// </summary>
    internal int NextBeamId;

    /// <summary>
    /// Color bomb beam hit times -- maps target grid position to beam arrival time.
    /// Used to delay TileDestroyedEvent animation until the beam visually reaches the target.
    /// Persists across batches; entries are consumed (removed) in beam target destroy logic.
    /// </summary>
    internal readonly Dictionary<Position, float> BeamHitTimes = new();

    /// <summary>
    /// UFO flight tracking -- projectile-driven: ProjectileLaunchedEvent.SourceTileId
    /// links to the visual tile, ProjectileRetargetedEvent emits UfoRetargetCommand,
    /// ProjectileImpactEvent emits destroy + remove.
    /// Persists across Choreograph() batches because launch/retarget/impact may span ticks.
    /// </summary>
    internal readonly Dictionary<int, UfoFlightInfo> ActiveUfoFlights = new();

    /// <summary>
    /// ColorBomb session tracking -- persists across batches because session events span ticks.
    /// Key = BombTileId. Cleared when ColorBombBatchDestroyEvent is processed.
    /// </summary>
    internal readonly Dictionary<int, ColorBombSessionInfo> ActiveColorBombSessions = new();

    /// <summary>
    /// Compute the start time for a game event relative to the current batch's base time.
    /// </summary>
    /// <param name="evt">The game event whose start time to calculate.</param>
    /// <returns>Absolute timeline time for the event.</returns>
    internal float GetStartTime(GameEvent evt)
    {
        // Use relative simulation time so first event starts at baseTime
        return BaseTime + (evt.SimulationTime - MinSimulationTime);
    }
}

/// <summary>
/// Tracks an active UFO flight across batches for retarget/impact coordination.
/// </summary>
internal record struct UfoFlightInfo(
    int TileId, float LaunchTime, float Duration,
    Vector2 Origin, Vector2 Target, float StayFraction,
    Vector2? DivergeControl, Vector2? ApproachControl,
    int? PassengerTileId = null);

/// <summary>
/// Tracks an active ColorBomb session across batches for beam coordination.
/// </summary>
internal record struct ColorBombSessionInfo(float SessionStartTime, Vector2 HoppedOrigin, float MaxBeamArrivalTime);
