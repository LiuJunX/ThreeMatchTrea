using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Obstacles;

/// <summary>
/// Default implementation of <see cref="IObstacleSystem"/>.
/// Handles direct-hit damage, adjacent reactions, and global reactions.
/// Stateless (no mutable fields) — safe to share across Clone() boundaries.
/// </summary>
public sealed class ObstacleSystem : IObstacleSystem
{
    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public ObstacleSystem(ILevelObjectiveSystem? objectiveSystem = null)
    {
        _objectiveSystem = objectiveSystem;
    }

    /// <inheritdoc />
    public ObstacleHitResult TryHit(
        ref GameState state, Position pos, in ElimContext ctx,
        int tick, float simTime, IEventCollector events)
    {
        ref var obstacle = ref state.GetObstacle(pos);
        if (!obstacle.HasObstacle)
            return ObstacleHitResult.NoObstacle;

        if (!ObstacleRules.CanHit(in obstacle, in ctx))
            return ObstacleHitResult.Blocked;

        return ApplyDamage(ref state, pos, ref obstacle, tick, simTime, events);
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

                ProcessNeighborReactions(ref state, info.Pos, info.Tile.Type,
                    hit, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(hit);
        }
    }

    /// <inheritdoc />
    public void NotifyGlobalColorElimination(
        ref GameState state, ElementType color,
        int tick, float simTime, IEventCollector events)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                ref var obstacle = ref state.GetObstacle(x, y);
                if (obstacle.Type != ObstacleType.Curtain)
                    continue;

                var curtainColor = (ElementType)obstacle.State;
                if (curtainColor != color)
                    continue;

                ApplyDamage(ref state, new Position(x, y), ref obstacle, tick, simTime, events);
            }
        }
    }

    /// <summary>
    /// Determines whether an elimination source triggers adjacent obstacle reactions.
    /// Only Match and ColorBomb-related eliminations trigger adjacency.
    /// </summary>
    private static bool IsAdjacentSource(ElimSource source, Tile tile)
    {
        return source switch
        {
            ElimSource.Match     => true,
            ElimSource.ColorBomb => true,
            ElimSource.ConsumeBomb => tile.Type == ElementType.ColorBomb,
            _ => false
        };
    }

    private void ProcessNeighborReactions(
        ref GameState state, Position pos, ElementType triggerType,
        HashSet<Position> hit, int tick, float simTime, IEventCollector events)
    {
        TryReactAt(ref state, new Position(pos.X - 1, pos.Y), triggerType, hit, tick, simTime, events);
        TryReactAt(ref state, new Position(pos.X + 1, pos.Y), triggerType, hit, tick, simTime, events);
        TryReactAt(ref state, new Position(pos.X, pos.Y - 1), triggerType, hit, tick, simTime, events);
        TryReactAt(ref state, new Position(pos.X, pos.Y + 1), triggerType, hit, tick, simTime, events);
    }

    private void TryReactAt(
        ref GameState state, Position neighbor, ElementType triggerType,
        HashSet<Position> hit, int tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(neighbor.X, neighbor.Y))
            return;
        if (!hit.Add(neighbor))
            return; // dedup: already hit in this batch

        ref var obstacle = ref state.GetObstacle(neighbor);
        if (!obstacle.HasObstacle)
            return;
        if (!ObstacleRules.CanReactAdjacent(in obstacle, triggerType))
            return;

        ApplyDamage(ref state, neighbor, ref obstacle, tick, simTime, events);
    }

    private ObstacleHitResult ApplyDamage(
        ref GameState state, Position pos, ref Obstacle obstacle,
        int tick, float simTime, IEventCollector events)
    {
        var type = obstacle.Type;
        obstacle.Stage--;

        if (obstacle.Stage == 0)
        {
            // Destroyed
            if (events.IsEnabled)
            {
                events.Emit(new ObstacleDestroyedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    GridPosition = pos,
                    Type = type,
                    IsGoal = _objectiveSystem != null
                        && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Obstacle, (int)type)
                });
            }

            _objectiveSystem?.OnObstacleDestroyed(ref state, type, tick, simTime, events);
            state.SetObstacle(pos, Obstacle.Empty);

            // Death effect: obstacle-specific board writes
            ExecuteDeathEffect(ref state, pos, type, tick, simTime, events);

            return ObstacleHitResult.Destroyed;
        }

        // Damaged but survived
        if (events.IsEnabled)
        {
            events.Emit(new ObstacleDamagedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                GridPosition = pos,
                Type = type,
                RemainingStage = obstacle.Stage,
                IsGoal = _objectiveSystem != null
                    && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Obstacle, (int)type)
            });
        }

        return ObstacleHitResult.Damaged;
    }

    #region Death Effects

    /// <summary>Protection duration for ground spawned by death effects (seconds).</summary>
    internal const float GroundProtectDuration = 0.4f;

    /// <summary>Protection duration for tiles released by death effects (seconds).</summary>
    internal const float TileProtectDuration = 0.5f;

    private void ExecuteDeathEffect(
        ref GameState state, Position pos, ObstacleType type,
        int tick, float simTime, IEventCollector events)
    {
        switch (type)
        {
            case ObstacleType.Bush:
                SpreadGround(ref state, pos, GroundType.Grass, simTime, tick, events);
                break;
            // Flowerpot, Oyster, Cupboard reserved for future ObstacleType additions
        }
    }

    private void SpreadGround(
        ref GameState state, Position center, GroundType groundType,
        float simTime, int tick, IEventCollector events)
    {
        float protectUntil = simTime + GroundProtectDuration;
        SpreadGroundAt(ref state, new Position(center.X - 1, center.Y), groundType, protectUntil, center, tick, simTime, events);
        SpreadGroundAt(ref state, new Position(center.X + 1, center.Y), groundType, protectUntil, center, tick, simTime, events);
        SpreadGroundAt(ref state, new Position(center.X, center.Y - 1), groundType, protectUntil, center, tick, simTime, events);
        SpreadGroundAt(ref state, new Position(center.X, center.Y + 1), groundType, protectUntil, center, tick, simTime, events);
    }

    private void SpreadGroundAt(
        ref GameState state, Position target, GroundType groundType, float protectUntil,
        Position source, int tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(target.X, target.Y)) return;
        if (state.GetCell(target.X, target.Y) != CellKind.Slot) return;
        if (state.GetGround(target).HasGround) return;
        if (state.GetObstacle(target).HasObstacle) return;

        state.SetGround(target, new Ground(groundType, GroundRules.GetDefaultHealth(groundType), protectUntil));

        if (events.IsEnabled)
        {
            events.Emit(new GroundSpawnedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                GridPosition = target,
                Type = groundType,
                SourcePosition = source,
            });
        }
    }

    internal static void ReleaseToSelf(
        ref GameState state, Position pos, ElementType elementType, float simTime,
        int tick, IEventCollector events)
    {
        if (state.GetTile(pos).Type != ElementType.None) return;

        float protectUntil = simTime + TileProtectDuration;
        var tile = new Tile(state.NextTileId++, elementType, pos.X, pos.Y)
        {
            ProtectUntil = protectUntil
        };
        state.SetTile(pos, tile);

        if (events.IsEnabled)
        {
            events.Emit(new TileSpawnedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                TileId = tile.Id,
                GridPosition = pos,
                Type = elementType,
                SpawnPosition = new Vector2(pos.X, pos.Y),
            });
        }
    }

    #endregion
}
