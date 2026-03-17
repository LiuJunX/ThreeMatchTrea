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
        state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));

        // Ground notification
        _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);

        return EliminateResult.Eliminated(tile);
    }
}
