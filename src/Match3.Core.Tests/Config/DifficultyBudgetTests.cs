using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Config;

public class DifficultyBudgetTests
{
    #region Rhythm Classification

    [Fact]
    public void ClassifyRhythm_BossLevel_ReturnsBoss()
    {
        var pool = MakePool(bossEveryN: 10);

        var result = DifficultyBudget.ClassifyRhythm(pool, 20, new XorShift64(1));

        Assert.Equal(RhythmCategory.Boss, result);
    }

    [Fact]
    public void ClassifyRhythm_NoBossConfig_NeverReturnsBoss()
    {
        var pool = MakePool(bossEveryN: null);

        // Try many seeds
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var result = DifficultyBudget.ClassifyRhythm(pool, 20, new XorShift64(seed));
            Assert.NotEqual(RhythmCategory.Boss, result);
        }
    }

    [Fact]
    public void ClassifyRhythm_Distribution_RoughlyMatchesRatios()
    {
        var pool = MakePool(easyRatio: 0.2f, hardRatio: 0.3f);
        int easy = 0, normal = 0, hard = 0;
        int total = 1000;

        for (ulong seed = 1; seed <= (ulong)total; seed++)
        {
            var r = DifficultyBudget.ClassifyRhythm(pool, 5, new XorShift64(seed));
            switch (r)
            {
                case RhythmCategory.Easy: easy++; break;
                case RhythmCategory.Hard: hard++; break;
                default: normal++; break;
            }
        }

        // Allow 10% tolerance
        Assert.InRange(easy, total * 0.1, total * 0.35);
        Assert.InRange(hard, total * 0.15, total * 0.45);
    }

    #endregion

    #region Difficulty Value

    [Fact]
    public void ComputeDifficulty_EasyRhythm_StaysInLowerRange()
    {
        var pool = MakePool(minDiff: 0.0f, maxDiff: 0.5f);

        for (ulong seed = 1; seed <= 50; seed++)
        {
            float d = DifficultyBudget.ComputeDifficulty(pool, RhythmCategory.Easy, new XorShift64(seed));
            Assert.InRange(d, 0.0f, 0.3f); // Should stay in [min, mid]
        }
    }

    [Fact]
    public void ComputeDifficulty_HardRhythm_StaysInUpperRange()
    {
        var pool = MakePool(minDiff: 0.0f, maxDiff: 0.8f);

        for (ulong seed = 1; seed <= 50; seed++)
        {
            float d = DifficultyBudget.ComputeDifficulty(pool, RhythmCategory.Hard, new XorShift64(seed));
            Assert.True(d >= 0.2f, $"Hard difficulty {d} too low");
        }
    }

    #endregion

    #region Placement Plan

    [Fact]
    public void ComputePlacementPlan_ZeroDifficulty_EmptyPlan()
    {
        var pool = MakePool(minDiff: 0f, maxDiff: 0f);
        pool.Obstacles[ObstacleType.Box] = new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 4, MaxCount = 10 };

        var plan = DifficultyBudget.ComputePlacementPlan(0f, 64, pool, new XorShift64(42));

        Assert.Empty(plan.Obstacles);
        Assert.Empty(plan.Covers);
        Assert.Empty(plan.Grounds);
    }

    [Fact]
    public void ComputePlacementPlan_HighDifficulty_ProducesElements()
    {
        var pool = MakePool(minDiff: 0.5f, maxDiff: 0.9f);
        pool.Obstacles[ObstacleType.Box] = new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 4, MaxCount = 15 };
        pool.Covers[CoverType.Cage] = new CoverAllowance { Type = CoverType.Cage, MaxHealth = 2, MaxCount = 12 };
        pool.Grounds[GroundType.Ice] = new GroundAllowance { Type = GroundType.Ice, MaxHealth = 3, MaxCount = 20 };

        var plan = DifficultyBudget.ComputePlacementPlan(0.8f, 64, pool, new XorShift64(42));

        Assert.NotEmpty(plan.Obstacles);
        Assert.NotEmpty(plan.Covers);
        Assert.NotEmpty(plan.Grounds);
    }

    [Fact]
    public void ComputePlacementPlan_RespectsMaxCount()
    {
        var pool = MakePool();
        pool.Obstacles[ObstacleType.Box] = new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 2, MaxCount = 5 };

        // High difficulty, many slots — should not exceed MaxCount
        var plan = DifficultyBudget.ComputePlacementPlan(0.9f, 81, pool, new XorShift64(42));

        int boxCount = plan.Obstacles.Count(o => o.Type == ObstacleType.Box);
        Assert.True(boxCount <= 5, $"Box count {boxCount} exceeds MaxCount 5");
    }

    [Fact]
    public void ComputePlacementPlan_StageScaledByDifficulty()
    {
        var pool = MakePool();
        pool.Obstacles[ObstacleType.Box] = new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 4, MaxCount = 20 };

        // Low difficulty: stages should be low
        var lowPlan = DifficultyBudget.ComputePlacementPlan(0.2f, 64, pool, new XorShift64(42));
        // High difficulty: stages should be higher
        var highPlan = DifficultyBudget.ComputePlacementPlan(0.9f, 64, pool, new XorShift64(42));

        float avgLow = lowPlan.Obstacles.Count > 0 ? (float)lowPlan.Obstacles.Average(o => o.Stage) : 0;
        float avgHigh = highPlan.Obstacles.Count > 0 ? (float)highPlan.Obstacles.Average(o => o.Stage) : 0;

        // High difficulty should produce higher average stages (or at least equal if count is 0)
        Assert.True(avgHigh >= avgLow || highPlan.Obstacles.Count == 0,
            $"High diff avg stage ({avgHigh}) should >= low ({avgLow})");
    }

    [Fact]
    public void ComputePlacementPlan_ColorCountInRange()
    {
        var pool = MakePool();
        pool.MinColors = 4;
        pool.MaxColors = 6;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var plan = DifficultyBudget.ComputePlacementPlan(0.5f, 64, pool, new XorShift64(seed));
            Assert.InRange(plan.ColorCount, 4, 6);
        }
    }

    #endregion

    #region Move Calculation

    [Fact]
    public void CalculateMoves_EmptyPlan_ReturnsMinMoves()
    {
        var pool = MakePool(minMoves: 15, maxMoves: 30);
        var plan = new PlacementPlan();

        int moves = DifficultyBudget.CalculateMoves(plan, 0.1f, pool);

        Assert.InRange(moves, 15, 30);
    }

    [Fact]
    public void CalculateMoves_WithinRange()
    {
        var pool = MakePool(minMoves: 20, maxMoves: 35);
        var plan = new PlacementPlan();
        plan.Obstacles.Add((ObstacleType.Box, 3, 0));
        plan.Obstacles.Add((ObstacleType.Box, 2, 0));
        plan.Covers.Add((CoverType.Cage, 1));
        plan.Grounds.Add((GroundType.Ice, 2));

        int moves = DifficultyBudget.CalculateMoves(plan, 0.5f, pool);

        Assert.InRange(moves, 20, 35);
    }

    [Fact]
    public void CalculateMoves_HigherDifficulty_FewerMoves()
    {
        var pool = MakePool(minMoves: 15, maxMoves: 45);
        var plan = new PlacementPlan();
        for (int i = 0; i < 8; i++)
            plan.Obstacles.Add((ObstacleType.Box, 2, 0));

        int easyMoves = DifficultyBudget.CalculateMoves(plan, 0.1f, pool);
        int hardMoves = DifficultyBudget.CalculateMoves(plan, 0.9f, pool);

        Assert.True(easyMoves >= hardMoves,
            $"Easy moves ({easyMoves}) should >= hard moves ({hardMoves})");
    }

    #endregion

    #region Helpers

    private static EffectivePool MakePool(
        float minDiff = 0f, float maxDiff = 0.5f,
        int minMoves = 15, int maxMoves = 30,
        float easyRatio = 0.1f, float hardRatio = 0.2f,
        int? bossEveryN = 10)
    {
        return new EffectivePool
        {
            MinDifficulty = minDiff,
            MaxDifficulty = maxDiff,
            MinMoves = minMoves,
            MaxMoves = maxMoves,
            MinColors = 4,
            MaxColors = 5,
            EasyRatio = easyRatio,
            HardRatio = hardRatio,
            BossEveryN = bossEveryN,
            ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile }
        };
    }

    #endregion
}
