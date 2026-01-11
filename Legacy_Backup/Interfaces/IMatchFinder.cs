using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface IMatchFinder
{
    /// <summary>
    /// Finds all connected groups of matching tiles in the current state.
    /// </summary>
    /// <param name="state">The current game state (read-only).</param>
    /// <param name="foci">Optional positions to prioritize when determining bomb origin.</param>
    /// <returns>A list of detected match groups.</returns>
    List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null);

    /// <summary>
    /// Checks if there are any matches in the current state.
    /// Used for checking stability or possible moves.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <returns>True if at least one match exists.</returns>
    bool HasMatches(in GameState state);

    /// <summary>
    /// Checks if a specific position is part of any match.
    /// Optimized for checking move validity without scanning the whole board.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <param name="p">The position to check.</param>
    /// <returns>True if the position is part of a match.</returns>
    bool HasMatchAt(in GameState state, Position p);
}
