using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;

namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Default implementation of <see cref="ICellEliminator"/>.
/// Stateless (no mutable fields) — safe to share across Clone() boundaries.
/// </summary>
public sealed class CellEliminator : ICellEliminator
{
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly IObstacleSystem? _obstacleSystem;

    public CellEliminator(
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        ILevelObjectiveSystem? objectiveSystem = null,
        IObstacleSystem? obstacleSystem = null)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
        _obstacleSystem = obstacleSystem;
    }

    /// <inheritdoc />
    public EliminateResult Eliminate(
        ref GameState state, Position pos, ElimContext ctx,
        int tick, float simTime, IEventCollector events)
    {
        // Cover: absorb hit first (applies regardless of tile presence or immunity)
        if (_coverSystem.IsTileProtected(in state, pos))
        {
            _coverSystem.TryDamageCover(ref state, pos, tick, simTime, events);
            var tile2 = state.GetTile(pos.X, pos.Y);
            if (tile2.Type == ElementType.None) return EliminateResult.Blocked;
            return (tile2.Type == ElementType.ColorBomb && ctx.Source != ElimSource.ConsumeBomb)
                ? EliminateResult.Immune(tile2)
                : EliminateResult.Absorbed(tile2);
        }

        // Obstacle: intercepts hit (direct-hit path)
        if (_obstacleSystem != null)
        {
            var obsResult = _obstacleSystem.TryHit(ref state, pos, in ctx, tick, simTime, events);
            if (obsResult != ObstacleHitResult.NoObstacle)
            {
                return obsResult switch
                {
                    ObstacleHitResult.Damaged   => EliminateResult.ObstacleDamaged,
                    ObstacleHitResult.Destroyed => EliminateResult.ObstacleDestroyed,
                    _ => EliminateResult.Blocked
                };
            }
        }

        // No tile — bare ground can still be damaged
        var tile = state.GetTile(pos.X, pos.Y);
        if (tile.Type == ElementType.None)
        {
            var ground = state.GetGround(pos);
            if (ground.Type != GroundType.None)
            {
                _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
                return EliminateResult.GroundOnly;
            }
            return EliminateResult.Blocked;
        }

        // ColorBomb immunity: tile survives, but ground still takes damage
        if (tile.Type == ElementType.ColorBomb && ctx.Source != ElimSource.ConsumeBomb)
        {
            _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
            return EliminateResult.Immune(tile);
        }

        // Indestructible lock
        if (!state.CanDestroy(pos))
            return EliminateResult.Blocked;

        // Protected tile (freshly released by death effect) is immune
        if (tile.ProtectUntil > simTime)
            return EliminateResult.Blocked;

        // Tile elimination source restriction (e.g., power-up-only moving obstacles)
        if (!TileRules.CanEliminate(tile.Type, ctx.Source))
            return EliminateResult.Blocked;

        // Multi-stage tile: decrement Stage, emit damage event if still alive
        if (tile.Stage > 1)
        {
            tile.Stage--;
            state.SetTile(pos.X, pos.Y, tile);

            if (events.IsEnabled)
            {
                events.Emit(new TileDamagedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    TileId = tile.Id,
                    GridPosition = pos,
                    Type = tile.Type,
                    RemainingStage = tile.Stage,
                    Reason = ctx.Source
                });
            }

            // Ground still takes damage on each hit
            _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);

            return EliminateResult.Absorbed(tile);
        }

        // Event (with IsGoal)
        if (events.IsEnabled)
        {
            events.Emit(new TileDestroyedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                TileId = tile.Id,
                GridPosition = pos,
                Type = tile.Type,
                Reason = ctx.Source,
                IsGoal = _objectiveSystem != null
                    && _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Tile, (int)tile.Type)
            });
        }

        // Objective tracking
        _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, tick, simTime, events);

        // Mutate: clear tile
        var destroyedType = tile.Type;
        state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));

        // Tile death effect (Oyster → Pearl, Flowerpot → 3×3 Grass)
        ExecuteTileDeathEffect(ref state, pos, destroyedType, tick, simTime, events);

        // Ground notification
        _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);

        return EliminateResult.Eliminated(tile);
    }

    /// <summary>
    /// Execute type-specific death effects after a tile is destroyed.
    /// Mirrors <see cref="ObstacleSystem"/>.ExecuteDeathEffect for the tile layer.
    /// </summary>
    private static void ExecuteTileDeathEffect(
        ref GameState state, Position pos, ElementType type,
        int tick, float simTime, IEventCollector events)
    {
        switch (type)
        {
            case ElementType.Oyster:
                // Release Pearl at self position (same pattern as Cupboard → Plate)
                ObstacleSystem.ReleaseToSelf(ref state, pos, ElementType.Pearl,
                    simTime, tick, events);
                break;

            case ElementType.Flowerpot:
                // Spread 3×3 Grass around self position
                SpreadGround3x3(ref state, pos, GroundType.Grass, simTime, tick, events);
                break;
        }
    }

    /// <summary>
    /// Spread ground in a 3×3 area centered on the given position.
    /// Skips invalid cells, existing ground, and obstacle-occupied cells.
    /// </summary>
    private static void SpreadGround3x3(
        ref GameState state, Position center, GroundType groundType,
        float simTime, int tick, IEventCollector events)
    {
        float protectUntil = simTime + ObstacleSystem.GroundProtectDuration;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var target = new Position(center.X + dx, center.Y + dy);
                ObstacleSystem.SpreadGroundAt(ref state, target, groundType, protectUntil,
                    center, tick, simTime, events);
            }
        }
    }
}
