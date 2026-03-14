using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Manages explosion lifecycles: creation, wave timing, and cleanup.
/// Delegates per-wave tile processing to <see cref="WavePropagation"/>.
/// </summary>
public class ExplosionSystem : IExplosionSystem
{
    private readonly List<Explosion> _activeExplosions = new();
    private readonly List<Explosion> _explosionsToRemove = new();
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler? _lockScheduler;
    private readonly WavePropagation _wavePropagation;
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
        _wavePropagation = new WavePropagation(coverSystem, groundSystem, objectiveSystem, lockScheduler);
        _config = config ?? new ExplosionConfig();
    }

    /// <summary>Explosion timing configuration used by this system.</summary>
    public ExplosionConfig Config => _config;


    public bool HasActiveExplosions => _activeExplosions.Count > 0;

    /// <summary>
    /// Creates a square explosion centered on <paramref name="origin"/> with the given radius.
    /// Locks all affected tiles to prevent gravity until the wave reaches them.
    /// </summary>
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

    /// <inheritdoc />
    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets)
        => CreateTargetedExplosion(ref state, origin, targets, _config.DefaultWaveInterval);

    /// <inheritdoc />
    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval)
        => CreateTargetedExplosion(ref state, origin, targets, waveInterval, 1f);

    /// <inheritdoc />
    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration)
        => CreateTargetedExplosion(ref state, origin, targets, waveInterval, acceleration, ReceiveLockTimings.ExplosionClear);

    /// <summary>
    /// Creates a targeted explosion affecting only the specified positions.
    /// Locks all target cells and computes the max Chebyshev radius for wave scheduling.
    /// </summary>
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

    /// <summary>
    /// Advances all active explosions by <paramref name="deltaTime"/>, processing
    /// as many waves as time allows. Finished explosions are cleaned up.
    /// </summary>
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
                _wavePropagation.ProcessWave(ref state, explosion, tick, simTime, eventCollector, triggeredBombs);

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

    /// <inheritdoc />
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
