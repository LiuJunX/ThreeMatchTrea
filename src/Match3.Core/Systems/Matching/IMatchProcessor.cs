using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Matching;

public interface IMatchProcessor
{
    /// <summary>
    /// Processes a list of match groups, removing tiles, generating bombs, and calculating scores.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="groups">The list of match groups to process.</param>
    /// <returns>The total score earned from this processing step.</returns>
    int ProcessMatches(ref GameState state, List<MatchGroup> groups);

    /// <summary>
    /// Processes a list of match groups with event emission support.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="groups">The list of match groups to process.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for emitting events.</param>
    /// <returns>The total score earned from this processing step.</returns>
    int ProcessMatches(
        ref GameState state,
        List<MatchGroup> groups,
        long tick,
        float simTime,
        IEventCollector events);
}
