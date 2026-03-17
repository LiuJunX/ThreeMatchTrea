using System;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;

namespace Match3.Core.Systems.Obstacles;

/// <summary>
/// Manages obstacle damage, destruction, and reaction processing.
/// Three damage paths:
/// <list type="bullet">
///   <item>Path 1 — Direct hit: <see cref="TryHit"/> (called by CellEliminator guard chain)</item>
///   <item>Path 2 — Adjacent reaction: <see cref="NotifyBatchElimination"/> (called by processors after batch elimination)</item>
///   <item>Path 3 — Global reaction: <see cref="NotifyGlobalColorElimination"/> (called by processors, e.g. for Curtain)</item>
/// </list>
/// </summary>
public interface IObstacleSystem
{
    /// <summary>
    /// Attempt to damage the obstacle at <paramref name="pos"/> via direct hit.
    /// Returns <see cref="ObstacleHitResult.NoObstacle"/> if no obstacle is present,
    /// allowing the elimination pipeline to continue to tile processing.
    /// </summary>
    ObstacleHitResult TryHit(
        ref GameState state, Position pos, in ElimContext ctx,
        int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Process adjacent obstacle reactions for a batch of eliminated tiles.
    /// Each call is one dedup scope — an obstacle adjacent to multiple tiles
    /// in the same batch is damaged only once.
    /// Called by processors after per-group or per-activation elimination.
    /// </summary>
    /// <param name="eliminated">
    /// Tiles that were actually destroyed (outcome == Eliminated) in this batch.
    /// </param>
    void NotifyBatchElimination(
        ref GameState state,
        ReadOnlySpan<EliminatedTileInfo> eliminated,
        int tick, float simTime, IEventCollector events);

    /// <summary>
    /// Process global obstacle reactions for a specific color elimination.
    /// Used for obstacles like Curtain that react to any same-color elimination
    /// anywhere on the board. Called once per batch, not per tile.
    /// </summary>
    void NotifyGlobalColorElimination(
        ref GameState state, ElementType color,
        int tick, float simTime, IEventCollector events);
}
