using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

/// <summary>
/// PowerUpHandler 单元测试
///
/// 职责：
/// - 处理特殊移动（彩虹+炸弹组合）
/// - 激活炸弹效果
/// - 清除特定颜色/区域
/// </summary>
public class PowerUpHandlerTests
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

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Match3.Core.Models.Gameplay.MatchGroup match) => 10;
        public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2) => 100;
    }

    private PowerUpHandler CreateHandler()
    {
        return new PowerUpHandler(new StubScoreSystem());
    }

    private GameState CreateFilledState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow };
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
            }
        }
        return state;
    }

    private GameState CreateEmptyState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));
            }
        }
        return state;
    }

    #region ActivateBomb Tests

    [Fact]
    public void ActivateBomb_HorizontalBomb_ClearsRow()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bombTile = new Tile(100, TileType.Red, 3, 3);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(3, 3, bombTile);

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 整行应该被清除
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 3).Type);
        }
    }

    [Fact]
    public void ActivateBomb_VerticalBomb_ClearsColumn()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bombTile = new Tile(100, TileType.Blue, 3, 3);
        bombTile.Bomb = BombType.Vertical;
        state.SetTile(3, 3, bombTile);

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 整列应该被清除
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(TileType.None, state.GetTile(3, y).Type);
        }
    }

    [Fact]
    public void ActivateBomb_Square5x5Bomb_ClearsArea()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bombTile = new Tile(100, TileType.Green, 4, 4);
        bombTile.Bomb = BombType.Square5x5;
        state.SetTile(4, 4, bombTile);

        // Act
        handler.ActivateBomb(ref state, new Position(4, 4));

        // Assert: 5x5 区域应该被清除（半径 2）
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = 4 + dx;
                int y = 4 + dy;
                if (x >= 0 && x < 8 && y >= 0 && y < 8)
                {
                    Assert.Equal(TileType.None, state.GetTile(x, y).Type);
                }
            }
        }
    }

    [Fact]
    public void ActivateBomb_ColorBomb_ClearsMostFrequentColor()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateEmptyState();

        // 设置彩球
        var bombTile = new Tile(100, TileType.Rainbow, 3, 3);
        bombTile.Bomb = BombType.Color;
        state.SetTile(3, 3, bombTile);

        // 放置不同颜色的方块，红色最多 (5个)，蓝色较少 (3个)
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.Red, 0, 2));
        state.SetTile(1, 0, new Tile(4, TileType.Red, 1, 0));
        state.SetTile(1, 1, new Tile(5, TileType.Red, 1, 1));

        state.SetTile(7, 0, new Tile(6, TileType.Blue, 7, 0));
        state.SetTile(7, 1, new Tile(7, TileType.Blue, 7, 1));
        state.SetTile(7, 2, new Tile(8, TileType.Blue, 7, 2));

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 红色（最多的颜色）应该被清除，蓝色保留
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type);
        Assert.Equal(TileType.None, state.GetTile(0, 2).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 1).Type);

        // 蓝色不应该被清除
        Assert.Equal(TileType.Blue, state.GetTile(7, 0).Type);
        Assert.Equal(TileType.Blue, state.GetTile(7, 1).Type);
        Assert.Equal(TileType.Blue, state.GetTile(7, 2).Type);

        // 彩球本身应该被清除
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);
    }

    [Fact]
    public void ActivateBomb_UfoBomb_ClearsRandomTile()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bombTile = new Tile(100, TileType.Yellow, 3, 3);
        bombTile.Bomb = BombType.Ufo;
        state.SetTile(3, 3, bombTile);

        // 记录初始非空 tile 数量
        int initialCount = 0;
        for (int i = 0; i < state.Grid.Length; i++)
            if (state.Grid[i].Type != TileType.None) initialCount++;

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 应该清除炸弹本身和至少一个随机 tile
        int finalCount = 0;
        for (int i = 0; i < state.Grid.Length; i++)
            if (state.Grid[i].Type != TileType.None) finalCount++;

        Assert.True(finalCount < initialCount);
    }

    [Fact]
    public void ActivateBomb_NoBomb_DoesNothing()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        // 普通 tile，没有炸弹

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 应该保持原样（没有 None bomb）
        var tile = state.GetTile(3, 3);
        Assert.NotEqual(TileType.None, tile.Type);
    }

    #endregion

    #region ProcessSpecialMove Tests - Rainbow Combos

    [Fact]
    public void ProcessSpecialMove_RainbowPlusRainbow_ClearsAll()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var rainbow1 = new Tile(100, TileType.Rainbow, 3, 3);
        rainbow1.Bomb = BombType.Color;
        var rainbow2 = new Tile(101, TileType.Rainbow, 4, 3);
        rainbow2.Bomb = BombType.Color;
        state.SetTile(3, 3, rainbow1);
        state.SetTile(4, 3, rainbow2);

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 全部清除
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Equal(TileType.None, state.GetTile(x, y).Type);
            }
        }
    }

    [Fact]
    public void ProcessSpecialMove_RainbowPlusNormalTile_ClearsColor()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var rainbow = new Tile(100, TileType.Rainbow, 3, 3);
        rainbow.Bomb = BombType.Color;
        state.SetTile(3, 3, rainbow);
        // 确保 (4, 3) 是红色
        state.SetTile(4, 3, new Tile(101, TileType.Red, 4, 3));

        // 放置一些红色方块
        state.SetTile(0, 0, new Tile(102, TileType.Red, 0, 0));
        state.SetTile(7, 7, new Tile(103, TileType.Red, 7, 7));

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 红色应该被清除，彩虹和目标位置也被清除
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(7, 7).Type);
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 3).Type);
    }

    [Fact]
    public void ProcessSpecialMove_RainbowPlusBomb_TransformsAndExplodes()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var rainbow = new Tile(100, TileType.Rainbow, 3, 3);
        rainbow.Bomb = BombType.Color;
        state.SetTile(3, 3, rainbow);

        var horizontalBomb = new Tile(101, TileType.Red, 4, 3);
        horizontalBomb.Bomb = BombType.Horizontal;
        state.SetTile(4, 3, horizontalBomb);

        // 放置更多红色方块
        state.SetTile(0, 0, new Tile(102, TileType.Red, 0, 0));
        state.SetTile(1, 1, new Tile(103, TileType.Red, 1, 1));

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 红色方块应该变成炸弹并爆炸
        // 彩虹和炸弹位置被清除
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 3).Type);
    }

    #endregion

    #region ProcessSpecialMove Tests - Bomb Combos

    [Fact]
    public void ProcessSpecialMove_LineBombPlusLineBomb_ClearsRowAndColumn()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var hBomb = new Tile(100, TileType.Red, 3, 3);
        hBomb.Bomb = BombType.Horizontal;
        var vBomb = new Tile(101, TileType.Red, 4, 3);
        vBomb.Bomb = BombType.Vertical;
        state.SetTile(3, 3, hBomb);
        state.SetTile(4, 3, vBomb);

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 行和列应该被清除
        // 检查 y=3 这一行
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 3).Type);
        }
        // 检查 x=4 这一列
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(TileType.None, state.GetTile(4, y).Type);
        }
    }

    [Fact]
    public void ProcessSpecialMove_AreaBombPlusAreaBomb_ClearsLargeArea()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bomb1 = new Tile(100, TileType.Red, 3, 3);
        bomb1.Bomb = BombType.Square5x5;
        var bomb2 = new Tile(101, TileType.Red, 4, 3);
        bomb2.Bomb = BombType.Square5x5;
        state.SetTile(3, 3, bomb1);
        state.SetTile(4, 3, bomb2);

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 两个炸弹位置应该被清除
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 3).Type);
    }

    [Fact]
    public void ProcessSpecialMove_NoBombs_ReturnsZeroPoints()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        // 两个普通 tile

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert
        Assert.Equal(0, points);
    }

    [Fact]
    public void ProcessSpecialMove_TwoHorizontalRockets_ClearsOnlyOneRowAndOneColumn()
    {
        // Arrange: 两个水平火箭交换
        // Bug: 之前会触发重复爆炸，导致消除2行1列
        // Fix: 组合后清除炸弹属性，只消除1行1列
        var handler = CreateHandler();
        var state = CreateFilledState();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        var hBomb1 = new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal };
        var hBomb2 = new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Horizontal };
        state.SetTile(p1.X, p1.Y, hBomb1);
        state.SetTile(p2.X, p2.Y, hBomb2);

        // Act
        handler.ProcessSpecialMove(ref state, p1, p2, out _);

        // Assert: 统计被消除的格子数
        // 十字形 = 1行(8格) + 1列(8格) - 1交点 = 15格
        int clearedCount = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (state.GetTile(x, y).Type == TileType.None)
                    clearedCount++;
            }
        }

        // 期望：15格（1行+1列-交点），而非 23格（2行+1列-2交点）
        Assert.Equal(15, clearedCount);
    }

    [Fact]
    public void ProcessSpecialMove_TwoVerticalRockets_ClearsOnlyOneRowAndOneColumn()
    {
        // Arrange: 两个垂直火箭交换
        var handler = CreateHandler();
        var state = CreateFilledState();
        var p1 = new Position(4, 3);
        var p2 = new Position(4, 4);

        var vBomb1 = new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Vertical };
        var vBomb2 = new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical };
        state.SetTile(p1.X, p1.Y, vBomb1);
        state.SetTile(p2.X, p2.Y, vBomb2);

        // Act
        handler.ProcessSpecialMove(ref state, p1, p2, out _);

        // Assert: 十字形 = 15格
        int clearedCount = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (state.GetTile(x, y).Type == TileType.None)
                    clearedCount++;
            }
        }

        Assert.Equal(15, clearedCount);
    }

    [Fact]
    public void ProcessSpecialMove_HorizontalPlusVerticalRocket_ClearsOnlyOneRowAndOneColumn()
    {
        // Arrange: 水平 + 垂直火箭交换
        var handler = CreateHandler();
        var state = CreateFilledState();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        var hBomb = new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal };
        var vBomb = new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical };
        state.SetTile(p1.X, p1.Y, hBomb);
        state.SetTile(p2.X, p2.Y, vBomb);

        // Act
        handler.ProcessSpecialMove(ref state, p1, p2, out _);

        // Assert: 十字形 = 15格
        int clearedCount = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (state.GetTile(x, y).Type == TileType.None)
                    clearedCount++;
            }
        }

        Assert.Equal(15, clearedCount);

        // 额外验证：确认是正确的行和列被消除
        // 行 y=4 应该全部为 None
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 4).Type);
        }
        // 列 x=4 应该全部为 None
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(TileType.None, state.GetTile(4, y).Type);
        }
    }

    #endregion

    #region Chain Explosion Tests

    [Fact]
    public void ActivateBomb_ChainExplosion_TriggersNeighborBombs()
    {
        // Arrange: 横向火箭会触发同行的纵向火箭
        var handler = CreateHandler();
        var state = CreateFilledState();

        // 在 (3, 3) 放置横向火箭
        var hBomb = new Tile(100, TileType.Red, 3, 3);
        hBomb.Bomb = BombType.Horizontal;
        state.SetTile(3, 3, hBomb);

        // 在 (6, 3) 放置纵向火箭（同一行，会被横向火箭波及）
        var vBomb = new Tile(101, TileType.Blue, 6, 3);
        vBomb.Bomb = BombType.Vertical;
        state.SetTile(6, 3, vBomb);

        // Act
        handler.ActivateBomb(ref state, new Position(3, 3));

        // Assert: 横向火箭消除整行
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 3).Type);
        }
        // 纵向火箭被触发，消除整列（递归连锁爆炸）
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(TileType.None, state.GetTile(6, y).Type);
        }
    }

    [Fact]
    public void ActivateBomb_ChainExplosion_Square5x5TriggersInnerBombs()
    {
        // Arrange: 方块炸弹会触发范围内的横向火箭
        var handler = CreateHandler();
        var state = CreateFilledState();

        // 在 (4, 4) 放置方块炸弹
        var squareBomb = new Tile(100, TileType.Red, 4, 4);
        squareBomb.Bomb = BombType.Square5x5;
        state.SetTile(4, 4, squareBomb);

        // 在 (3, 4) 放置横向火箭（在5x5范围内）
        var hBomb = new Tile(101, TileType.Blue, 3, 4);
        hBomb.Bomb = BombType.Horizontal;
        state.SetTile(3, 4, hBomb);

        // Act
        handler.ActivateBomb(ref state, new Position(4, 4));

        // Assert: 5x5 区域被清除，横向火箭触发后整行 y=4 被清除
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 4).Type);
        }
    }

    [Fact]
    public void ActivateBomb_ChainExplosion_ThreeLevelChain()
    {
        // Arrange: 三级连锁 - H火箭 → V火箭 → 方块炸弹
        var handler = CreateHandler();
        var state = CreateFilledState();

        // 在 (2, 3) 放置横向火箭
        var hBomb = new Tile(100, TileType.Red, 2, 3);
        hBomb.Bomb = BombType.Horizontal;
        state.SetTile(2, 3, hBomb);

        // 在 (5, 3) 放置纵向火箭（同一行）
        var vBomb = new Tile(101, TileType.Blue, 5, 3);
        vBomb.Bomb = BombType.Vertical;
        state.SetTile(5, 3, vBomb);

        // 在 (5, 6) 放置方块炸弹（同一列）
        var squareBomb = new Tile(102, TileType.Green, 5, 6);
        squareBomb.Bomb = BombType.Square5x5;
        state.SetTile(5, 6, squareBomb);

        // Act
        handler.ActivateBomb(ref state, new Position(2, 3));

        // Assert:
        // 1. 横向火箭消除 y=3 整行
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(TileType.None, state.GetTile(x, 3).Type);
        }
        // 2. 纵向火箭被触发，消除 x=5 整列
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(TileType.None, state.GetTile(5, y).Type);
        }
        // 3. 方块炸弹被触发，消除 (5,6) 周围的 5x5 区域
        // 检查 (4, 5) 应该被清除（在方块炸弹范围内）
        Assert.Equal(TileType.None, state.GetTile(4, 5).Type);
        Assert.Equal(TileType.None, state.GetTile(6, 7).Type);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ActivateBomb_AtBoardEdge_HandlesGracefully()
    {
        // Arrange: 炸弹在边角
        var handler = CreateHandler();
        var state = CreateFilledState();
        var bombTile = new Tile(100, TileType.Red, 0, 0);
        bombTile.Bomb = BombType.Square5x5;
        state.SetTile(0, 0, bombTile);

        // Act & Assert: 不应该抛出异常
        var ex = Record.Exception(() => handler.ActivateBomb(ref state, new Position(0, 0)));
        Assert.Null(ex);
    }

    [Fact]
    public void ProcessSpecialMove_RainbowPlusNone_DoesNothing()
    {
        // Arrange: Rainbow + None tile
        var handler = CreateHandler();
        var state = CreateEmptyState();
        var rainbow = new Tile(100, TileType.Rainbow, 3, 3);
        rainbow.Bomb = BombType.Color;
        state.SetTile(3, 3, rainbow);
        // (4, 3) 是 None

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out int points);

        // Assert: 不应该崩溃
        Assert.Equal(0, points);
    }

    [Fact]
    public void ProcessSpecialMove_RainbowPlusRainbow_ClearsSourcePositions()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateFilledState();
        var rainbow1 = new Tile(100, TileType.Rainbow, 3, 3);
        rainbow1.Bomb = BombType.Color;
        var rainbow2 = new Tile(101, TileType.Rainbow, 4, 3);
        rainbow2.Bomb = BombType.Color;
        state.SetTile(3, 3, rainbow1);
        state.SetTile(4, 3, rainbow2);

        // Act
        handler.ProcessSpecialMove(ref state, new Position(3, 3), new Position(4, 3), out _);

        // Assert: 源位置应该被清除
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 3).Type);
    }

    #endregion
}
