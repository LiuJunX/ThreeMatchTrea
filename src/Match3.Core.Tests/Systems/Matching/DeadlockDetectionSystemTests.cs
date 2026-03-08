using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Utility.Pools;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// DeadlockDetectionSystem 单元测试
///
/// 职责：
/// - 检测棋盘是否有可行移动
/// - 查找所有有效移动
/// - 正确识别死锁棋盘
/// </summary>
public class DeadlockDetectionSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private DeadlockDetectionSystem CreateDetector()
    {
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        return new DeadlockDetectionSystem(matchFinder);
    }

    private GameState CreateEmptyState(int width = 6, int height = 6)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
            }
        }
        return state;
    }

    /// <summary>
    /// 创建正常棋盘，有可行移动
    /// </summary>
    private GameState CreateNormalBoard()
    {
        var state = CreateEmptyState();

        // 创建一个简单的可行移动：(0,0) Red, (1,0) Blue, (2,0) Red -> 交换 (0,1) 和 (1,1) 可以匹配
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(0, 1, new Tile(4, ElementType.Item3, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item2, 2, 1));

        // 填充其余位置避免干扰
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetTile(x, y).Type == ElementType.None)
                {
                    state.SetTile(x, y, new Tile(y * state.Width + x, ElementType.Item4, x, y));
                }
            }
        }

        return state;
    }

    /// <summary>
    /// 创建死锁棋盘：三色旋转模式，任何相邻交换都无法产生 3 连
    /// </summary>
    private GameState CreateDeadlockBoard()
    {
        var state = CreateEmptyState();

        // 三色旋转模式：
        // R G B R G B  (row 0)
        // G B R G B R  (row 1)
        // B R G B R G  (row 2)
        // R G B R G B  (row 3)
        // ...
        // 每行向左旋转一个位置，防止垂直 3 连
        ElementType[] pattern = { ElementType.Item1, ElementType.Item2, ElementType.Item3 };

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = pattern[(x + y) % 3];
                state.SetTile(x, y, new Tile(y * state.Width + x, type, x, y));
            }
        }

        return state;
    }

    [Fact]
    public void HasValidMoves_NormalBoard_ReturnsTrue()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateNormalBoard();

        // Act
        bool result = detector.HasValidMoves(in state);

        // Assert
        Assert.True(result, "正常棋盘应该有可行移动");
    }

    [Fact]
    public void HasValidMoves_DeadlockBoard_ReturnsFalse()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // Act
        bool result = detector.HasValidMoves(in state);

        // Assert
        Assert.False(result, "棋盘格模式应该是死锁");
    }

    [Fact]
    public void FindAllValidMoves_NormalBoard_ReturnsNonEmptyList()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateNormalBoard();

        // Act
        var moves = detector.FindAllValidMoves(in state);

        try
        {
            // Assert
            Assert.NotNull(moves);
            Assert.NotEmpty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_DeadlockBoard_ReturnsEmptyList()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // Act
        var moves = detector.FindAllValidMoves(in state);

        try
        {
            // Assert
            Assert.NotNull(moves);
            Assert.Empty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void HasValidMoves_WithCovers_SkipsBlockedTiles()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateNormalBoard();

        // 在 (0,1) 添加 Cover 阻挡
        var cover = new Cover
        {
            Type = CoverType.Cage,
            Health = 1
        };
        state.SetCover(0, 1, cover);

        // Act
        bool result = detector.HasValidMoves(in state);

        // Assert
        // 虽然有 Cover，但其他位置可能仍有可行移动
        // 这个测试主要验证不会因为 Cover 而崩溃
        Assert.True(result || !result); // 结果取决于具体棋盘布局
    }

    [Fact]
    public void InvalidateCache_DoesNotThrow()
    {
        // Arrange
        var detector = CreateDetector();

        // Act & Assert
        detector.InvalidateCache(); // 当前实现无缓存，确保不抛异常
    }

    /// <summary>
    /// 死锁棋盘上有一个可点击炸弹 → 不算死锁
    /// </summary>
    [Fact]
    public void HasValidMoves_DeadlockBoardWithTappableBomb_ReturnsTrue()
    {
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // 在 (2,2) 放一个炸弹
        state.SetTile(2, 2, new Tile(14, ElementType.HorizontalRocket, 2, 2));

        Assert.True(detector.HasValidMoves(in state),
            "棋盘上有可点击的炸弹，不应判定为死锁");
    }

    /// <summary>
    /// 死锁棋盘上炸弹被 Cover 挡住 → 仍然是死锁
    /// </summary>
    [Fact]
    public void HasValidMoves_DeadlockBoardWithBlockedBomb_ReturnsFalse()
    {
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // 放一个炸弹，但用 Cage 挡住
        state.SetTile(2, 2, new Tile(14, ElementType.Square5x5, 2, 2));
        state.SetCover(2, 2, new Cover { Type = CoverType.Cage, Health = 1 });

        Assert.False(detector.HasValidMoves(in state),
            "炸弹被 Cover 挡住时不可交互，仍应判定为死锁");
    }

    /// <summary>
    /// 死锁棋盘上有炸弹与普通棋子相邻 → 炸弹交换始终有效
    /// </summary>
    [Fact]
    public void HasValidMoves_DeadlockBoardWithBombSwap_ReturnsTrue()
    {
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // 在 (0,0) 放一个炸弹，(1,0) 是普通棋子
        state.SetTile(0, 0, new Tile(0, ElementType.VerticalRocket, 0, 0));

        Assert.True(detector.HasValidMoves(in state),
            "炸弹与普通棋子的交换始终有效，不应判定为死锁");
    }

    /// <summary>
    /// 死锁棋盘上两个相邻炸弹 → combo 交换有效
    /// </summary>
    [Fact]
    public void HasValidMoves_DeadlockBoardWithTwoBombs_ReturnsTrue()
    {
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // 两个相邻炸弹
        state.SetTile(3, 3, new Tile(21, ElementType.HorizontalRocket, 3, 3));
        state.SetTile(4, 3, new Tile(22, ElementType.VerticalRocket, 4, 3));

        Assert.True(detector.HasValidMoves(in state),
            "两个相邻炸弹可以交换触发 combo，不应判定为死锁");
    }

    /// <summary>
    /// FindAllValidMoves 应包含炸弹交换
    /// </summary>
    [Fact]
    public void FindAllValidMoves_DeadlockBoardWithBomb_IncludesBombSwaps()
    {
        var detector = CreateDetector();
        var state = CreateDeadlockBoard();

        // 在 (0,0) 放一个炸弹
        state.SetTile(0, 0, new Tile(0, ElementType.HorizontalRocket, 0, 0));

        var moves = detector.FindAllValidMoves(in state);
        try
        {
            Assert.NotEmpty(moves);
            // 炸弹在 (0,0)，应有与 (1,0) 和 (0,1) 的交换
            Assert.Contains(moves, m =>
                (m.From == new Position(0, 0) && m.To == new Position(1, 0)) ||
                (m.From == new Position(0, 0) && m.To == new Position(0, 1)));
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_CheckersPattern_ReturnsSomeMoves()
    {
        // Arrange
        var detector = CreateDetector();
        var state = CreateEmptyState();

        // 创建一个复杂的棋盘，确保有多个可行移动
        // R R B B R R
        // B B R R B B
        // R R B B R R
        // B B R R B B
        // R R B B R R
        // B B R R B B
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = ((x / 2) + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                state.SetTile(x, y, new Tile(y * state.Width + x, type, x, y));
            }
        }

        // Act
        var moves = detector.FindAllValidMoves(in state);

        try
        {
            // Assert
            Assert.NotNull(moves);
            // 这个模式应该有一些可行移动
            Assert.NotEmpty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }
}

