using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

public class ExplosionSystem : IExplosionSystem
{
    private readonly List<Explosion> _activeExplosions = new();
    private readonly List<Explosion> _explosionsToRemove = new();
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler? _lockScheduler;

    // Config
    private const float WaveInterval = 0.1f; // 100ms per wave

    public ExplosionSystem()
        : this(new CoverSystem(), new GroundSystem(), null, null)
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem, ILevelObjectiveSystem? objectiveSystem = null)
        : this(coverSystem, groundSystem, objectiveSystem, null)
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem, ILevelObjectiveSystem? objectiveSystem, LockScheduler? lockScheduler)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
    }

    public bool HasActiveExplosions => _activeExplosions.Count > 0;

    public void CreateExplosion(ref GameState state, Position origin, int radius)
    {
        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, radius, WaveInterval);

        // Calculate affected area and lock tiles
        int width = state.Width;
        int height = state.Height;

        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    var pos = new Position(x, y);
                    explosion.AffectedArea.Add(pos);

                    var tile = state.GetTile(x, y);
                    if (tile.Type != ElementType.None)
                    {
                        if (_lockScheduler != null)
                        {
                            var token = _lockScheduler.Acquire(ref state, pos, CellLockType.Drop);
                            explosion.LockTokens.Add(token);
                        }
                        else
                        {
                            state.Lock(pos, CellLockType.Drop);
                            explosion.LockedPositions.Add(pos);
                        }
                    }
                }
            }
        }

        _activeExplosions.Add(explosion);
    }

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets)
        => CreateTargetedExplosion(ref state, origin, targets, WaveInterval);

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval)
        => CreateTargetedExplosion(ref state, origin, targets, waveInterval, 1f);

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration)
    {
        // 1. Calculate MaxRadius
        int maxRadius = 0;
        foreach (var pos in targets)
        {
            int dist = Math.Max(
                Math.Abs(pos.X - origin.X),
                Math.Abs(pos.Y - origin.Y)
            );
            if (dist > maxRadius) maxRadius = dist;
        }

        // 2. Initialize Explosion
        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, maxRadius, waveInterval, acceleration);

        // 3. Populate AffectedArea and lock tiles
        foreach (var pos in targets)
        {
            explosion.AffectedArea.Add(pos);

            if (pos.X >= 0 && pos.X < state.Width && pos.Y >= 0 && pos.Y < state.Height)
            {
                var tile = state.GetTile(pos.X, pos.Y);
                if (tile.Type != ElementType.None)
                {
                    if (_lockScheduler != null)
                    {
                        var token = _lockScheduler.Acquire(ref state, pos, CellLockType.Drop);
                        explosion.LockTokens.Add(token);
                    }
                    else
                    {
                        state.Lock(pos, CellLockType.Drop);
                        explosion.LockedPositions.Add(pos);
                    }
                }
            }
        }

        _activeExplosions.Add(explosion);
    }

    public void Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector eventCollector,
        List<Position> triggeredBombs)
    {
        _explosionsToRemove.Clear();

        foreach (var explosion in _activeExplosions)
        {
            explosion.Timer += deltaTime;

            // Process as many waves as time allows (to handle lag spikes)
            while (explosion.Timer >= explosion.WaveInterval && !explosion.IsFinished)
            {
                explosion.Timer -= explosion.WaveInterval;
                ProcessWave(ref state, explosion, tick, simTime, eventCollector, triggeredBombs);

                // Apply acceleration: shrink interval for next wave
                if (explosion.Acceleration != 1f)
                    explosion.WaveInterval *= explosion.Acceleration;
            }

            if (explosion.IsFinished)
            {
                _explosionsToRemove.Add(explosion);
            }
        }

        foreach (var ex in _explosionsToRemove)
        {
            _activeExplosions.Remove(ex);
            ex.Release();
            Pools.Release(ex);
        }
    }

    private void ProcessWave(
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

                    // Destroy (Set to None) — lock is released since tile is gone
                    state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));
                    ReleaseLockForCell(ref state, explosion, pos);

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
    /// Find and release the lock token for a specific cell position.
    /// </summary>
    private void ReleaseLockForCell(ref GameState state, Explosion explosion, Position pos)
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

    public void Reset()
    {
        foreach (var ex in _activeExplosions)
        {
            ex.Release();
            Pools.Release(ex);
        }
        _activeExplosions.Clear();
    }
}
