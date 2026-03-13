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
    private readonly ExplosionConfig _config;

    /// <summary>Default wave interval for generic explosions (seconds).</summary>
    [System.Obsolete("Use ExplosionConfig.DefaultWaveInterval instead.")]
    public const float DefaultWaveInterval = 0.1f;

    public ExplosionSystem()
        : this(new CoverSystem(), new GroundSystem(), null, null)
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem, ILevelObjectiveSystem? objectiveSystem = null)
        : this(coverSystem, groundSystem, objectiveSystem, null)
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem, ILevelObjectiveSystem? objectiveSystem, LockScheduler? lockScheduler, ExplosionConfig? config = null)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
        _config = config ?? new ExplosionConfig();
    }

    /// <summary>Explosion timing configuration used by this system.</summary>
    public ExplosionConfig Config => _config;

    public bool HasActiveExplosions => _activeExplosions.Count > 0;

    public void CreateExplosion(ref GameState state, Position origin, int radius)
    {
        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, radius, _config.DefaultWaveInterval);

        // Calculate affected area and lock tiles
        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (state.IsValid(x, y))
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
        => CreateTargetedExplosion(ref state, origin, targets, _config.DefaultWaveInterval);

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval)
        => CreateTargetedExplosion(ref state, origin, targets, waveInterval, 1f);

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration)
        => CreateTargetedExplosion(ref state, origin, targets, waveInterval, acceleration, ReceiveLockTimings.ExplosionClear);

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration, float receiveLockDuration)
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
        explosion.ReceiveLockDuration = receiveLockDuration;

        // 3. Populate AffectedArea and lock tiles
        foreach (var pos in targets)
        {
            explosion.AffectedArea.Add(pos);

            if (state.IsValid(pos.X, pos.Y))
            {
                // Lock ALL target positions (including None tiles cleared by ClearBombAttribute)
                // to prevent premature gravity fill during multi-wave explosions.
                // ProcessWave releases the lock when the wave reaches each position.
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
