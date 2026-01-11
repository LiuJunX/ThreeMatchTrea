using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface IMatchProcessor
{
    /// <summary>
    /// Processes a list of match groups, removing tiles, generating bombs, and calculating scores.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="groups">The list of match groups to process.</param>
    /// <returns>The total score earned from this processing step.</returns>
    int ProcessMatches(ref GameState state, List<MatchGroup> groups);
}
