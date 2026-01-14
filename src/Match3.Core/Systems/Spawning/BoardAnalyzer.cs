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
    private static readonly TileType[] Colors = new[]
    {
        TileType.Red, TileType.Green, TileType.Blue,
        TileType.Yellow, TileType.Purple, TileType.Orange
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
    public static int GetColorIndex(TileType type)
    {
        return type switch
        {
            TileType.Red => 0,
            TileType.Green => 1,
            TileType.Blue => 2,
            TileType.Yellow => 3,
            TileType.Purple => 4,
            TileType.Orange => 5,
            _ => -1
        };
    }

    /// <summary>
    /// Gets the tile type for a color index.
    /// </summary>
    public static TileType GetColorType(int index)
    {
        return index >= 0 && index < Colors.Length ? Colors[index] : TileType.None;
    }

    /// <summary>
    /// Simulates where a tile dropped from spawnX would land.
    /// Returns the Y position where the tile would settle.
    /// </summary>
    public static int SimulateDropTarget(ref GameState state, int spawnX)
    {
        for (int y = 0; y < state.Height; y++)
        {
            if (state.GetTile(spawnX, y).Type == TileType.None)
            {
                return y;
            }
        }
        return state.Height - 1;
    }

    /// <summary>
    /// Checks if placing a color at (x, y) would create an immediate match (3+ in a row).
    /// </summary>
    public static bool WouldCreateMatch(ref GameState state, int x, int y, TileType color)
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
    /// Checks if placing a color at (x, y) would create a "near match" (2 in a row with potential).
    /// This is useful for creating tension without immediate resolution.
    /// </summary>
    public static bool WouldCreateNearMatch(ref GameState state, int x, int y, TileType color)
    {
        // Check horizontal pairs
        int left = 0, right = 0;
        for (int dx = 1; x - dx >= 0 && state.GetType(x - dx, y) == color; dx++)
            left++;
        for (int dx = 1; x + dx < state.Width && state.GetType(x + dx, y) == color; dx++)
            right++;

        if (left + right >= 1) return true;

        // Check vertical pairs
        int up = 0, down = 0;
        for (int dy = 1; y - dy >= 0 && state.GetType(x, y - dy) == color; dy++)
            up++;
        for (int dy = 1; y + dy < state.Height && state.GetType(x, y + dy) == color; dy++)
            down++;

        return up + down >= 1;
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
                if (type == TileType.None) continue;

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
    public static TileType FindRarestColor(ref GameState state, int maxColors)
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
    public static TileType FindMostCommonColor(ref GameState state, int maxColors)
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
}
