using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Handles the per-wave tile processing logic for explosions.
/// Evaluates each cell at the current wave distance — checking covers,
/// indestructible locks, chain reactions, and performing tile destruction
/// with event emission, objective tracking, and lock management.
/// </summary>
internal sealed class WavePropagation
{
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler? _lockScheduler;

    /// <summary>
    /// Creates a new <see cref="WavePropagation"/> instance.
    /// </summary>
    /// <param name="coverSystem">Cover layer system for protection checks.</param>
    /// <param name="groundSystem">Ground layer system for damage notification.</param>
    /// <param name="objectiveSystem">Optional objective system for goal tracking.</param>
    /// <param name="lockScheduler">Optional lock scheduler for cell lock management.</param>
    internal WavePropagation(
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        ILevelObjectiveSystem? objectiveSystem,
        LockScheduler? lockScheduler)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
    }

    /// <summary>
    /// Process a single wave of the explosion, destroying tiles at the current
    /// Chebyshev distance from the origin. Handles cover absorption, indestructible
    /// tiles, bomb chain reactions, tile destruction events, and lock release.
    /// </summary>
    /// <param name="state">Current game state (passed by ref for mutation).</param>
    /// <param name="explosion">The active explosion being processed.</param>
    /// <param name="tick">Current simulation tick for event timestamps.</param>
    /// <param name="simTime">Current simulation time for event timestamps.</param>
    /// <param name="eventCollector">Event collector for emitting destruction events.</param>
    /// <param name="triggeredBombs">Output list to collect positions of chain-triggered bombs.</param>
    internal void ProcessWave(
        ref GameState state,
        Explosion explosion,
        int tick,
        float simTime,
        IEventCollector eventCollector,
        List<Position> triggeredBombs)
    {
        int currentWave = explosion.CurrentWaveRadius;

        // Iterate through affected area and process tiles at current wave distance
        foreach (var pos in explosion.AffectedArea)
        {
            // Chebyshev distance for Square
            int dist = Math.Max(
                Math.Abs(pos.X - explosion.Origin.X),
                Math.Abs(pos.Y - explosion.Origin.Y)
            );

            if (dist == currentWave)
            {
                // Check cover layer first
                if (_coverSystem.IsTileProtected(in state, pos))
                {
                    // Damage the cover, tile is protected this round
                    _coverSystem.TryDamageCover(ref state, pos, tick, simTime, eventCollector);

                    // Release lock for this cell (cover absorbed the hit)
                    ReleaseLockForCell(ref state, explosion, pos);
                    continue;
                }

                // Check Indestructible lock — tile survives the wave
                if (!state.CanDestroy(pos))
                {
                    ReleaseLockForCell(ref state, explosion, pos);
                    continue;
                }

                var tile = state.GetTile(pos.X, pos.Y);

                // If tile exists
                if (tile.Type != ElementType.None)
                {
                    // Check for chain reaction (Bombs)
                    // If it's a bomb and NOT the origin (which is the source of this explosion), trigger it
                    if (tile.Type.IsBomb() && !(pos.X == explosion.Origin.X && pos.Y == explosion.Origin.Y))
                    {
                        triggeredBombs.Add(pos);
                        // Release lock but don't destroy - let triggered activation handle it
                        ReleaseLockForCell(ref state, explosion, pos);
                        continue;
                    }

                    // Emit event
                    if (eventCollector.IsEnabled)
                    {
                        eventCollector.Emit(new TileDestroyedEvent
                        {
                            Tick = tick,
                            SimulationTime = simTime,
                            TileId = tile.Id,
                            GridPosition = pos,
                            Type = tile.Type,
                            Reason = DestroyReason.BombEffect,
                            IsGoal = _objectiveSystem != null && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Tile, (int)tile.Type)
                        });
                    }

                    // Track objective progress before destroying
                    _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, tick, simTime, eventCollector);

                    // Destroy (Set to None) — Drop lock is released since tile is gone
                    state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));
                    ReleaseLockForCell(ref state, explosion, pos);

                    // Apply timed Receive lock to prevent premature gravity fill
                    if (_lockScheduler != null)
                        _lockScheduler.Acquire(ref state, pos, CellLockType.Receive, explosion.ReceiveLockDuration);

                    // Notify ground layer
                    _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, eventCollector);
                }
                else
                {
                    // Empty cell — release any lock that may have been acquired
                    ReleaseLockForCell(ref state, explosion, pos);
                }
            }
        }

        explosion.CurrentWaveRadius++;
    }

    /// <summary>
    /// Find and release the lock token (or legacy lock) for a specific cell position.
    /// When using <see cref="LockScheduler"/>, searches the explosion's token list
    /// by cell index. Otherwise falls back to direct state unlock.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="explosion">The explosion owning the lock.</param>
    /// <param name="pos">The cell position to release.</param>
    internal void ReleaseLockForCell(ref GameState state, Explosion explosion, Position pos)
    {
        if (_lockScheduler != null)
        {
            int cellIndex = state.Index(pos);
            for (int i = explosion.LockTokens.Count - 1; i >= 0; i--)
            {
                if (explosion.LockTokens[i].CellIndex == cellIndex)
                {
                    _lockScheduler.Release(ref state, explosion.LockTokens[i]);
                    explosion.LockTokens.RemoveAt(i);
                    break;
                }
            }
        }
        else
        {
            // Only unlock if this explosion actually locked the cell
            if (explosion.LockedPositions.Remove(pos))
            {
                state.Unlock(pos, CellLockType.Drop);
            }
        }
    }
}
