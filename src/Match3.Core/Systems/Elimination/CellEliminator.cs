using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;

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

    public CellEliminator(
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        ILevelObjectiveSystem? objectiveSystem = null)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _objectiveSystem = objectiveSystem;
    }

    /// <inheritdoc />
    public EliminateResult Eliminate(
        ref GameState state, Position pos, ElimSource reason,
        int tick, float simTime, IEventCollector events)
    {
        // Guard: empty cell
        var tile = state.GetTile(pos.X, pos.Y);
        if (tile.Type == ElementType.None)
            return EliminateResult.Blocked;

        // ColorBomb immunity: immune to collateral damage (bombs, chain reactions, etc.).
        // ConsumeBomb bypasses this — the bomb is being voluntarily activated by player tap/swap or combo.
        if (tile.Type == ElementType.ColorBomb && reason != ElimSource.ConsumeBomb)
            return EliminateResult.Immune(tile);

        // Cover: absorb hit
        if (_coverSystem.IsTileProtected(in state, pos))
        {
            _coverSystem.TryDamageCover(ref state, pos, tick, simTime, events);
            return EliminateResult.Absorbed(tile);
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
                Reason = reason,
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
