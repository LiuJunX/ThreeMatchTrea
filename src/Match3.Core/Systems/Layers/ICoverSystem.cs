using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// System responsible for managing cover elements.
/// Covers protect tiles from being destroyed.
/// </summary>
public interface ICoverSystem
{
    /// <summary>
    /// Attempts to damage the cover at the specified position.
    /// Returns true if the cover was destroyed (HP reached 0).
    /// Returns false if the cover still exists or there was no cover.
    /// </summary>
    /// <param name="state">The game state.</param>
    /// <param name="position">The position to damage.</param>
    /// <param name="tick">Current simulation tick for event.</param>
    /// <param name="simTime">Current simulation time for event.</param>
    /// <param name="events">Event collector for emitting events.</param>
    /// <returns>True if cover was destroyed, false otherwise.</returns>
    bool TryDamageCover(ref GameState state, Position position, long tick, float simTime, IEventCollector events);

    /// <summary>
    /// Checks if a cover at the position blocks the tile from being destroyed.
    /// If cover exists and has HP > 0, the tile is protected.
    /// </summary>
    bool IsTileProtected(in GameState state, Position position);

    /// <summary>
    /// Moves dynamic covers along with their tiles.
    /// Should be called after tile positions are updated.
    /// </summary>
    void SyncDynamicCovers(ref GameState state, Position from, Position to);
}
