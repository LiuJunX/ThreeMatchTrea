using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Projectiles;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Factory methods for creating configured <see cref="BombResolution"/> instances.
/// Extracts builder/wiring concerns from <see cref="IPowerUpHandler"/> so the interface
/// stays focused on domain operations.
/// </summary>
public static class PowerUpHandlerFactory
{
    /// <summary>
    /// Create a clone of <paramref name="source"/> with all subsystem dependencies replaced.
    /// Used by <see cref="Simulation.SimulationEngine.Clone"/> to produce an independent
    /// handler for parallel simulation branches (AI / DryRun).
    /// </summary>
    public static BombResolution CloneForSimulation(
        BombResolution source,
        IExplosionSystem explosionSystem,
        IProjectileSystem projectileSystem,
        LockScheduler lockScheduler,
        IColorBombSessionManager colorBombSessionManager)
    {
        return source
            .WithExplosionSystem(explosionSystem)
            .WithProjectileSystem(projectileSystem)
            .WithLockScheduler(lockScheduler)
            .WithColorBombSessionManager(colorBombSessionManager);
    }

    /// <summary>
    /// Clone with SimulationContext — replaces CellEliminator, BombEffectRegistry,
    /// LockScheduler from context while preserving source's ScoreSystem and ComboHandler.
    /// </summary>
    public static BombResolution CloneForSimulation(
        BombResolution source,
        SimulationContext context,
        IExplosionSystem explosionSystem,
        IProjectileSystem projectileSystem,
        IColorBombSessionManager colorBombSessionManager)
    {
        return source
            .WithExplosionSystem(explosionSystem)
            .WithProjectileSystem(projectileSystem)
            .WithLockScheduler(context.LockScheduler)
            .WithColorBombSessionManager(colorBombSessionManager);
    }
}
