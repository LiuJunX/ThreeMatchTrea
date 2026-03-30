using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

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
    public bool TryDamageCover(ref GameState state, Position position, int tick, float simTime, IEventCollector events)
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
                    Type = destroyedType,
                    IsGoal = _objectiveSystem != null && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Cover, (int)destroyedType)
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
            var toCover = state.GetCover(to);

            // Don't overwrite existing static covers (e.g. Bubble must not silently delete a Cage)
            if (toCover.Type != CoverType.None && !toCover.IsDynamic)
            {
                // Target has a static cover — drop the dynamic cover instead of overwriting
                state.SetCover(from, Cover.Empty);
                return;
            }

            // Move cover from old position to new position
            state.SetCover(to, fromCover);
            state.SetCover(from, Cover.Empty);
        }
    }

    /// <inheritdoc />
    public void NotifyBatchElimination(
        ref GameState state,
        ReadOnlySpan<EliminatedTileInfo> eliminated,
        int tick, float simTime, IEventCollector events)
    {
        var hit = Pools.ObtainHashSet<Position>();
        try
        {
            foreach (ref readonly var info in eliminated)
            {
                if (!IsAdjacentSource(info.Source, info.Tile))
                    continue;

                TryDamageAdjacentCover(ref state, new Position(info.Pos.X - 1, info.Pos.Y), hit, tick, simTime, events);
                TryDamageAdjacentCover(ref state, new Position(info.Pos.X + 1, info.Pos.Y), hit, tick, simTime, events);
                TryDamageAdjacentCover(ref state, new Position(info.Pos.X, info.Pos.Y - 1), hit, tick, simTime, events);
                TryDamageAdjacentCover(ref state, new Position(info.Pos.X, info.Pos.Y + 1), hit, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(hit);
        }
    }

    /// <summary>
    /// All elimination sources trigger adjacent cover reactions (Honey / Frost).
    /// Royal Match consistency: power-ups are always at least as effective as matching.
    /// </summary>
    private static bool IsAdjacentSource(ElimSource source, Tile tile) => true;

    private void TryDamageAdjacentCover(
        ref GameState state, Position neighbor,
        HashSet<Position> hit,
        int tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(neighbor.X, neighbor.Y))
            return;
        if (!hit.Add(neighbor))
            return; // dedup: already hit in this batch

        var cover = state.GetCover(neighbor);
        if (cover.Type == CoverType.None || cover.Health <= 0)
            return;
        if (!CoverRules.DamagedByAdjacent(cover.Type))
            return;

        TryDamageCover(ref state, neighbor, tick, simTime, events);
    }
}
