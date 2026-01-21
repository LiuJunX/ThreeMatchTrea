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

        Console.WriteLine($"[Spawn] colorCount={colorCount}, TileTypesCount={state.TileTypesCount}, strategy={strategy}");
        Console.WriteLine($"[Spawn] Context: FailedAttempts={context.FailedAttempts}, RemainingMoves={context.RemainingMoves}, GoalProgress={context.GoalProgress:F2}, TargetDifficulty={context.TargetDifficulty:F2}");

        var result = strategy switch
        {
            SpawnStrategy.Help => SpawnHelpful(ref state, spawnX, colorCount),
            SpawnStrategy.Neutral => SpawnNeutral(ref state, spawnX, colorCount),
            SpawnStrategy.Challenge => SpawnChallenging(ref state, spawnX, colorCount),
            SpawnStrategy.Balance => SpawnBalanced(ref state, spawnX, colorCount),
            _ => SpawnNeutral(ref state, spawnX, colorCount)
        };

        Console.WriteLine($"[Spawn] Result: {result}");
        return result;
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

        Console.WriteLine($"[SpawnHelpful] wouldMatch: R={wouldMatch[0]}, G={wouldMatch[1]}, B={wouldMatch[2]}, Y={wouldMatch[3]}, P={wouldMatch[4]}, O={wouldMatch[5]}");

        // Collect all colors that create immediate matches
        Span<int> matchingIndices = stackalloc int[6];
        int matchCount = 0;
        for (int i = 0; i < colorCount; i++)
        {
            if (wouldMatch[i])
                matchingIndices[matchCount++] = i;
        }

        if (matchCount > 0)
        {
            // Randomly select from matching colors
            var rng = _rng ?? state.Random;
            int selected = matchingIndices[rng.Next(0, matchCount)];
            Console.WriteLine($"[SpawnHelpful] Returning immediate match: {Colors[selected]} (from {matchCount} options)");
            return Colors[selected];
        }

        // No immediate match possible, collect colors that create near-matches
        int targetY = BoardAnalyzer.SimulateDropTarget(ref state, spawnX);
        Span<int> nearMatchIndices = stackalloc int[6];
        int nearMatchCount = 0;
        for (int i = 0; i < colorCount; i++)
        {
            if (BoardAnalyzer.WouldCreateNearMatch(ref state, spawnX, targetY, Colors[i]))
                nearMatchIndices[nearMatchCount++] = i;
        }

        if (nearMatchCount > 0)
        {
            // Randomly select from near-match colors
            var rng = _rng ?? state.Random;
            int selected = nearMatchIndices[rng.Next(0, nearMatchCount)];
            Console.WriteLine($"[SpawnHelpful] Returning near-match: {Colors[selected]} (from {nearMatchCount} options)");
            return Colors[selected];
        }

        // Fallback: spawn a random color
        Console.WriteLine($"[SpawnHelpful] Fallback to random");
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

        Console.WriteLine($"[SpawnChallenging] commonColor={commonColor}, commonIndex={commonIndex}, colorCount={colorCount}");
        Console.WriteLine($"[SpawnChallenging] wouldNotMatch: R={wouldNotMatch[0]}, G={wouldNotMatch[1]}, B={wouldNotMatch[2]}, Y={wouldNotMatch[3]}, P={wouldNotMatch[4]}, O={wouldNotMatch[5]}");

        // Prefer spawning common colors that don't match (creates clutter)
        if (commonIndex >= 0 && commonIndex < colorCount && wouldNotMatch[commonIndex])
        {
            Console.WriteLine($"[SpawnChallenging] Returning common color: {commonColor}");
            return commonColor;
        }

        // Collect all non-matching colors
        Span<int> nonMatchingIndices = stackalloc int[6];
        int nonMatchCount = 0;
        for (int i = 0; i < colorCount; i++)
        {
            if (wouldNotMatch[i])
                nonMatchingIndices[nonMatchCount++] = i;
        }

        if (nonMatchCount > 0)
        {
            // Randomly select from non-matching colors
            var rng = _rng ?? state.Random;
            int selected = nonMatchingIndices[rng.Next(0, nonMatchCount)];
            Console.WriteLine($"[SpawnChallenging] Returning non-matching: {Colors[selected]} (from {nonMatchCount} options)");
            return Colors[selected];
        }

        // All colors would match - just pick random
        Console.WriteLine($"[SpawnChallenging] Fallback to random");
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

        Console.WriteLine($"[SpawnBalanced] Color counts: R={counts[0]}, G={counts[1]}, B={counts[2]}, Y={counts[3]}, P={counts[4]}, O={counts[5]}");

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

        Console.WriteLine($"[SpawnBalanced] Weights: R={weights[0]}, G={weights[1]}, B={weights[2]}, Y={weights[3]}, P={weights[4]}, O={weights[5]}, total={totalWeight}");

        if (totalWeight <= 0)
        {
            Console.WriteLine($"[SpawnBalanced] totalWeight=0, fallback to random");
            return SpawnRandom(ref state, colorCount);
        }

        // Weighted random selection using integers
        var rng = _rng ?? state.Random;
        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        Console.WriteLine($"[SpawnBalanced] roll={roll}");

        for (int i = 0; i < colorCount; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                Console.WriteLine($"[SpawnBalanced] Selected {Colors[i]} (cumulative={cumulative})");
                return Colors[i];
            }
        }

        Console.WriteLine($"[SpawnBalanced] Fallback to Colors[0]");
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
