using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Absorbs scraps using collinearity rules.
/// Extracted from BombGenerator.AbsorbScraps.
/// </summary>
public sealed class CollinearScrapAbsorber : IScrapAbsorber
{
    /// <inheritdoc />
    public void AbsorbScraps(
        HashSet<Position> component,
        List<DetectedShape> candidates,
        List<int> selectedIndices)
    {
        var allUsed = Pools.ObtainHashSet<Position>();
        var scraps = Pools.ObtainList<Position>();
        var unassignedScraps = Pools.ObtainHashSet<Position>();
        var toRemove = Pools.ObtainList<Position>();
        var ownerMap = Pools.Obtain<Dictionary<Position, DetectedShape>>();
        var solutionShapes = Pools.ObtainList<DetectedShape>();

        try
        {
            foreach (var idx in selectedIndices) solutionShapes.Add(candidates[idx]);

            // Mark used cells
            foreach (var shape in solutionShapes)
            {
                foreach (var p in shape.Cells!) allUsed.Add(p);
            }

            // Identify scraps
            foreach (var p in component)
            {
                if (!allUsed.Contains(p)) scraps.Add(p);
            }

            // Assign scraps (BFS / Flood Fill)
            if (scraps.Count > 0 && solutionShapes.Count > 0)
            {
                foreach (var s in scraps) unassignedScraps.Add(s);

                // Map each position to its owner shape
                foreach (var shape in solutionShapes)
                {
                    foreach (var p in shape.Cells!) ownerMap[p] = shape;
                }

                bool changed = true;
                while (changed && unassignedScraps.Count > 0)
                {
                    changed = false;
                    toRemove.Clear();

                    foreach (var scrap in unassignedScraps)
                    {
                        DetectedShape? bestOwner = null;

                        // Check 4 neighbors
                        var n1 = new Position(scrap.X - 1, scrap.Y);
                        var n2 = new Position(scrap.X + 1, scrap.Y);
                        var n3 = new Position(scrap.X, scrap.Y - 1);
                        var n4 = new Position(scrap.X, scrap.Y + 1);

                        if (ownerMap.TryGetValue(n1, out var o1)) bestOwner = GetBestOwner(bestOwner, o1);
                        if (ownerMap.TryGetValue(n2, out var o2)) bestOwner = GetBestOwner(bestOwner, o2);
                        if (ownerMap.TryGetValue(n3, out var o3)) bestOwner = GetBestOwner(bestOwner, o3);
                        if (ownerMap.TryGetValue(n4, out var o4)) bestOwner = GetBestOwner(bestOwner, o4);

                        // Determine if scrap can be absorbed:
                        // - Cross/Square shapes: always absorb
                        // - Line shapes: only absorb if scrap is collinear (extends the line)
                        if (bestOwner != null && CanAbsorbScrap(bestOwner, scrap))
                        {
                            ownerMap[scrap] = bestOwner;
                            bestOwner.Cells!.Add(scrap); // Add directly to shape
                            toRemove.Add(scrap);
                            changed = true;
                        }
                    }

                    foreach (var p in toRemove) unassignedScraps.Remove(p);
                }
            }
        }
        finally
        {
            Pools.Release(allUsed);
            Pools.Release(scraps);
            Pools.Release(unassignedScraps);
            Pools.Release(toRemove);
            ownerMap.Clear();
            Pools.Release(ownerMap);
            Pools.Release(solutionShapes);
        }
    }

    private static DetectedShape? GetBestOwner(DetectedShape? currentBest, DetectedShape candidate)
    {
        if (currentBest == null) return candidate;
        return candidate.Weight > currentBest.Weight ? candidate : currentBest;
    }

    /// <summary>
    /// Determines if a specific scrap cell can be absorbed into a shape.
    /// Rules (有结构依据的吸收):
    /// - Simple3: never absorb
    /// - Line4/Line5: collinear + continuous (must be adjacent to existing line)
    /// - Square (2x2): orthogonal adjacent only (no diagonal), recursive via BFS
    /// - Cross (T/L/+): collinear with intersection point + continuous
    /// </summary>
    private static bool CanAbsorbScrap(DetectedShape shape, Position scrap)
    {
        // Square shapes: only absorb orthogonally adjacent (not diagonal)
        if (shape.Shape == MatchShape.Square)
        {
            return IsOrthogonallyAdjacent(shape.Cells!, scrap);
        }

        // Cross shapes: collinear with intersection point + continuous
        if (shape.Shape == MatchShape.Cross)
        {
            return CanAbsorbIntoCross(shape, scrap);
        }

        // Line4/Line5 shapes: collinear + continuous (adjacent to line)
        if (shape.Shape == MatchShape.Line4Horizontal || shape.Shape == MatchShape.Line4Vertical ||
            shape.Shape == MatchShape.Line5)
        {
            return IsCollinearAndAdjacent(shape.Cells!, scrap);
        }

        // Simple3 and unknown shapes - no absorption
        return false;
    }

    /// <summary>
    /// Check if scrap is orthogonally adjacent to any cell in the shape (not diagonal).
    /// Used for Square (2x2) absorption.
    /// </summary>
    private static bool IsOrthogonallyAdjacent(HashSet<Position> cells, Position scrap)
    {
        // Check 4 orthogonal neighbors
        var up = new Position(scrap.X, scrap.Y - 1);
        var down = new Position(scrap.X, scrap.Y + 1);
        var left = new Position(scrap.X - 1, scrap.Y);
        var right = new Position(scrap.X + 1, scrap.Y);

        return cells.Contains(up) || cells.Contains(down) ||
               cells.Contains(left) || cells.Contains(right);
    }

    /// <summary>
    /// Check if scrap can be absorbed into a Cross (T/L/+) shape.
    /// Rule: scrap must be collinear with intersection point AND the path must be continuous.
    /// Equivalent: rectangle formed by scrap and intersection must have all cells filled.
    /// </summary>
    private static bool CanAbsorbIntoCross(DetectedShape shape, Position scrap)
    {
        if (!shape.Intersection.HasValue || shape.Cells == null)
            return false;

        var intersection = shape.Intersection.Value;

        // Must be collinear with intersection (same row or same column)
        if (scrap.X != intersection.X && scrap.Y != intersection.Y)
            return false;

        // Check continuity: all cells between scrap and intersection must exist in shape
        // AND scrap must be adjacent to an existing cell
        return IsPathContinuousAndAdjacent(shape.Cells, scrap, intersection);
    }

    /// <summary>
    /// Check if scrap is collinear with the line AND adjacent to an existing cell.
    /// Used for Line4/Line5 absorption.
    /// </summary>
    private static bool IsCollinearAndAdjacent(HashSet<Position> cells, Position scrap)
    {
        if (cells.Count == 0) return false;

        // Determine line direction
        int? commonX = null;
        int? commonY = null;
        bool first = true;

        foreach (var p in cells)
        {
            if (first)
            {
                commonX = p.X;
                commonY = p.Y;
                first = false;
            }
            else
            {
                if (commonX.HasValue && p.X != commonX.Value) commonX = null;
                if (commonY.HasValue && p.Y != commonY.Value) commonY = null;
            }
        }

        // Horizontal line: scrap must have same Y AND be adjacent to existing cell
        if (commonY.HasValue)
        {
            if (scrap.Y != commonY.Value) return false;
            // Check if adjacent (left or right neighbor exists)
            return cells.Contains(new Position(scrap.X - 1, scrap.Y)) ||
                   cells.Contains(new Position(scrap.X + 1, scrap.Y));
        }

        // Vertical line: scrap must have same X AND be adjacent to existing cell
        if (commonX.HasValue)
        {
            if (scrap.X != commonX.Value) return false;
            // Check if adjacent (up or down neighbor exists)
            return cells.Contains(new Position(scrap.X, scrap.Y - 1)) ||
                   cells.Contains(new Position(scrap.X, scrap.Y + 1));
        }

        return false;
    }

    /// <summary>
    /// Check if path from scrap to intersection is continuous AND scrap is adjacent to an existing cell.
    /// </summary>
    private static bool IsPathContinuousAndAdjacent(HashSet<Position> cells, Position scrap, Position intersection)
    {
        // First check if scrap is adjacent to any existing cell
        bool isAdjacent = cells.Contains(new Position(scrap.X - 1, scrap.Y)) ||
                          cells.Contains(new Position(scrap.X + 1, scrap.Y)) ||
                          cells.Contains(new Position(scrap.X, scrap.Y - 1)) ||
                          cells.Contains(new Position(scrap.X, scrap.Y + 1));

        if (!isAdjacent) return false;

        // Check continuity: all cells between scrap and intersection must exist
        if (scrap.X == intersection.X)
        {
            // Same column - check vertical path
            int minY = System.Math.Min(scrap.Y, intersection.Y);
            int maxY = System.Math.Max(scrap.Y, intersection.Y);
            for (int y = minY; y <= maxY; y++)
            {
                var pos = new Position(scrap.X, y);
                if (pos != scrap && !cells.Contains(pos))
                    return false;
            }
            return true;
        }
        else if (scrap.Y == intersection.Y)
        {
            // Same row - check horizontal path
            int minX = System.Math.Min(scrap.X, intersection.X);
            int maxX = System.Math.Max(scrap.X, intersection.X);
            for (int x = minX; x <= maxX; x++)
            {
                var pos = new Position(x, scrap.Y);
                if (pos != scrap && !cells.Contains(pos))
                    return false;
            }
            return true;
        }

        return false;
    }
}
