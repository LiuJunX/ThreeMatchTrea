using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Objectives;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// Default implementation of ICoverSystem.
/// Manages cover element damage and destruction.
/// </summary>
public class CoverSystem : ICoverSystem
{
    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public CoverSystem(ILevelObjectiveSystem? objectiveSystem = null)
    {
        _objectiveSystem = objectiveSystem;
    }
    /// <inheritdoc />
    public bool TryDamageCover(ref GameState state, Position position, long tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(position))
            return false;

        ref var cover = ref state.GetCover(position);

        if (cover.Type == CoverType.None)
            return false;

        // Damage the cover
        cover.Health--;

        if (cover.Health <= 0)
        {
            // Cover is destroyed
            var destroyedType = cover.Type;
            cover = Cover.Empty;

            if (events.IsEnabled)
            {
                events.Emit(new CoverDestroyedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    GridPosition = position,
                    Type = destroyedType
                });
            }

            // Track objective progress
            _objectiveSystem?.OnCoverDestroyed(ref state, destroyedType, tick, simTime, events);

            return true;
        }

        // Cover damaged but not destroyed
        return false;
    }

    /// <inheritdoc />
    public bool IsTileProtected(in GameState state, Position position)
    {
        if (!state.IsValid(position))
            return false;

        var cover = state.GetCover(position);
        return cover.Type != CoverType.None && cover.Health > 0;
    }

    /// <inheritdoc />
    public void SyncDynamicCovers(ref GameState state, Position from, Position to)
    {
        if (!state.IsValid(from) || !state.IsValid(to))
            return;

        var fromCover = state.GetCover(from);

        // Only sync if the cover is dynamic
        if (fromCover.Type != CoverType.None && fromCover.IsDynamic)
        {
            // Move cover from old position to new position
            state.SetCover(to, fromCover);
            state.SetCover(from, Cover.Empty);
        }
    }
}
