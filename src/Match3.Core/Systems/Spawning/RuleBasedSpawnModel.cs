using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Phase 1: Rule-based spawn model.
/// Uses heuristics to control difficulty through spawn decisions.
/// </summary>
public class RuleBasedSpawnModel : ISpawnModel
{
    private static readonly TileType[] Colors = new[]
    {
        TileType.Red, TileType.Green, TileType.Blue,
        TileType.Yellow, TileType.Purple, TileType.Orange
    };

    private readonly IRandom? _rng;

    public RuleBasedSpawnModel()
    {
    }

    public RuleBasedSpawnModel(IRandom rng)
    {
        _rng = rng;
    }

    public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
    {
        int colorCount = Math.Min(state.TileTypesCount, Colors.Length);
        if (colorCount <= 0) return TileType.None;

        // Determine spawn strategy based on context
        var strategy = DetermineStrategy(context);

        return strategy switch
        {
            SpawnStrategy.Help => SpawnHelpful(ref state, spawnX, colorCount),
            SpawnStrategy.Neutral => SpawnNeutral(ref state, spawnX, colorCount),
            SpawnStrategy.Challenge => SpawnChallenging(ref state, spawnX, colorCount),
            SpawnStrategy.Balance => SpawnBalanced(ref state, spawnX, colorCount),
            _ => SpawnNeutral(ref state, spawnX, colorCount)
        };
    }

    private SpawnStrategy DetermineStrategy(in SpawnContext context)
    {
        // Mercy rule: help struggling players
        if (context.FailedAttempts >= 3)
            return SpawnStrategy.Help;

        // Last few moves: give a chance
        if (context.RemainingMoves <= 3 && context.GoalProgress < 0.9f)
            return SpawnStrategy.Help;

        // Player doing well, add challenge
        if (context.GoalProgress > 0.7f && context.RemainingMoves > 5)
            return SpawnStrategy.Challenge;

        // Use target difficulty to determine strategy
        if (context.TargetDifficulty < 0.3f)
            return SpawnStrategy.Help;
        if (context.TargetDifficulty > 0.7f)
            return SpawnStrategy.Challenge;

        // Keep board balanced for neutral difficulty
        return SpawnStrategy.Balance;
    }

    /// <summary>
    /// Spawn a tile that creates or enables a match.
    /// </summary>
    private TileType SpawnHelpful(ref GameState state, int spawnX, int colorCount)
    {
        Span<bool> wouldMatch = stackalloc bool[6];
        BoardAnalyzer.FindMatchingColors(ref state, spawnX, wouldMatch);

        // First, try to find a color that creates an immediate match
        for (int i = 0; i < colorCount; i++)
        {
            if (wouldMatch[i])
                return Colors[i];
        }

        // No immediate match possible, find colors that create near-matches
        int targetY = BoardAnalyzer.SimulateDropTarget(ref state, spawnX);
        for (int i = 0; i < colorCount; i++)
        {
            if (BoardAnalyzer.WouldCreateNearMatch(ref state, spawnX, targetY, Colors[i]))
                return Colors[i];
        }

        // Fallback: spawn a random color
        return SpawnRandom(ref state, colorCount);
    }

    /// <summary>
    /// Spawn a tile that avoids immediate matches.
    /// </summary>
    private TileType SpawnChallenging(ref GameState state, int spawnX, int colorCount)
    {
        Span<bool> wouldNotMatch = stackalloc bool[6];
        BoardAnalyzer.FindNonMatchingColors(ref state, spawnX, wouldNotMatch);

        // Find colors that don't create matches and are already common
        var commonColor = BoardAnalyzer.FindMostCommonColor(ref state, colorCount);
        int commonIndex = BoardAnalyzer.GetColorIndex(commonColor);

        // Prefer spawning common colors that don't match (creates clutter)
        if (commonIndex >= 0 && commonIndex < colorCount && wouldNotMatch[commonIndex])
            return commonColor;

        // Otherwise, any non-matching color
        for (int i = 0; i < colorCount; i++)
        {
            if (wouldNotMatch[i])
                return Colors[i];
        }

        // All colors would match - just pick random
        return SpawnRandom(ref state, colorCount);
    }

    /// <summary>
    /// Spawn a tile with balanced probability (no match avoidance).
    /// </summary>
    private TileType SpawnNeutral(ref GameState state, int spawnX, int colorCount)
    {
        return SpawnRandom(ref state, colorCount);
    }

    /// <summary>
    /// Spawn a tile that balances color distribution.
    /// </summary>
    private TileType SpawnBalanced(ref GameState state, int spawnX, int colorCount)
    {
        // Get color distribution
        Span<int> counts = stackalloc int[6];
        BoardAnalyzer.GetColorDistribution(ref state, counts);

        // Calculate weights inversely proportional to count (as integers)
        Span<int> weights = stackalloc int[6];
        int totalWeight = 0;

        for (int i = 0; i < colorCount; i++)
        {
            // Inverse weight: rarer colors get higher weight
            // Use integer math: weight = 100 / (count + 1)
            weights[i] = 100 / (counts[i] + 1);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0)
            return SpawnRandom(ref state, colorCount);

        // Weighted random selection using integers
        var rng = _rng ?? state.Random;
        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < colorCount; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return Colors[i];
        }

        return Colors[0];
    }

    private TileType SpawnRandom(ref GameState state, int colorCount)
    {
        var rng = _rng ?? state.Random;
        int idx = rng.Next(0, colorCount);
        return Colors[idx];
    }

    private enum SpawnStrategy
    {
        Help,       // Create matches to help player
        Neutral,    // Random, no preference
        Challenge,  // Avoid matches, increase difficulty
        Balance     // Balance color distribution
    }
}
