using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

public interface IExplosionSystem
{
    void CreateExplosion(ref GameState state, Position origin, int radius);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets, float waveInterval);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets, float waveInterval, float acceleration);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets, float waveInterval, float acceleration, float receiveLockDuration);

    /// <summary>
    /// Advances active explosions. Returns the number of chain reactions triggered this tick.
    /// When <paramref name="triggeredBombs"/> is provided, bombs eliminated by waves are collected
    /// into the list instead of being chain-reacted internally, allowing the orchestrator to route
    /// them through <see cref="IPowerUpHandler.ActivateChainBomb"/>.
    /// </summary>
    int Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector eventCollector,
        List<(Position Pos, Tile Tile)>? triggeredBombs = null);

    bool HasActiveExplosions { get; }
    void Reset();
}
