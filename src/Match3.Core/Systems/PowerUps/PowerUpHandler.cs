using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

public class PowerUpHandler : IPowerUpHandler
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombComboHandler _comboHandler;
    private readonly BombEffectRegistry _effectRegistry;
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly IExplosionSystem? _explosionSystem;
    private readonly IProjectileSystem? _projectileSystem;
    private readonly IColorBombSessionManager? _colorBombSessionManager;

    public PowerUpHandler(IScoreSystem scoreSystem)
        : this(scoreSystem, new BombComboHandler(), BombEffectRegistry.CreateDefault(),
               new CoverSystem(), new GroundSystem())
    {
    }

    public PowerUpHandler(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        BombEffectRegistry effectRegistry,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        IExplosionSystem? explosionSystem = null,
        IProjectileSystem? projectileSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null)
    {
        _scoreSystem = scoreSystem;
        _comboHandler = comboHandler;
        _effectRegistry = effectRegistry;
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _explosionSystem = explosionSystem;
        _projectileSystem = projectileSystem;
        _colorBombSessionManager = colorBombSessionManager;
    }

    public void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points)
    {
        ProcessSpecialMove(ref state, p1, p2, 0, 0f, NullEventCollector.Instance, out points);
    }

    public void ProcessSpecialMove(
        ref GameState state,
        Position p1,
        Position p2,
        int tick,
        float simTime,
        IEventCollector events,
        out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Calculate score before modifying state (tiles might be cleared)
        points = _scoreSystem.CalculateSpecialMoveScore(t1.Type, t2.Type);

        // Use BombComboHandler to process combos
        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_comboHandler.TryApplyCombo(ref state, p1, p2, affected))
            {
                // Emit BombComboEvent before modifying state
                if (events.IsEnabled)
                {
                    events.Emit(new BombComboEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileIdA = t1.Id,
                        TileIdB = t2.Id,
                        BombTypeA = t1.Type,
                        BombTypeB = t2.Type,
                        PositionA = p1,
                        PositionB = p2,
                        AffectedPositions = new List<Position>(affected)
                    });
                }

                // Clear bomb attributes from combo participants to prevent double explosion
                // The combo effect already accounts for both bombs' effects
                ClearBombAttribute(ref state, p1);
                ClearBombAttribute(ref state, p2);

                // Double color bomb: use wave-based destruction from midpoint
                bool isDoubleColorBomb = t1.Type == ElementType.ColorBomb && t2.Type == ElementType.ColorBomb;
                if (isDoubleColorBomb && _explosionSystem != null)
                {
                    // Use p2 as origin (swap destination), slow wave for dramatic effect
                    _explosionSystem.CreateTargetedExplosion(ref state, p2, affected,
                        DoubleColorBombConstants.WipeInterval, DoubleColorBombConstants.WipeAcceleration);
                }
                else
                {
                    // NOTE: Without ExplosionSystem, tiles are destroyed instantly in one tick.
                    // The Choreographer still emits 4-phase wipe animation from BombComboEvent,
                    // causing a visual/logic desync. Production always has ExplosionSystem;
                    // this path exists only for backward-compatible test fallback.
                    ClearAffectedTiles(ref state, affected, tick, simTime, events);
                }

                // UFO + UFO: launch projectiles for remote targets
                // ClearBombAttribute preserved tile IDs (set to None), so Choreographer
                // can animate the existing tile visuals flying to their targets.
                if (t1.Type == ElementType.Ufo && t2.Type == ElementType.Ufo && _projectileSystem != null)
                {
                    LaunchUfoComboProjectiles(ref state, t1.Id, t2.Id, p1, p2, tick, simTime, events);
                }

                return;
            }
        }
        finally
        {
            Pools.Release(affected);
        }

        // If no special move happened, reset points
        points = 0;
    }

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
            ClearBombAttribute(ref state, p);
            _colorBombSessionManager.CreateSession(ref state, p, bombTileId, tick, simTime, events);
            return;
        }

        bool isUfo = t.Type.IsUfo();
        int ufoTileId = t.Id;

        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_effectRegistry.TryGetEffect(t.Type, out var effect))
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
                        TileId = t.Id,
                        Position = p,
                        BombType = t.Type,
                        AffectedPositions = new List<Position>(affected),
                        IsChainReaction = isChainReaction
                    });
                }

                // Clear bomb attribute to prevent re-activation
                ClearBombAttribute(ref state, p);

                if (_explosionSystem != null)
                {
                    // Rockets spread 2x faster than other bombs
                    bool isRocket = t.Type == ElementType.HorizontalRocket || t.Type == ElementType.VerticalRocket;
                    if (isRocket)
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected, 0.04f, 0.8f);
                    else if (t.Type.IsAreaBomb())
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected, 0.05f, 0.85f);
                    else
                        _explosionSystem.CreateTargetedExplosion(ref state, p, affected);
                }
                else
                {
                    // Fallback: instant destruction (backward compatible)
                    ClearAffectedTiles(ref state, affected, tick, simTime, events);

                    // Ensure the bomb itself is cleared
                    var currentT = state.GetTile(p.X, p.Y);
                    if (currentT.Type != ElementType.None)
                    {
                        if (events.IsEnabled)
                        {
                            events.Emit(new TileDestroyedEvent
                            {
                                Tick = tick,
                                SimulationTime = simTime,
                                TileId = currentT.Id,
                                GridPosition = p,
                                Type = currentT.Type,
                                Reason = DestroyReason.BombEffect
                            });
                        }

                        state.SetTile(p.X, p.Y, new Tile(0, ElementType.None, p.X, p.Y));
                        _groundSystem.OnTileDestroyed(ref state, p, tick, simTime, events);
                    }
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
                    SourceTileId = ufoTileId
                };
                _projectileSystem.Launch(projectile, tick, simTime, events);
            }
        }
    }

    public IPowerUpHandler WithExplosionSystem(IExplosionSystem? explosionSystem)
    {
        return new PowerUpHandler(_scoreSystem, _comboHandler, _effectRegistry, _coverSystem, _groundSystem, explosionSystem, _projectileSystem, _colorBombSessionManager);
    }

    public IPowerUpHandler WithProjectileSystem(IProjectileSystem? projectileSystem)
    {
        return new PowerUpHandler(_scoreSystem, _comboHandler, _effectRegistry, _coverSystem, _groundSystem, _explosionSystem, projectileSystem, _colorBombSessionManager);
    }

    /// <summary>
    /// Launch 3 UFO projectiles for UFO+UFO combo.
    /// First 2 reuse the original UFO tile visuals; 3rd spawns a new visual.
    /// </summary>
    private void LaunchUfoComboProjectiles(
        ref GameState state, int tileId1, int tileId2,
        Position p1, Position p2,
        int tick, float simTime, IEventCollector events)
    {
        // UFO 1: from p1 (reuses existing tile visual)
        var target1 = UfoEffect.PickRemoteTarget(in state, p1);
        if (target1.HasValue)
        {
            var proj = new UfoProjectile(
                _projectileSystem!.GenerateProjectileId(), p1, target1.Value)
            { SourceTileId = tileId1 };
            _projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 2: from p2 (reuses existing tile visual)
        var target2 = UfoEffect.PickRemoteTarget(in state, p2);
        if (target2.HasValue)
        {
            var proj = new UfoProjectile(
                _projectileSystem!.GenerateProjectileId(), p2, target2.Value)
            { SourceTileId = tileId2 };
            _projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 3: from p1 (spawns a new tile visual via Choreographer)
        var target3 = UfoEffect.PickRemoteTarget(in state, p1);
        if (target3.HasValue)
        {
            int syntheticTileId = state.NextTileId++;
            var proj = new UfoProjectile(
                _projectileSystem!.GenerateProjectileId(), p1, target3.Value)
            { SourceTileId = syntheticTileId, SpawnVisual = true };
            _projectileSystem.Launch(proj, tick, simTime, events);
        }
    }

    /// <summary>
    /// Clears the bomb attribute from a tile to prevent double explosion during combo processing.
    /// </summary>
    private static void ClearBombAttribute(ref GameState state, Position p)
    {
        var tile = state.GetTile(p.X, p.Y);
        if (tile.Type.IsBomb())
        {
            state.SetTile(p.X, p.Y, new Tile(tile.Id, ElementType.None, p.X, p.Y) { Position = tile.Position });
        }
    }

    /// <summary>
    /// Clears all affected tiles (supports chain explosions using queue to avoid recursion)
    /// </summary>
    private void ClearAffectedTiles(
        ref GameState state,
        HashSet<Position> affected,
        int tick,
        float simTime,
        IEventCollector events)
    {
        var queue = Pools.ObtainQueue<Position>();
        var chainEffect = Pools.ObtainHashSet<Position>();
        var processed = Pools.ObtainHashSet<Position>();
        try
        {
            // Initialize queue
            foreach (var pos in affected)
            {
                queue.Enqueue(pos);
            }

            // BFS process all tiles (including chain explosions)
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();

                if (pos.X < 0 || pos.X >= state.Width || pos.Y < 0 || pos.Y >= state.Height)
                    continue;

                if (processed.Contains(pos))
                    continue;

                processed.Add(pos);

                // Check cover layer first
                if (_coverSystem.IsTileProtected(in state, pos))
                {
                    // Damage the cover, tile is protected this round
                    _coverSystem.TryDamageCover(ref state, pos, tick, simTime, events);
                    continue;
                }

                var tile = state.GetTile(pos.X, pos.Y);

                // Already empty, skip
                if (tile.Type == ElementType.None)
                    continue;

                // If it's a bomb, trigger chain explosion
                if (tile.Type.IsBomb() && _effectRegistry.TryGetEffect(tile.Type, out var effect))
                {
                    chainEffect.Clear();
                    effect!.Apply(in state, pos, chainEffect);

                    // Add chain positions to queue
                    foreach (var chainPos in chainEffect)
                    {
                        if (!processed.Contains(chainPos))
                            queue.Enqueue(chainPos);
                    }
                }

                // Emit event
                if (events.IsEnabled)
                {
                    events.Emit(new TileDestroyedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = tile.Id,
                        GridPosition = pos,
                        Type = tile.Type,
                        Reason = DestroyReason.BombEffect
                    });
                }

                // Clear the tile
                state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));

                // Notify ground layer
                _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(processed);
            Pools.Release(chainEffect);
            Pools.Release(queue);
        }
    }
}
