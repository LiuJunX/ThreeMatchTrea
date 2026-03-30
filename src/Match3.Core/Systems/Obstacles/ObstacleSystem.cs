using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Physics;
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
    private readonly LockScheduler? _lockScheduler;

    /// <summary>Receive lock duration after obstacle destruction (seconds).</summary>
    private const float DestroyReceiveLockDuration = 0.15f;

    public ObstacleSystem(ILevelObjectiveSystem? objectiveSystem = null, LockScheduler? lockScheduler = null)
    {
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
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

        if (obstacle.Type == ObstacleType.PotionBottle)
            return DamagePotionBottle(ref state, pos, ref obstacle, ElementType.None, tick, simTime, events);

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
            // All elimination sources trigger adjacent reactions (Royal Match consistency:
            // power-ups are always at least as effective as matching).
            foreach (ref readonly var info in eliminated)
            {
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

        if (obstacle.Type == ObstacleType.PotionBottle)
            DamagePotionBottle(ref state, neighbor, ref obstacle, triggerType, tick, simTime, events);
        else if (ObstacleRules.IsGenerator(obstacle.Type))
            ActivateGenerator(ref state, neighbor, ref obstacle, tick, simTime, events);
        else
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

            // Receive lock: prevent tiles from filling this cell during death animation
            _lockScheduler?.Acquire(ref state, pos, CellLockType.Receive, DestroyReceiveLockDuration);

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

    #region PotionBottle

    /// <summary>
    /// PotionBottle-specific damage: clear one sub-bottle bit, recalc Stage via popcount.
    /// If all sub-bottles cleared, destroy the obstacle.
    /// </summary>
    private ObstacleHitResult DamagePotionBottle(
        ref GameState state, Position pos, ref Obstacle obstacle,
        ElementType triggerType, int tick, float simTime, IEventCollector events)
    {
        // Safety: should not be called with empty state, but guard anyway
        if (obstacle.State == 0)
            return ObstacleHitResult.Blocked;

        // Determine which sub-bottle to break
        int bitToClear;
        if (triggerType.IsColor())
        {
            bitToClear = (int)triggerType - 1; // exact color match
        }
        else
        {
            // ColorBomb wildcard or power-up direct hit → break lowest remaining sub-bottle
            bitToClear = TrailingZeroCount(obstacle.State);
        }

        // Clear the bit and recalculate Stage
        obstacle.State = (byte)(obstacle.State & ~(1 << bitToClear));
        obstacle.Stage = (byte)PopCount(obstacle.State);

        bool destroyed = obstacle.Stage == 0;
        bool isGoal = _objectiveSystem != null
            && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Obstacle, (int)ObstacleType.PotionBottle);

        if (destroyed)
        {
            if (events.IsEnabled)
            {
                events.Emit(new ObstacleDestroyedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    GridPosition = pos,
                    Type = ObstacleType.PotionBottle,
                    IsGoal = isGoal
                });
            }

            _objectiveSystem?.OnObstacleDestroyed(ref state, ObstacleType.PotionBottle, tick, simTime, events);
            state.SetObstacle(pos, Obstacle.Empty);

            // Receive lock: prevent tiles from filling this cell during death animation
            _lockScheduler?.Acquire(ref state, pos, CellLockType.Receive, DestroyReceiveLockDuration);

            // No death effect for PotionBottle

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
                Type = ObstacleType.PotionBottle,
                RemainingStage = obstacle.Stage,
                NewState = obstacle.State,
                IsGoal = isGoal
            });
        }

        return ObstacleHitResult.Damaged;
    }

    #endregion

    #region Generator Activation

    private void ActivateGenerator(
        ref GameState state, Position pos, ref Obstacle obstacle,
        int tick, float simTime, IEventCollector events)
    {
        if (obstacle.Type == ObstacleType.MagicHat)
        {
            ActivateMagicHat(ref state, pos, ref obstacle, tick, simTime, events);
            return;
        }

        var productType = obstacle.Type switch
        {
            ObstacleType.Mailbox => ElementType.Envelope,
            _ => ElementType.None
        };

        if (productType == ElementType.None)
            return;

        SpawnProduct(ref state, pos, ref obstacle, productType, tick, simTime, events);
    }

    /// <summary>
    /// MagicHat accumulation logic: State increments each activation,
    /// spawns Diamond when State reaches 3, then resets.
    /// </summary>
    private void ActivateMagicHat(
        ref GameState state, Position pos, ref Obstacle obstacle,
        int tick, float simTime, IEventCollector events)
    {
        const int threshold = 3;

        // Accumulate only if below threshold
        if (obstacle.State < threshold)
        {
            obstacle.State++;

            // Progress feedback event (reuse ObstacleDamagedEvent — Presenter reads as progress)
            if (events.IsEnabled)
            {
                events.Emit(new ObstacleDamagedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    GridPosition = pos,
                    Type = ObstacleType.MagicHat,
                    RemainingStage = obstacle.State, // carries accumulation count (1/2/3)
                    IsGoal = false
                });
            }
        }

        // Try to spawn when threshold reached
        if (obstacle.State >= threshold)
        {
            if (!SpawnProduct(ref state, pos, ref obstacle, ElementType.Diamond, tick, simTime, events))
                return; // no room — State stays ≥3, retry next activation

            obstacle.State = 0;
        }
    }

    /// <summary>
    /// Shared product spawning logic for all generator types.
    /// Returns false if no empty adjacent slot is available.
    /// </summary>
    private bool SpawnProduct(
        ref GameState state, Position pos, ref Obstacle obstacle,
        ElementType productType, int tick, float simTime, IEventCollector events)
    {
        var slot = FindEmptyAdjacentSlot(in state, pos);
        if (slot == null)
            return false; // no room — skip silently

        var target = slot.Value;
        float protectUntil = simTime + TileProtectDuration;
        var tile = new Tile(state.NextTileId++, productType, target.X, target.Y)
        {
            ProtectUntil = protectUntil
        };
        state.SetTile(target, tile);

        if (events.IsEnabled)
        {
            events.Emit(new GeneratorActivatedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                GridPosition = pos,
                ObstacleType = obstacle.Type,
                ProductType = productType,
                ProductPosition = target
            });

            events.Emit(new TileSpawnedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                TileId = tile.Id,
                GridPosition = target,
                Type = productType,
                SpawnPosition = new Vector2(pos.X, pos.Y), // fly out from generator
            });
        }

        return true;
    }

    /// <summary>
    /// Find an empty adjacent cell (up, right, down, left) suitable for product placement.
    /// </summary>
    internal static Position? FindEmptyAdjacentSlot(in GameState state, Position center)
    {
        ReadOnlySpan<Position> neighbors = stackalloc Position[]
        {
            new(center.X, center.Y - 1), // up
            new(center.X + 1, center.Y), // right
            new(center.X, center.Y + 1), // down
            new(center.X - 1, center.Y), // left
        };

        foreach (var n in neighbors)
        {
            if (!state.IsValid(n.X, n.Y)) continue;
            if (state.GetCell(n.X, n.Y) != CellKind.Slot) continue;
            if (state.GetTile(n).Type != ElementType.None) continue;
            if (state.GetObstacle(n).HasObstacle) continue;
            return n;
        }

        return null;
    }

    #endregion

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
            case ObstacleType.Cupboard:
                ReleaseToSelf(ref state, pos, ElementType.Plate, simTime, tick, events);
                break;
            // Flowerpot/Oyster are tile-layer moving obstacles — see CellEliminator.ExecuteTileDeathEffect
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

    internal static void SpreadGroundAt(
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

    #region Bit Helpers (netstandard2.1 compat)

    private static int PopCount(uint value)
    {
        int count = 0;
        while (value != 0) { count++; value &= value - 1; }
        return count;
    }

    private static int TrailingZeroCount(uint value)
    {
        if (value == 0) return 32;
        int count = 0;
        while ((value & 1) == 0) { count++; value >>= 1; }
        return count;
    }

    #endregion
}
