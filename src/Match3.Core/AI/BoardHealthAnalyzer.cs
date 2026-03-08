using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.AI;

/// <summary>
/// Analyzes board health metrics for AI decision making.
/// Extracted from AIService to reduce class size.
/// </summary>
internal sealed class BoardHealthAnalyzer
{
    /// <summary>
    /// Analyze the health of the current board state.
    /// </summary>
    public BoardHealth Analyze(in GameState state)
    {
        int bombCount = 0;
        int isolatedCount = 0;

        // Use object pool to avoid allocation
        var typeCounts = Pools.Obtain<Dictionary<ElementType, int>>();
        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var tile = state.GetTile(x, y);
                    if (tile.Type == ElementType.None) continue;

                    if (tile.Type.IsBomb())
                        bombCount++;

                    // Use TryGetValue to minimize lookups (single lookup on hit, two on miss)
                    if (typeCounts.TryGetValue(tile.Type, out int existingCount))
                    {
                        typeCounts[tile.Type] = existingCount + 1;
                    }
                    else
                    {
                        typeCounts[tile.Type] = 1;
                    }

                    // Check if isolated (no adjacent same-type)
                    if (IsIsolated(state, x, y, tile.Type))
                        isolatedCount++;
                }
            }

            float variance = CalculateVariance(typeCounts);

            return new BoardHealth
            {
                ExistingBombs = bombCount,
                TypeDistributionVariance = variance,
                IsolatedTiles = isolatedCount,
                ClusterCount = 0, // Would need flood fill for accurate count
                AverageClusterSize = 0
            };
        }
        finally
        {
            typeCounts.Clear();
            Pools.Release(typeCounts);
        }
    }

    private static bool IsIsolated(in GameState state, int x, int y, ElementType type)
    {
        ReadOnlySpan<(int dx, int dy)> directions = stackalloc (int, int)[]
        {
            (-1, 0), (1, 0), (0, -1), (0, 1)
        };

        foreach (var (dx, dy) in directions)
        {
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && nx < state.Width && ny >= 0 && ny < state.Height)
            {
                var neighbor = state.GetTile(nx, ny);
                if (neighbor.Type == type)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static float CalculateVariance(Dictionary<ElementType, int> typeCounts)
    {
        if (typeCounts.Count == 0) return 0;

        float mean = 0;
        foreach (var count in typeCounts.Values)
            mean += count;
        mean /= typeCounts.Count;

        float variance = 0;
        foreach (var count in typeCounts.Values)
        {
            float diff = count - mean;
            variance += diff * diff;
        }
        variance /= typeCounts.Count;

        return variance;
    }
}

/// <summary>
/// Calculates difficulty scores and categories.
/// Extracted from AIService to reduce class size.
/// </summary>
internal sealed class DifficultyCalculator
{
    /// <summary>
    /// Calculate difficulty score based on move analysis.
    /// </summary>
    public float CalculateScore(int moveCount, float avgScore, long maxScore, float avgCascade)
    {
        float score = 50f; // Base difficulty

        // Fewer moves = harder
        if (moveCount == 0)
            return 100f;
        else if (moveCount < 3)
            score += 30f;
        else if (moveCount < 5)
            score += 15f;
        else if (moveCount > 10)
            score -= 15f;

        // Lower average score = harder
        if (avgScore < 50)
            score += 20f;
        else if (avgScore < 100)
            score += 10f;
        else if (avgScore > 300)
            score -= 20f;

        // Lower cascade potential = harder
        if (avgCascade < 1)
            score += 10f;
        else if (avgCascade > 2)
            score -= 10f;

        return Math.Clamp(score, 0f, 100f);
    }

    /// <summary>
    /// Categorize difficulty based on move count and score.
    /// </summary>
    public DifficultyCategory Categorize(int moveCount, float difficultyScore)
    {
        if (moveCount == 0)
            return DifficultyCategory.Deadlock;

        return difficultyScore switch
        {
            >= 80 => DifficultyCategory.VeryHard,
            >= 60 => DifficultyCategory.Hard,
            >= 40 => DifficultyCategory.Medium,
            >= 20 => DifficultyCategory.Easy,
            _ => DifficultyCategory.VeryEasy
        };
    }
}
