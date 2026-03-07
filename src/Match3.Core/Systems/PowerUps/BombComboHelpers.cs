using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Helper methods for bomb combo effects.
/// Extracted from BombComboHandler to reduce class size.
/// </summary>
internal static class BombComboHelpers
{
    /// <summary>
    /// Apply a small cross pattern (5 positions).
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
    /// Apply an area effect (square pattern).
    /// </summary>
    public static void ApplyArea(in GameState state, Position center, int radius, HashSet<Position> affected)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = center.X + dx;
                int y = center.Y + dy;
                if (x >= 0 && x < state.Width && y >= 0 && y < state.Height)
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
    /// Find the most frequent color on the board.
    /// </summary>
    public static ElementType FindMostFrequentColor(ref GameState state)
    {
        var counts = Pools.Obtain<Dictionary<ElementType, int>>();
        try
        {
            for (int i = 0; i < state.Grid.Length; i++)
            {
                var t = state.Grid[i];
                if (t.Type != ElementType.None && t.Type != ElementType.Universal)
                {
                    // Use TryGetValue to minimize lookups
                    if (counts.TryGetValue(t.Type, out int existingCount))
                    {
                        counts[t.Type] = existingCount + 1;
                    }
                    else
                    {
                        counts[t.Type] = 1;
                    }
                }
            }

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
        finally
        {
            counts.Clear();
            Pools.Release(counts);
        }
    }

    /// <summary>
    /// Get the effective bomb type for a tile (handles Rainbow tiles).
    /// </summary>
    public static BombType GetEffectiveBombType(Tile tile)
    {
        if (tile.Type == ElementType.Universal || tile.Bomb == BombType.Color)
            return BombType.Color;
        return tile.Bomb;
    }

    /// <summary>
    /// Check if the bomb type is a rocket (horizontal or vertical).
    /// </summary>
    public static bool IsRocket(BombType type)
    {
        return type == BombType.Horizontal || type == BombType.Vertical;
    }

    /// <summary>
    /// Check if this is a color bomb with a normal tile combination.
    /// </summary>
    public static bool IsColorBombWithNormalTile(Tile t1, Tile t2)
    {
        bool t1IsColorBomb = t1.Type == ElementType.Universal || t1.Bomb == BombType.Color;
        bool t2IsColorBomb = t2.Type == ElementType.Universal || t2.Bomb == BombType.Color;
        bool t1IsNormal = !t1IsColorBomb && t1.Bomb == BombType.None && t1.Type != ElementType.None;
        bool t2IsNormal = !t2IsColorBomb && t2.Bomb == BombType.None && t2.Type != ElementType.None;

        return (t1IsColorBomb && t2IsNormal) || (t2IsColorBomb && t1IsNormal);
    }
}
