using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// WeightedDefaultCondition 单元测试
/// 测试权重分布、列作用域、anti-streak
/// </summary>
public class WeightedDefaultConditionTests
{
    private static GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        return state;
    }

    #region Column Scope

    [Fact]
    public void IsConditionMet_TrueForClaimedColumn()
    {
        var weights = new Dictionary<ElementType, int> { { ElementType.Item1, 50 }, { ElementType.Item3, 50 } };
        var condition = new WeightedDefaultCondition(new[] { 2, 5 }, weights, new SequentialRandom());
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 2, in context));
        Assert.True(condition.IsConditionMet(ref state, 5, in context));
    }

    [Fact]
    public void IsConditionMet_FalseForUnclaimedColumn()
    {
        var weights = new Dictionary<ElementType, int> { { ElementType.Item1, 50 } };
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, new SequentialRandom());
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.False(condition.IsConditionMet(ref state, 1, in context));
        Assert.False(condition.IsConditionMet(ref state, 7, in context));
    }

    [Fact]
    public void Priority_Is100()
    {
        var weights = new Dictionary<ElementType, int> { { ElementType.Item1, 50 } };
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, new SequentialRandom());
        Assert.Equal(100, condition.Priority);
    }

    #endregion

    #region Weight Distribution

    [Fact]
    public void Generate_OnlyProducesConfiguredElements()
    {
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Item1, 50 },
            { ElementType.Item4, 50 }
        };
        var rng = new Match3.Random.XorShift64(42);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState();
        var context = SpawnContext.Default;

        var seen = new HashSet<ElementType>();
        for (int i = 0; i < 100; i++)
        {
            var result = condition.Generate(ref state, 0, in context);
            seen.Add(result);
        }

        // Should only produce Item1 and Item4
        Assert.True(seen.Contains(ElementType.Item1));
        Assert.True(seen.Contains(ElementType.Item4));
        Assert.DoesNotContain(ElementType.Item2, seen);
        Assert.DoesNotContain(ElementType.Item3, seen);
        Assert.DoesNotContain(ElementType.Item5, seen);
        Assert.DoesNotContain(ElementType.Item6, seen);
    }

    [Fact]
    public void Generate_RespectsWeightRatio()
    {
        // Item1 weight=90, Item2 weight=10 → ~90% Item1
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Item1, 90 },
            { ElementType.Item2, 10 }
        };
        var rng = new Match3.Random.XorShift64(42);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState();
        var context = SpawnContext.Default;

        int item1Count = 0;
        const int total = 1000;
        for (int i = 0; i < total; i++)
        {
            if (condition.Generate(ref state, 0, in context) == ElementType.Item1)
                item1Count++;
        }

        // ~90% ± tolerance (anti-streak may deflect a few)
        Assert.True(item1Count > 700, $"Item1 appeared {item1Count}/{total} times, expected ~900");
        Assert.True(item1Count < total, "Item2 should appear at least occasionally");
    }

    [Fact]
    public void Generate_ZeroWeightElementNeverAppears()
    {
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Item1, 100 },
            { ElementType.Item2, 0 }   // zero weight
        };
        var rng = new Match3.Random.XorShift64(42);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState();
        var context = SpawnContext.Default;

        for (int i = 0; i < 100; i++)
        {
            var result = condition.Generate(ref state, 0, in context);
            Assert.NotEqual(ElementType.Item2, result);
        }
    }

    [Fact]
    public void Generate_SupportsNonColorElements()
    {
        // Bird + colors in the same weight table
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Item1, 50 },
            { ElementType.Bird, 50 }
        };
        var rng = new Match3.Random.XorShift64(42);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState();
        var context = SpawnContext.Default;

        bool seenBird = false;
        bool seenItem1 = false;
        for (int i = 0; i < 100; i++)
        {
            var result = condition.Generate(ref state, 0, in context);
            if (result == ElementType.Bird) seenBird = true;
            if (result == ElementType.Item1) seenItem1 = true;
        }

        Assert.True(seenBird, "Bird should appear from weights");
        Assert.True(seenItem1, "Item1 should appear from weights");
    }

    #endregion

    #region Anti-Streak

    [Fact]
    public void Generate_AntiStreak_DeflectsColumnTopColor()
    {
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Item1, 100 },
            { ElementType.Item3, 1 }
        };
        // StubRandom always returns 0 → always picks Item1 first
        var rng = StubRandom.WithFixedValue(0);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState(5, 5);

        // Place Item1 at column 0 top
        state.SetTile(0, 4, new Tile(1, ElementType.Item1, 0, 4));

        var context = SpawnContext.Default;
        var result = condition.Generate(ref state, 0, in context);

        // Anti-streak should deflect to Item3
        Assert.Equal(ElementType.Item3, result);
    }

    [Fact]
    public void Generate_NoAntiStreak_ForNonColorElements()
    {
        var weights = new Dictionary<ElementType, int>
        {
            { ElementType.Bird, 100 }
        };
        var rng = StubRandom.WithFixedValue(0);
        var condition = new WeightedDefaultCondition(new[] { 0 }, weights, rng);
        var state = CreateState(5, 5);

        // Column top is Bird — anti-streak should NOT apply to non-color
        state.SetTile(0, 4, new Tile(1, ElementType.Bird, 0, 4));

        var context = SpawnContext.Default;
        var result = condition.Generate(ref state, 0, in context);

        Assert.Equal(ElementType.Bird, result);
    }

    #endregion

    #region Factory Integration

    [Fact]
    public void Factory_WeightedDefault_OverridesGlobalForClaimedColumns()
    {
        var spawners = new[]
        {
            new SpawnerConfig
            {
                Id = 0,
                Columns = new[] { 0, 1 },
                Weights = new Dictionary<ElementType, int>
                {
                    { ElementType.Item1, 100 }  // only Item1
                }
            }
        };

        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom(), spawners);

        var state = CreateState();
        var context = SpawnContext.Default;

        // Column 0: claimed → only Item1
        var result = model.Predict(ref state, 0, in context);
        Assert.Equal(ElementType.Item1, result);

        // Column 5: unclaimed → default color (could be any)
        var result2 = model.Predict(ref state, 5, in context);
        Assert.True(result2.IsColor());
    }

    [Fact]
    public void Factory_PresetThenWeighted_WorksTogether()
    {
        var spawners = new[]
        {
            new SpawnerConfig
            {
                Id = 0,
                Columns = new[] { 0 },
                Preset = new PresetQueueConfig
                {
                    Sequence = new[] { ElementType.ColorBomb },
                    Cycles = 1
                },
                Weights = new Dictionary<ElementType, int>
                {
                    { ElementType.Item4, 100 }
                }
            }
        };

        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom(), spawners);

        var state = CreateState();
        var context = SpawnContext.Default;

        // First: preset fires (priority 600)
        Assert.Equal(ElementType.ColorBomb, model.Predict(ref state, 0, in context));

        // Second: preset exhausted → WeightedDefault fires (priority 100)
        Assert.Equal(ElementType.Item4, model.Predict(ref state, 0, in context));
    }

    [Fact]
    public void Factory_NoSpawners_BackwardCompatible()
    {
        // Exactly the same as before — no spawners parameter
        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom());

        var state = CreateState();
        var context = SpawnContext.Default;

        for (int x = 0; x < 8; x++)
        {
            var result = model.Predict(ref state, x, in context);
            Assert.True(result.IsColor(), $"Column {x} should produce a color without spawner config");
        }
    }

    [Fact]
    public void Factory_MultipleSpawners_DifferentZones()
    {
        var spawners = new[]
        {
            new SpawnerConfig
            {
                Id = 0,
                Columns = new[] { 0, 1 },
                Weights = new Dictionary<ElementType, int>
                {
                    { ElementType.Item1, 100 }
                }
            },
            new SpawnerConfig
            {
                Id = 1,
                Columns = new[] { 6, 7 },
                Weights = new Dictionary<ElementType, int>
                {
                    { ElementType.Item6, 100 }
                }
            }
        };

        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom(), spawners);

        var state = CreateState();
        var context = SpawnContext.Default;

        // Zone 0: columns 0-1 → Item1
        Assert.Equal(ElementType.Item1, model.Predict(ref state, 0, in context));
        Assert.Equal(ElementType.Item1, model.Predict(ref state, 1, in context));

        // Zone 1: columns 6-7 → Item6
        Assert.Equal(ElementType.Item6, model.Predict(ref state, 6, in context));
        Assert.Equal(ElementType.Item6, model.Predict(ref state, 7, in context));

        // Middle: columns 3 → default
        var mid = model.Predict(ref state, 3, in context);
        Assert.True(mid.IsColor());
    }

    #endregion
}
