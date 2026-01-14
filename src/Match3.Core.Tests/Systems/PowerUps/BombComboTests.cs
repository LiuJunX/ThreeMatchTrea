using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

/// <summary>
/// 组合炸弹测试
///
/// 组合规则：
/// - 火箭 + 火箭 = 十字（1行+1列）
/// - 火箭 + 方块炸弹 = 3行+3列
/// - 火箭 + UFO = 起飞前小十字，落地后消除一行或一列
/// - 火箭 + 彩球 = 最多颜色全变火箭并爆炸
/// - 方块炸弹 + 方块炸弹 = 9x9
/// - 方块炸弹 + UFO = 起飞前小十字，落地后消除5x5
/// - 方块炸弹 + 彩球 = 最多颜色全变3x3炸弹并爆炸
/// - UFO + UFO = 两个原地小十字 + 飞出3个UFO
/// - UFO + 彩球 = 最多颜色全变UFO并起飞
/// - 彩球 + 彩球 = 全屏消除
/// </summary>
public class BombComboTests
{
    private class StubRandom : IRandom
    {
        private readonly Queue<int> _values = new();
        private int _defaultValue = 0;

        public StubRandom(int defaultValue = 0)
        {
            _defaultValue = defaultValue;
        }

        public void EnqueueValues(params int[] values)
        {
            foreach (var v in values)
                _values.Enqueue(v);
        }

        public float NextFloat() => 0f;
        public int Next(int max) => _values.Count > 0 ? _values.Dequeue() % max : _defaultValue % max;
        public int Next(int min, int max) => min + (_values.Count > 0 ? _values.Dequeue() % (max - min) : _defaultValue % (max - min));
        public void SetState(ulong state) { _defaultValue = (int)state; }
        public ulong GetState() => (ulong)_defaultValue;
    }

    private GameState CreateFilledState(int width = 8, int height = 8, IRandom? rng = null)
    {
        rng ??= new StubRandom();
        var state = new GameState(width, height, 6, rng);
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

    private GameState CreateEmptyState(int width = 8, int height = 8, IRandom? rng = null)
    {
        rng ??= new StubRandom();
        var state = new GameState(width, height, 6, rng);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));
            }
        }
        return state;
    }

    private int CountNonEmptyTiles(GameState state)
    {
        int count = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Type != TileType.None)
                count++;
        }
        return count;
    }

    private int CountClearedTiles(GameState before, GameState after)
    {
        return CountNonEmptyTiles(before) - CountNonEmptyTiles(after);
    }

    #region 火箭 + 火箭 = 十字

    [Fact]
    public void RocketPlusRocket_Horizontal_Horizontal_CreatesCross()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        // 设置两个横向火箭
        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Horizontal });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 十字 = 1行 + 1列 - 1交点 = 8 + 8 - 1 = 15
        Assert.Equal(15, affected.Count);
        // 验证整行
        for (int x = 0; x < 8; x++)
            Assert.Contains(new Position(x, p2.Y), affected);
        // 验证整列
        for (int y = 0; y < 8; y++)
            Assert.Contains(new Position(p2.X, y), affected);
    }

    [Fact]
    public void RocketPlusRocket_Vertical_Vertical_CreatesCross()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(4, 3);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Vertical });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 十字
        Assert.Equal(15, affected.Count);
    }

    [Fact]
    public void RocketPlusRocket_Horizontal_Vertical_CreatesCross()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 十字
        Assert.Equal(15, affected.Count);
    }

    #endregion

    #region 火箭 + 方块炸弹 = 3行3列

    [Fact]
    public void RocketPlusSquare_Creates3Rows3Cols()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Square5x5 });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 3行 + 3列，中心在p2
        // 3行: y=3,4,5 各8格 = 24
        // 3列: x=3,4,5 各8格 = 24
        // 重叠: 3x3 = 9
        // 总计: 24 + 24 - 9 = 39
        Assert.Equal(39, affected.Count);

        // 验证3行
        for (int x = 0; x < 8; x++)
        {
            Assert.Contains(new Position(x, 3), affected);
            Assert.Contains(new Position(x, 4), affected);
            Assert.Contains(new Position(x, 5), affected);
        }
        // 验证3列
        for (int y = 0; y < 8; y++)
        {
            Assert.Contains(new Position(3, y), affected);
            Assert.Contains(new Position(4, y), affected);
            Assert.Contains(new Position(5, y), affected);
        }
    }

    [Fact]
    public void SquarePlusRocket_Creates3Rows3Cols()
    {
        // Arrange: 顺序反过来
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Square5x5 });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 同样是3行3列
        Assert.Equal(39, affected.Count);
    }

    [Fact]
    public void RocketPlusSquare_AtCorner_ClipsToBoard()
    {
        // Arrange: 在角落
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(0, 0);
        var p2 = new Position(1, 0);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Square5x5 });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 在角落会裁剪
        // 3行: y=0,1 (y=-1超出) 各8格 = 16
        // 3列: x=0,1,2 各8格 = 24
        // 重叠: 2x3 = 6
        // 总计: 16 + 24 - 6 = 34
        Assert.Equal(34, affected.Count);
    }

    #endregion

    #region 火箭 + UFO = 小十字 + 一行或一列

    [Fact]
    public void RocketPlusUfo_ClearsSmallCrossAndRowOrCol()
    {
        // Arrange
        var rng = new StubRandom();
        rng.EnqueueValues(10); // 随机目标索引
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert:
        // UFO小十字: 5格
        // UFO落地后一行或一列: 8格
        // 可能有重叠
        Assert.True(affected.Count >= 8); // 至少一行/列

        // 验证小十字被包含
        Assert.Contains(p2, affected); // UFO中心
    }

    [Fact]
    public void UfoPlusRocket_SameEffect()
    {
        // Arrange: 顺序反过来
        var rng = new StubRandom();
        rng.EnqueueValues(10);
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Ufo });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert
        Assert.True(affected.Count >= 8);
    }

    #endregion

    #region 火箭 + 彩球 = 最多颜色变火箭并爆炸

    [Fact]
    public void RocketPlusColorBomb_TransformsAndExplodes()
    {
        // Arrange: 红色最多
        var state = CreateEmptyState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        // 设置火箭和彩球
        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Rainbow, p2.X, p2.Y) { Bomb = BombType.Color });

        // 放置红色方块（最多）
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));
        // 放置蓝色（较少）
        state.SetTile(0, 1, new Tile(4, TileType.Blue, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Blue, 1, 1));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 红色位置应该被火箭爆炸影响
        // 3个红色各触发横向火箭，消除3行
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(1, 0), affected);
        Assert.Contains(new Position(2, 0), affected);
    }

    [Fact]
    public void ColorBombPlusRocket_SameEffect()
    {
        // Arrange: 顺序反过来
        var state = CreateEmptyState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Rainbow, p1.X, p1.Y) { Bomb = BombType.Color });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Vertical });

        // 放置绿色方块（最多）
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 绿色位置应该被影响
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(1, 0), affected);
        Assert.Contains(new Position(2, 0), affected);
    }

    #endregion

    #region 方块炸弹 + 方块炸弹 = 9x9

    [Fact]
    public void SquarePlusSquare_Creates9x9()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Square5x5 });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Square5x5 });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 9x9 = 81，但会被棋盘边界裁剪
        // 中心在p2(4,4)，半径4
        // x: 0-8 (全部在范围内)
        // y: 0-8 (y=0到y=8，但棋盘只有0-7)
        // 实际: x=0-7, y=0-7 全部 = 64
        Assert.Equal(64, affected.Count);
    }

    [Fact]
    public void SquarePlusSquare_AtCenter_FullCoverage()
    {
        // Arrange: 在更大的棋盘中心
        var state = CreateFilledState(12, 12);
        var combo = new BombComboHandler();
        var p1 = new Position(5, 5);
        var p2 = new Position(6, 6);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Square5x5 });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Square5x5 });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 完整9x9 = 81
        Assert.Equal(81, affected.Count);
    }

    #endregion

    #region 方块炸弹 + UFO = 小十字 + 5x5

    [Fact]
    public void SquarePlusUfo_ClearsSmallCrossAnd5x5()
    {
        // Arrange
        var rng = new StubRandom();
        rng.EnqueueValues(20); // 随机目标
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Square5x5 });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert:
        // UFO小十字: 5格
        // UFO落地后5x5: 25格
        // 可能有重叠
        Assert.True(affected.Count >= 25);

        // 验证UFO中心
        Assert.Contains(p2, affected);
    }

    [Fact]
    public void UfoPlusSquare_SameEffect()
    {
        // Arrange
        var rng = new StubRandom();
        rng.EnqueueValues(20);
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Ufo });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Square5x5 });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert
        Assert.True(affected.Count >= 25);
    }

    #endregion

    #region 方块炸弹 + 彩球 = 最多颜色变3x3炸弹并爆炸

    [Fact]
    public void SquarePlusColorBomb_TransformsTo3x3AndExplodes()
    {
        // Arrange: 红色最多
        var state = CreateEmptyState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Square5x5 });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Rainbow, p2.X, p2.Y) { Bomb = BombType.Color });

        // 放置红色（最多）- 分散在棋盘上
        state.SetTile(1, 1, new Tile(1, TileType.Red, 1, 1));
        state.SetTile(5, 5, new Tile(2, TileType.Red, 5, 5));
        // 放置蓝色（较少）
        state.SetTile(0, 0, new Tile(3, TileType.Blue, 0, 0));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 红色位置触发3x3爆炸
        Assert.Contains(new Position(1, 1), affected);
        Assert.Contains(new Position(5, 5), affected);
        // 3x3范围也应该被影响
        Assert.Contains(new Position(0, 0), affected); // (1,1)的3x3范围包含(0,0)
        Assert.Contains(new Position(4, 4), affected); // (5,5)的3x3范围包含(4,4)
    }

    #endregion

    #region UFO + UFO = 两个小十字 + 3个UFO

    [Fact]
    public void UfoPlusUfo_CreatesTwoSmallCrossesAndThreeUfos()
    {
        // Arrange
        var rng = new StubRandom();
        rng.EnqueueValues(10, 20, 30); // 3个UFO的随机目标
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(2, 4);
        var p2 = new Position(5, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Ufo });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert:
        // 两个小十字: 最多10格（可能重叠）
        // 3个UFO各击中1个随机目标: 3格
        Assert.True(affected.Count >= 10); // 至少两个小十字

        // 验证两个UFO中心的小十字
        Assert.Contains(p1, affected);
        Assert.Contains(p2, affected);
        Assert.Contains(new Position(p1.X - 1, p1.Y), affected); // p1左
        Assert.Contains(new Position(p1.X + 1, p1.Y), affected); // p1右
        Assert.Contains(new Position(p2.X - 1, p2.Y), affected); // p2左
        Assert.Contains(new Position(p2.X + 1, p2.Y), affected); // p2右
    }

    #endregion

    #region UFO + 彩球 = 最多颜色变UFO并起飞

    [Fact]
    public void UfoPlusColorBomb_TransformsAndLaunches()
    {
        // Arrange
        var rng = new StubRandom();
        rng.EnqueueValues(0, 1, 2); // 多个UFO的随机目标
        var state = CreateEmptyState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Ufo });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Rainbow, p2.X, p2.Y) { Bomb = BombType.Color });

        // 放置红色（最多）
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(2, 2, new Tile(2, TileType.Red, 2, 2));
        state.SetTile(6, 6, new Tile(3, TileType.Red, 6, 6));
        // 放置蓝色（较少）
        state.SetTile(7, 7, new Tile(4, TileType.Blue, 7, 7));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 红色位置变成UFO并起飞
        // 每个UFO: 小十字 + 1随机目标
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(2, 2), affected);
        Assert.Contains(new Position(6, 6), affected);
    }

    [Fact]
    public void ColorBombPlusUfo_SameEffect()
    {
        // Arrange: 顺序反过来
        var rng = new StubRandom();
        rng.EnqueueValues(0, 1, 2);
        var state = CreateEmptyState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Rainbow, p1.X, p1.Y) { Bomb = BombType.Color });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // 放置绿色（最多）
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(2, 2, new Tile(2, TileType.Green, 2, 2));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(2, 2), affected);
    }

    #endregion

    #region 彩球 + 彩球 = 全屏消除

    [Fact]
    public void ColorBombPlusColorBomb_ClearsEntireBoard()
    {
        // Arrange
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Rainbow, p1.X, p1.Y) { Bomb = BombType.Color });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Rainbow, p2.X, p2.Y) { Bomb = BombType.Color });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 全屏 = 64
        Assert.Equal(64, affected.Count);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Contains(new Position(x, y), affected);
            }
        }
    }

    [Fact]
    public void ColorBombPlusColorBomb_SmallBoard_ClearsAll()
    {
        // Arrange: 3x3棋盘
        var state = CreateFilledState(3, 3);
        var combo = new BombComboHandler();
        var p1 = new Position(0, 1);
        var p2 = new Position(1, 1);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Rainbow, p1.X, p1.Y) { Bomb = BombType.Color });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Rainbow, p2.X, p2.Y) { Bomb = BombType.Color });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 全屏 = 9
        Assert.Equal(9, affected.Count);
    }

    #endregion

    #region 彩球颜色选择规则

    [Fact]
    public void ColorBomb_WithNormalTile_ClearsSpecifiedColor()
    {
        // Arrange: 手动交换彩球与普通色块
        var state = CreateEmptyState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        // 彩球
        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Rainbow, p1.X, p1.Y) { Bomb = BombType.Color });
        // 普通蓝色方块（不是炸弹）
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Blue, p2.X, p2.Y));

        // 放置蓝色
        state.SetTile(0, 0, new Tile(1, TileType.Blue, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        // 放置红色（更多，但不应该被消除）
        state.SetTile(0, 1, new Tile(3, TileType.Red, 0, 1));
        state.SetTile(1, 1, new Tile(4, TileType.Red, 1, 1));
        state.SetTile(2, 1, new Tile(5, TileType.Red, 2, 1));

        // Act: 使用 TryApplyCombo 处理彩球+普通方块的情况
        var affected = new HashSet<Position>();
        bool result = combo.TryApplyCombo(ref state, p1, p2, affected);

        // Assert: 应该消除蓝色（指定颜色），不是红色（最多颜色）
        Assert.True(result);
        Assert.Contains(new Position(0, 0), affected);
        Assert.Contains(new Position(1, 0), affected);
        Assert.Contains(new Position(4, 4), affected); // 被交换的蓝色方块
        Assert.DoesNotContain(new Position(0, 1), affected); // 红色不应该被消除
        Assert.DoesNotContain(new Position(1, 1), affected);
        Assert.DoesNotContain(new Position(2, 1), affected);
    }

    #endregion

    #region 火箭 + UFO 方向测试

    [Fact]
    public void HorizontalRocketPlusUfo_ClearsRow()
    {
        // Arrange: 横向火箭 + UFO，应该消除一行
        var rng = new StubRandom();
        rng.EnqueueValues(10); // 随机目标索引
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 应该包含完整的一行（小十字5 + 一行8 - 重叠）
        Assert.True(affected.Count >= 8);

        // 验证小十字包含 UFO 位置
        Assert.Contains(p2, affected);
    }

    [Fact]
    public void VerticalRocketPlusUfo_ClearsColumn()
    {
        // Arrange: 纵向火箭 + UFO，应该消除一列
        var rng = new StubRandom();
        rng.EnqueueValues(10);
        var state = CreateFilledState(rng: rng);
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Vertical });
        state.SetTile(p2.X, p2.Y, new Tile(101, TileType.Red, p2.X, p2.Y) { Bomb = BombType.Ufo });

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: 应该包含完整的一列（8 格）
        Assert.True(affected.Count >= 8);
    }

    #endregion

    #region 边界情况

    [Fact]
    public void Combo_NoBombs_NoEffect()
    {
        // Arrange: 两个普通方块
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        // 不设置炸弹类型

        // Act
        var affected = new HashSet<Position>();
        bool result = combo.TryApplyCombo(ref state, p1, p2, affected);

        // Assert
        Assert.False(result);
        Assert.Empty(affected);
    }

    [Fact]
    public void Combo_OneBomb_NoCombo()
    {
        // Arrange: 只有一个炸弹
        var state = CreateFilledState();
        var combo = new BombComboHandler();
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);

        state.SetTile(p1.X, p1.Y, new Tile(100, TileType.Red, p1.X, p1.Y) { Bomb = BombType.Horizontal });
        // p2 不是炸弹

        // Act
        var affected = new HashSet<Position>();
        bool result = combo.TryApplyCombo(ref state, p1, p2, affected);

        // Assert: 单个炸弹不触发组合
        Assert.False(result);
    }

    #endregion
}
