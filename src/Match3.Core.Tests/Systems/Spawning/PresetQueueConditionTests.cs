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
/// PresetQueueCondition 单元测试
/// 测试固定序列掉落、循环、耗尽、列独立队列
/// </summary>
public class PresetQueueConditionTests
{
    private static GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        return state;
    }

    #region Basic Sequence

    [Fact]
    public void Generate_EmitsSequenceInOrder()
    {
        var sequence = new[] { ElementType.Item1, ElementType.Item3, ElementType.ColorBomb };
        var condition = new PresetQueueCondition(new[] { 0 }, sequence, cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 0, in context));

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.Equal(ElementType.Item3, condition.Generate(ref state, 0, in context));

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.Equal(ElementType.ColorBomb, condition.Generate(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_ReturnsFalse_AfterSequenceExhausted()
    {
        var condition = new PresetQueueCondition(
            new[] { 0 }, new[] { ElementType.Item1 }, cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Consume the single item
        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        condition.Generate(ref state, 0, in context);

        // Exhausted
        Assert.False(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void Priority_Is600()
    {
        var condition = new PresetQueueCondition(
            new[] { 0 }, new[] { ElementType.Item1 }, cycles: 1);
        Assert.Equal(600, condition.Priority);
    }

    #endregion

    #region Cycles

    [Fact]
    public void Generate_CyclesSequence_MultipleTimes()
    {
        var sequence = new[] { ElementType.Item1, ElementType.Item2 };
        var condition = new PresetQueueCondition(new[] { 0 }, sequence, cycles: 2);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Cycle 1
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 0, in context));
        Assert.Equal(ElementType.Item2, condition.Generate(ref state, 0, in context));

        // Cycle 2
        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 0, in context));
        Assert.Equal(ElementType.Item2, condition.Generate(ref state, 0, in context));

        // Exhausted after 2 cycles
        Assert.False(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void Generate_InfiniteCycles_NeverExhausts()
    {
        var sequence = new[] { ElementType.Item1, ElementType.Item3 };
        var condition = new PresetQueueCondition(new[] { 0 }, sequence, cycles: 0);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Run many iterations — should never exhaust
        for (int i = 0; i < 100; i++)
        {
            Assert.True(condition.IsConditionMet(ref state, 0, in context));
            var expected = sequence[i % 2];
            Assert.Equal(expected, condition.Generate(ref state, 0, in context));
        }
    }

    #endregion

    #region Column Isolation

    [Fact]
    public void IsConditionMet_ReturnsFalse_ForUnclaimedColumn()
    {
        var condition = new PresetQueueCondition(
            new[] { 0, 1 }, new[] { ElementType.Item1 }, cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.True(condition.IsConditionMet(ref state, 1, in context));
        Assert.False(condition.IsConditionMet(ref state, 2, in context));
    }

    [Fact]
    public void Generate_EachColumnHasIndependentQueue()
    {
        var sequence = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3 };
        var condition = new PresetQueueCondition(new[] { 0, 3 }, sequence, cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Column 0: consume first element
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 0, in context));

        // Column 3: starts from beginning (independent)
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 3, in context));

        // Column 0: second element
        Assert.Equal(ElementType.Item2, condition.Generate(ref state, 0, in context));

        // Column 3: second element (independent progress)
        Assert.Equal(ElementType.Item2, condition.Generate(ref state, 3, in context));
    }

    [Fact]
    public void Generate_OneColumnExhausted_OtherContinues()
    {
        var condition = new PresetQueueCondition(
            new[] { 0, 1 }, new[] { ElementType.Item1 }, cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Exhaust column 0
        condition.Generate(ref state, 0, in context);
        Assert.False(condition.IsConditionMet(ref state, 0, in context));

        // Column 1 still has its item
        Assert.True(condition.IsConditionMet(ref state, 1, in context));
        Assert.Equal(ElementType.Item1, condition.Generate(ref state, 1, in context));
    }

    #endregion

    #region Empty Sequence

    [Fact]
    public void IsConditionMet_EmptySequence_ReturnsFalse()
    {
        var condition = new PresetQueueCondition(
            new[] { 0 }, System.Array.Empty<ElementType>(), cycles: 1);
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.False(condition.IsConditionMet(ref state, 0, in context));
    }

    #endregion

    #region Factory Integration

    [Fact]
    public void Factory_PresetTakesPrecedence_OverDefault()
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
                }
            }
        };

        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom(), spawners);

        var state = CreateState();
        var context = SpawnContext.Default;

        // Column 0: preset fires first (priority 600)
        var result = model.Predict(ref state, 0, in context);
        Assert.Equal(ElementType.ColorBomb, result);

        // Column 0: preset exhausted → falls to DefaultCondition
        var result2 = model.Predict(ref state, 0, in context);
        Assert.True(result2.IsColor(), "After preset exhausted, should fall to default color");
    }

    [Fact]
    public void Factory_UnclaimedColumn_UsesDefault()
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
                }
            }
        };

        var model = SpawnConditionFactory.Create(
            6, new LevelObjective[4], 25,
            new SequentialRandom(), new SequentialRandom(), spawners);

        var state = CreateState();
        var context = SpawnContext.Default;

        // Column 5 is unclaimed — should get default color
        var result = model.Predict(ref state, 5, in context);
        Assert.True(result.IsColor());
    }

    #endregion
}
