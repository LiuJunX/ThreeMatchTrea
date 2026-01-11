using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

/// <summary>
/// Responsible for analyzing connected components and generating match groups with appropriate bomb types.
/// Implements the "Global Optimal Partitioning" strategy.
/// </summary>
public interface IBombGenerator
{
    /// <summary>
    /// Analyzes a connected component of tiles and partitions it into one or more MatchGroups.
    /// Determines the best bomb types and spawn positions based on game rules.
    /// </summary>
    /// <param name="component">The set of connected positions of the same color.</param>
    /// <param name="foci">Optional priority positions (e.g., user swap/input positions) for bomb generation.</param>
    /// <returns>A list of valid MatchGroups. Returns empty list if no valid match is found.</returns>
    List<MatchGroup> Generate(HashSet<Position> component, IEnumerable<Position>? foci);
}
