using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Simulation;

/// <summary>
/// Orchestrates the simulation update cycle.
/// Extracted from SimulationEngine to reduce complexity and improve testability.
/// </summary>
/// <remarks>
/// <b>Naming conventions:</b>
/// <list type="bullet">
///   <item><b>Update* methods</b> — tick-phase updates called every frame (e.g. <see cref="UpdateRefill"/>,
///     <see cref="UpdatePhysics"/>). Always executed unconditionally.</item>
///   <item><b>Process* methods</b> — conditional processing that may skip work
///     (e.g. <see cref="ProcessMatches"/> only processes settled, non-falling tiles).</item>
///   <item><b>Properties</b> (<see cref="HasActiveProjectiles"/>, <see cref="HasActiveExplosions"/>)
///     — query subsystem internal state; no external parameters needed.</item>
///   <item><b>Has* methods</b> (<see cref="HasPendingMatches"/>) — queries that require
///     an external <c>GameState</c> parameter for evaluation.</item>
/// </list>
/// </remarks>
public interface ISimulationOrchestrator
{
    /// <summary>
    /// Update tile refill for empty columns.
    /// </summary>
    void UpdateRefill(ref GameState state);

    /// <summary>
    /// Update physics (gravity) simulation.
    /// </summary>
    void UpdatePhysics(ref GameState state, float deltaTime);

    /// <summary>
    /// Update projectile simulation.
    /// </summary>
    /// <returns>Number of tiles affected by projectile impacts.</returns>
    int UpdateProjectiles(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Update explosion effects.
    /// </summary>
    /// <returns>Number of bombs triggered.</returns>
    int UpdateExplosions(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Process stable matches on the board.
    /// Only considers tiles that have settled (not falling); in-flight tiles are ignored
    /// so that gravity can finish before match detection runs.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="tick">Current tick number.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector.</param>
    /// <param name="foci">Optional priority positions for bomb generation.</param>
    /// <returns>Number of matches processed.</returns>
    int ProcessMatches(ref GameState state, int tick, float simTime, IEventCollector events, Position[]? foci = null);

    /// <summary>
    /// Check if physics simulation is stable.
    /// </summary>
    bool IsPhysicsStable(in GameState state);

    /// <summary>
    /// Check if there are any pending matches.
    /// </summary>
    bool HasPendingMatches(in GameState state);

    /// <summary>
    /// Check if there are active projectiles.
    /// </summary>
    bool HasActiveProjectiles { get; }

    /// <summary>
    /// Check if there are active explosions.
    /// </summary>
    bool HasActiveExplosions { get; }
}
