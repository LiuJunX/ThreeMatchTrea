using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Projectiles;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Factory methods for creating configured <see cref="PowerUpHandler"/> instances.
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
    public static PowerUpHandler CloneForSimulation(
        PowerUpHandler source,
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
}
