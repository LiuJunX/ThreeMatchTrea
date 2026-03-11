using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Projectiles;

namespace Match3.Core.Systems.PowerUps;

public interface IPowerUpHandler
{
    void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points);

    void ProcessSpecialMove(
        ref GameState state,
        Position p1,
        Position p2,
        int tick,
        float simTime,
        IEventCollector events,
        out int points);

    void ActivateBomb(ref GameState state, Position p);

    void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events,
        bool isChainReaction = false);

    /// <summary>
    /// Create a copy of this handler using a different explosion system (for Clone scenarios).
    /// </summary>
    IPowerUpHandler WithExplosionSystem(IExplosionSystem? explosionSystem);

    /// <summary>
    /// Create a copy of this handler using a different projectile system (for Clone scenarios).
    /// </summary>
    IPowerUpHandler WithProjectileSystem(IProjectileSystem? projectileSystem);

    /// <summary>
    /// Create a copy of this handler using a different lock scheduler (for Clone scenarios).
    /// </summary>
    IPowerUpHandler WithLockScheduler(LockScheduler? lockScheduler);
}
