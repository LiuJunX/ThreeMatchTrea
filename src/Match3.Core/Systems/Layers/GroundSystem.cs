using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Objectives;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// Default implementation of IGroundSystem.
/// Manages ground element damage and destruction.
/// </summary>
public class GroundSystem : IGroundSystem
{
    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public GroundSystem(ILevelObjectiveSystem? objectiveSystem = null)
    {
        _objectiveSystem = objectiveSystem;
    }
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

            // Track objective progress
            _objectiveSystem?.OnGroundDestroyed(ref state, destroyedType, tick, simTime, events);
        }
    }
}
