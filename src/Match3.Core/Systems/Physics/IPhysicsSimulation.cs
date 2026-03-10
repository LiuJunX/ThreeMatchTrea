using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

public interface IPhysicsSimulation
{
    void Update(ref GameState state, float deltaTime);
    bool IsStable(in GameState state);

    /// <summary>
    /// Create an independent clone with a new random source.
    /// Required for parallel simulation — implementations with mutable frame buffers
    /// or IRandom fields MUST return a fresh instance; stateless implementations may return <c>this</c>.
    /// </summary>
    IPhysicsSimulation CloneForSimulation(IRandom newRandom);
}
