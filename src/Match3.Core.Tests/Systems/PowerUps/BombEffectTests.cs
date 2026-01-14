using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

/// <summary>
/// BombEffect 单元测试
///
/// 覆盖所有炸弹效果实现：
/// - HorizontalRocketEffect: 清除整行
/// - VerticalRocketEffect: 清除整列
/// - SquareBombEffect: 清除 5x5 区域
/// - ColorBombEffect: 清除出现最多的颜色
/// - UfoEffect: 随机清除一个方块
/// </summary>
public class BombEffectTests
{
    private class StubRandom : IRandom
    {
        private int _returnValue = 0;

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

    #region HorizontalRocketEffect Tests

    [Fact]
    public void HorizontalRocket_Type_IsHorizontal()
    {
        var effect = new HorizontalRocketEffect();
        Assert.Equal(BombType.Horizontal, effect.Type);
    }

    [Fact]
    public void HorizontalRocket_Apply_ClearsEntireRow()
    {
        // Arrange
        var effect = new HorizontalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(3, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该包含整行 (y=4)
        Assert.Equal(8, affected.Count);
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 4), affected);
        }
    }

    [Fact]
    public void HorizontalRocket_Apply_AtTopRow()
    {
        // Arrange
        var effect = new HorizontalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(5, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert
        Assert.Equal(8, affected.Count);
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 0), affected);
        }
    }

    [Fact]
    public void HorizontalRocket_Apply_AtBottomRow()
    {
        // Arrange
        var effect = new HorizontalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 7);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert
        Assert.Equal(8, affected.Count);
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 7), affected);
        }
    }

    [Fact]
    public void HorizontalRocket_Apply_SmallBoard()
    {
        // Arrange: 3x3 棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, TileType.Red, x, y));

        var effect = new HorizontalRocketEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(1, 1);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该清除中间行的 3 个位置
        Assert.Equal(3, affected.Count);
        for (int x = 0; x < 3; x++)
        {
            Assert.Contains(new Position(x, 1), affected);
        }
    }

    [Fact]
    public void HorizontalRocket_Apply_WideBoard()
    {
        // Arrange: 10x5 棋盘
        var rng = new StubRandom();
        var state = new GameState(10, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 10; x++)
                state.SetTile(x, y, new Tile(y * 10 + x + 1, TileType.Blue, x, y));

        var effect = new HorizontalRocketEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(5, 2);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该清除整行的 10 个位置
        Assert.Equal(10, affected.Count);
        for (int x = 0; x < 10; x++)
        {
            Assert.Contains(new Position(x, 2), affected);
        }
    }

    #endregion

    #region VerticalRocketEffect Tests

    [Fact]
    public void VerticalRocket_Type_IsVertical()
    {
        var effect = new VerticalRocketEffect();
        Assert.Equal(BombType.Vertical, effect.Type);
    }

    [Fact]
    public void VerticalRocket_Apply_ClearsEntireColumn()
    {
        // Arrange
        var effect = new VerticalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(3, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该包含整列 (x=3)
        Assert.Equal(8, affected.Count);
        for (int y = 0; y < 8; y++)
        {
            Assert.Contains(new Position(3, y), affected);
        }
    }

    [Fact]
    public void VerticalRocket_Apply_AtLeftColumn()
    {
        // Arrange
        var effect = new VerticalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(0, 5);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert
        Assert.Equal(8, affected.Count);
        for (int y = 0; y < 8; y++)
        {
            Assert.Contains(new Position(0, y), affected);
        }
    }

    [Fact]
    public void VerticalRocket_Apply_AtRightColumn()
    {
        // Arrange
        var effect = new VerticalRocketEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(7, 3);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert
        Assert.Equal(8, affected.Count);
        for (int y = 0; y < 8; y++)
        {
            Assert.Contains(new Position(7, y), affected);
        }
    }

    [Fact]
    public void VerticalRocket_Apply_SmallBoard()
    {
        // Arrange: 3x3 棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, TileType.Red, x, y));

        var effect = new VerticalRocketEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(1, 1);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该清除中间列的 3 个位置
        Assert.Equal(3, affected.Count);
        for (int y = 0; y < 3; y++)
        {
            Assert.Contains(new Position(1, y), affected);
        }
    }

    [Fact]
    public void VerticalRocket_Apply_TallBoard()
    {
        // Arrange: 5x10 棋盘
        var rng = new StubRandom();
        var state = new GameState(5, 10, 6, rng);
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Blue, x, y));

        var effect = new VerticalRocketEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 5);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该清除整列的 10 个位置
        Assert.Equal(10, affected.Count);
        for (int y = 0; y < 10; y++)
        {
            Assert.Contains(new Position(2, y), affected);
        }
    }

    #endregion

    #region SquareBombEffect Tests

    [Fact]
    public void SquareBomb_Type_IsSquare5x5()
    {
        var effect = new SquareBombEffect();
        Assert.Equal(BombType.Square5x5, effect.Type);
    }

    [Fact]
    public void SquareBomb_Apply_Clears5x5Area()
    {
        // Arrange
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该包含 5x5 区域 (25 个位置)
        Assert.Equal(25, affected.Count);
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                Assert.Contains(new Position(4 + dx, 4 + dy), affected);
            }
        }
    }

    [Fact]
    public void SquareBomb_Apply_AtCorner_ClipsToBoard()
    {
        // Arrange: 在左上角
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(0, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 只有 9 个位置（3x3 在边界内）
        Assert.Equal(9, affected.Count);
        for (int y = 0; y <= 2; y++)
        {
            for (int x = 0; x <= 2; x++)
            {
                Assert.Contains(new Position(x, y), affected);
            }
        }
    }

    [Fact]
    public void SquareBomb_Apply_AtBottomRightCorner_ClipsToBoard()
    {
        // Arrange: 在右下角
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(7, 7);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 只有 9 个位置（3x3 在边界内）
        Assert.Equal(9, affected.Count);
        for (int y = 5; y <= 7; y++)
        {
            for (int x = 5; x <= 7; x++)
            {
                Assert.Contains(new Position(x, y), affected);
            }
        }
    }

    [Fact]
    public void SquareBomb_Apply_NearEdge_PartiallyClips()
    {
        // Arrange: 在靠近右边缘的位置
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(6, 4); // x=6, 半径2 -> x=4-8, 但8超出

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该是 4 列 * 5 行 = 20 个位置
        Assert.Equal(20, affected.Count);
    }

    [Fact]
    public void SquareBomb_Apply_AtTopRightCorner_ClipsToBoard()
    {
        // Arrange: 在右上角
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(7, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 只有 9 个位置（3x3 在边界内）
        Assert.Equal(9, affected.Count);
        for (int y = 0; y <= 2; y++)
        {
            for (int x = 5; x <= 7; x++)
            {
                Assert.Contains(new Position(x, y), affected);
            }
        }
    }

    [Fact]
    public void SquareBomb_Apply_AtBottomLeftCorner_ClipsToBoard()
    {
        // Arrange: 在左下角
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(0, 7);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 只有 9 个位置（3x3 在边界内）
        Assert.Equal(9, affected.Count);
        for (int y = 5; y <= 7; y++)
        {
            for (int x = 0; x <= 2; x++)
            {
                Assert.Contains(new Position(x, y), affected);
            }
        }
    }

    [Fact]
    public void SquareBomb_Apply_NearTopEdge_PartiallyClips()
    {
        // Arrange: 在靠近上边缘的位置
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 1); // y=1, 半径2 -> y=-1到3, 但-1超出

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该是 5 列 * 4 行 = 20 个位置
        Assert.Equal(20, affected.Count);
    }

    [Fact]
    public void SquareBomb_Apply_NearLeftEdge_PartiallyClips()
    {
        // Arrange: 在靠近左边缘的位置
        var effect = new SquareBombEffect();
        var state = CreateFilledState();
        var affected = new HashSet<Position>();
        var origin = new Position(1, 4); // x=1, 半径2 -> x=-1到3, 但-1超出

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该是 4 列 * 5 行 = 20 个位置
        Assert.Equal(20, affected.Count);
    }

    [Fact]
    public void SquareBomb_Apply_SmallBoard_CoversEntireBoard()
    {
        // Arrange: 在 3x3 棋盘上
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, TileType.Red, x, y));

        var effect = new SquareBombEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(1, 1);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 覆盖整个 3x3 棋盘
        Assert.Equal(9, affected.Count);
    }

    #endregion

    #region ColorBombEffect Tests

    [Fact]
    public void ColorBomb_Type_IsColor()
    {
        var effect = new ColorBombEffect();
        Assert.Equal(BombType.Color, effect.Type);
    }

    [Fact]
    public void ColorBomb_Apply_ClearsMostFrequentColor()
    {
        // Arrange: 创建一个红色最多的棋盘
        var state = CreateEmptyState();
        // 放置 10 个红色
        for (int x = 0; x < 8; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Red, x, 0));
        }
        state.SetTile(0, 1, new Tile(9, TileType.Red, 0, 1));
        state.SetTile(1, 1, new Tile(10, TileType.Red, 1, 1));
        // 放置 5 个蓝色
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 2, new Tile(11 + x, TileType.Blue, x, 2));
        }

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该清除所有红色 (10 个)
        Assert.Equal(10, affected.Count);
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 0), affected);
        }
        Assert.Contains(new Position(0, 1), affected);
        Assert.Contains(new Position(1, 1), affected);
    }

    [Fact]
    public void ColorBomb_Apply_IgnoresRainbowAndBomb()
    {
        // Arrange: 有 Rainbow 和 Bomb 类型
        var state = CreateEmptyState();
        // 3 个红色
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));
        // 10 个 Rainbow（不计入统计）
        for (int x = 0; x < 8; x++)
        {
            state.SetTile(x, 1, new Tile(10 + x, TileType.Rainbow, x, 1));
        }
        state.SetTile(0, 2, new Tile(20, TileType.Rainbow, 0, 2));
        state.SetTile(1, 2, new Tile(21, TileType.Rainbow, 1, 2));
        // 5 个 Bomb 类型（不计入统计）
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 3, new Tile(30 + x, TileType.Bomb, x, 3));
        }

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert: 只清除红色（最多的普通颜色）
        Assert.Equal(3, affected.Count);
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(1, 0), affected);
        Assert.Contains(new Position(2, 0), affected);
    }

    [Fact]
    public void ColorBomb_Apply_EmptyBoard_NoEffect()
    {
        // Arrange
        var state = CreateEmptyState();
        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert
        Assert.Empty(affected);
    }

    [Fact]
    public void ColorBomb_Apply_TieBreaker_SelectsOne()
    {
        // Arrange: 两种颜色数量相同
        var state = CreateEmptyState();
        // 5 个红色
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Red, x, 0));
        }
        // 5 个蓝色
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 1, new Tile(10 + x, TileType.Blue, x, 1));
        }

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert: 应该选择其中一种颜色（5 个）
        Assert.Equal(5, affected.Count);
    }

    [Fact]
    public void ColorBomb_Apply_SingleColor_ClearsAll()
    {
        // Arrange: 只有一种颜色
        var state = CreateEmptyState();
        for (int x = 0; x < 8; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Green, x, 0));
        }

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert: 应该清除所有绿色
        Assert.Equal(8, affected.Count);
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 0), affected);
        }
    }

    [Fact]
    public void ColorBomb_Apply_OnlySpecialTypes_NoEffect()
    {
        // Arrange: 只有 Bomb 和 Rainbow 类型（无普通颜色）
        var state = CreateEmptyState();
        for (int x = 0; x < 4; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, TileType.Bomb, x, 0));
        }
        for (int x = 0; x < 4; x++)
        {
            state.SetTile(x, 1, new Tile(10 + x, TileType.Rainbow, x, 1));
        }

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert: 没有普通颜色可消除
        Assert.Empty(affected);
    }

    [Fact]
    public void ColorBomb_Apply_AllSixColors_ClearsMostFrequent()
    {
        // Arrange: 所有 6 种颜色，紫色最多
        var state = CreateEmptyState();
        // 1 红
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        // 2 绿
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));
        // 3 蓝
        state.SetTile(3, 0, new Tile(4, TileType.Blue, 3, 0));
        state.SetTile(4, 0, new Tile(5, TileType.Blue, 4, 0));
        state.SetTile(5, 0, new Tile(6, TileType.Blue, 5, 0));
        // 4 黄
        state.SetTile(0, 1, new Tile(7, TileType.Yellow, 0, 1));
        state.SetTile(1, 1, new Tile(8, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(9, TileType.Yellow, 2, 1));
        state.SetTile(3, 1, new Tile(10, TileType.Yellow, 3, 1));
        // 5 紫（最多）
        state.SetTile(4, 1, new Tile(11, TileType.Purple, 4, 1));
        state.SetTile(5, 1, new Tile(12, TileType.Purple, 5, 1));
        state.SetTile(6, 1, new Tile(13, TileType.Purple, 6, 1));
        state.SetTile(7, 1, new Tile(14, TileType.Purple, 7, 1));
        state.SetTile(0, 2, new Tile(15, TileType.Purple, 0, 2));
        // 2 橙
        state.SetTile(1, 2, new Tile(16, TileType.Orange, 1, 2));
        state.SetTile(2, 2, new Tile(17, TileType.Orange, 2, 2));

        var effect = new ColorBombEffect();
        var affected = new HashSet<Position>();

        // Act
        effect.Apply(in state, new Position(4, 4), affected);

        // Assert: 应该清除所有紫色 (5 个)
        Assert.Equal(5, affected.Count);
        Assert.Contains(new Position(4, 1), affected);
        Assert.Contains(new Position(5, 1), affected);
        Assert.Contains(new Position(6, 1), affected);
        Assert.Contains(new Position(7, 1), affected);
        Assert.Contains(new Position(0, 2), affected);
    }

    #endregion

    #region UfoEffect Tests

    [Fact]
    public void UfoEffect_Type_IsUfo()
    {
        var effect = new UfoEffect();
        Assert.Equal(BombType.Ufo, effect.Type);
    }

    [Fact]
    public void UfoEffect_Apply_CreatesSmallCrossPattern()
    {
        // Arrange
        var rng = new StubRandom(0);
        var state = new GameState(8, 8, 6, rng);
        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow };
        int id = 1;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
            }
        }

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字 (5格) + 1个随机 = 6
        Assert.Equal(6, affected.Count);

        // 验证小十字被包含
        Assert.Contains(new Position(4, 4), affected); // 中心
        Assert.Contains(new Position(3, 4), affected); // 左
        Assert.Contains(new Position(5, 4), affected); // 右
        Assert.Contains(new Position(4, 3), affected); // 上
        Assert.Contains(new Position(4, 5), affected); // 下
    }

    [Fact]
    public void UfoEffect_Apply_AtCorner_ClipsSmallCross()
    {
        // Arrange: UFO 在左上角
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(0, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字在角落只有 3 格 (中心、右、下) + 1 随机 = 4
        Assert.Equal(4, affected.Count);
        Assert.Contains(new Position(0, 0), affected); // 中心
        Assert.Contains(new Position(1, 0), affected); // 右
        Assert.Contains(new Position(0, 1), affected); // 下
    }

    [Fact]
    public void UfoEffect_Apply_NoRandomTarget_OnlySmallCross()
    {
        // Arrange: 只有小十字范围内有方块
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));

        // 只在小十字范围放置方块
        state.SetTile(2, 2, new Tile(1, TileType.Red, 2, 2)); // 中心
        state.SetTile(1, 2, new Tile(2, TileType.Red, 1, 2)); // 左
        state.SetTile(3, 2, new Tile(3, TileType.Red, 3, 2)); // 右
        state.SetTile(2, 1, new Tile(4, TileType.Red, 2, 1)); // 上
        state.SetTile(2, 3, new Tile(5, TileType.Red, 2, 3)); // 下

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 2);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 只有小十字 5 格，没有随机目标
        Assert.Equal(5, affected.Count);
    }

    [Fact]
    public void UfoEffect_Apply_AtBottomRightCorner_ClipsSmallCross()
    {
        // Arrange: UFO 在右下角
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字在右下角只有 3 格 (中心、左、上) + 1 随机 = 4
        Assert.Equal(4, affected.Count);
        Assert.Contains(new Position(4, 4), affected); // 中心
        Assert.Contains(new Position(3, 4), affected); // 左
        Assert.Contains(new Position(4, 3), affected); // 上
    }

    [Fact]
    public void UfoEffect_Apply_AtTopRightCorner_ClipsSmallCross()
    {
        // Arrange: UFO 在右上角
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(4, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字在右上角只有 3 格 (中心、左、下) + 1 随机 = 4
        Assert.Equal(4, affected.Count);
        Assert.Contains(new Position(4, 0), affected); // 中心
        Assert.Contains(new Position(3, 0), affected); // 左
        Assert.Contains(new Position(4, 1), affected); // 下
    }

    [Fact]
    public void UfoEffect_Apply_AtBottomLeftCorner_ClipsSmallCross()
    {
        // Arrange: UFO 在左下角
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(0, 4);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字在左下角只有 3 格 (中心、右、上) + 1 随机 = 4
        Assert.Equal(4, affected.Count);
        Assert.Contains(new Position(0, 4), affected); // 中心
        Assert.Contains(new Position(1, 4), affected); // 右
        Assert.Contains(new Position(0, 3), affected); // 上
    }

    [Fact]
    public void UfoEffect_Apply_AtEdge_ClipsSmallCross()
    {
        // Arrange: UFO 在顶边中间
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 0);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字在顶边有 4 格 (中心、左、右、下) + 1 随机 = 5
        Assert.Equal(5, affected.Count);
        Assert.Contains(new Position(2, 0), affected); // 中心
        Assert.Contains(new Position(1, 0), affected); // 左
        Assert.Contains(new Position(3, 0), affected); // 右
        Assert.Contains(new Position(2, 1), affected); // 下
    }

    [Fact]
    public void UfoEffect_Apply_RandomTargetSelection()
    {
        // Arrange: 测试不同的随机目标选择
        var rng = new StubRandom(5); // 不同的随机值
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, TileType.Red, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 2);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 应该有 6 个 (小十字 5 + 随机 1)
        Assert.Equal(6, affected.Count);
        // 验证小十字
        Assert.Contains(new Position(2, 2), affected);
        Assert.Contains(new Position(1, 2), affected);
        Assert.Contains(new Position(3, 2), affected);
        Assert.Contains(new Position(2, 1), affected);
        Assert.Contains(new Position(2, 3), affected);
    }

    [Fact]
    public void UfoEffect_Apply_EmptyBoard_OnlySmallCrossPositions()
    {
        // Arrange: 空棋盘，小十字位置也是空的
        var rng = new StubRandom(0);
        var state = new GameState(5, 5, 6, rng);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));

        var effect = new UfoEffect();
        var affected = new HashSet<Position>();
        var origin = new Position(2, 2);

        // Act
        effect.Apply(in state, origin, affected);

        // Assert: 小十字位置仍然被添加，即使是空的
        Assert.Equal(5, affected.Count);
        Assert.Contains(new Position(2, 2), affected);
        Assert.Contains(new Position(1, 2), affected);
        Assert.Contains(new Position(3, 2), affected);
        Assert.Contains(new Position(2, 1), affected);
        Assert.Contains(new Position(2, 3), affected);
    }

    #endregion

    #region BombEffectRegistry Tests

    [Fact]
    public void BombEffectRegistry_CreateDefault_ContainsAllEffects()
    {
        // Act
        var registry = BombEffectRegistry.CreateDefault();

        // Assert: 验证所有效果都已注册
        Assert.True(registry.TryGetEffect(BombType.Horizontal, out var h));
        Assert.IsType<HorizontalRocketEffect>(h);

        Assert.True(registry.TryGetEffect(BombType.Vertical, out var v));
        Assert.IsType<VerticalRocketEffect>(v);

        Assert.True(registry.TryGetEffect(BombType.Square5x5, out var square));
        Assert.IsType<SquareBombEffect>(square);

        Assert.True(registry.TryGetEffect(BombType.Color, out var color));
        Assert.IsType<ColorBombEffect>(color);

        Assert.True(registry.TryGetEffect(BombType.Ufo, out var ufo));
        Assert.IsType<UfoEffect>(ufo);
    }

    [Fact]
    public void BombEffectRegistry_TryGetEffect_UnknownType_ReturnsFalse()
    {
        // Arrange
        var registry = new BombEffectRegistry(new List<IBombEffect>());

        // Act
        bool found = registry.TryGetEffect(BombType.Horizontal, out var effect);

        // Assert
        Assert.False(found);
        Assert.Null(effect);
    }

    [Fact]
    public void BombEffectRegistry_Register_OverwritesPreviousEffect()
    {
        // Arrange
        var effect1 = new HorizontalRocketEffect();
        var effect2 = new HorizontalRocketEffect();
        var registry = new BombEffectRegistry(new List<IBombEffect> { effect1 });

        // Act
        registry.Register(effect2);
        registry.TryGetEffect(BombType.Horizontal, out var retrieved);

        // Assert: 应该是新注册的
        Assert.Same(effect2, retrieved);
    }

    [Fact]
    public void BombEffectRegistry_CustomEffects_CanBeRegistered()
    {
        // Arrange
        var customEffects = new List<IBombEffect>
        {
            new HorizontalRocketEffect(),
            new VerticalRocketEffect()
        };
        var registry = new BombEffectRegistry(customEffects);

        // Assert
        Assert.True(registry.TryGetEffect(BombType.Horizontal, out _));
        Assert.True(registry.TryGetEffect(BombType.Vertical, out _));
        Assert.False(registry.TryGetEffect(BombType.Color, out _));
    }

    #endregion

    #region Edge Cases and Consistency Tests

    [Fact]
    public void AllEffects_Apply_ToEmptyHashSet_DoesNotThrow()
    {
        // Arrange
        var state = CreateFilledState();
        var effects = new IBombEffect[]
        {
            new HorizontalRocketEffect(),
            new VerticalRocketEffect(),
            new SquareBombEffect(),
            new ColorBombEffect(),
            new UfoEffect()
        };

        // Act & Assert
        foreach (var effect in effects)
        {
            var affected = new HashSet<Position>();
            var ex = Record.Exception(() => effect.Apply(in state, new Position(4, 4), affected));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void AllEffects_Apply_MultipleTimes_ProducesSameResult()
    {
        // Arrange: 测试确定性（除了 UFO，它依赖随机）
        var state = CreateFilledState();
        var deterministicEffects = new IBombEffect[]
        {
            new HorizontalRocketEffect(),
            new VerticalRocketEffect(),
            new SquareBombEffect()
        };

        foreach (var effect in deterministicEffects)
        {
            // Act
            var affected1 = new HashSet<Position>();
            var affected2 = new HashSet<Position>();
            effect.Apply(in state, new Position(4, 4), affected1);
            effect.Apply(in state, new Position(4, 4), affected2);

            // Assert
            Assert.Equal(affected1.Count, affected2.Count);
            foreach (var pos in affected1)
            {
                Assert.Contains(pos, affected2);
            }
        }
    }

    [Fact]
    public void AllEffects_Apply_SmallBoard_HandlesGracefully()
    {
        // Arrange: 最小 3x3 棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, TileType.Red, x, y));

        var effects = new IBombEffect[]
        {
            new HorizontalRocketEffect(),
            new VerticalRocketEffect(),
            new SquareBombEffect(),
            new ColorBombEffect(),
            new UfoEffect()
        };

        // Act & Assert
        foreach (var effect in effects)
        {
            var affected = new HashSet<Position>();
            var ex = Record.Exception(() => effect.Apply(in state, new Position(1, 1), affected));
            Assert.Null(ex);
            // 至少应该有一些效果（除非是 UFO 且随机导致没有选中）
            if (effect.Type != BombType.Ufo)
            {
                Assert.NotEmpty(affected);
            }
        }
    }

    [Fact]
    public void HorizontalAndVertical_Combined_CreatesCross()
    {
        // Arrange: 模拟水平+垂直炸弹组合的效果
        var state = CreateFilledState();
        var hEffect = new HorizontalRocketEffect();
        var vEffect = new VerticalRocketEffect();
        var origin = new Position(4, 4);

        // Act
        var hAffected = new HashSet<Position>();
        var vAffected = new HashSet<Position>();
        hEffect.Apply(in state, origin, hAffected);
        vEffect.Apply(in state, origin, vAffected);

        // 合并
        var combined = new HashSet<Position>(hAffected);
        combined.UnionWith(vAffected);

        // Assert: 应该形成十字形
        Assert.Equal(15, combined.Count); // 8 + 8 - 1 (重叠点)
        // 验证十字形状
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 4), combined);
        }
        for (int y = 0; y < 8; y++)
        {
            Assert.Contains(new Position(4, y), combined);
        }
    }

    #endregion
}
