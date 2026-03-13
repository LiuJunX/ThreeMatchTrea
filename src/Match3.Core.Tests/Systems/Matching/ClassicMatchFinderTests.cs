using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Tests.TestFixtures;
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
    private ClassicMatchFinder CreateMatchFinder()
    {
        var bombGenerator = new BombGenerator();
        return new ClassicMatchFinder(bombGenerator);
    }

    private GameState CreateEmptyState(int width = 8, int height = 8)
    {
        return GameStateBuilder.CreateEmptyState(width, height);
    }

    #region HasMatchAt Tests

    [Fact]
    public void HasMatchAt_HorizontalMatch_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));
        state.SetTile(0, 2, new Tile(3, ElementType.Item3, 0, 2));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
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
        state.SetTile(0, 0, new Tile(1, ElementType.ColorBomb, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.ColorBomb, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.ColorBomb, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
    }

    [Fact]
    public void HasMatchAt_BombTile_ReturnsFalse()
    {
        // Arrange: Bomb tiles (HorizontalRocket etc.) do NOT participate in color matching
        // Their Type is the bomb ElementType, not a color
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.HorizontalRocket, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert: Bomb tile does not match with color tiles
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
    }

    [Fact]
    public void HasMatchAt_FourInARow_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item4, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item4, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item4, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item4, 3, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(2, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(3, 0)));
    }

    [Fact]
    public void HasMatchAt_2x2Square_ReturnsTrue()
    {
        // Arrange: 2x2 方块应该被识别为有效匹配
        // A A
        // A A
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(0, 1, new Tile(3, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(4, ElementType.Item1, 1, 1));

        var finder = CreateMatchFinder();

        // Act & Assert: 所有4个位置都应该返回true
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)), "Top-left should match");
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)), "Top-right should match");
        Assert.True(finder.HasMatchAt(in state, new Position(0, 1)), "Bottom-left should match");
        Assert.True(finder.HasMatchAt(in state, new Position(1, 1)), "Bottom-right should match");
    }

    [Fact]
    public void HasMatchAt_2x2SquareAfterSwap_ReturnsTrue()
    {
        // Arrange: 用户报告的bug场景
        // A A
        // A B A  <- 交换 B 和右边的 A 后形成 2x2
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(0, 1, new Tile(3, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(4, ElementType.Item1, 1, 1)); // 交换后这里是A
        state.SetTile(2, 1, new Tile(5, ElementType.Item3, 2, 1)); // 交换后这里是B

        var finder = CreateMatchFinder();

        // Act & Assert: 交换位置 (1,1) 应该识别为有效匹配
        Assert.True(finder.HasMatchAt(in state, new Position(1, 1)), "Swapped position should form 2x2 match");
    }

    [Fact]
    public void HasMatchAt_2x2SquareAtCorner_ReturnsTrue()
    {
        // Arrange: 2x2 在棋盘右下角
        var state = CreateEmptyState(8, 8);
        state.SetTile(6, 6, new Tile(1, ElementType.Item2, 6, 6));
        state.SetTile(7, 6, new Tile(2, ElementType.Item2, 7, 6));
        state.SetTile(6, 7, new Tile(3, ElementType.Item2, 6, 7));
        state.SetTile(7, 7, new Tile(4, ElementType.Item2, 7, 7));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(7, 7)), "Corner 2x2 should match");
    }

    [Fact]
    public void HasMatchAt_2x2SquareAtTopLeft_ReturnsTrue()
    {
        // Arrange: 2x2 在棋盘左上角
        var state = CreateEmptyState(8, 8);
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(0, 1, new Tile(3, ElementType.Item3, 0, 1));
        state.SetTile(1, 1, new Tile(4, ElementType.Item3, 1, 1));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)), "Top-left corner 2x2 should match");
    }

    [Fact]
    public void HasMatchAt_Incomplete2x2_ReturnsFalse()
    {
        // Arrange: 只有3个相同颜色，不足以形成2x2
        // A A
        // A B
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(0, 1, new Tile(3, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(4, ElementType.Item3, 1, 1)); // 不同颜色

        var finder = CreateMatchFinder();

        // Act & Assert: 没有3连也没有2x2，应该返回false
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(0, 1)));
    }

    #endregion

    #region HasMatches Tests

    [Fact]
    public void HasMatches_WithMatch_ReturnsTrue()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatches(in state));
    }

    [Fact]
    public void HasMatches_NoMatch_ReturnsFalse()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.Single(groups);
        Assert.Equal(ElementType.Item1, groups[0].Type);
        Assert.Equal(3, groups[0].Positions.Count);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_TwoSeparateMatches_ReturnsTwoGroups()
    {
        // Arrange
        var state = CreateEmptyState();
        // 第一组：红色水平
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // 第二组：蓝色水平
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(5, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(6, ElementType.Item3, 2, 2));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));
        // 垂直部分
        state.SetTile(0, 1, new Tile(4, ElementType.Item2, 0, 1));
        state.SetTile(0, 2, new Tile(5, ElementType.Item2, 0, 2));

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
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

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
            state.SetTile(x, 0, new Tile(x + 1, ElementType.Item5, x, 0));
        }

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        // 5连应该生成 Color 炸弹
        Assert.Contains(groups, g => g.SpawnBombType != ElementType.None);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_FourInARow_GeneratesLineBomb()
    {
        // Arrange: 4连应该生成线炸弹
        var state = CreateEmptyState();
        for (int x = 0; x < 4; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, ElementType.Item6, x, 0));
        }

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert
        Assert.NotEmpty(groups);
        // 4连水平应该生成 Horizontal 或 Vertical 炸弹
        var bombGroup = groups.FirstOrDefault(g => g.SpawnBombType != ElementType.None);
        Assert.NotNull(bombGroup);
        Assert.True(bombGroup.SpawnBombType == ElementType.HorizontalRocket ||
                    bombGroup.SpawnBombType == ElementType.VerticalRocket);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FindMatchGroups_SmallBoard_WorksCorrectly()
    {
        // Arrange: 3x3 最小棋盘
        var state = CreateEmptyState(3, 3);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

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
            state.SetTile(x, 0, new Tile(x + 1, ElementType.Item1, x, 0));
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
        state.SetTile(5, 0, new Tile(1, ElementType.Item1, 5, 0));
        state.SetTile(6, 0, new Tile(2, ElementType.Item1, 6, 0));
        state.SetTile(7, 0, new Tile(3, ElementType.Item1, 7, 0));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(7, 0)));
    }

    #endregion

    #region Cover Blocking Tests

    [Fact]
    public void HasMatchAt_CageBlocksMatch_ReturnsFalse()
    {
        // Arrange: 三连中间有 Cage，CanMatch=false，匹配不成立
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        // Cage on middle tile blocks matching
        state.SetCover(new Position(1, 0), new Cover(CoverType.Cage, 1));

        var finder = CreateMatchFinder();

        // Act & Assert: Cage 打断三连
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(2, 0)));
    }

    [Fact]
    public void HasMatchAt_ChainAllowsMatch_ReturnsTrue()
    {
        // Arrange: Chain 不阻断匹配（BlocksMatch=false）
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        // Chain on middle tile — does NOT block matching
        state.SetCover(new Position(1, 0), new Cover(CoverType.Chain, 1));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(2, 0)));
    }

    [Fact]
    public void HasMatchAt_CageBlocksVerticalMatch_ReturnsFalse()
    {
        // Arrange: 垂直三连中间有 Cage
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));
        state.SetTile(0, 2, new Tile(3, ElementType.Item3, 0, 2));

        state.SetCover(new Position(0, 1), new Cover(CoverType.Cage, 1));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.False(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.False(finder.HasMatchAt(in state, new Position(0, 1)));
        Assert.False(finder.HasMatchAt(in state, new Position(0, 2)));
    }

    [Fact]
    public void HasMatchAt_BubbleAllowsMatch_ReturnsTrue()
    {
        // Arrange: Bubble（动态）不阻断匹配
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

        state.SetCover(new Position(1, 0), new Cover(CoverType.Bubble, 1, true));

        var finder = CreateMatchFinder();

        // Act & Assert
        Assert.True(finder.HasMatchAt(in state, new Position(0, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(1, 0)));
        Assert.True(finder.HasMatchAt(in state, new Position(2, 0)));
    }

    [Fact]
    public void FindMatchGroups_CageBreaksMatch_ExcludesLockedTile()
    {
        // Arrange: 四连中第 2 个有 Cage，变成两段各 1+2，都不够三连
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item1, 3, 0));

        // Cage on (1,0) breaks the chain: [0] | [2,3] — neither is 3+
        state.SetCover(new Position(1, 0), new Cover(CoverType.Cage, 1));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert: no match groups found
        Assert.Empty(groups);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    [Fact]
    public void FindMatchGroups_CageOnEdge_DoesNotAffectAdjacentMatch()
    {
        // Arrange: Cage 在 (0,0)，相邻 (1,0)-(2,0)-(3,0) 三连不受影响
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item1, 3, 0));

        // Cage only on (0,0)
        state.SetCover(new Position(0, 0), new Cover(CoverType.Cage, 1));

        var finder = CreateMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);

        // Assert: (1,0)-(2,0)-(3,0) 仍是有效三连
        Assert.Single(groups);
        Assert.Equal(3, groups[0].Positions.Count);
        Assert.DoesNotContain(new Position(0, 0), groups[0].Positions);

        ClassicMatchFinder.ReleaseGroups(groups);
    }

    #endregion
}


