using System;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Obstacles;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// System responsible for managing cover elements.
/// Covers protect tiles from being destroyed.
/// Two damage paths:
/// <list type="bullet">
///   <item>Path 1 — Direct hit: <see cref="TryDamageCover"/> (called by CellEliminator guard chain)</item>
///   <item>Path 2 — Adjacent reaction: <see cref="NotifyBatchElimination"/> (called by processors after batch elimination)</item>
/// </list>
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
    bool TryDamageCover(ref GameState state, Position position, int tick, float simTime, IEventCollector events);

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

    /// <summary>
    /// Process adjacent cover reactions for a batch of eliminated tiles.
    /// Covers with <c>DamagedByAdjacent = true</c> (e.g. Honey) are damaged
    /// when a neighboring tile is eliminated. Each cover is damaged at most once
    /// per batch (dedup scope = one call).
    /// </summary>
    void NotifyBatchElimination(
        ref GameState state,
        ReadOnlySpan<EliminatedTileInfo> eliminated,
        int tick, float simTime, IEventCollector events);
}
