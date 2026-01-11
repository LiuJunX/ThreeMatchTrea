using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface IGravitySystem
{
    /// <summary>
    /// Applies gravity to the board, moving tiles down into empty spaces.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <returns>A list of tile movements that occurred.</returns>
    List<TileMove> ApplyGravity(ref GameState state);

    /// <summary>
    /// Fills empty spaces at the top of the board with new random tiles.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <returns>A list of new tiles created and their movements.</returns>
    List<TileMove> Refill(ref GameState state);
}
