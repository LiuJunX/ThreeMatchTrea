using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Provides geometry-based validation helpers for connected components.
/// Determines whether a set of positions contains valid match lines
/// (3+ consecutive tiles in a row or column) and extracts those positions.
/// </summary>
internal static class BombTypeMapper
{
    /// <summary>
    /// Check if the component contains at least one valid line (3+ consecutive in a row or column).
    /// This prevents L-shapes, diagonals, or scattered groups from being treated as matches.
    /// </summary>
    /// <param name="component">The set of positions forming a connected component.</param>
    /// <returns><c>true</c> if any row or column contains 3+ consecutive positions.</returns>
    internal static bool HasValidLine(HashSet<Position> component)
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

    /// <summary>
    /// Extract only positions that are part of valid lines (3+ consecutive in a row or column).
    /// This prevents stray tiles connected to a valid match from being incorrectly cleared.
    /// For example, in an L-shape like:
    ///   A A A
    ///   B C A
    /// Only the top 3 A's should be extracted, not the bottom-right A.
    /// </summary>
    /// <param name="component">The set of positions forming a connected component.</param>
    /// <returns>
    /// A pooled <see cref="HashSet{Position}"/> containing only positions that belong to
    /// valid lines. Caller is responsible for releasing via <see cref="Pools.Release{T}"/>.
    /// </returns>
    internal static HashSet<Position> ExtractValidLinePositions(HashSet<Position> component)
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
}
