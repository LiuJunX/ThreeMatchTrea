using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Unified callback for chain reactions triggered by subsystems
/// (ExplosionSystem, ProjectileSystem, ColorBombSessionManager).
/// </summary>
public interface IChainReactionHandler
{
    /// <summary>
    /// A bomb was hit by an explosion wave. The tile has already been eliminated
    /// by CellEliminator — skip ConsumeBomb, go straight to activation.
    /// </summary>
    void HandleExplosionChain(ref GameState state, Position pos, Tile bombTile,
        int tick, float simTime, IEventCollector events);

    /// <summary>
    /// A bomb was hit by a projectile impact or ColorBomb batch activate.
    /// The tile is still on the grid — needs full ActivateBomb(isChainReaction: true).
    /// </summary>
    void HandleImpactChain(ref GameState state, Position pos,
        int tick, float simTime, IEventCollector events);
}
