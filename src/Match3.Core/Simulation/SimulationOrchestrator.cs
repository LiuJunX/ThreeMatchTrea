using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Simulation;

/// <summary>
/// Default implementation of ISimulationOrchestrator.
/// Coordinates subsystems for simulation updates.
/// </summary>
public sealed class SimulationOrchestrator : ISimulationOrchestrator
{
    private readonly IPhysicsSimulation _physics;
    private readonly IRefillSystem _refill;
    private readonly IProjectileSystem _projectileSystem;
    private readonly IExplosionSystem _explosionSystem;
    private readonly SimulationMatchHandler _matchHandler;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly IColorBombSessionManager? _colorBombSessionManager;
    private readonly IChainReactionHandler _chainReactionHandler;

    public SimulationOrchestrator(
        IPhysicsSimulation physics,
        IRefillSystem refill,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IPowerUpHandler powerUpHandler,
        IProjectileSystem? projectileSystem = null,
        IExplosionSystem? explosionSystem = null,
        ILevelObjectiveSystem? objectiveSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null,
        LockScheduler? lockScheduler = null,
        ChoreographyConfig? choreographyConfig = null,
        ICellEliminator? cellEliminator = null,
        IChainReactionHandler? chainReactionHandler = null)
    {
        _physics = physics;
        _refill = refill;
        _projectileSystem = projectileSystem ?? new ProjectileSystem();
        _explosionSystem = explosionSystem ?? new ExplosionSystem();
        _objectiveSystem = objectiveSystem;
        _colorBombSessionManager = colorBombSessionManager;
        _chainReactionHandler = chainReactionHandler ?? new ChainReactionHandler(powerUpHandler);
        _matchHandler = new SimulationMatchHandler(matchFinder, matchProcessor, cellEliminator, objectiveSystem, lockScheduler, choreographyConfig);
    }

    /// <inheritdoc />
    public void UpdateRefill(ref GameState state)
    {
        _refill.Update(ref state);
    }

    /// <inheritdoc />
    public void UpdatePhysics(ref GameState state, float deltaTime)
    {
        _physics.Update(ref state, deltaTime);
    }

    /// <inheritdoc />
    public int UpdateProjectiles(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events)
    {
        var affectedPositions = _projectileSystem.Update(
            ref state,
            deltaTime,
            tick,
            simTime,
            events);

        int count = affectedPositions.Count;

        if (count > 0)
        {
            var triggeredBombs = Pools.ObtainList<Position>();
            try
            {
                _matchHandler.ProcessProjectileImpacts(ref state, affectedPositions, tick, simTime, events, triggeredBombs);

                foreach (var pos in triggeredBombs)
                    _chainReactionHandler.HandleImpactChain(ref state, pos, tick, simTime, events);
            }
            finally
            {
                Pools.Release(triggeredBombs);
            }
        }

        Pools.Release(affectedPositions);
        return count;
    }

    /// <inheritdoc />
    public int UpdateExplosions(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events)
    {
        var triggeredBombs = Pools.ObtainList<(Position, Tile)>();
        try
        {
            int count = _explosionSystem.Update(ref state, deltaTime, tick, simTime, events, triggeredBombs);

            foreach (var (pos, tile) in triggeredBombs)
                _chainReactionHandler.HandleExplosionChain(ref state, pos, tile, tick, simTime, events);

            return count + triggeredBombs.Count;
        }
        finally
        {
            Pools.Release(triggeredBombs);
        }
    }

    /// <inheritdoc />
    public int ProcessMatches(ref GameState state, int tick, float simTime, IEventCollector events, Position[]? foci = null)
    {
        return _matchHandler.ProcessStableMatches(ref state, tick, simTime, events, foci);
    }

    /// <inheritdoc />
    public bool IsPhysicsStable(in GameState state)
    {
        return _physics.IsStable(in state);
    }

    /// <inheritdoc />
    public bool HasPendingMatches(in GameState state)
    {
        return _matchHandler.HasPendingMatches(in state);
    }

    /// <inheritdoc />
    public bool HasActiveProjectiles => _projectileSystem.HasActiveProjectiles;

    /// <inheritdoc />
    public bool HasActiveExplosions => _explosionSystem.HasActiveExplosions;

    /// <summary>True if any ColorBomb session is still active.</summary>
    public bool HasActiveColorBombSessions =>
        _colorBombSessionManager != null && _colorBombSessionManager.HasActiveSessions;

    /// <summary>
    /// Update ColorBomb sessions (beam timing, re-scan, batch destruction / batch activation).
    /// Combo sessions output triggered bomb positions that are activated here.
    /// </summary>
    public void UpdateColorBombSessions(ref GameState state, float deltaTime, int tick, float simTime, IEventCollector events)
    {
        if (_colorBombSessionManager == null) return;

        var triggeredBombs = Pools.ObtainList<Position>();
        try
        {
            _colorBombSessionManager.Update(ref state, deltaTime, tick, simTime, events, triggeredBombs);

            foreach (var pos in triggeredBombs)
                _chainReactionHandler.HandleImpactChain(ref state, pos, tick, simTime, events);
        }
        finally
        {
            Pools.Release(triggeredBombs);
        }
    }

    /// <summary>
    /// Clears all active subsystem state (projectiles, explosions, color bomb sessions).
    /// Used by undo to ensure a clean restore to a prior board state.
    /// </summary>
    public void ClearActiveState()
    {
        _projectileSystem.Clear();
        _explosionSystem.Reset();
        _colorBombSessionManager?.Reset();
    }

    /// <summary>
    /// Gets the projectile system for direct access.
    /// </summary>
    public IProjectileSystem ProjectileSystem => _projectileSystem;

    /// <summary>
    /// Gets the explosion system for direct access.
    /// </summary>
    public IExplosionSystem ExplosionSystem => _explosionSystem;
}
