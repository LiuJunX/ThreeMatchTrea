using System.Collections.Generic;
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
    /// Create a new ColorBomb session (normal mode: beam → destroy).
    /// Target color is auto-selected (most frequent). The bomb tile should already be cleared.
    /// </summary>
    void CreateSession(ref GameState state, Position origin, int bombTileId,
        int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Create a new ColorBomb session with a specified target color (swap with normal tile).
    /// The bomb tile should already be consumed (ConsumeBomb) before calling this.
    /// </summary>
    void CreateSession(ref GameState state, Position origin, int bombTileId,
        ElementType targetColor, int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Create a combo session (beam → transform to bomb → batch activate).
    /// Both bomb tiles should already be cleared before calling this.
    /// </summary>
    void CreateComboSession(ref GameState state, Position origin, int bombTileId,
        ElementType comboBombType, int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Update all active sessions. Called once per tick from SimulationOrchestrator.
    /// Combo sessions output bomb positions to activate into <paramref name="triggeredBombs"/>.
    /// </summary>
    void Update(ref GameState state, float deltaTime, int tick, float simTime,
        IEventCollector events, List<Position>? triggeredBombs = null);

    /// <summary>
    /// Reset all sessions (e.g., on game restart).
    /// </summary>
    void Reset();

    /// <summary>
    /// Capture current state for ring buffer snapshot. Returns null when idle (zero allocation).
    /// </summary>
    ColorBombSessionSnapshot? SaveState();

    /// <summary>
    /// Restore state from a previously captured snapshot. Pass null to clear all state.
    /// </summary>
    void RestoreState(ColorBombSessionSnapshot? snapshot);
}
