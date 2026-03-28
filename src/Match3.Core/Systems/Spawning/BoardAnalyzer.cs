using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Analyzes board state for spawn decision making.
/// Provides color distribution, match potential, and drop simulation.
/// </summary>
public static class BoardAnalyzer
{
    public static readonly ElementType[] Colors = new[]
    {
        ElementType.Item1, ElementType.Item2, ElementType.Item3,
        ElementType.Item4, ElementType.Item5, ElementType.Item6
    };

    /// <summary>
    /// Counts the distribution of each color on the board.
    /// </summary>
    public static void GetColorDistribution(ref GameState state, Span<int> counts)
    {
        counts.Clear();

        for (int i = 0; i < state.Grid.Length; i++)
        {
            var type = state.Grid[i].Type;
            int colorIndex = GetColorIndex(type);
            if (colorIndex >= 0 && colorIndex < counts.Length)
            {
                counts[colorIndex]++;
            }
        }
    }

    /// <summary>
    /// Gets the color index (0-5) for a tile type.
    /// </summary>
    public static int GetColorIndex(ElementType type)
    {
        return type switch
        {
            ElementType.Item1 => 0,
            ElementType.Item2 => 1,
            ElementType.Item3 => 2,
            ElementType.Item4 => 3,
            ElementType.Item5 => 4,
            ElementType.Item6 => 5,
            _ => -1
        };
    }

    /// <summary>
    /// Gets the tile type for a color index.
    /// </summary>
    public static ElementType GetColorType(int index)
    {
        return index >= 0 && index < Colors.Length ? Colors[index] : ElementType.None;
    }

    /// <summary>
    /// Simulates where a tile dropped from spawnX would land.
    /// Returns the Y position where the tile would settle.
    /// </summary>
    public static int SimulateDropTarget(ref GameState state, int spawnX)
    {
        // Scan bottom-to-top: find the lowest empty, non-void, non-obstacle cell.
        // This approximates where a tile dropped from the top would land after gravity.
        for (int y = state.Height - 1; y >= 0; y--)
        {
            if (state.IsHole(spawnX, y)) continue;
            if (state.HasObstacle(spawnX, y)) continue;
            if (state.GetTile(spawnX, y).Type == ElementType.None)
                return y;
        }
        return 0;
    }

    /// <summary>
    /// Checks if placing a color at (x, y) would create an immediate match (3+ in a row).
    /// </summary>
    public static bool WouldCreateMatch(ref GameState state, int x, int y, ElementType color)
    {
        // Check horizontal
        int hCount = 1;
        // Left
        for (int dx = 1; x - dx >= 0 && state.GetType(x - dx, y) == color; dx++)
            hCount++;
        // Right
        for (int dx = 1; x + dx < state.Width && state.GetType(x + dx, y) == color; dx++)
            hCount++;

        if (hCount >= 3) return true;

        // Check vertical
        int vCount = 1;
        // Up
        for (int dy = 1; y - dy >= 0 && state.GetType(x, y - dy) == color; dy++)
            vCount++;
        // Down
        for (int dy = 1; y + dy < state.Height && state.GetType(x, y + dy) == color; dy++)
            vCount++;

        return vCount >= 3;
    }

    /// <summary>
    /// Checks if placing a color at (x, y) would create a "near match" (2 in a row).
    /// A near match means placing this tile creates a pair that needs only 1 more to complete.
    /// This is useful for creating tension without immediate resolution.
    /// </summary>
    public static bool WouldCreateNearMatch(ref GameState state, int x, int y, ElementType color)
    {
        // Check horizontal: need at least 1 adjacent same-color tile
        int left = 0, right = 0;
        for (int dx = 1; x - dx >= 0 && state.GetType(x - dx, y) == color; dx++)
            left++;
        for (int dx = 1; x + dx < state.Width && state.GetType(x + dx, y) == color; dx++)
            right++;

        // left + right >= 1 means we'd form at least a pair (2 tiles total including this one)
        // But we don't want 3+ (that's a match, not near-match)
        int hTotal = left + right + 1;
        if (hTotal == 2) return true;  // Exactly a pair

        // Check vertical
        int up = 0, down = 0;
        for (int dy = 1; y - dy >= 0 && state.GetType(x, y - dy) == color; dy++)
            up++;
        for (int dy = 1; y + dy < state.Height && state.GetType(x, y + dy) == color; dy++)
            down++;

        int vTotal = up + down + 1;
        return vTotal == 2;  // Exactly a pair
    }

    /// <summary>
    /// Finds colors that would create a match if dropped at spawnX.
    /// </summary>
    public static void FindMatchingColors(ref GameState state, int spawnX, Span<bool> wouldMatch)
    {
        int targetY = SimulateDropTarget(ref state, spawnX);

        for (int i = 0; i < Colors.Length && i < wouldMatch.Length; i++)
        {
            wouldMatch[i] = WouldCreateMatch(ref state, spawnX, targetY, Colors[i]);
        }
    }

    /// <summary>
    /// Finds colors that would NOT create a match if dropped at spawnX.
    /// </summary>
    public static void FindNonMatchingColors(ref GameState state, int spawnX, Span<bool> wouldNotMatch)
    {
        int targetY = SimulateDropTarget(ref state, spawnX);

        for (int i = 0; i < Colors.Length && i < wouldNotMatch.Length; i++)
        {
            wouldNotMatch[i] = !WouldCreateMatch(ref state, spawnX, targetY, Colors[i]);
        }
    }

    /// <summary>
    /// Calculates the "match potential" score for the current board.
    /// Higher scores mean more possible matches available.
    /// </summary>
    public static int CalculateMatchPotential(ref GameState state)
    {
        int potential = 0;

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = state.GetType(x, y);
                if (type == ElementType.None) continue;

                // Check right neighbor
                if (x + 1 < state.Width && state.GetType(x + 1, y) == type)
                    potential++;

                // Check bottom neighbor
                if (y + 1 < state.Height && state.GetType(x, y + 1) == type)
                    potential++;
            }
        }

        return potential;
    }

    /// <summary>
    /// Finds the rarest color on the board.
    /// </summary>
    public static ElementType FindRarestColor(ref GameState state, int maxColors)
    {
        Span<int> counts = stackalloc int[6];
        GetColorDistribution(ref state, counts);

        int minCount = int.MaxValue;
        int minIndex = 0;

        for (int i = 0; i < maxColors && i < 6; i++)
        {
            if (counts[i] < minCount)
            {
                minCount = counts[i];
                minIndex = i;
            }
        }

        return GetColorType(minIndex);
    }

    /// <summary>
    /// Finds the most common color on the board.
    /// </summary>
    public static ElementType FindMostCommonColor(ref GameState state, int maxColors)
    {
        Span<int> counts = stackalloc int[6];
        GetColorDistribution(ref state, counts);

        int maxCount = -1;
        int maxIndex = 0;

        for (int i = 0; i < maxColors && i < 6; i++)
        {
            if (counts[i] > maxCount)
            {
                maxCount = counts[i];
                maxIndex = i;
            }
        }

        return GetColorType(maxIndex);
    }

    /// <summary>
    /// Counts how many tiles of the given type are currently on the board.
    /// </summary>
    public static int CountElementOnBoard(ref GameState state, ElementType type)
    {
        int count = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Type == type)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Counts how many obstacles of the given type are currently on the board.
    /// </summary>
    public static int CountObstacleOnBoard(ref GameState state, ObstacleType type)
    {
        int count = 0;
        for (int i = 0; i < state.ObstacleLayer.Length; i++)
        {
            if (state.ObstacleLayer[i].Type == type)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns the color of the topmost non-empty tile in the column.
    /// </summary>
    public static ElementType GetColumnTopColor(ref GameState state, int x)
    {
        for (int y = 0; y < state.Height; y++)
        {
            var type = state.GetType(x, y);
            if (type != ElementType.None)
                return type;
        }
        return ElementType.None;
    }
}
