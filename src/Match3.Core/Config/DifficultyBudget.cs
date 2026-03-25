using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Random;

namespace Match3.Core.Config;

/// <summary>
/// Rhythm category determines the "feel" of a level within its phase.
/// </summary>
public enum RhythmCategory { Easy, Normal, Hard, Boss }

/// <summary>
/// Translates a difficulty value (0.0-1.0) into concrete element placement counts,
/// respecting EffectivePool constraints. Also handles rhythm classification and move calculation.
/// </summary>
public static class DifficultyBudget
{
    // ── Move calculation coefficients (from level-design.md §6) ──
    private const float ObstacleHpCoeff = 1.2f;
    private const float CoverHpCoeff = 1.0f;
    private const float GroundHpCoeff = 0.8f;
    private const float TileCollectCoeff = 0.5f;

    // ── Difficulty multipliers by rhythm ──
    private const float EasyMultiplier = 1.5f;
    private const float NormalMultiplier = 1.3f;
    private const float HardMultiplier = 1.1f;
    private const float BossMultiplier = 1.0f;

    /// <summary>
    /// Classify a level into a rhythm category based on phase ratios.
    /// </summary>
    public static RhythmCategory ClassifyRhythm(EffectivePool pool, int levelNumber, XorShift64 rng)
    {
        // Boss check first
        if (pool.BossEveryN.HasValue && pool.BossEveryN.Value > 0 && levelNumber % pool.BossEveryN.Value == 0)
            return RhythmCategory.Boss;

        float roll = rng.NextFloat();
        if (roll < pool.EasyRatio)
            return RhythmCategory.Easy;
        if (roll > 1f - pool.HardRatio)
            return RhythmCategory.Hard;
        return RhythmCategory.Normal;
    }

    /// <summary>
    /// Compute a concrete difficulty value within the phase range, adjusted by rhythm.
    /// </summary>
    public static float ComputeDifficulty(EffectivePool pool, RhythmCategory rhythm, XorShift64 rng)
    {
        float min = pool.MinDifficulty;
        float max = pool.MaxDifficulty;
        float mid = (min + max) / 2f;
        float t = rng.NextFloat();

        return rhythm switch
        {
            RhythmCategory.Easy => Lerp(min, mid, t),
            RhythmCategory.Normal => Lerp(Math.Max(min, mid - 0.1f), Math.Min(max, mid + 0.1f), t),
            RhythmCategory.Hard => Lerp(mid, max, t),
            RhythmCategory.Boss => Lerp(max * 0.85f, max, t),
            _ => mid
        };
    }

    /// <summary>
    /// Given a difficulty value, total slot count, and effective pool,
    /// produce concrete placement counts for each element type.
    /// </summary>
    public static PlacementPlan ComputePlacementPlan(
        float difficulty, int totalSlots, EffectivePool pool, IRandom rng)
    {
        var plan = new PlacementPlan
        {
            ColorCount = rng.Next(pool.MinColors, pool.MaxColors + 1)
        };

        // Budget = fraction of total slots for each category
        // Conservative: ~15% of board at max difficulty, much less at low difficulty
        int obstacleBudget = (int)(difficulty * 0.15f * totalSlots);
        int coverBudget = (int)(difficulty * 0.10f * totalSlots);
        int groundBudget = (int)(difficulty * 0.12f * totalSlots);

        // Distribute obstacles
        DistributeObstacles(plan, pool, obstacleBudget, difficulty, rng);

        // Distribute covers
        DistributeCovers(plan, pool, coverBudget, difficulty, rng);

        // Distribute grounds
        DistributeGrounds(plan, pool, groundBudget, difficulty, rng);

        // Moving obstacles (small count, separate from obstacle budget)
        DistributeMovingObstacles(plan, pool, difficulty, rng);

        return plan;
    }

    /// <summary>
    /// Calculate move limit based on placed content and difficulty.
    /// </summary>
    public static int CalculateMoves(PlacementPlan plan, float difficulty, EffectivePool pool)
    {
        float baseMoves = 0;

        // Obstacle HP cost (each HP requires ~2 effective actions: find match + execute)
        foreach (var (_, stage, _) in plan.Obstacles)
            baseMoves += stage * ObstacleHpCoeff;

        // Cover HP cost
        foreach (var (_, health) in plan.Covers)
            baseMoves += health * CoverHpCoeff;

        // Ground HP cost
        foreach (var (_, health) in plan.Grounds)
            baseMoves += health * GroundHpCoeff;

        // Moving obstacle cost
        foreach (var (_, stage) in plan.MovingObstacles)
            baseMoves += stage * ObstacleHpCoeff;

        // Operation overhead: random AI needs ~2 moves per useful elimination
        // More elements = more overhead because of competing priorities
        int totalElements = plan.Obstacles.Count + plan.Covers.Count + plan.Grounds.Count + plan.MovingObstacles.Count;
        baseMoves += totalElements * 0.8f;

        // Base minimum: even an empty board with a collect objective needs moves
        baseMoves = Math.Max(baseMoves, 12f);

        // Apply difficulty multiplier (easier = more moves)
        float multiplier = difficulty switch
        {
            <= 0.2f => EasyMultiplier,
            <= 0.5f => NormalMultiplier,
            <= 0.75f => HardMultiplier,
            _ => BossMultiplier
        };

        int moves = (int)Math.Round(baseMoves * multiplier);
        return Math.Clamp(moves, pool.MinMoves, pool.MaxMoves);
    }

    // ── Distribution Helpers ──

    private static void DistributeObstacles(
        PlacementPlan plan, EffectivePool pool, int budget, float difficulty, IRandom rng)
    {
        if (pool.Obstacles.Count == 0 || budget <= 0) return;

        var types = new List<ObstacleAllowance>(pool.Obstacles.Values);
        Shuffle(types, rng);

        // Pick 1-N types based on difficulty
        int typesToUse = Math.Min(types.Count, Math.Max(1, (int)(difficulty * types.Count + 0.5f)));
        int remaining = budget;

        for (int i = 0; i < typesToUse && remaining > 0; i++)
        {
            var allowance = types[i];
            int share = Math.Min(remaining, allowance.MaxCount);
            int count = rng.Next(1, share + 1);
            count = Math.Min(count, remaining);

            int maxAllowedStage = Math.Max(1, (int)(difficulty * allowance.MaxStage + 0.5f));

            for (int j = 0; j < count; j++)
            {
                byte stage = (byte)rng.Next(1, maxAllowedStage + 1);
                byte state = 0; // Generator sets special states later
                plan.Obstacles.Add((allowance.Type, stage, state));
                remaining--;
                if (remaining <= 0) break;
            }
        }
    }

    private static void DistributeCovers(
        PlacementPlan plan, EffectivePool pool, int budget, float difficulty, IRandom rng)
    {
        if (pool.Covers.Count == 0 || budget <= 0) return;

        var types = new List<CoverAllowance>(pool.Covers.Values);
        Shuffle(types, rng);

        int typesToUse = Math.Min(types.Count, Math.Max(1, (int)(difficulty * types.Count + 0.5f)));
        int remaining = budget;

        for (int i = 0; i < typesToUse && remaining > 0; i++)
        {
            var allowance = types[i];
            int count = Math.Min(rng.Next(1, allowance.MaxCount + 1), remaining);

            int maxAllowedHealth = Math.Max(1, (int)(difficulty * allowance.MaxHealth + 0.5f));

            for (int j = 0; j < count; j++)
            {
                byte health = (byte)rng.Next(1, maxAllowedHealth + 1);
                plan.Covers.Add((allowance.Type, health));
                remaining--;
                if (remaining <= 0) break;
            }
        }
    }

    private static void DistributeGrounds(
        PlacementPlan plan, EffectivePool pool, int budget, float difficulty, IRandom rng)
    {
        if (pool.Grounds.Count == 0 || budget <= 0) return;

        var types = new List<GroundAllowance>(pool.Grounds.Values);
        Shuffle(types, rng);

        int typesToUse = Math.Min(types.Count, Math.Max(1, (int)(difficulty * types.Count + 0.5f)));
        int remaining = budget;

        for (int i = 0; i < typesToUse && remaining > 0; i++)
        {
            var allowance = types[i];
            int count = Math.Min(rng.Next(1, allowance.MaxCount + 1), remaining);

            int maxAllowedHealth = Math.Max(1, (int)(difficulty * allowance.MaxHealth + 0.5f));

            for (int j = 0; j < count; j++)
            {
                byte health = (byte)rng.Next(1, maxAllowedHealth + 1);
                plan.Grounds.Add((allowance.Type, health));
                remaining--;
                if (remaining <= 0) break;
            }
        }
    }

    private static void DistributeMovingObstacles(
        PlacementPlan plan, EffectivePool pool, float difficulty, IRandom rng)
    {
        if (pool.MovingObstacles.Count == 0) return;

        // Small fixed budget: 0-3 based on difficulty
        int budget = (int)(difficulty * 3 + 0.5f);
        if (budget <= 0) return;

        var types = new List<MovingObstacleAllowance>(pool.MovingObstacles.Values);
        Shuffle(types, rng);

        int remaining = budget;
        foreach (var allowance in types)
        {
            if (remaining <= 0) break;
            int count = Math.Min(rng.Next(1, allowance.MaxCount + 1), remaining);
            for (int j = 0; j < count; j++)
            {
                byte stage = (byte)Math.Min(allowance.MaxStage, Math.Max(1, (int)(difficulty * allowance.MaxStage + 0.5f)));
                plan.MovingObstacles.Add((allowance.Type, stage));
                remaining--;
                if (remaining <= 0) break;
            }
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static void Shuffle<T>(List<T> list, IRandom rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>
/// Concrete counts of elements to place on the board.
/// </summary>
public sealed class PlacementPlan
{
    public List<(ObstacleType Type, byte Stage, byte State)> Obstacles { get; set; } = new();
    public List<(CoverType Type, byte Health)> Covers { get; set; } = new();
    public List<(GroundType Type, byte Health)> Grounds { get; set; } = new();
    public List<(ElementType Type, byte Stage)> MovingObstacles { get; set; } = new();
    public int ColorCount { get; set; } = 5;

    /// <summary>Total obstacle HP for move calculation.</summary>
    public int TotalObstacleHp => SumStages(Obstacles);
    public int TotalCoverHp => SumHealthCovers(Covers);
    public int TotalGroundHp => SumHealthGrounds(Grounds);

    private static int SumStages(List<(ObstacleType, byte Stage, byte)> list)
    {
        int sum = 0;
        foreach (var (_, s, _) in list) sum += s;
        return sum;
    }
    private static int SumHealthCovers(List<(CoverType, byte Health)> list)
    {
        int sum = 0;
        foreach (var (_, h) in list) sum += h;
        return sum;
    }
    private static int SumHealthGrounds(List<(GroundType, byte Health)> list)
    {
        int sum = 0;
        foreach (var (_, h) in list) sum += h;
        return sum;
    }
}
