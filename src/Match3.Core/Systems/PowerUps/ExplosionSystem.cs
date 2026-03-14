using System;
using System.Collections.Generic;
using System.Diagnostics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Manages explosion lifecycles: creation, wave timing, and cleanup.
/// No pre-locking — cells are locked only when the wave actually hits them.
/// When a wave hits a bomb, it is eliminated and immediately starts a new wave front.
/// </summary>
public class ExplosionSystem : IExplosionSystem
{
    private readonly List<Explosion> _activeExplosions = new();
    private readonly List<Explosion> _explosionsToRemove = new();
    private readonly List<Explosion> _pendingExplosions = new();
    private readonly ICellEliminator _cellEliminator;
    private readonly BombEffectRegistry _bombEffectRegistry;
    private readonly LockScheduler? _lockScheduler;
    private readonly ExplosionConfig _config;
    private int _chainReactionCount;

    public ExplosionSystem()
        : this(
            new CellEliminator(new CoverSystem(), new GroundSystem()),
            BombEffectRegistry.CreateDefault(),
            null)
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem, ILevelObjectiveSystem? objectiveSystem = null)
        : this(
            new CellEliminator(coverSystem, groundSystem, objectiveSystem),
            BombEffectRegistry.CreateDefault(),
            null)
    {
    }

    public ExplosionSystem(
        ICoverSystem coverSystem, IGroundSystem groundSystem,
        ILevelObjectiveSystem? objectiveSystem, LockScheduler? lockScheduler,
        ExplosionConfig? config = null)
        : this(
            new CellEliminator(coverSystem, groundSystem, objectiveSystem),
            BombEffectRegistry.CreateDefault(),
            lockScheduler,
            config)
    {
    }

    public ExplosionSystem(
        ICellEliminator cellEliminator,
        BombEffectRegistry bombEffectRegistry,
        LockScheduler? lockScheduler,
        ExplosionConfig? config = null)
    {
        _cellEliminator = cellEliminator;
        _bombEffectRegistry = bombEffectRegistry;
        _lockScheduler = lockScheduler;
        _config = config ?? new ExplosionConfig();
    }

    /// <summary>Explosion timing configuration used by this system.</summary>
    public ExplosionConfig Config => _config;

    public bool HasActiveExplosions => _activeExplosions.Count > 0;

    /// <summary>
    /// Creates a square explosion centered on <paramref name="origin"/> with the given radius.
    /// No pre-locking — cells are processed when the wave reaches them.
    /// </summary>
    public void CreateExplosion(ref GameState state, Position origin, int radius)
    {
        Debug.Assert(_config.DefaultWaveInterval < ReceiveLockTimings.ExplosionClear,
            $"WaveInterval({_config.DefaultWaveInterval}) must be < ReceiveLockDuration({ReceiveLockTimings.ExplosionClear}), " +
            "otherwise gravity can steal tiles before the next wave reaches them.");

        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, radius, _config.DefaultWaveInterval);

        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (state.IsValid(x, y))
                    explosion.AffectedArea.Add(new Position(x, y));
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
    /// No pre-locking — cells are processed when the wave reaches them.
    /// </summary>
    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration, float receiveLockDuration)
    {
        Debug.Assert(waveInterval < receiveLockDuration,
            $"WaveInterval({waveInterval}) must be < ReceiveLockDuration({receiveLockDuration}), " +
            "otherwise gravity can steal tiles before the next wave reaches them.");

        int maxRadius = 0;
        foreach (var pos in targets)
        {
            int dist = Math.Max(
                Math.Abs(pos.X - origin.X),
                Math.Abs(pos.Y - origin.Y)
            );
            if (dist > maxRadius) maxRadius = dist;
        }

        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, maxRadius, waveInterval, acceleration);
        explosion.ReceiveLockDuration = receiveLockDuration;

        foreach (var pos in targets)
            explosion.AffectedArea.Add(pos);

        _activeExplosions.Add(explosion);
    }

    /// <summary>
    /// Advances all active explosions by <paramref name="deltaTime"/>, processing
    /// as many waves as time allows. When a wave hits a bomb, it is eliminated
    /// and a new wave front is started immediately. Finished explosions are cleaned up.
    /// </summary>
    public int Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector eventCollector)
    {
        _explosionsToRemove.Clear();
        _chainReactionCount = 0;

        foreach (var explosion in _activeExplosions)
        {
            explosion.Timer += deltaTime;

            while (explosion.Timer >= explosion.WaveInterval && !explosion.IsFinished)
            {
                explosion.Timer -= explosion.WaveInterval;
                ProcessWave(ref state, explosion, tick, simTime, eventCollector);

                if (explosion.Acceleration != 1f)
                    explosion.WaveInterval *= explosion.Acceleration;
            }

            if (explosion.IsFinished)
                _explosionsToRemove.Add(explosion);
        }

        foreach (var ex in _explosionsToRemove)
        {
            _activeExplosions.Remove(ex);
            ex.Release();
            Pools.Release(ex);
        }

        // Add any explosions created during this tick (chain reactions)
        if (_pendingExplosions.Count > 0)
        {
            _activeExplosions.AddRange(_pendingExplosions);
            _pendingExplosions.Clear();
        }

        return _chainReactionCount;
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
        _pendingExplosions.Clear();
    }

    /// <summary>
    /// Process a single wave: eliminate all cells at the current Chebyshev distance.
    /// Bombs are eliminated and immediately start new wave fronts.
    /// </summary>
    private void ProcessWave(
        ref GameState state,
        Explosion explosion,
        int tick,
        float simTime,
        IEventCollector eventCollector)
    {
        int currentWave = explosion.CurrentWaveRadius;

        foreach (var pos in explosion.AffectedArea)
        {
            int dist = Math.Max(
                Math.Abs(pos.X - explosion.Origin.X),
                Math.Abs(pos.Y - explosion.Origin.Y)
            );

            if (dist != currentWave) continue;

            var result = _cellEliminator.Eliminate(ref state, pos, ElimSource.Bomb, tick, simTime, eventCollector);

            // ColorBomb returns Immune here (ElimSource.Bomb ≠ ConsumeBomb).
            // By design, ColorBombs are not chain-triggered by explosions;
            // they can only be activated via player swap or ConsumeBomb.

            if (result.Outcome == EliminateOutcome.Eliminated)
            {
                _lockScheduler?.Acquire(ref state, pos, CellLockType.Receive, explosion.ReceiveLockDuration);

                // Chain reaction: eliminated bomb starts a new wave front
                if (result.Tile.Type.IsBomb()
                    && !(pos.X == explosion.Origin.X && pos.Y == explosion.Origin.Y))
                {
                    StartChainExplosion(ref state, pos, result.Tile, tick, simTime, eventCollector);
                }
            }
        }

        explosion.CurrentWaveRadius++;
    }

    /// <summary>
    /// Creates a new explosion for a bomb that was hit by a wave.
    /// Emits BombActivatedEvent and computes the effect area.
    /// </summary>
    private void StartChainExplosion(
        ref GameState state, Position pos, Tile bombTile,
        int tick, float simTime, IEventCollector eventCollector)
    {
        if (!_bombEffectRegistry.TryGetEffect(bombTile.Type, out var effect))
            return;

        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            effect!.Apply(in state, pos, affected);
            affected.Add(pos);

            if (eventCollector.IsEnabled)
            {
                eventCollector.Emit(new BombActivatedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    TileId = bombTile.Id,
                    Position = pos,
                    BombType = bombTile.Type,
                    AffectedPositions = new List<Position>(affected),
                    IsChainReaction = true
                });
            }

            // Determine wave interval based on bomb type
            float waveInterval;
            float acceleration;
            if (bombTile.Type == ElementType.HorizontalRocket || bombTile.Type == ElementType.VerticalRocket)
            {
                waveInterval = _config.RocketWaveInterval;
                acceleration = _config.RocketAcceleration;
            }
            else if (bombTile.Type.IsAreaBomb())
            {
                waveInterval = _config.AreaBombWaveInterval;
                acceleration = _config.AreaBombAcceleration;
            }
            else
            {
                waveInterval = _config.DefaultWaveInterval;
                acceleration = 1f;
            }

            // Calculate max radius
            int maxRadius = 0;
            foreach (var p in affected)
            {
                int dist = Math.Max(Math.Abs(p.X - pos.X), Math.Abs(p.Y - pos.Y));
                if (dist > maxRadius) maxRadius = dist;
            }

            Debug.Assert(waveInterval < ReceiveLockTimings.ExplosionClear,
                $"Chain WaveInterval({waveInterval}) must be < ReceiveLockDuration({ReceiveLockTimings.ExplosionClear}), " +
                "otherwise gravity can steal tiles before the next wave reaches them.");

            var chainExplosion = Pools.Obtain<Explosion>();
            chainExplosion.Initialize(pos, maxRadius, waveInterval, acceleration);
            chainExplosion.ReceiveLockDuration = ReceiveLockTimings.ExplosionClear;

            foreach (var p in affected)
                chainExplosion.AffectedArea.Add(p);

            // Add to pending list (processed next tick, not recursively)
            _pendingExplosions.Add(chainExplosion);
            _chainReactionCount++;
        }
        finally
        {
            Pools.Release(affected);
        }
    }
}
