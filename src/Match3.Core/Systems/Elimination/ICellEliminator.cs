using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Unified entry point for eliminating a single cell.
/// Ensures consistent Guard → Cover → Indestructible → Event → Objective → Mutate → Ground
/// across all destruction paths (match, bomb, projectile).
/// </summary>
public interface ICellEliminator
{
    /// <summary>
    /// Attempt to eliminate the tile at <paramref name="pos"/>.
    /// </summary>
    EliminateResult Eliminate(
        ref GameState state, Position pos, ElimSource reason,
        int tick, float simTime, IEventCollector events);
}
