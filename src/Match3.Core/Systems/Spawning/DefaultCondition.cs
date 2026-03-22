using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Default spawn condition (priority 100). Always matches as a fallback.
/// Generates seed-driven random colors with minimal safety nets:
/// color guarantee (hard threshold), column-top avoidance, and mercy mode.
/// Replaces RuleBasedSpawnModel's complex strategy system with "only help, never harm".
/// </summary>
public class DefaultCondition : ISpawnCondition
{
    private static readonly ElementType[] Colors = new[]
    {
        ElementType.Item1, ElementType.Item2, ElementType.Item3,
        ElementType.Item4, ElementType.Item5, ElementType.Item6
    };

    private readonly IRandom _rng;
    private readonly int _colorCount;

    /// <summary>Per-color consecutive miss counter for color guarantee (hard threshold, not PRD).</summary>
    private readonly int[] _colorMissCounters;

    /// <summary>After this many consecutive misses, force the color.</summary>
    private const int ColorGuaranteeThreshold = 4;

    /// <inheritdoc />
    public int Priority => 100;

    /// <summary>
    /// Creates a default spawn condition.
    /// </summary>
    /// <param name="rng">Random number generator (seed-driven).</param>
    /// <param name="colorCount">Number of active colors (1-6).</param>
    public DefaultCondition(IRandom rng, int colorCount)
    {
        _rng = rng;
        _colorCount = Math.Min(colorCount, Colors.Length);
        _colorMissCounters = new int[Colors.Length];
    }

    /// <summary>Always returns true — this is the fallback condition.</summary>
    public bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context)
    {
        return true;
    }

    /// <inheritdoc />
    public ElementType Generate(ref GameState state, int spawnX, in SpawnContext context)
    {
        if (_colorCount <= 0) return ElementType.None;

        // 1. Color guarantee: force a color that has been absent too long
        int forcedColor = FindForcedColor();
        if (forcedColor >= 0)
        {
            var forced = ApplyAntiStreak(ref state, spawnX, Colors[forcedColor]);
            int forcedIdx = BoardAnalyzer.GetColorIndex(forced);
            if (forcedIdx >= 0) UpdateColorMissCounters(forcedIdx);
            return forced;
        }

        // 2. Mercy: bias toward matching colors when player is struggling
        //    NOTE: Currently inactive — FailedAttempts is always 0 until Phase 2 session tracking.
        if (ShouldApplyMercy(in context))
        {
            var mercyResult = TryMercySpawn(ref state, spawnX);
            if (mercyResult != ElementType.None)
            {
                int idx = BoardAnalyzer.GetColorIndex(mercyResult);
                if (idx >= 0) UpdateColorMissCounters(idx);
                return mercyResult; // Mercy bypasses anti-streak intentionally
            }
        }

        // 3. Normal: random color from seed RNG (~80% of cases)
        int selected = _rng.Next(0, _colorCount);
        var result = Colors[selected];

        // 4. Anti-streak: avoid repeating column top color
        result = ApplyAntiStreak(ref state, spawnX, result);

        int resultIdx = BoardAnalyzer.GetColorIndex(result);
        if (resultIdx >= 0) UpdateColorMissCounters(resultIdx);

        return result;
    }

    /// <summary>
    /// Finds the color that has been absent the longest past the threshold.
    /// Returns -1 if no color needs forcing.
    /// </summary>
    private int FindForcedColor()
    {
        int maxMiss = -1;
        int maxIdx = -1;
        for (int i = 0; i < _colorCount; i++)
        {
            if (_colorMissCounters[i] >= ColorGuaranteeThreshold && _colorMissCounters[i] > maxMiss)
            {
                maxMiss = _colorMissCounters[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    /// <summary>
    /// Updates color miss counters: resets selected, increments all others.
    /// </summary>
    private void UpdateColorMissCounters(int selectedIndex)
    {
        for (int i = 0; i < _colorCount; i++)
        {
            if (i == selectedIndex)
                _colorMissCounters[i] = 0;
            else
                _colorMissCounters[i]++;
        }
    }

    /// <summary>
    /// Mercy triggers only when the player is truly struggling:
    /// failed 3+ times AND in last few moves AND not close to goal.
    /// </summary>
    private static bool ShouldApplyMercy(in SpawnContext context)
    {
        return context.FailedAttempts >= 3
            && context.RemainingMoves <= 3
            && context.GoalProgress < 0.9f;
    }

    /// <summary>
    /// Attempts to spawn a color that creates an immediate match.
    /// Returns None if no matching color is available.
    /// </summary>
    private ElementType TryMercySpawn(ref GameState state, int spawnX)
    {
        Span<bool> wouldMatch = stackalloc bool[6];
        BoardAnalyzer.FindMatchingColors(ref state, spawnX, wouldMatch);

        Span<int> matchingIndices = stackalloc int[6];
        int matchCount = 0;
        for (int i = 0; i < _colorCount; i++)
        {
            if (wouldMatch[i])
                matchingIndices[matchCount++] = i;
        }

        if (matchCount > 0)
        {
            int selected = matchingIndices[_rng.Next(0, matchCount)];
            return Colors[selected];
        }

        return ElementType.None;
    }

    /// <summary>
    /// Avoids repeating the color at the top of the column.
    /// </summary>
    private ElementType ApplyAntiStreak(ref GameState state, int spawnX, ElementType result)
    {
        if (_colorCount <= 1) return result;

        var topColor = BoardAnalyzer.GetColumnTopColor(ref state, spawnX);
        if (result == topColor && topColor != ElementType.None)
        {
            int idx = BoardAnalyzer.GetColorIndex(result);
            int offset = _rng.Next(1, _colorCount);
            result = Colors[(idx + offset) % _colorCount];
        }

        return result;
    }
}
