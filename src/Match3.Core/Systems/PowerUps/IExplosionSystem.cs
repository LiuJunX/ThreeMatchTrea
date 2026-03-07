using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Systems.PowerUps;

public interface IExplosionSystem
{
    void CreateExplosion(ref GameState state, Position origin, int radius);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets, float waveInterval);
    void CreateTargetedExplosion(ref GameState state, Position origin, System.Collections.Generic.IEnumerable<Position> targets, float waveInterval, float acceleration);
    
    void Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector eventCollector,
        System.Collections.Generic.List<Position> triggeredBombs);
        
    bool HasActiveExplosions { get; }
    void Reset();
}
