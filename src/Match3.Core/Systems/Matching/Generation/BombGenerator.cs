using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Matching.Generation;

public class BombGenerator : IBombGenerator
{
    private readonly IShapeDetector _detector;
    private readonly IBombPlacementSelector _placementSelector;
    private readonly IPartitionSolver _partitionSolver;
    private readonly IScrapAbsorber _scrapAbsorber;
    private readonly IBombTypeSelector _typeSelector;

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
                var linePositions = ExtractValidLinePositions(component);
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
        simpleGroup.SpawnBombType = BombType.None;
        simpleGroup.Type = TileType.None; // Set by caller
        simpleGroup.BombOrigin = null;

        // Release the linePositions set (caller expects us to take ownership)
        Pools.Release(linePositions);

        var results = Pools.ObtainList<MatchGroup>();
        results.Add(simpleGroup);
        return results;
    }

    /// <summary>
    /// Extract only positions that are part of valid lines (3+ consecutive in a row or column).
    /// This prevents stray tiles connected to a valid match from being incorrectly cleared.
    /// For example, in an L-shape like:
    ///   A A A
    ///   B C A
    /// Only the top 3 A's should be extracted, not the bottom-right A.
    /// </summary>
    private static HashSet<Position> ExtractValidLinePositions(HashSet<Position> component)
    {
        var result = Pools.ObtainHashSet<Position>();

        if (component.Count < 3) return result;

        // Get bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var p in component)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        // Find horizontal lines and add their positions
        for (int y = minY; y <= maxY; y++)
        {
            int startX = -1;
            int consecutive = 0;

            for (int x = minX; x <= maxX + 1; x++) // +1 to handle end of line
            {
                if (x <= maxX && component.Contains(new Position(x, y)))
                {
                    if (consecutive == 0) startX = x;
                    consecutive++;
                }
                else
                {
                    // End of a run - add if it was 3+
                    if (consecutive >= 3)
                    {
                        for (int i = startX; i < startX + consecutive; i++)
                        {
                            result.Add(new Position(i, y));
                        }
                    }
                    consecutive = 0;
                }
            }
        }

        // Find vertical lines and add their positions
        for (int x = minX; x <= maxX; x++)
        {
            int startY = -1;
            int consecutive = 0;

            for (int y = minY; y <= maxY + 1; y++) // +1 to handle end of line
            {
                if (y <= maxY && component.Contains(new Position(x, y)))
                {
                    if (consecutive == 0) startY = y;
                    consecutive++;
                }
                else
                {
                    // End of a run - add if it was 3+
                    if (consecutive >= 3)
                    {
                        for (int i = startY; i < startY + consecutive; i++)
                        {
                            result.Add(new Position(x, i));
                        }
                    }
                    consecutive = 0;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Check if the component contains at least one valid line (3+ consecutive in a row or column).
    /// This prevents L-shapes, diagonals, or scattered groups from being treated as matches.
    /// </summary>
    private static bool HasValidLine(HashSet<Position> component)
    {
        if (component.Count < 3) return false;

        // Get bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var p in component)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        // Check horizontal lines
        for (int y = minY; y <= maxY; y++)
        {
            int consecutive = 0;
            for (int x = minX; x <= maxX + 1; x++) // +1 to check end
            {
                if (component.Contains(new Position(x, y)))
                {
                    consecutive++;
                    if (consecutive >= 3) return true;
                }
                else
                {
                    consecutive = 0;
                }
            }
        }

        // Check vertical lines
        for (int x = minX; x <= maxX; x++)
        {
            int consecutive = 0;
            for (int y = minY; y <= maxY + 1; y++) // +1 to check end
            {
                if (component.Contains(new Position(x, y)))
                {
                    consecutive++;
                    if (consecutive >= 3) return true;
                }
                else
                {
                    consecutive = 0;
                }
            }
        }

        return false;
    }

    private void SortCandidates(List<DetectedShape> candidates, IEnumerable<Position>? foci)
    {
        var fociSet = Pools.ObtainHashSet<Position>();
        if (foci != null) foreach(var f in foci) fociSet.Add(f);

        try
        {
            candidates.Sort((a, b) =>
            {
                // Primary: Weight
                int weightDiff = b.Weight.CompareTo(a.Weight);
                if (weightDiff != 0) return weightDiff;

                // Secondary: Affinity (Does it touch foci?)
                bool aTouches = a.Cells!.Overlaps(fociSet);
                bool bTouches = b.Cells!.Overlaps(fociSet);

                if (aTouches && !bTouches) return -1;
                if (!aTouches && bTouches) return 1;

                // Tertiary: Size (larger shapes preferred for same weight)
                return b.Cells!.Count.CompareTo(a.Cells!.Count);
            });
        }
        finally
        {
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
                group.Type = TileType.None; // Set by caller

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

                var validOrphans = ExtractValidLinePositions(orphanSet);
                Pools.Release(orphanSet);

                if (validOrphans.Count >= 3)
                {
                    var orphanGroup = Pools.Obtain<MatchGroup>();
                    orphanGroup.Positions.Clear();
                    foreach (var p in validOrphans) orphanGroup.Positions.Add(p);
                    orphanGroup.Shape = MatchShape.Simple3;
                    orphanGroup.SpawnBombType = BombType.None;
                    orphanGroup.Type = TileType.None;
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
