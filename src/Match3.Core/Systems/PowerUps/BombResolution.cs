using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Unified bomb activation and effect resolution.
/// Implements <see cref="IPowerUpHandler"/>; swap routing is delegated to an internal <see cref="BombDispatcher"/>.
/// </summary>
public class BombResolution : IPowerUpHandler
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombComboHandler _comboHandler;
    private readonly BombDispatcher _dispatcher;
    private readonly BombEffectRegistry _effectRegistry;
    private readonly ICellEliminator _cellEliminator;
    private readonly IExplosionSystem? _explosionSystem;
    private readonly IProjectileSystem? _projectileSystem;
    private readonly IColorBombSessionManager? _colorBombSessionManager;
    private readonly LockScheduler? _lockScheduler;
    private readonly ExplosionConfig _explosionConfig;

    public BombResolution(IScoreSystem scoreSystem)
        : this(scoreSystem, new BombComboHandler(), BombEffectRegistry.CreateDefault(),
               new CellEliminator(new CoverSystem(), new GroundSystem()))
    {
    }

    /// <summary>
    /// Backward-compatible constructor — creates a <see cref="CellEliminator"/> internally.
    /// </summary>
    public BombResolution(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        BombEffectRegistry effectRegistry,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        IExplosionSystem? explosionSystem = null,
        IProjectileSystem? projectileSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null,
        LockScheduler? lockScheduler = null,
        ExplosionConfig? explosionConfig = null)
        : this(scoreSystem, comboHandler, effectRegistry,
               new CellEliminator(coverSystem, groundSystem),
               explosionSystem, projectileSystem, colorBombSessionManager,
               lockScheduler, explosionConfig)
    {
    }

    public BombResolution(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        BombEffectRegistry effectRegistry,
        ICellEliminator cellEliminator,
        IExplosionSystem? explosionSystem = null,
        IProjectileSystem? projectileSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null,
        LockScheduler? lockScheduler = null,
        ExplosionConfig? explosionConfig = null)
    {
        _scoreSystem = scoreSystem;
        _comboHandler = comboHandler;
        _effectRegistry = effectRegistry;
        _cellEliminator = cellEliminator;
        _explosionSystem = explosionSystem;
        _projectileSystem = projectileSystem;
        _colorBombSessionManager = colorBombSessionManager;
        _lockScheduler = lockScheduler;
        _explosionConfig = explosionConfig ?? new ExplosionConfig();
        _dispatcher = new BombDispatcher(scoreSystem, comboHandler, this, colorBombSessionManager);
    }

    public BombResolution(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        SimulationContext context,
        IExplosionSystem? explosionSystem = null,
        IProjectileSystem? projectileSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null,
        ExplosionConfig? explosionConfig = null)
        : this(scoreSystem, comboHandler, context.BombEffectRegistry, context.CellEliminator,
               explosionSystem, projectileSystem, colorBombSessionManager,
               context.LockScheduler, explosionConfig)
    {
    }

    // ── IPowerUpHandler: routing delegated to BombDispatcher ──

    public void ProcessBombSwap(ref GameState state, Position p1, Position p2, out int points)
    {
        _dispatcher.ProcessBombSwap(ref state, p1, p2, out points);
    }

    public void ProcessBombSwap(
        ref GameState state,
        Position p1,
        Position p2,
        int tick,
        float simTime,
        IEventCollector events,
        out int points)
    {
        _dispatcher.ProcessBombSwap(ref state, p1, p2, tick, simTime, events, out points);
    }

    // ── IPowerUpHandler: activation ──

    public void ActivateBomb(ref GameState state, Position p)
    {
        ActivateBomb(ref state, p, 0, 0f, NullEventCollector.Instance);
    }

    public void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events,
        bool isChainReaction = false)
    {
        var t = state.GetTile(p.X, p.Y);
        if (!t.Type.IsBomb()) return;

        // ColorBomb with session manager: route to multi-tick session (player-initiated only).
        // Chain reactions bypass the session to avoid conflicts with the active explosion.
        if (t.Type == ElementType.ColorBomb && _colorBombSessionManager != null && !isChainReaction)
        {
            int bombTileId = t.Id;
            ConsumeBomb(ref state, p, tick, simTime, events);
            _colorBombSessionManager.CreateSession(ref state, p, bombTileId, tick, simTime, events);
            return;
        }

        // Clear bomb attribute to prevent re-activation
        ConsumeBomb(ref state, p, tick, simTime, events);

        ExecuteBombActivation(ref state, p, t, tick, simTime, events, isChainReaction);
    }

    /// <inheritdoc />
    public void ActivateChainBomb(ref GameState state, Position p, Tile bombTile,
        int tick, float simTime, IEventCollector events)
    {
        // Tile already eliminated by CellEliminator in ExplosionSystem.ProcessWave —
        // skip tile read and ConsumeBomb, go straight to activation.
        ExecuteBombActivation(ref state, p, bombTile, tick, simTime, events, isChainReaction: true);
    }

    // ── Internal: called by BombDispatcher ──

    /// <summary>
    /// Consumes a bomb tile via CellEliminator to ensure Cover/Ground/Objective handling.
    /// Applies a timed Receive lock to prevent premature gravity fill.
    /// </summary>
    internal void ConsumeBomb(ref GameState state, Position p,
        int tick, float simTime, IEventCollector events)
    {
        var tile = state.GetTile(p.X, p.Y);
        if (tile.Type.IsBomb())
        {
            _cellEliminator.Eliminate(ref state, p, ElimSource.ConsumeBomb, tick, simTime, events);
            _lockScheduler?.Acquire(ref state, p, CellLockType.Receive, ReceiveLockTimings.BombActivateClear);
        }
    }

    /// <summary>
    /// Executes combo explosion and projectile launch after BombComboHandler
    /// has determined the affected positions and combo metadata.
    /// </summary>
    internal void ExecuteComboResult(
        ref GameState state, Position p1, Position p2, Tile t1, Tile t2,
        HashSet<Position> affected, ComboResult comboResult,
        int tick, float simTime, IEventCollector events)
    {
        // Create explosion with timing based on combo type
        if (_explosionSystem != null)
        {
            if (comboResult.IsDoubleColorBomb)
            {
                _explosionSystem.CreateTargetedExplosion(ref state, p2, affected,
                    DoubleColorBombConstants.WipeInterval, DoubleColorBombConstants.WipeAcceleration,
                    ReceiveLockTimings.ColorBombBatchClear);
            }
            else if (comboResult.HasColorBomb)
            {
                _explosionSystem.CreateTargetedExplosion(ref state, p1, affected,
                    _explosionConfig.DefaultWaveInterval, 1f,
                    ReceiveLockTimings.ColorBombBatchClear);
            }
            else
            {
                _explosionSystem.CreateTargetedExplosion(ref state, p1, affected);
            }
        }

        // Launch UFO projectiles described by ComboResult
        if (_projectileSystem != null && comboResult.UfoLaunch is { } ufo)
        {
            if (ufo.IsUfoUfoCombo)
            {
                UfoLaunchHelper.LaunchUfoComboProjectiles(
                    _projectileSystem, ref state,
                    ufo.UfoTileId, ufo.OtherTileId,
                    ufo.UfoPosition, ufo.OtherPosition,
                    tick, simTime, events);
            }
            else
            {
                UfoLaunchHelper.LaunchUfoPayloadProjectile(
                    _projectileSystem, ref state,
                    ufo.UfoTileId, ufo.UfoPosition, ufo.Payload,
                    tick, simTime, events,
                    passengerTileId: ufo.OtherTileId,
                    passengerPos: ufo.OtherPosition);
            }
        }
    }

    // ── Shared activation logic ──

    /// <summary>
    /// Shared activation logic: apply effect → emit event → create explosion → launch UFO projectile.
    /// </summary>
    private void ExecuteBombActivation(ref GameState state, Position p, Tile bombTile,
        int tick, float simTime, IEventCollector events, bool isChainReaction)
    {
        bool isUfo = bombTile.Type.IsUfo();
        int ufoTileId = bombTile.Id;

        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_effectRegistry.TryGetEffect(bombTile.Type, out var effect))
            {
                effect!.Apply(in state, p, affected);
                affected.Add(p); // Ensure origin is in the affected set

                // Emit BombActivatedEvent
                if (events.IsEnabled)
                {
                    events.Emit(new BombActivatedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = bombTile.Id,
                        Position = p,
                        BombType = bombTile.Type,
                        AffectedPositions = new List<Position>(affected),
                        IsChainReaction = isChainReaction
                    });
                }

                // Create explosion — ExplosionSystem handles wave propagation
                if (_explosionSystem != null)
                {
                    bool isRocket = bombTile.Type == ElementType.HorizontalRocket || bombTile.Type == ElementType.VerticalRocket;
                    if (isRocket)
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected,
                            _explosionConfig.RocketWaveInterval, _explosionConfig.RocketAcceleration);
                    else if (bombTile.Type.IsAreaBomb())
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected,
                            _explosionConfig.AreaBombWaveInterval, _explosionConfig.AreaBombAcceleration);
                    else
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected);
                }
            }
        }
        finally
        {
            Pools.Release(affected);
        }

        // UFO: launch projectile for remote target (deferred destruction with dynamic tracking)
        if (isUfo && _projectileSystem != null)
        {
            var remoteTarget = UfoEffect.PickRemoteTarget(in state, p);
            if (remoteTarget.HasValue)
            {
                var projectile = new UfoProjectile(
                    _projectileSystem.GenerateProjectileId(),
                    p,
                    remoteTarget.Value)
                {
                    SourceTileId = ufoTileId,
                    // Chain-triggered UFOs: tile was already eliminated by CellEliminator
                    // (DestroyTileCommand + RemoveTileCommand), so UfoChoreographer must
                    // spawn a fresh tile visual for the flight animation.
                    SpawnVisual = isChainReaction
                };
                _projectileSystem.Launch(projectile, tick, simTime, events);
            }
        }
    }

    // ── Clone support ──

    /// <summary>
    /// Create a copy with a different explosion system (for Clone scenarios).
    /// </summary>
    public BombResolution WithExplosionSystem(IExplosionSystem? explosionSystem)
    {
        return new BombResolution(_scoreSystem, _comboHandler, _effectRegistry, _cellEliminator,
            explosionSystem, _projectileSystem, _colorBombSessionManager, _lockScheduler, _explosionConfig);
    }

    /// <summary>
    /// Create a copy with a different projectile system (for Clone scenarios).
    /// </summary>
    public BombResolution WithProjectileSystem(IProjectileSystem? projectileSystem)
    {
        return new BombResolution(_scoreSystem, _comboHandler, _effectRegistry, _cellEliminator,
            _explosionSystem, projectileSystem, _colorBombSessionManager, _lockScheduler, _explosionConfig);
    }

    /// <summary>
    /// Create a copy with a different lock scheduler (for Clone scenarios).
    /// </summary>
    public BombResolution WithLockScheduler(LockScheduler? lockScheduler)
    {
        return new BombResolution(_scoreSystem, _comboHandler, _effectRegistry, _cellEliminator,
            _explosionSystem, _projectileSystem, _colorBombSessionManager, lockScheduler, _explosionConfig);
    }

    /// <summary>
    /// Create a copy with a different ColorBomb session manager (for Clone scenarios).
    /// </summary>
    public BombResolution WithColorBombSessionManager(IColorBombSessionManager? sessionManager)
    {
        return new BombResolution(_scoreSystem, _comboHandler, _effectRegistry, _cellEliminator,
            _explosionSystem, _projectileSystem, sessionManager, _lockScheduler, _explosionConfig);
    }
}
