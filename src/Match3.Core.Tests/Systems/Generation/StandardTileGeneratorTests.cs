using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
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
    private class StubRandom : IRandom
    {
        private int _returnValue;

        public StubRandom(int returnValue = 0)
        {
            _returnValue = returnValue;
        }

        public void SetReturnValue(int value) => _returnValue = value;

        public float NextFloat() => 0f;
        public int Next(int max) => _returnValue % max;
        public int Next(int min, int max) => min + (_returnValue % (max - min));
        public void SetState(ulong state) { _returnValue = (int)state; }
        public ulong GetState() => (ulong)_returnValue;
    }

    private class SequentialRandom : IRandom
    {
        private int _counter = 0;

        public float NextFloat() => 0f;
        public int Next(int max) => _counter++ % max;
        public int Next(int min, int max) => min + (_counter++ % (max - min));
        public void SetState(ulong state) { _counter = (int)state; }
        public ulong GetState() => (ulong)_counter;
    }

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, TileType.None, x, y));
            }
        }
        return state;
    }

    #region Basic Generation Tests

    [Fact]
    public void GenerateNonMatchingTile_EmptyPosition_ReturnsTile()
    {
        // Arrange
        var generator = new StandardTileGenerator(new StubRandom(0));
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert
        Assert.NotEqual(TileType.None, type);
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
            TileType.Red, TileType.Green, TileType.Blue,
            TileType.Yellow, TileType.Purple, TileType.Orange
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
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));

        // Act: 在 (2, 0) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert: 不应该是 Red（会形成三连）
        Assert.NotEqual(TileType.Red, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AvoidVerticalMatch()
    {
        // Arrange: 上面两个相同
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, TileType.Blue, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Blue, 0, 1));

        // Act: 在 (0, 2) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 0, 2);

        // Assert: 不应该是 Blue（会形成三连）
        Assert.NotEqual(TileType.Blue, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_NoMatchIfOnlyOneSameNeighbor()
    {
        // Arrange: 左边只有一个相同的
        var generator = new StandardTileGenerator(new StubRandom(0)); // 总是返回 Red
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0)); // 不同颜色

        // Act: 在 (2, 0) 生成
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert: 可以是 Red（只有一个相邻，不会形成三连）
        Assert.Equal(TileType.Red, type);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GenerateNonMatchingTile_AtOrigin_NoNeighborCheck()
    {
        // Arrange: 在 (0, 0)，没有左边或上面的邻居
        var generator = new StandardTileGenerator(new StubRandom(0));
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该能正常生成
        Assert.NotEqual(TileType.None, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AtFirstRow_OnlyCheckLeftNeighbors()
    {
        // Arrange: 在第一行，只检查左边
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 2, 0);

        // Assert
        Assert.NotEqual(TileType.Green, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_AtFirstColumn_OnlyCheckTopNeighbors()
    {
        // Arrange: 在第一列，只检查上面
        var generator = new StandardTileGenerator(new SequentialRandom());
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, TileType.Yellow, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Yellow, 0, 1));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 2);

        // Assert
        Assert.NotEqual(TileType.Yellow, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_LimitedTileTypes_StillWorks()
    {
        // Arrange: 只有 2 种方块类型
        var rng = new SequentialRandom();
        var state = new GameState(3, 3, 2, rng); // 只有 2 种
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));

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
    public void GenerateNonMatchingTile_ZeroTileTypes_ReturnsNone()
    {
        // Arrange: 0 种方块类型（边界情况）
        var rng = new SequentialRandom();
        var state = new GameState(3, 3, 0, rng); // 0 种
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));

        var generator = new StandardTileGenerator(rng);

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert
        Assert.Equal(TileType.None, type);
    }

    #endregion

    #region RNG Usage Tests

    [Fact]
    public void GenerateNonMatchingTile_UsesProvidedRng()
    {
        // Arrange: 使用固定返回值的 RNG
        // _colors 数组: [0]=Red, [1]=Green, [2]=Blue, [3]=Yellow, [4]=Purple, [5]=Orange
        var rng = new StubRandom(2); // 返回索引 2 -> Blue
        var generator = new StandardTileGenerator(rng);
        var state = CreateState();

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该返回 Blue（索引 2）
        Assert.Equal(TileType.Blue, type);
    }

    [Fact]
    public void GenerateNonMatchingTile_FallbackToStateRng_WhenNoRngProvided()
    {
        // Arrange: 不提供 RNG
        var generator = new StandardTileGenerator();
        var stateRng = new StubRandom(1); // 返回索引 1 -> Blue
        var state = new GameState(3, 3, 6, stateRng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));

        // Act
        var type = generator.GenerateNonMatchingTile(ref state, 0, 0);

        // Assert: 应该使用 state 的 RNG
        Assert.NotEqual(TileType.None, type);
    }

    #endregion
}
