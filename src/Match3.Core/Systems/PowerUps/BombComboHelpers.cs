using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Shared helper methods for bomb and power-up effects.
/// Consolidates area-fill, color-distribution, and grid-candidate logic
/// used by BombComboHandler, individual IBombEffect implementations,
/// and BoardHealthAnalyzer.
/// </summary>
internal static class BombComboHelpers
{
    /// <summary>
    /// Apply a small cross pattern (up to 5 positions: center + 4 orthogonal neighbors).
    /// Positions outside the board are silently clipped.
    /// </summary>
    public static void ApplySmallCross(in GameState state, Position center, HashSet<Position> affected)
    {
        affected.Add(center);
        if (center.X > 0) affected.Add(new Position(center.X - 1, center.Y));
        if (center.X < state.Width - 1) affected.Add(new Position(center.X + 1, center.Y));
        if (center.Y > 0) affected.Add(new Position(center.X, center.Y - 1));
        if (center.Y < state.Height - 1) affected.Add(new Position(center.X, center.Y + 1));
    }

    /// <summary>
    /// Apply an area effect (square pattern) centered at <paramref name="center"/>
    /// with the given <paramref name="radius"/>. Positions outside the board are clipped.
    /// </summary>
    public static void ApplyArea(in GameState state, Position center, int radius, HashSet<Position> affected)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = center.X + dx;
                int y = center.Y + dy;
                if (state.IsValid(x, y))
                {
                    affected.Add(new Position(x, y));
                }
            }
        }
    }

    /// <summary>
    /// Get a random target position that is not excluded and not already affected.
    /// </summary>
    public static Position? GetRandomTarget(ref GameState state, Position exclude, HashSet<Position> alreadyAffected)
    {
        var candidates = Pools.ObtainList<Position>();
        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var pos = new Position(x, y);
                    if (pos.X == exclude.X && pos.Y == exclude.Y) continue;
                    if (alreadyAffected.Contains(pos)) continue;
                    if (state.GetType(x, y) != ElementType.None)
                    {
                        candidates.Add(pos);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int idx = state.Random.Next(0, candidates.Count);
                return candidates[idx];
            }
            return null;
        }
        finally
        {
            Pools.Release(candidates);
        }
    }

    /// <summary>
    /// Count the distribution of element types on the board into a pooled dictionary.
    /// Only types for which <paramref name="filter"/> returns <c>true</c> are counted.
    /// The caller receives a pooled dictionary that <b>must</b> be cleared and released
    /// via <see cref="Pools.Release{T}"/> after use.
    /// </summary>
    public static Dictionary<ElementType, int> CountTypeDistribution(
        in GameState state,
        Func<ElementType, bool> filter)
    {
        var counts = Pools.Obtain<Dictionary<ElementType, int>>();

        for (int i = 0; i < state.Grid.Length; i++)
        {
            var t = state.Grid[i].Type;
            if (!filter(t)) continue;

            if (counts.TryGetValue(t, out int existing))
            {
                counts[t] = existing + 1;
            }
            else
            {
                counts[t] = 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Find the most frequent element type on the board among types accepted by
    /// <paramref name="filter"/>. Returns <see cref="ElementType.None"/> when the
    /// board contains no qualifying tiles.
    /// </summary>
    public static ElementType FindMostFrequentType(
        in GameState state,
        Func<ElementType, bool> filter)
    {
        var counts = CountTypeDistribution(in state, filter);
        try
        {
            return MaxKey(counts);
        }
        finally
        {
            counts.Clear();
            Pools.Release(counts);
        }
    }

    /// <summary>
    /// Find the most frequent matchable color (Item1-Item6) on the board.
    /// </summary>
    public static ElementType FindMostFrequentColor(ref GameState state)
    {
        return FindMostFrequentType(in state, static t => t.IsColor());
    }

    /// <summary>
    /// Collect all board positions whose tile type equals <paramref name="targetType"/>
    /// into <paramref name="affected"/>.
    /// </summary>
    public static void CollectPositionsOfType(
        in GameState state,
        ElementType targetType,
        HashSet<Position> affected)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetType(x, y) == targetType)
                {
                    affected.Add(new Position(x, y));
                }
            }
        }
    }

    /// <summary>
    /// Check if this is a color bomb with a normal tile combination.
    /// </summary>
    public static bool IsColorBombWithNormalTile(Tile t1, Tile t2)
    {
        bool t1IsColorBomb = t1.Type.IsColorBomb();
        bool t2IsColorBomb = t2.Type.IsColorBomb();
        bool t1IsNormal = t1.Type.IsColor();
        bool t2IsNormal = t2.Type.IsColor();

        return (t1IsColorBomb && t2IsNormal) || (t2IsColorBomb && t1IsNormal);
    }

    /// <summary>
    /// Return the key with the highest value, or <see cref="ElementType.None"/> if empty.
    /// </summary>
    private static ElementType MaxKey(Dictionary<ElementType, int> counts)
    {
        ElementType maxType = ElementType.None;
        int maxCount = -1;
        foreach (var kvp in counts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                maxType = kvp.Key;
            }
        }
        return maxType;
    }
}
