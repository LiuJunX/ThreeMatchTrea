using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Handles the per-wave tile processing logic for explosions.
/// Evaluates each cell at the current wave distance — checking covers,
/// indestructible locks, chain reactions, and performing tile destruction
/// with event emission, objective tracking, and lock management.
/// </summary>
internal sealed class WavePropagation
{
    private readonly ICellEliminator _cellEliminator;
    private readonly LockScheduler? _lockScheduler;

    /// <summary>
    /// Backward-compatible constructor — creates a <see cref="CellEliminator"/> internally.
    /// </summary>
    internal WavePropagation(
        Layers.ICoverSystem coverSystem,
        Layers.IGroundSystem groundSystem,
        Objectives.ILevelObjectiveSystem? objectiveSystem,
        LockScheduler? lockScheduler)
        : this(new CellEliminator(coverSystem, groundSystem, objectiveSystem), lockScheduler)
    {
    }

    internal WavePropagation(
        ICellEliminator cellEliminator,
        LockScheduler? lockScheduler)
    {
        _cellEliminator = cellEliminator;
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
                var tile = state.GetTile(pos.X, pos.Y);

                // Bomb chain reaction: trigger bomb, don't destroy (skip origin).
                // Respect indestructible lock — protected bombs should not be triggered.
                if (tile.Type.IsBomb() && !(pos.X == explosion.Origin.X && pos.Y == explosion.Origin.Y))
                {
                    if (state.CanDestroy(pos))
                        triggeredBombs.Add(pos);
                    ReleaseLockForCell(ref state, explosion, pos);
                    continue;
                }

                // Unified elimination
                var result = _cellEliminator.Eliminate(ref state, pos, DestroyReason.BombEffect, tick, simTime, eventCollector);
                ReleaseLockForCell(ref state, explosion, pos);

                if (result == EliminateResult.Eliminated)
                {
                    // Apply timed Receive lock to prevent premature gravity fill
                    if (_lockScheduler != null)
                        _lockScheduler.Acquire(ref state, pos, CellLockType.Receive, explosion.ReceiveLockDuration);
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
