using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Manages active ColorBomb sessions: color reservation, beam timing, cell locking, batch destruction.
/// </summary>
public interface IColorBombSessionManager
{
    /// <summary>True if any session is still active.</summary>
    bool HasActiveSessions { get; }

    /// <summary>Check if a color is currently reserved by an active session.</summary>
    bool IsColorReserved(ElementType color);

    /// <summary>
    /// Create a new ColorBomb session. Called from PowerUpHandler when a ColorBomb is activated.
    /// The bomb tile should already be cleared (ClearBombAttribute) before calling this.
    /// </summary>
    void CreateSession(ref GameState state, Position origin, int bombTileId,
        int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Update all active sessions. Called once per tick from SimulationOrchestrator.
    /// </summary>
    void Update(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Reset all sessions (e.g., on game restart).
    /// </summary>
    void Reset();
}
