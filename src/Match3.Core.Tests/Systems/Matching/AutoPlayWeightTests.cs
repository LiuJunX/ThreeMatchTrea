using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// 验证 Auto Play 加权计算场景
/// 测试交换后能否正确识别将生成的炸弹类型
/// </summary>
public class AutoPlayWeightTests
{
    private readonly ITestOutputHelper _output;

    public AutoPlayWeightTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private GameState CreateEmptyState(int width, int height)
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

    /// <summary>
    /// 场景：
    /// A A B A
    /// C D A F
    ///
    /// 交换 B(2,0) 和 A(2,1) 后：
    /// A A A A  <- 4连横，应生成 Line 炸弹
    /// C D B F
    ///
    /// 期望：FindMatchGroups 返回的 MatchGroup.SpawnBombType 应为 Horizontal 或 Vertical
    /// </summary>
    [Fact]
    public void SwapCreates4Match_ShouldReturnLineBombType()
    {
        // Arrange: 创建 4x2 棋盘
        // A A B A
        // C D A F
        var state = CreateEmptyState(4, 2);

        // Row 0: A A B A
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));    // A
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));    // A
        state.SetTile(2, 0, new Tile(3, TileType.Blue, 2, 0));   // B
        state.SetTile(3, 0, new Tile(4, TileType.Red, 3, 0));    // A

        // Row 1: C D A F
        state.SetTile(0, 1, new Tile(5, TileType.Green, 0, 1));  // C
        state.SetTile(1, 1, new Tile(6, TileType.Yellow, 1, 1)); // D
        state.SetTile(2, 1, new Tile(7, TileType.Red, 2, 1));    // A
        state.SetTile(3, 1, new Tile(8, TileType.Purple, 3, 1)); // F

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);

        var from = new Position(2, 0); // B
        var to = new Position(2, 1);   // A

        // Act: 临时交换检测
        GridUtility.SwapTilesForCheck(ref state, from, to);

        _output.WriteLine("交换后棋盘:");
        for (int y = 0; y < state.Height; y++)
        {
            var row = "";
            for (int x = 0; x < state.Width; x++)
            {
                row += state.GetType(x, y).ToString()[0] + " ";
            }
            _output.WriteLine(row);
        }

        var foci = new[] { from, to };
        var matchGroups = matchFinder.FindMatchGroups(in state, foci);

        GridUtility.SwapTilesForCheck(ref state, from, to); // 交换回来

        // Assert
        _output.WriteLine($"\n找到 {matchGroups.Count} 个匹配组:");

        bool foundLineBomb = false;
        foreach (var group in matchGroups)
        {
            _output.WriteLine($"  - Type: {group.Type}, Positions: {group.Positions.Count}, SpawnBombType: {group.SpawnBombType}");

            if (group.SpawnBombType == BombType.Horizontal || group.SpawnBombType == BombType.Vertical)
            {
                foundLineBomb = true;
            }
        }

        int matchCount = matchGroups.Count;

        // 清理池化对象
        ClassicMatchFinder.ReleaseGroups(matchGroups);

        Assert.True(matchCount > 0, "应该找到匹配");
        Assert.True(foundLineBomb, "4连横应该生成 Line 炸弹 (Horizontal 或 Vertical)");
    }

    /// <summary>
    /// 场景：普通3连不生成炸弹
    /// A A B
    /// C A D
    ///
    /// 交换 B(2,0) 和 A(1,1) 后不会形成匹配（非相邻）
    /// 但如果是：
    /// A A B
    /// C D A
    /// 交换 B(2,0) 和 A(2,1) (假设相邻)
    /// 结果不会形成横向匹配
    ///
    /// 我们测试简单3连场景
    /// </summary>
    [Fact]
    public void SwapCreates3Match_ShouldReturnNoBomb()
    {
        // Arrange: 创建场景
        // A B A
        // C A D
        // 交换 B(1,0) 和 A(1,1) 后:
        // A A A <- 3连
        // C B D
        var state = CreateEmptyState(3, 2);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));    // A
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));   // B
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));    // A

        state.SetTile(0, 1, new Tile(4, TileType.Green, 0, 1));  // C
        state.SetTile(1, 1, new Tile(5, TileType.Red, 1, 1));    // A
        state.SetTile(2, 1, new Tile(6, TileType.Yellow, 2, 1)); // D

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);

        var from = new Position(1, 0); // B
        var to = new Position(1, 1);   // A

        // Act
        GridUtility.SwapTilesForCheck(ref state, from, to);

        _output.WriteLine("交换后棋盘:");
        for (int y = 0; y < state.Height; y++)
        {
            var row = "";
            for (int x = 0; x < state.Width; x++)
            {
                row += state.GetType(x, y).ToString()[0] + " ";
            }
            _output.WriteLine(row);
        }

        var foci = new[] { from, to };
        var matchGroups = matchFinder.FindMatchGroups(in state, foci);

        GridUtility.SwapTilesForCheck(ref state, from, to);

        // Assert
        _output.WriteLine($"\n找到 {matchGroups.Count} 个匹配组:");
        foreach (var group in matchGroups)
        {
            _output.WriteLine($"  - Type: {group.Type}, Positions: {group.Positions.Count}, SpawnBombType: {group.SpawnBombType}");
            Assert.Equal(BombType.None, group.SpawnBombType); // 3连不生成炸弹
        }

        int matchCount = matchGroups.Count;
        ClassicMatchFinder.ReleaseGroups(matchGroups);

        Assert.True(matchCount > 0, "应该找到3连匹配");
    }

    /// <summary>
    /// 验证5连生成彩虹炸弹
    /// </summary>
    [Fact]
    public void SwapCreates5Match_ShouldReturnColorBomb()
    {
        // A A B A A
        // C D A E F
        // 交换 B(2,0) 和 A(2,1) 后:
        // A A A A A <- 5连，应生成 Color 炸弹
        var state = CreateEmptyState(5, 2);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Blue, 2, 0));
        state.SetTile(3, 0, new Tile(4, TileType.Red, 3, 0));
        state.SetTile(4, 0, new Tile(5, TileType.Red, 4, 0));

        state.SetTile(0, 1, new Tile(6, TileType.Green, 0, 1));
        state.SetTile(1, 1, new Tile(7, TileType.Yellow, 1, 1));
        state.SetTile(2, 1, new Tile(8, TileType.Red, 2, 1));
        state.SetTile(3, 1, new Tile(9, TileType.Purple, 3, 1));
        state.SetTile(4, 1, new Tile(10, TileType.Orange, 4, 1));

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);

        var from = new Position(2, 0);
        var to = new Position(2, 1);

        GridUtility.SwapTilesForCheck(ref state, from, to);

        _output.WriteLine("交换后棋盘 (5连):");
        for (int y = 0; y < state.Height; y++)
        {
            var row = "";
            for (int x = 0; x < state.Width; x++)
            {
                row += state.GetType(x, y).ToString()[0] + " ";
            }
            _output.WriteLine(row);
        }

        var foci = new[] { from, to };
        var matchGroups = matchFinder.FindMatchGroups(in state, foci);

        GridUtility.SwapTilesForCheck(ref state, from, to);

        _output.WriteLine($"\n找到 {matchGroups.Count} 个匹配组:");
        bool foundColorBomb = false;
        foreach (var group in matchGroups)
        {
            _output.WriteLine($"  - Type: {group.Type}, Positions: {group.Positions.Count}, SpawnBombType: {group.SpawnBombType}");
            if (group.SpawnBombType == BombType.Color)
            {
                foundColorBomb = true;
            }
        }

        ClassicMatchFinder.ReleaseGroups(matchGroups);

        Assert.True(foundColorBomb, "5连应该生成 Color (彩虹) 炸弹");
    }
}
