using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Solves the optimal partition problem for overlapping shapes.
/// Maximizes total weight while ensuring no cell is used twice.
/// </summary>
public interface IPartitionSolver
{
    /// <summary>
    /// Find the optimal non-overlapping subset of shapes.
    /// </summary>
    /// <param name="candidates">All detected shapes.</param>
    /// <param name="component">Original component for validation.</param>
    /// <param name="bestIndices">Output list to fill with selected shape indices.</param>
    void FindOptimalPartition(
        List<DetectedShape> candidates,
        HashSet<Position> component,
        List<int> bestIndices);
}
