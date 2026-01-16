using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// Default implementation of IGroundSystem.
/// Manages ground element damage and destruction.
/// </summary>
public class GroundSystem : IGroundSystem
{
    /// <inheritdoc />
    public void OnTileDestroyed(ref GameState state, Position position, long tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(position))
            return;

        ref var ground = ref state.GetGround(position);

        if (ground.Type == GroundType.None)
            return;

        // Damage the ground
        ground.Health--;

        if (ground.Health <= 0)
        {
            // Ground is destroyed
            var destroyedType = ground.Type;
            ground = Ground.Empty;

            if (events.IsEnabled)
            {
                events.Emit(new GroundDestroyedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    GridPosition = position,
                    Type = destroyedType
                });
            }
        }
    }
}
