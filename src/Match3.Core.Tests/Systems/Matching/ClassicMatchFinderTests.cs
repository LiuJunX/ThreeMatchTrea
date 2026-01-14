using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// ClassicMatchFinder 单元测试
///
/// 职责：
/// - 检测棋盘上的三消匹配
/// - 查找所有匹配组
/// - 判断特定位置是否有匹配
/// </summary>
public class ClassicMatchFinderTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private ClassicMatchFinder CreateMatchFinder()
    {
        var bombGenerator = new BombGenerator();
        return new ClassicMatchFinder(bombGenerator);
    }

    private GameState CreateEmptyState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, TileType.None, x, y));
            }
        }
        return state;
    }

    #region HasMatchAt Tests

    [Fact]
    public void HasMatchAt_HorizontalMatch_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(2, 0)));
    }

    [Fact]
    public void HasMatchAt_VerticalMatch_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Blue, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Blue, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.Blue, 0, 2));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(0, 1)));
        Assert.True(finder.HasMatchAt(in state, new Position(0, 2)));
    }

    [Fact]
    public void HasMatchAt_NoMatch_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(2, 0)));
    }

    [Fact]
    public void HasMatchAt_TwoTilesOnly_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        // 只有两个相同的，不足以形成匹配

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(1, 0)));
    }

    [Fact]
    public void HasMatchAt_EmptyTile_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
    }

    [Fact]
    public void HasMatchAt_RainbowTile_ReturnsFalse()
    {
        // Arrange: Rainbow 类型不参与普通匹配
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Rainbow, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Rainbow, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Rainbow, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
    }

    [Fact]
    public void HasMatchAt_BombTile_ReturnsFalse()
    {
        // Arrange: Bomb 类型不参与普通匹配
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Bomb, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Bomb, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Bomb, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
    }

    [Fact]
    public void HasMatchAt_FourInARow_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Yellow, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Yellow, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Yellow, 2, 0));
        state.SetTile(3, 0, new Tile(4, TileType.Yellow, 3, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(2, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(3, 0)));
    }

    #endregion

    #region HasMatches Tests

    [Fact]
    public void HasMatches_WithMatch_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatches(in state));
    }

    [Fact]
    public void HasMatches_NoMatch_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatches(in state));
    }

    [Fact]
    public void HasMatches_EmptyBoard_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatches(in state));
    }

    #endregion

    #region FindMatchGroups Tests

    [Fact]
    public void FindMatchGroups_SingleHorizontalMatch_ReturnsOneGroup()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.Single(groups);
        Assert.Equal(TileType.Red, groups[0].Type);
        Assert.Equal(3, groups[0].Positions.Count);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_TwoSeparateMatches_ReturnsTwoGroups()
    {
        // Arrange
        var state = CreateEmptyState();
        // 第一组：红色水平
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));
        // 第二组：蓝色水平
        state.SetTile(0, 2, new Tile(4, TileType.Blue, 0, 2));
        state.SetTile(1, 2, new Tile(5, TileType.Blue, 1, 2));
        state.SetTile(2, 2, new Tile(6, TileType.Blue, 2, 2));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.Equal(2, groups.Count);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_LShapeMatch_ReturnsCorrectGroup()
    {
        // Arrange: L形状（3+3）
        var state = CreateEmptyState();
        // 水平部分
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));
        // 垂直部分
        state.SetTile(0, 1, new Tile(4, TileType.Green, 0, 1));
        state.SetTile(0, 2, new Tile(5, TileType.Green, 0, 2));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        // L形应该产生炸弹或特殊匹配
        var totalPositions = groups.SelectMany(g => g.Positions).Distinct().Count();
        Assert.Equal(5, totalPositions);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_NoMatch_ReturnsEmptyList()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.Empty(groups);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_FiveInARow_GeneratesBomb()
    {
        // Arrange: 5连应该生成炸弹
        var state = CreateEmptyState();
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Purple, x, 0));
        }

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        // 5连应该生成 Color 炸弹
        Assert.Contains(groups, g => g.SpawnBombType != BombType.None);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_FourInARow_GeneratesLineBomb()
    {
        // Arrange: 4连应该生成线炸弹
        var state = CreateEmptyState();
        for (int x = 0; x < 4; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Orange, x, 0));
        }

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        // 4连水平应该生成 Horizontal 或 Vertical 炸弹
        var bombGroup = groups.FirstOrDefault(g => g.SpawnBombType != BombType.None);
        Assert.NotNull(bombGroup);
        Assert.True(bombGroup.SpawnBombType == BombType.Horizontal ||
                    bombGroup.SpawnBombType == BombType.Vertical);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FindMatchGroups_SmallBoard_WorksCorrectly()
    {
        // Arrange: 3x3 最小棋盘
        var state = CreateEmptyState(3, 3);
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.Single(groups);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_FullRowMatch_WorksCorrectly()
    {
        // Arrange: 整行相同
        var state = CreateEmptyState(8, 8);
        for (int x = 0; x < 8; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Red, x, 0));
        }

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        var totalPositions = groups.SelectMany(g => g.Positions).Distinct().Count();
        Assert.Equal(8, totalPositions);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void HasMatchAt_BoundaryPosition_WorksCorrectly()
    {
        // Arrange: 边界位置的匹配
        var state = CreateEmptyState(8, 8);
        // 右边界
        state.SetTile(5, 0, new Tile(1, TileType.Red, 5, 0));
        state.SetTile(6, 0, new Tile(2, TileType.Red, 6, 0));
        state.SetTile(7, 0, new Tile(3, TileType.Red, 7, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(7, 0)));
    }

    #endregion
}
