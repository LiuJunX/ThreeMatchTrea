using Xunit;
using Match3.Random;

namespace Match3.Random.Tests;

/// <summary>
/// PrdCalculator 单元测试
/// 测试 PRD 渐进式概率、保底机制、C 值精确推导
/// </summary>
public class PrdCalculatorTests
{
    #region Roll Mechanics

    [Fact]
    public void Roll_LowRoll_HitsImmediately()
    {
        var prd = new PrdCalculator(0.1f);

        // C=0.1, MissCount=0, threshold = 0.1 * 1 * 10000 = 1000
        // roll=0 < 1000 → hit
        Assert.True(prd.Roll(0));
        Assert.Equal(0, prd.MissCount);
    }

    [Fact]
    public void Roll_HighRoll_MissesAndIncrements()
    {
        var prd = new PrdCalculator(0.1f);

        // roll=9999 > threshold=1000 → miss
        Assert.False(prd.Roll(9999));
        Assert.Equal(1, prd.MissCount);
    }

    [Fact]
    public void Roll_MissCountResetsOnHit()
    {
        var prd = new PrdCalculator(0.1f);

        prd.Roll(9999);
        prd.Roll(9999);
        Assert.Equal(2, prd.MissCount);

        prd.Roll(0);
        Assert.Equal(0, prd.MissCount);
    }

    [Fact]
    public void Roll_CMultiplier_ScalesThreshold()
    {
        // C=0.1 without multiplier: threshold = 0.1 * 1 * 10000 = 1000
        var prd = new PrdCalculator(0.1f);
        Assert.True(prd.Roll(500, 10000, 1f)); // 500 < 1000 → hit

        // C=0.1 with 0.1 multiplier: threshold = 0.01 * 1 * 10000 = 100
        var prd2 = new PrdCalculator(0.1f);
        Assert.False(prd2.Roll(500, 10000, 0.1f)); // 500 > 100 → miss
    }

    #endregion

    #region Guaranteed Upper Bound

    [Fact]
    public void Roll_GuaranteedWithin_CeilOfOneOverC()
    {
        // C=0.1 → at N=10, threshold = 0.1 * 10 * 10000 = 10000 → guaranteed
        var prd = new PrdCalculator(0.1f);

        for (int i = 0; i < 9; i++)
            Assert.False(prd.Roll(9999));

        Assert.True(prd.Roll(9999));
    }

    [Fact]
    public void Roll_ThresholdCappedAtScale()
    {
        // C=0.5 → at N=2, threshold = 0.5 * 2 * 10000 = 10000 → guaranteed
        var prd = new PrdCalculator(0.5f);
        prd.Roll(9999); // N=1: 5000, miss
        Assert.True(prd.Roll(9999)); // N=2: 10000, hit
    }

    #endregion

    #region FromProbability Precision

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.10f)]
    [InlineData(0.15f)]
    [InlineData(0.20f)]
    [InlineData(0.25f)]
    [InlineData(0.30f)]
    [InlineData(0.40f)]
    [InlineData(0.50f)]
    public void FromProbability_AverageTriggerRate_MatchesTarget(float targetP)
    {
        var rng = new XorShift64(42);
        var prd = PrdCalculator.FromProbability(targetP);
        int hits = 0;
        const int trials = 50000;

        for (int i = 0; i < trials; i++)
        {
            if (prd.Roll(rng.Next(0, 10000)))
                hits++;
        }

        float rate = (float)hits / trials;
        float tolerance = 0.03f; // 3% absolute tolerance
        Assert.True(rate > targetP - tolerance && rate < targetP + tolerance,
            $"Target={targetP:P0}, actual={rate:P1}, C={prd.C:F6}; " +
            $"expected within ±{tolerance:P0}");
    }

    [Fact]
    public void FromProbability_CValueDecreases_WithLowerProbability()
    {
        var prd10 = PrdCalculator.FromProbability(0.10f);
        var prd25 = PrdCalculator.FromProbability(0.25f);
        var prd50 = PrdCalculator.FromProbability(0.50f);

        Assert.True(prd10.C < prd25.C);
        Assert.True(prd25.C < prd50.C);
    }

    [Fact]
    public void FromProbability_CLessThanTargetP()
    {
        // For all probabilities < 1, C should be less than p
        // (PRD's escalation effect makes average higher than C)
        for (float p = 0.05f; p <= 0.95f; p += 0.05f)
        {
            var prd = PrdCalculator.FromProbability(p);
            Assert.True(prd.C <= p,
                $"C={prd.C} should be <= p={p}");
        }
    }

    #endregion

    #region Drought Elimination

    [Fact]
    public void Roll_EliminatesLongDroughts()
    {
        var rng = new XorShift64(123);
        var prd = PrdCalculator.FromProbability(0.1f);
        int maxDrought = 0;
        int currentDrought = 0;

        for (int i = 0; i < 10000; i++)
        {
            if (prd.Roll(rng.Next(0, 10000)))
            {
                if (currentDrought > maxDrought)
                    maxDrought = currentDrought;
                currentDrought = 0;
            }
            else
            {
                currentDrought++;
            }
        }

        // C for 10% is small, but guaranteed within ceil(1/C) attempts
        int maxAllowed = (int)Math.Ceiling(1.0 / prd.C);
        Assert.True(maxDrought < maxAllowed,
            $"Max drought was {maxDrought}; PRD guarantees < {maxAllowed}");
    }

    #endregion

    #region Table Integrity

    [Fact]
    public void Table_IsStrictlyIncreasing()
    {
        int size = PrdCalculator.GetTableSize();
        for (int i = 1; i <= size; i++)
        {
            float prev = PrdCalculator.GetTableValue(i - 1);
            float curr = PrdCalculator.GetTableValue(i);
            Assert.True(curr > prev,
                $"Table[{i}]={curr} must be > Table[{i - 1}]={prev}");
        }
    }

    [Fact]
    public void Table_FirstEntryIsZero()
    {
        Assert.Equal(0f, PrdCalculator.GetTableValue(0));
    }

    [Fact]
    public void Table_LastEntryIsOne()
    {
        int size = PrdCalculator.GetTableSize();
        Assert.Equal(1f, PrdCalculator.GetTableValue(size));
    }

    [Fact]
    public void Table_KnownDota2Values_WithinTolerance()
    {
        // Verify against well-known Dota 2 PRD C values at key points.
        // These serve as regression anchors — if the table builder is broken, these fail.
        // Reference: https://dota2.fandom.com/wiki/Random_Distribution
        var knownValues = new (int index, float expectedC, float tolerance)[]
        {
            ( 5, 0.00380f, 0.001f),  //  5%
            (10, 0.01475f, 0.001f),  // 10%
            (15, 0.03222f, 0.002f),  // 15%
            (25, 0.08474f, 0.003f),  // 25%
            (50, 0.30210f, 0.005f),  // 50%
        };

        foreach (var (index, expectedC, tolerance) in knownValues)
        {
            float actual = PrdCalculator.GetTableValue(index);
            Assert.True(Math.Abs(actual - expectedC) < tolerance,
                $"Table[{index}]: expected C≈{expectedC}, got {actual:F6} (tolerance {tolerance})");
        }
    }

    [Fact]
    public void Table_AllCValues_LessThanCorrespondingP()
    {
        int size = PrdCalculator.GetTableSize();
        for (int i = 1; i < size; i++)
        {
            float p = i / (float)size;
            float c = PrdCalculator.GetTableValue(i);
            Assert.True(c < p,
                $"Table[{i}]: C={c} must be < p={p}");
        }
    }

    #endregion

    #region Input Validation

    [Fact]
    public void FromProbability_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrdCalculator.FromProbability(-0.1f));
    }

    [Fact]
    public void FromProbability_GreaterThanOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrdCalculator.FromProbability(1.1f));
    }

    [Fact]
    public void FromProbability_BelowMinRecommended_StillWorks()
    {
        // Values below MinRecommendedProbability (0.01) work but with reduced precision.
        // This test ensures no crash, not accuracy.
        var prd = PrdCalculator.FromProbability(0.005f);
        Assert.True(prd.C >= 0f);
        Assert.True(prd.C < 0.01f);
    }

    [Fact]
    public void MinRecommendedProbability_IsExposed()
    {
        // Callers can check against this before calling FromProbability.
        Assert.True(PrdCalculator.MinRecommendedProbability > 0f);
        Assert.True(PrdCalculator.MinRecommendedProbability <= 0.05f);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FromProbability_Zero_ReturnsZeroC()
    {
        var prd = PrdCalculator.FromProbability(0f);
        Assert.Equal(0f, prd.C);
    }

    [Fact]
    public void FromProbability_One_ReturnsOneC()
    {
        var prd = PrdCalculator.FromProbability(1f);
        Assert.Equal(1f, prd.C);
    }

    [Fact]
    public void FromProbability_MissCountStartsAtZero()
    {
        var prd = PrdCalculator.FromProbability(0.25f);
        Assert.Equal(0, prd.MissCount);
    }

    [Fact]
    public void Constructor_SetsC()
    {
        var prd = new PrdCalculator(0.05f);
        Assert.Equal(0.05f, prd.C);
        Assert.Equal(0, prd.MissCount);
    }

    #endregion
}
