using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Handles absorption of leftover cells into selected shapes.
/// </summary>
public interface IScrapAbsorber
{
    /// <summary>
    /// Absorb orphan cells into adjacent selected shapes.
    /// </summary>
    /// <param name="component">Original component.</param>
    /// <param name="candidates">All detected shapes.</param>
    /// <param name="selectedIndices">Indices of shapes from partition solution.</param>
    void AbsorbScraps(
        HashSet<Position> component,
        List<DetectedShape> candidates,
        List<int> selectedIndices);
}
