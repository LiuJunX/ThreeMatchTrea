using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Generation;

/// <summary>
/// BoardInitializer 单元测试
///
/// 职责：
/// - 根据 LevelConfig 初始化棋盘
/// - 随机生成初始棋盘（无即时匹配）
/// </summary>
public class BoardInitializerTests
{
    private class StubRandom : IRandom
    {
        private int _counter = 0;

        public float NextFloat() => 0f;
        public int Next(int max) => _counter++ % max;
        public int Next(int min, int max) => min + (_counter++ % (max - min));
        public void SetState(ulong state) { _counter = (int)state; }
        public ulong GetState() => (ulong)_counter;
    }

    private class StubTileGenerator : ITileGenerator
    {
        private int _counter = 0;
        private readonly ElementType[] _types = { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4 };

        public ElementType GenerateNonMatchingTile(ref GameState state, int x, int y)
        {
            // 简单循环返回不同类型，避免匹配
            return _types[(_counter++ + x + y) % _types.Length];
        }
    }

    private BoardInitializer CreateInitializer()
    {
        return new BoardInitializer(new StubTileGenerator());
    }

    private BoardInitializer CreateInitializerWithRealGenerator(IRandom rng)
    {
        return new BoardInitializer(new StandardTileGenerator(rng));
    }

    #region LevelConfig Initialization Tests

    [Fact]
    public void Initialize_WithLevelConfig_SetsCorrectTiles()
    {
        // Arrange
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        var levelConfig = new LevelConfig
        {
            Width = 3,
            Height = 3,
            Grid = new[]
            {
                ElementType.Item1, ElementType.Item3, ElementType.Item2,
                ElementType.Item4, ElementType.Item5, ElementType.Item6,
                ElementType.Item1, ElementType.Item3, ElementType.Item2
            }
        };

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.Item3, state.GetTile(1, 0).Type);
        Assert.Equal(ElementType.Item2, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.Item4, state.GetTile(0, 1).Type);
        Assert.Equal(ElementType.Item5, state.GetTile(1, 1).Type);
    }

    [Fact]
    public void Initialize_WithLevelConfig_SetsBombs()
    {
        // Arrange
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        var levelConfig = new LevelConfig
        {
            Width = 3,
            Height = 3,
            Grid = new[]
            {
                ElementType.Item1, ElementType.HorizontalRocket, ElementType.Item2,
                ElementType.VerticalRocket, ElementType.Item5, ElementType.Item6,
                ElementType.Item1, ElementType.Item3, ElementType.Square5x5
            }
        };

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert — bombs are stored directly as ElementType in Grid
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.HorizontalRocket, state.GetTile(1, 0).Type);
        Assert.Equal(ElementType.VerticalRocket, state.GetTile(0, 1).Type);
        Assert.Equal(ElementType.Square5x5, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void Initialize_WithLevelConfig_AssignsUniqueIds()
    {
        // Arrange
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        state.NextTileId = 1;
        var levelConfig = new LevelConfig
        {
            Width = 3,
            Height = 3,
            Grid = new ElementType[9]
        };
        for (int i = 0; i < 9; i++) levelConfig.Grid[i] = ElementType.Item1;

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: 每个 tile 应该有唯一 ID
        var ids = new System.Collections.Generic.HashSet<long>();
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var tile = state.GetTile(x, y);
                Assert.True(ids.Add(tile.Id), $"Duplicate ID found at ({x}, {y})");
            }
        }
    }

    #endregion

    #region Random Initialization Tests

    [Fact]
    public void Initialize_WithoutLevelConfig_FillsEntireBoard()
    {
        // Arrange
        var initializer = CreateInitializer();
        var state = new GameState(8, 8, 6, new StubRandom());

        // Act
        initializer.Initialize(ref state, levelConfig: null);

        // Assert: 所有位置应该有有效的 tile
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var tile = state.GetTile(x, y);
                Assert.NotEqual(ElementType.None, tile.Type);
            }
        }
    }

    [Fact]
    public void Initialize_WithoutLevelConfig_NoImmediateMatches()
    {
        // Arrange: 使用真实的 TileGenerator
        var rng = new StubRandom();
        var initializer = CreateInitializerWithRealGenerator(rng);
        var state = new GameState(8, 8, 6, rng);

        // Act
        initializer.Initialize(ref state, levelConfig: null);

        // Assert: 检查没有水平或垂直的三连
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 6; x++) // 检查水平
            {
                var t1 = state.GetTile(x, y).Type;
                var t2 = state.GetTile(x + 1, y).Type;
                var t3 = state.GetTile(x + 2, y).Type;
                if (t1 != ElementType.None && t1 == t2 && t2 == t3)
                {
                    Assert.Fail($"Horizontal match found at ({x}, {y})");
                }
            }
        }

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 6; y++) // 检查垂直
            {
                var t1 = state.GetTile(x, y).Type;
                var t2 = state.GetTile(x, y + 1).Type;
                var t3 = state.GetTile(x, y + 2).Type;
                if (t1 != ElementType.None && t1 == t2 && t2 == t3)
                {
                    Assert.Fail($"Vertical match found at ({x}, {y})");
                }
            }
        }
    }

    #endregion

    #region Cells Array (Board Shape) Tests

    [Fact]
    public void Initialize_WithCellsArray_VoidCellsHaveNoTile()
    {
        // Arrange: 3x3 board with center cell as Void
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        var levelConfig = new LevelConfig(3, 3);
        levelConfig.Cells[4] = CellKind.Void; // center (1,1)

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: Void cell has no tile, others have tiles
        Assert.NotEqual(ElementType.None, state.GetTile(0, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type); // Void → no tile
        Assert.NotEqual(ElementType.None, state.GetTile(0, 2).Type);
    }

    [Fact]
    public void Initialize_NullGrid_WithCells_GeneratesRandomForSlots()
    {
        // Arrange: 3x3, grid=null, cells defines shape
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        var levelConfig = new LevelConfig(3, 3) { Grid = null! };
        // Top-left corner is Void
        levelConfig.Cells[0] = CellKind.Void;

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: Void cell has no tile
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        // Slot cells get random tiles
        Assert.NotEqual(ElementType.None, state.GetTile(1, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void Initialize_CrossShape_OnlySlotCellsHaveTiles()
    {
        // Arrange: 5x5 cross (corners are Void)
        var initializer = CreateInitializer();
        var state = new GameState(5, 5, 6, new StubRandom());
        var cells = new CellKind[]
        {
            CellKind.Void, CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Void,
            CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot,
            CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot,
            CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot,
            CellKind.Void, CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Void,
        };
        var levelConfig = new LevelConfig(5, 5) { Grid = null!, Cells = cells };

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: corners are empty
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(4, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 4).Type);
        Assert.Equal(ElementType.None, state.GetTile(4, 4).Type);
        // center and arms have tiles
        Assert.NotEqual(ElementType.None, state.GetTile(2, 2).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(1, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(3, 4).Type);
    }

    [Fact]
    public void Initialize_VoidCells_SetCellKindCorrectly()
    {
        // Arrange
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());
        var levelConfig = new LevelConfig(3, 3) { Grid = null! };
        levelConfig.Cells[0] = CellKind.Void;
        levelConfig.Cells[8] = CellKind.Void;

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: CellKind propagated to GameState
        Assert.True(state.IsVoid(0, 0));
        Assert.False(state.IsVoid(1, 0));
        Assert.True(state.IsVoid(2, 2));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Initialize_SmallBoard_WorksCorrectly()
    {
        // Arrange: 3x3 最小棋盘
        var initializer = CreateInitializer();
        var state = new GameState(3, 3, 6, new StubRandom());

        // Act
        initializer.Initialize(ref state, levelConfig: null);

        // Assert
        Assert.Equal(3, state.Width);
        Assert.Equal(3, state.Height);
        Assert.NotEqual(ElementType.None, state.GetTile(0, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void Initialize_LevelConfigSmallerThanState_OnlyFillsLevelConfigArea()
    {
        // Arrange: LevelConfig 比 state 小
        var initializer = CreateInitializer();
        var state = new GameState(5, 5, 6, new StubRandom());

        // 先填充一些初始值
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        var levelConfig = new LevelConfig
        {
            Width = 3,
            Height = 3,
            Grid = new ElementType[9]
        };
        for (int i = 0; i < 9; i++) levelConfig.Grid[i] = ElementType.Item1;

        // Act
        initializer.Initialize(ref state, levelConfig);

        // Assert: 只有 3x3 区域被填充
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(2, 2).Type);
        // 超出 LevelConfig 范围的位置保持原样
        Assert.Equal(ElementType.None, state.GetTile(4, 4).Type);
    }

    #endregion
}

