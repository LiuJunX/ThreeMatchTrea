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
    private static readonly ElementType[] Colors = new[]
    {
        ElementType.Item1, ElementType.Item2, ElementType.Item3,
        ElementType.Item4, ElementType.Item5, ElementType.Item6
    };

    private readonly IRandom? _rng;

    public RuleBasedSpawnModel()
    {
    }

    public RuleBasedSpawnModel(IRandom rng)
    {
        _rng = rng;
    }

    /// <summary>
    /// Threshold: a color is "dominant" when it exceeds double its fair share.
    /// For 6 colors fair=16.7%, threshold=33.3%.
    /// </summary>
    private const int DominanceMultiplier = 2;

    public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
    {
        int colorCount = Math.Min(state.TileTypesCount, Colors.Length);
        if (colorCount <= 0) return ElementType.None;

        // Diversity guard: when any color dominates, enforce safe spawn to avoid cascade loops
        if (colorCount > 1 && IsDominant(ref state, colorCount))
            return SpawnSafe(ref state, spawnX, colorCount);

        var strategy = DetermineStrategy(context);

        var result = strategy switch
        {
            SpawnStrategy.Help => SpawnHelpful(ref state, spawnX, colorCount),
            _ => SpawnBalanced(ref state, spawnX, colorCount)
        };

        // Anti-streak: avoid repeating the column's top color
        if (colorCount > 1 && result == BoardAnalyzer.GetColumnTopColor(ref state, spawnX))
        {
            var rng = _rng ?? state.Random;
            int idx = BoardAnalyzer.GetColorIndex(result);
            int offset = rng.Next(1, colorCount);
            result = Colors[(idx + offset) % colorCount];
        }

        // Safety gate: prevent cascade loops from same-tick cross-column spawns.
        // Only block if the 3-in-a-row is formed entirely by just-spawned (IsFalling) tiles;
        // legitimate matches with established board tiles are allowed through.
        if (colorCount > 1)
        {
            int spawnY = BoardAnalyzer.SimulateDropTarget(ref state, spawnX);
            if (BoardAnalyzer.WouldCreateMatch(ref state, spawnX, spawnY, result) &&
                IsMatchWithOnlyFallingNeighbors(ref state, spawnX, spawnY, result))
                result = SpawnSafe(ref state, spawnX, colorCount);
        }

        return result;
    }

    /// <summary>
    /// Returns true if a horizontal 3-in-a-row at (x, y) would involve only IsFalling tiles.
    /// This identifies cascade-risk matches from same-tick refill spawns (IsFalling=true)
    /// vs legitimate matches with established board tiles (IsFalling=false).
    /// </summary>
    private static bool IsMatchWithOnlyFallingNeighbors(ref GameState state, int x, int y, ElementType color)
    {
        int hCount = 1;
        bool allFalling = true;

        for (int dx = 1; x - dx >= 0 && state.GetType(x - dx, y) == color; dx++)
        {
            hCount++;
            if (!state.GetTile(x - dx, y).IsFalling) allFalling = false;
        }
        for (int dx = 1; x + dx < state.Width && state.GetType(x + dx, y) == color; dx++)
        {
            hCount++;
            if (!state.GetTile(x + dx, y).IsFalling) allFalling = false;
        }

        return hCount >= 3 && allFalling;
    }

    private SpawnStrategy DetermineStrategy(in SpawnContext context)
    {
        // Mercy rule: help struggling players
        if (context.FailedAttempts >= 3)
            return SpawnStrategy.Help;

        // Last few moves: give a chance
        if (context.RemainingMoves <= 3 && context.GoalProgress < 0.9f)
            return SpawnStrategy.Help;

        // "Only help, never harm" — no Challenge strategy
        // Use target difficulty only for positive intervention
        if (context.TargetDifficulty < 0.3f)
            return SpawnStrategy.Help;

        // Keep board balanced for neutral difficulty
        return SpawnStrategy.Balance;
    }

    /// <summary>
    /// Spawn a tile that creates or enables a match.
    /// </summary>
    private ElementType SpawnHelpful(ref GameState state, int spawnX, int colorCount)
    {
        Span<bool> wouldMatch = stackalloc bool[6];
        BoardAnalyzer.FindMatchingColors(ref state, spawnX, wouldMatch);

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
            var rng = _rng ?? state.Random;
            int selected = matchingIndices[rng.Next(0, matchCount)];
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
            var rng = _rng ?? state.Random;
            int selected = nearMatchIndices[rng.Next(0, nearMatchCount)];
            return Colors[selected];
        }

        return SpawnSafe(ref state, spawnX, colorCount);
    }

    /// <summary>
    /// Spawn a tile that avoids creating immediate matches or near-matches.
    /// Used as fallback when no helpful color exists, to prevent cascade loops.
    /// </summary>
    private ElementType SpawnSafe(ref GameState state, int spawnX, int colorCount)
    {
        int spawnY = BoardAnalyzer.SimulateDropTarget(ref state, spawnX);
        Span<int> safeIndices = stackalloc int[6];
        int safeCount = 0;
        for (int i = 0; i < colorCount; i++)
        {
            if (!BoardAnalyzer.WouldCreateMatch(ref state, spawnX, spawnY, Colors[i]) &&
                !BoardAnalyzer.WouldCreateNearMatch(ref state, spawnX, spawnY, Colors[i]))
                safeIndices[safeCount++] = i;
        }
        if (safeCount > 0)
        {
            var rng = _rng ?? state.Random;
            return Colors[safeIndices[rng.Next(0, safeCount)]];
        }
        return SpawnRandom(ref state, colorCount);
    }

    /// <summary>
    /// Spawn a tile that balances color distribution.
    /// </summary>
    private ElementType SpawnBalanced(ref GameState state, int spawnX, int colorCount)
    {
        Span<int> counts = stackalloc int[6];
        BoardAnalyzer.GetColorDistribution(ref state, counts);

        Span<int> weights = stackalloc int[6];
        int totalWeight = 0;

        for (int i = 0; i < colorCount; i++)
        {
            weights[i] = 100 / (counts[i] + 1);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0)
            return SpawnRandom(ref state, colorCount);

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

    private static bool IsDominant(ref GameState state, int colorCount)
    {
        Span<int> counts = stackalloc int[6];
        BoardAnalyzer.GetColorDistribution(ref state, counts);

        int total = 0;
        int maxCount = 0;
        for (int i = 0; i < colorCount; i++)
        {
            total += counts[i];
            if (counts[i] > maxCount)
                maxCount = counts[i];
        }

        // Too few tiles on board to judge diversity
        if (total < colorCount) return false;

        // maxCount / total > DominanceMultiplier / colorCount
        return maxCount * colorCount > total * DominanceMultiplier;
    }

    private ElementType SpawnRandom(ref GameState state, int colorCount)
    {
        var rng = _rng ?? state.Random;
        int idx = rng.Next(0, colorCount);
        return Colors[idx];
    }

    private enum SpawnStrategy
    {
        Help,       // Create matches to help player
        Balance     // Balance color distribution
    }
}
