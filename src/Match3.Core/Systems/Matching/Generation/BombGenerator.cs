using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Reusable comparer for DetectedShape that considers foci affinity.
/// Avoids Lambda closure allocation by holding state in the instance.
/// </summary>
internal sealed class DetectedShapeComparer : IComparer<DetectedShape>
{
    private HashSet<Position>? _fociSet;

    /// <summary>
    /// Set the foci set for comparison. Call before each sort operation.
    /// </summary>
    public void SetFoci(HashSet<Position> fociSet)
    {
        _fociSet = fociSet;
    }

    /// <summary>
    /// Clear foci reference after use.
    /// </summary>
    public void Clear()
    {
        _fociSet = null;
    }

    public int Compare(DetectedShape? a, DetectedShape? b)
    {
        if (a == null || b == null) return 0;

        // Primary: Weight (descending)
        int weightDiff = b.Weight.CompareTo(a.Weight);
        if (weightDiff != 0) return weightDiff;

        // Secondary: Affinity (Does it touch foci?)
        if (_fociSet != null && _fociSet.Count > 0)
        {
            bool aTouches = a.Cells!.Overlaps(_fociSet);
            bool bTouches = b.Cells!.Overlaps(_fociSet);

            if (aTouches && !bTouches) return -1;
            if (!aTouches && bTouches) return 1;
        }

        // Tertiary: Size (larger shapes preferred for same weight)
        return b.Cells!.Count.CompareTo(a.Cells!.Count);
    }
}

public class BombGenerator : IBombGenerator
{
    private readonly IShapeDetector _detector;
    private readonly IBombPlacementSelector _placementSelector;
    private readonly IPartitionSolver _partitionSolver;
    private readonly IScrapAbsorber _scrapAbsorber;
    private readonly IBombTypeSelector _typeSelector;

    // Reusable comparer to avoid Lambda closure allocations
    private readonly DetectedShapeComparer _shapeComparer = new();

    public BombGenerator()
        : this(
            new ShapeDetector(),
            new DefaultBombPlacementSelector(),
            new LayeredPartitionSolver(),
            new CollinearScrapAbsorber(),
            new DefaultBombTypeSelector())
    {
    }

    public BombGenerator(IShapeDetector detector, IBombPlacementSelector placementSelector)
        : this(
            detector,
            placementSelector,
            new LayeredPartitionSolver(),
            new CollinearScrapAbsorber(),
            new DefaultBombTypeSelector())
    {
    }

    public BombGenerator(
        IShapeDetector detector,
        IBombPlacementSelector placementSelector,
        IPartitionSolver partitionSolver,
        IScrapAbsorber scrapAbsorber,
        IBombTypeSelector typeSelector)
    {
        _detector = detector;
        _placementSelector = placementSelector;
        _partitionSolver = partitionSolver;
        _scrapAbsorber = scrapAbsorber;
        _typeSelector = typeSelector;
    }

    public List<MatchGroup> Generate(HashSet<Position> component, IEnumerable<Position>? foci = null, IRandom? random = null)
    {
        // 0. Trivial Case
        if (component.Count < 3)
        {
            return Pools.ObtainList<MatchGroup>();
        }

        // 1. Detect All Candidates
        var candidates = Pools.ObtainList<DetectedShape>();

        try
        {
            _detector.DetectAll(component, candidates);

            // Handle Simple Match (No bomb candidates)
            // Only create a simple match if there's at least one valid line (3+ in a row/column)
            if (candidates.Count == 0)
            {
                // Extract only positions that form valid lines (not the entire connected component)
                var linePositions = BombTypeMapper.ExtractValidLinePositions(component);
                if (linePositions.Count >= 3)
                {
                    return CreateSimpleMatchGroup(linePositions);
                }
                Pools.Release(linePositions);
                // No valid line shape - not a match (e.g., L-shape, diagonal)
                return Pools.ObtainList<MatchGroup>();
            }

            // 2. Sort Candidates (Weight DESC, then Affinity)
            SortCandidates(candidates, foci);

            // 3. Solve Optimal Partition
            var bestIndices = Pools.ObtainList<int>();
            try
            {
                _partitionSolver.FindOptimalPartition(candidates, component, bestIndices);

                // 4. Scrap Absorption & Result Construction
                _scrapAbsorber.AbsorbScraps(component, candidates, bestIndices);

                // 5. Finalize Results
                return ConstructResults(candidates, bestIndices, component, foci, random);
            }
            finally
            {
                Pools.Release(bestIndices);
            }
        }
        finally
        {
            // Release detected shapes and their inner sets
            foreach(var c in candidates)
            {
                if (c.Cells != null) Pools.Release(c.Cells);
                c.Cells = null;
                Pools.Release(c);
            }
            Pools.Release(candidates);
        }
    }

    private List<MatchGroup> CreateSimpleMatchGroup(HashSet<Position> linePositions)
    {
        var simpleGroup = Pools.Obtain<MatchGroup>();
        simpleGroup.Positions.Clear();
        foreach (var p in linePositions) simpleGroup.Positions.Add(p);
        simpleGroup.Shape = MatchShape.Simple3;
        simpleGroup.SpawnBombType = ElementType.None;
        simpleGroup.Type = ElementType.None; // Set by caller
        simpleGroup.BombOrigin = null;

        // Release the linePositions set (caller expects us to take ownership)
        Pools.Release(linePositions);

        var results = Pools.ObtainList<MatchGroup>();
        results.Add(simpleGroup);
        return results;
    }

    private void SortCandidates(List<DetectedShape> candidates, IEnumerable<Position>? foci)
    {
        var fociSet = Pools.ObtainHashSet<Position>();
        if (foci != null) foreach(var f in foci) fociSet.Add(f);

        try
        {
            // Use reusable comparer to avoid Lambda closure allocation
            _shapeComparer.SetFoci(fociSet);
            candidates.Sort(_shapeComparer);
        }
        finally
        {
            _shapeComparer.Clear();
            Pools.Release(fociSet);
        }
    }

    private List<MatchGroup> ConstructResults(
        List<DetectedShape> candidates,
        List<int> bestIndices,
        HashSet<Position> component,
        IEnumerable<Position>? foci,
        IRandom? random)
    {
        var results = Pools.ObtainList<MatchGroup>();
        var finalUsed = Pools.ObtainHashSet<Position>();
        var orphans = Pools.ObtainList<Position>();

        try
        {
            foreach (var idx in bestIndices)
            {
                var shape = candidates[idx];
                var group = Pools.Obtain<MatchGroup>();
                group.Positions.Clear();
                foreach (var p in shape.Cells!) group.Positions.Add(p);

                group.Shape = shape.Shape;
                group.SpawnBombType = _typeSelector.SelectBombType(shape);
                group.Type = ElementType.None; // Set by caller

                // Use placement selector for bomb origin
                group.BombOrigin = _placementSelector.SelectBombPosition(shape.Cells!, foci, random);

                results.Add(group);
            }

            // Handle Orphans (Islands not connected to any solution shape)
            foreach (var r in results)
                foreach (var p in r.Positions) finalUsed.Add(p);

            foreach (var p in component)
            {
                if (!finalUsed.Contains(p)) orphans.Add(p);
            }

            // Only create orphan group if they form valid lines (3+ consecutive)
            // Single stray cells or small groups are discarded
            if (orphans.Count >= 3)
            {
                var orphanSet = Pools.ObtainHashSet<Position>();
                foreach (var p in orphans) orphanSet.Add(p);

                var validOrphans = BombTypeMapper.ExtractValidLinePositions(orphanSet);
                Pools.Release(orphanSet);

                if (validOrphans.Count >= 3)
                {
                    var orphanGroup = Pools.Obtain<MatchGroup>();
                    orphanGroup.Positions.Clear();
                    foreach (var p in validOrphans) orphanGroup.Positions.Add(p);
                    orphanGroup.Shape = MatchShape.Simple3;
                    orphanGroup.SpawnBombType = ElementType.None;
                    orphanGroup.Type = ElementType.None;
                    orphanGroup.BombOrigin = null;
                    results.Add(orphanGroup);
                }
                Pools.Release(validOrphans);
            }

            return results;
        }
        finally
        {
            Pools.Release(finalUsed);
            Pools.Release(orphans);
        }
    }
}
