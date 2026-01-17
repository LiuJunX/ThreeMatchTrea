using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Selection;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Selection;

/// <summary>
/// IMoveSelector 接口实现的测试
/// </summary>
public class MoveSelectorTests
{
    private readonly ITestOutputHelper _output;

    public MoveSelectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubRandom : IRandom
    {
        private int _counter;
        private readonly int[] _sequence;

        public StubRandom(params int[] sequence)
        {
            _sequence = sequence.Length > 0 ? sequence : new[] { 0 };
        }

        public float NextFloat() => 0f;
        public int Next(int max) => Next(0, max);
        public int Next(int min, int max)
        {
            if (_sequence.Length == 0) return min;
            var val = _sequence[_counter % _sequence.Length];
            _counter++;
            return Math.Max(min, Math.Min(max - 1, val));
        }
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private GameState CreateEmptyState(int width = 8, int height = 8, IRandom? random = null)
    {
        random ??= new StubRandom();
        var state = new GameState(width, height, 6, random);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, TileType.None, x, y));
            }
        }
        return state;
    }

    private IMatchFinder CreateMatchFinder()
    {
        var bombGenerator = new BombGenerator();
        return new ClassicMatchFinder(bombGenerator);
    }

    #region RandomMoveSelector Tests

    [Fact]
    public void RandomMoveSelector_WithValidMove_ReturnsTrue()
    {
        // Arrange: R R B R - 交换 B 和 R 后形成匹配
        var random = new StubRandom(2, 0);
        var state = CreateEmptyState(8, 8, random);

        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Red, 3, 0));

        var selector = new RandomMoveSelector(CreateMatchFinder());

        // Act
        bool found = selector.TryGetMove(in state, out var action);

        // Assert
        Assert.True(found);
        Assert.Equal(MoveActionType.Swap, action.ActionType);
    }

    [Fact]
    public void RandomMoveSelector_WithNoValidMoves_ReturnsFalse()
    {
        // Arrange: 空棋盘
        var random = new StubRandom(0, 0);
        var state = CreateEmptyState(4, 4, random);

        var selector = new RandomMoveSelector(CreateMatchFinder());

        // Act
        bool found = selector.TryGetMove(in state, out _);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void RandomMoveSelector_GetAllCandidates_ReturnsValidMoves()
    {
        // Arrange
        var state = CreateEmptyState(4, 2);

        // R R B R - 可以交换 (2,0) 和 (3,0)
        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Red, 3, 0));

        // 第二行填充其他颜色
        state.SetTile(0, 1, new Tile(4, TileType.Green, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.Purple, 2, 1));
        state.SetTile(3, 1, new Tile(7, TileType.Orange, 3, 1));

        var selector = new RandomMoveSelector(CreateMatchFinder());

        // Act
        var candidates = selector.GetAllCandidates(in state);

        // Assert
        _output.WriteLine($"找到 {candidates.Count} 个候选移动");
        Assert.True(candidates.Count > 0);
    }

    #endregion

    #region WeightedMoveSelector Tests

    [Fact]
    public void WeightedMoveSelector_WithValidMove_ReturnsTrue()
    {
        // Arrange
        var random = new StubRandom(0);
        var state = CreateEmptyState(4, 2, random);

        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Red, 3, 0));

        state.SetTile(0, 1, new Tile(4, TileType.Green, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(7, TileType.Orange, 3, 1));

        var selector = new WeightedMoveSelector(CreateMatchFinder(), random);

        // Act
        bool found = selector.TryGetMove(in state, out var action);

        // Assert
        Assert.True(found);
        Assert.Equal(MoveActionType.Swap, action.ActionType);
    }

    [Fact]
    public void WeightedMoveSelector_WithTappableBomb_IncludesTapAction()
    {
        // Arrange
        var random = new StubRandom(0);
        var state = CreateEmptyState(4, 2, random);

        // 放置一个炸弹
        var bombTile = new Tile(0, TileType.Red, 0, 0) { Bomb = BombType.Horizontal };
        state.SetTile(0, 0, bombTile);
        state.SetTile(1, 0, new Tile(1, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Green, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Yellow, 3, 0));

        state.SetTile(0, 1, new Tile(4, TileType.Purple, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Orange, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(7, TileType.Blue, 3, 1));

        var selector = new WeightedMoveSelector(CreateMatchFinder(), random);

        // Act
        var candidates = selector.GetAllCandidates(in state);

        // Assert
        _output.WriteLine($"找到 {candidates.Count} 个候选移动");
        bool hasTapAction = false;
        foreach (var c in candidates)
        {
            _output.WriteLine($"  - {c.ActionType}: ({c.From.X},{c.From.Y}) -> ({c.To.X},{c.To.Y}), Weight={c.Weight}");
            if (c.ActionType == MoveActionType.Tap)
            {
                hasTapAction = true;
            }
        }
        Assert.True(hasTapAction, "应该包含点击炸弹的操作");
    }

    [Fact]
    public void WeightedMoveSelector_BombCombination_HasHigherWeight()
    {
        // Arrange: 两个相邻的炸弹
        var random = new StubRandom(0);
        var state = CreateEmptyState(4, 2, random);

        var bombA = new Tile(0, TileType.Red, 0, 0) { Bomb = BombType.Horizontal };
        var bombB = new Tile(1, TileType.Blue, 1, 0) { Bomb = BombType.Vertical };
        state.SetTile(0, 0, bombA);
        state.SetTile(1, 0, bombB);
        state.SetTile(2, 0, new Tile(2, TileType.Green, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Yellow, 3, 0));

        state.SetTile(0, 1, new Tile(4, TileType.Purple, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Orange, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(7, TileType.Blue, 3, 1));

        var selector = new WeightedMoveSelector(CreateMatchFinder(), random);

        // Act
        var candidates = selector.GetAllCandidates(in state);

        // Assert
        _output.WriteLine($"找到 {candidates.Count} 个候选移动");

        // 找到炸弹+炸弹的交换
        MoveAction? bombCombo = null;
        foreach (var c in candidates)
        {
            _output.WriteLine($"  - {c.ActionType}: ({c.From.X},{c.From.Y}) -> ({c.To.X},{c.To.Y}), Weight={c.Weight}");
            if (c.ActionType == MoveActionType.Swap &&
                c.From.X == 0 && c.From.Y == 0 &&
                c.To.X == 1 && c.To.Y == 0)
            {
                bombCombo = c;
            }
        }

        Assert.NotNull(bombCombo);
        // 默认权重：Line=20，所以 20*20=400
        Assert.Equal(400, bombCombo.Value.Weight);
    }

    [Fact]
    public void WeightedMoveSelector_InvalidateCache_ClearsCache()
    {
        // Arrange
        var random = new StubRandom(0);
        var state = CreateEmptyState(4, 2, random);

        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Red, 3, 0));

        state.SetTile(0, 1, new Tile(4, TileType.Green, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(7, TileType.Orange, 3, 1));

        var selector = new WeightedMoveSelector(CreateMatchFinder(), random);

        // Act: 获取候选（建立缓存）
        var candidates1 = selector.GetAllCandidates(in state);
        int count1 = candidates1.Count;

        // 使缓存失效
        selector.InvalidateCache();

        // 再次获取
        var candidates2 = selector.GetAllCandidates(in state);
        int count2 = candidates2.Count;

        // Assert
        Assert.Equal(count1, count2);
    }

    #endregion

    #region Weight Calculation Tests

    [Fact]
    public void WeightConfig_GetWeight_ReturnsCorrectValues()
    {
        // Arrange
        var config = MoveSelectionConfig.Default;

        // Assert
        Assert.Equal(10, config.Weights.GetWeight(BombType.None));
        Assert.Equal(20, config.Weights.GetWeight(BombType.Ufo));
        Assert.Equal(20, config.Weights.GetWeight(BombType.Horizontal));
        Assert.Equal(20, config.Weights.GetWeight(BombType.Vertical));
        Assert.Equal(30, config.Weights.GetWeight(BombType.Square5x5));
        Assert.Equal(40, config.Weights.GetWeight(BombType.Color));
    }

    [Fact]
    public void WeightedMoveSelector_4Match_IncludesNewBombWeight()
    {
        // Arrange: 交换后形成 4 连
        // R R B R R
        // G Y R P O
        // 交换 B(2,0) 和 R(2,1) 后变成 R R R R R (5连)
        var random = new StubRandom(0);
        var state = CreateEmptyState(5, 2, random);

        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(2, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(3, TileType.Red, 3, 0));
        state.SetTile(4, 0, new Tile(4, TileType.Red, 4, 0));

        state.SetTile(0, 1, new Tile(5, TileType.Green, 0, 1));
        state.SetTile(1, 1, new Tile(6, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(7, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(8, TileType.Purple, 3, 1));
        state.SetTile(4, 1, new Tile(9, TileType.Orange, 4, 1));

        var selector = new WeightedMoveSelector(CreateMatchFinder(), random);

        // Act
        var candidates = selector.GetAllCandidates(in state);

        // Assert
        _output.WriteLine($"找到 {candidates.Count} 个候选移动");

        MoveAction? targetSwap = null;
        foreach (var c in candidates)
        {
            _output.WriteLine($"  - {c.ActionType}: ({c.From.X},{c.From.Y}) -> ({c.To.X},{c.To.Y}), Weight={c.Weight}");
            if (c.ActionType == MoveActionType.Swap &&
                c.From.X == 2 && c.From.Y == 0 &&
                c.To.X == 2 && c.To.Y == 1)
            {
                targetSwap = c;
            }
        }

        Assert.NotNull(targetSwap);
        // 基础权重 10 + 彩球权重 40 = 50
        Assert.True(targetSwap.Value.Weight >= 50, $"5连应该有更高权重，实际: {targetSwap.Value.Weight}");
    }

    #endregion
}
