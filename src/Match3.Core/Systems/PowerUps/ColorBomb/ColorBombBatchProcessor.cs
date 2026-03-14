using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Handles batch destruction (normal mode) and batch activation (combo mode)
/// for ColorBomb sessions after all beams have arrived.
/// </summary>
internal sealed class ColorBombBatchProcessor
{
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler _lockScheduler;

    /// <summary>
    /// Creates a new <see cref="ColorBombBatchProcessor"/>.
    /// </summary>
    /// <param name="coverSystem">Cover system for protection checks.</param>
    /// <param name="groundSystem">Ground system for ground-layer damage.</param>
    /// <param name="objectiveSystem">Optional objective system for goal tracking.</param>
    /// <param name="lockScheduler">Lock scheduler for cell lock lifecycle management.</param>
    public ColorBombBatchProcessor(
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        ILevelObjectiveSystem? objectiveSystem,
        LockScheduler lockScheduler)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
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
            // First pass: collect valid targets, handle covers
            foreach (var target in session.ArrivedTargets)
            {
                var tile = state.GetTile(target.Position.X, target.Position.Y);
                if (tile.Id != target.TileId || tile.Type == ElementType.None)
                    continue;

                // Check cover
                if (_coverSystem.IsTileProtected(in state, target.Position))
                {
                    _coverSystem.TryDamageCover(ref state, target.Position, tick, simTime, events);
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

            // Second pass: destroy tiles and emit per-tile events
            for (int i = 0; i < destroyedPositions.Count; i++)
            {
                var pos = destroyedPositions[i];
                var tile = state.GetTile(pos.X, pos.Y);
                if (tile.Type == ElementType.None) continue;

                if (events.IsEnabled)
                {
                    events.Emit(new TileDestroyedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = tile.Id,
                        GridPosition = pos,
                        Type = tile.Type,
                        Reason = DestroyReason.BombEffect,
                        IsGoal = _objectiveSystem != null &&
                                 _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Tile, (int)tile.Type)
                    });
                }

                _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, tick, simTime, events);
                state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));
                _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
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
}
