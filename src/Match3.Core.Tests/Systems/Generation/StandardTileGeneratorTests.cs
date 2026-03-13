using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Generation;

/// <summary>
/// StandardTileGenerator 单元测试
///
/// 职责：
/// - 生成不会立即形成匹配的方块
/// - 随机选择方块类型
/// </summary>
public class StandardTileGeneratorTests
{

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
            }
        }
        return state;
    }

    #region Basic Generation Tests

    [Fact]
    public void GenerateNonMatchingTile_EmptyPosition_ReturnsTile()
    {
        // Arrange
        var generator = new StandardTileGenerator(StubRandom.WithFixedValue(0));
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert
        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_ReturnsValidColorType()
    {
        // Arrange
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该是有效的颜色类型
        var validTypes = new[]
        {
            ElementType.Item1, ElementType.Item2, ElementType.Item3,
            ElementType.Item4, ElementType.Item5, ElementType.Item6
        };
        Assert.Contains(type, validTypes);
    }

    #endregion

    #region Avoid Immediate Match Tests

    [Fact]
    public void GenerateNonMatchingTile_AvoidHorizontalMatch()
    {
        // Arrange: 左边两个相同
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        // Act: 在 (2, 0) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert: 不应该是 Red（会形成三连）
        Assert.NotEqual(ElementType.Item1, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AvoidVerticalMatch()
    {
        // Arrange: 上面两个相同
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));

        // Act: 在 (0, 2) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 0, 2);

        // Assert: 不应该是 Blue（会形成三连）
        Assert.NotEqual(ElementType.Item3, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_NoMatchIfOnlyOneSameNeighbor()
    {
        // Arrange: 左边只有一个相同的
        var generator = new StandardTileGenerator(StubRandom.WithFixedValue(0)); // 总是返回 Red
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0)); // 不同颜色

        // Act: 在 (2, 0) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert: 可以是 Red（只有一个相邻，不会形成三连）
        Assert.Equal(ElementType.Item1, type);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GenerateNonMatchingTile_AtOrigin_NoNeighborCheck()
    {
        // Arrange: 在 (0, 0)，没有左边或上面的邻居
        var generator = new StandardTileGenerator(StubRandom.WithFixedValue(0));
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该能正常生成
        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AtFirstRow_OnlyCheckLeftNeighbors()
    {
        // Arrange: 在第一行，只检查左边
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert
        Assert.NotEqual(ElementType.Item2, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AtFirstColumn_OnlyCheckTopNeighbors()
    {
        // Arrange: 在第一列，只检查上面
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item4, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item4, 0, 1));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 2);

        // Assert
        Assert.NotEqual(ElementType.Item4, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_LimitedElementTypes_StillWorks()
    {
        // Arrange: 只有 2 种方块类型
        var rng = new SequentialRandom();
        var state = new GameState(3, 3, 2, rng); // 只有 2 种
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, ElementType.None, x, y));

        var generator = new StandardTileGenerator(rng);

        // Act & Assert: 不应该抛出异常
        var ex = Record.Exception(() =>
        {
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    var type = generator.GenerateNonMatchingTile(ref state, x, y);
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                }
            }
        });
        Assert.Null(ex);
    }

    [Fact]
    public void GenerateNonMatchingTile_ZeroElementTypes_ReturnsNone()
    {
        // Arrange: 0 种方块类型（边界情况）
        var rng = new SequentialRandom();
        var state = new GameState(3, 3, 0, rng); // 0 种
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, ElementType.None, x, y));

        var generator = new StandardTileGenerator(rng);

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert
        Assert.Equal(ElementType.None, type);
    }

    #endregion

    #region RNG Usage Tests

    [Fact]
    public void GenerateNonMatchingTile_UsesProvidedRng()
    {
        // Arrange: 使用固定返回值的 RNG
        // _colors 数组: [0]=Red, [1]=Green, [2]=Blue, [3]=Yellow, [4]=Purple, [5]=Orange
        var rng = StubRandom.WithFixedValue(2); // 返回索引 2 -> Blue
        var generator = new StandardTileGenerator(rng);
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该返回 Blue（索引 2）
        Assert.Equal(ElementType.Item3, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_FallbackToStateRng_WhenNoRngProvided()
    {
        // Arrange: 不提供 RNG
        var generator = new StandardTileGenerator();
        var stateRng = StubRandom.WithFixedValue(1); // 返回索引 1 -> Blue
        var state = new GameState(3, 3, 6, stateRng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, ElementType.None, x, y));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该使用 state 的 RNG
        Assert.NotEqual(ElementType.None, type);
    }

    #endregion
}

