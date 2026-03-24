using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Handles batch destruction (normal mode) and batch activation (combo mode)
/// for ColorBomb sessions after all beams have arrived.
/// </summary>
internal sealed class ColorBombBatchProcessor
{
    private readonly ICellEliminator _cellEliminator;
    private readonly LockScheduler _lockScheduler;
    private readonly IObstacleSystem? _obstacleSystem;
    private readonly ICoverSystem? _coverSystem;

    /// <summary>
    /// Creates a new <see cref="ColorBombBatchProcessor"/>.
    /// </summary>
    /// <param name="cellEliminator">Unified cell elimination pipeline.</param>
    /// <param name="lockScheduler">Lock scheduler for cell lock lifecycle management.</param>
    /// <param name="obstacleSystem">Obstacle system for adjacent reaction notifications.</param>
    /// <param name="coverSystem">Cover system for adjacent reaction notifications.</param>
    public ColorBombBatchProcessor(
        ICellEliminator cellEliminator,
        LockScheduler lockScheduler,
        IObstacleSystem? obstacleSystem = null,
        ICoverSystem? coverSystem = null)
    {
        _cellEliminator = cellEliminator;
        _lockScheduler = lockScheduler;
        _obstacleSystem = obstacleSystem;
        _coverSystem = coverSystem;
    }

    /// <summary>
    /// Executes batch destruction for normal ColorBomb mode. Validates arrived targets,
    /// handles cover protection, emits batch and per-tile destroy events, clears tiles,
    /// releases session locks, and applies post-destruction grace period locks.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for destroy events.</param>
    internal void ExecuteBatchDestroy(ref GameState state, ColorBombSession session,
        int tick, float simTime, IEventCollector events)
    {
        var destroyedPositions = Pools.ObtainList<Position>(session.ArrivedTargets.Count);
        var destroyedTileIds = Pools.ObtainList<int>(session.ArrivedTargets.Count);
        try
        {
            // First pass: attempt elimination for each target via CellEliminator.
            // Cover-protected targets are handled (Absorbed), others are collected
            // for the batch event. Elimination is deferred until after the batch
            // event because Choreographer requires it before TileDestroyedEvents.
            foreach (var target in session.ArrivedTargets)
            {
                var tile = state.GetTile(target.Position.X, target.Position.Y);
                if (tile.Id != target.TileId || tile.Type == ElementType.None)
                    continue;

                // CellEliminator handles cover damage internally.
                // We call Eliminate here for covered targets to absorb the hit;
                // non-covered targets are deferred to pass 2.
                var cover = state.GetCover(target.Position);
                if (cover.Type != CoverType.None && cover.Health > 0)
                {
                    _cellEliminator.Eliminate(ref state, target.Position, ElimSource.ColorBomb, tick, simTime, events);
                    continue;
                }

                destroyedPositions.Add(target.Position);
                destroyedTileIds.Add(tile.Id);
            }

            // Emit batch destroy event FIRST — Choreographer uses this to synchronize
            // _beamHitTimes before TileDestroyedEvents are processed.
            // Event snapshots the lists because BufferedEventCollector stores events
            // beyond this method's lifetime.
            if (events.IsEnabled)
            {
                events.Emit(new ColorBombBatchDestroyEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    BombTileId = session.BombTileId,
                    BombPosition = session.BombPosition,
                    DestroyedPositions = new List<Position>(destroyedPositions),
                    DestroyedTileIds = new List<int>(destroyedTileIds)
                });
            }

            // Second pass: destroy tiles via CellEliminator (event, objective, clear, ground)
            var eliminatedList = Pools.ObtainList<EliminatedTileInfo>(destroyedPositions.Count);
            try
            {
                foreach (var pos in destroyedPositions)
                {
                    var result = _cellEliminator.Eliminate(ref state, pos, ElimSource.ColorBomb, tick, simTime, events);
                    if (result.Outcome == EliminateOutcome.Eliminated)
                    {
                        eliminatedList.Add(new EliminatedTileInfo(pos, result.Tile, ElimSource.ColorBomb));
                    }
                }

                // Notify adjacent obstacles and covers (same pattern as StandardMatchProcessor)
                if (eliminatedList.Count > 0)
                {
                    var eliminatedSpan = new ReadOnlySpan<EliminatedTileInfo>(eliminatedList.ToArray());

                    _obstacleSystem?.NotifyBatchElimination(
                        ref state, eliminatedSpan, tick, simTime, events);
                    _obstacleSystem?.NotifyGlobalColorElimination(
                        ref state, session.TargetColor, tick, simTime, events);
                    _coverSystem?.NotifyBatchElimination(
                        ref state, eliminatedSpan, tick, simTime, events);

                    // Damage adjacent moving obstacle tiles (same pattern as StandardMatchProcessor)
                    DamageAdjacentMultiStageTiles(ref state, eliminatedList, tick, simTime, events);
                }
            }
            finally
            {
                Pools.Release(eliminatedList);
            }

            // Release all remaining session locks
            ReleaseAllSessionLocks(ref state, session);

            // Apply timed Receive locks for destroyed positions (post-destruction grace period)
            foreach (var pos in destroyedPositions)
            {
                _lockScheduler.Acquire(ref state, pos, CellLockType.Receive, ReceiveLockTimings.ColorBombBatchClear);
            }

            session.ArrivedTargets.Clear();
        }
        finally
        {
            Pools.Release(destroyedTileIds);
            Pools.Release(destroyedPositions);
        }
    }

    /// <summary>
    /// Executes batch activation for combo ColorBomb mode. All beams have arrived
    /// and targets have been transformed to bombs. Releases locks, emits batch event,
    /// and outputs bomb positions for activation by the orchestrator.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for batch activate events.</param>
    /// <param name="triggeredBombs">Output list for bomb positions to activate; may be null.</param>
    internal void ExecuteBatchActivate(ref GameState state, ColorBombSession session,
        int tick, float simTime, IEventCollector events, List<Position>? triggeredBombs)
    {
        var activatedPositions = Pools.ObtainList<Position>(session.ArrivedTargets.Count);
        var activatedTileIds = Pools.ObtainList<int>(session.ArrivedTargets.Count);
        try
        {
            // Collect valid transformed bombs
            foreach (var target in session.ArrivedTargets)
            {
                var tile = state.GetTile(target.Position.X, target.Position.Y);
                if (tile.Id != target.TileId || tile.Type == ElementType.None)
                    continue;

                activatedPositions.Add(target.Position);
                activatedTileIds.Add(tile.Id);
            }

            // Emit batch activate event.
            // Event snapshots the lists because BufferedEventCollector stores events
            // beyond this method's lifetime.
            if (events.IsEnabled)
            {
                events.Emit(new ColorBombComboBatchActivateEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    BombTileId = session.BombTileId,
                    BombPosition = session.BombPosition,
                    ComboBombType = session.ComboBombType,
                    ActivatedPositions = new List<Position>(activatedPositions),
                    ActivatedTileIds = new List<int>(activatedTileIds)
                });
            }

            // Release ALL session locks (BeamTargetLock + Indestructible) before activation
            ReleaseAllSessionLocks(ref state, session);

            // Output bomb positions — orchestrator will call ActivateBomb for each
            if (triggeredBombs != null)
            {
                foreach (var pos in activatedPositions)
                    triggeredBombs.Add(pos);
            }

            session.ArrivedTargets.Clear();
        }
        finally
        {
            Pools.Release(activatedTileIds);
            Pools.Release(activatedPositions);
        }
    }

    /// <summary>
    /// Releases all remaining lock tokens for the entire session.
    /// </summary>
    private void ReleaseAllSessionLocks(ref GameState state, ColorBombSession session)
    {
        foreach (var token in session.LockTokens)
            _lockScheduler.Release(ref state, token);
        session.LockTokens.Clear();
    }

    /// <summary>
    /// Damage adjacent moving obstacle tiles after ColorBomb batch elimination.
    /// </summary>
    private void DamageAdjacentMultiStageTiles(
        ref GameState state, List<EliminatedTileInfo> eliminated,
        int tick, float simTime, IEventCollector events)
    {
        var hit = Pools.ObtainHashSet<Position>();
        try
        {
            foreach (var info in eliminated)
            {
                TryDamageMovingObstacleAt(ref state, new Position(info.Pos.X - 1, info.Pos.Y), hit, tick, simTime, events);
                TryDamageMovingObstacleAt(ref state, new Position(info.Pos.X + 1, info.Pos.Y), hit, tick, simTime, events);
                TryDamageMovingObstacleAt(ref state, new Position(info.Pos.X, info.Pos.Y - 1), hit, tick, simTime, events);
                TryDamageMovingObstacleAt(ref state, new Position(info.Pos.X, info.Pos.Y + 1), hit, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(hit);
        }
    }

    private void TryDamageMovingObstacleAt(
        ref GameState state, Position pos, HashSet<Position> hit,
        int tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(pos.X, pos.Y)) return;
        var tile = state.GetTile(pos.X, pos.Y);
        if (tile.Type == ElementType.None) return;
        if (!tile.Type.IsMovingObstacle()) return;
        if (!hit.Add(pos)) return; // dedup: one damage per position per batch
        _cellEliminator.Eliminate(ref state, pos, ElimSource.ColorBomb, tick, simTime, events);
    }
}
