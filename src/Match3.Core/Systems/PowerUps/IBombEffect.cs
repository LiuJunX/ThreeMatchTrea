using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Defines the behavior of a specific bomb type when it explodes.
/// </summary>
public interface IBombEffect
{
    /// <summary>
    /// The type of bomb this effect handles.
    /// </summary>
    ElementType Type { get; }

    /// <summary>
    /// Calculates which tiles are affected by the explosion at the given origin.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <param name="origin">The position where the bomb is exploding.</param>
    /// <param name="affectedTiles">The set to populate with affected positions.</param>
    void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles);
}
