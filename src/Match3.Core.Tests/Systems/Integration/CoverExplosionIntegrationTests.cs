using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Tests.TestHelpers;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 集成测试：验证 Cover + Explosion 系统协作
///
/// 覆盖的核心场景：
/// 1. 炸弹爆炸对覆盖物的伤害
/// 2. 覆盖物被破坏后的连锁爆炸
/// 3. 多层覆盖物（高 HP）的爆炸伤害
/// 4. 不同覆盖物类型对爆炸的响应
/// </summary>
public class CoverExplosionIntegrationTests
{
    private readonly ITestOutputHelper _output;

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
        public int CalculateMatchScore(MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2) => 100;
    }

    public CoverExplosionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Bomb Explosion + Cover Damage Tests

    /// <summary>
    /// 测试：水平炸弹爆炸伤害同一行的覆盖物
    ///
    /// 场景：
    ///   0 1 2 3 4
    /// 0 R [H] B [G] Y  <- [H] 水平炸弹，[G] 被 Chain 覆盖
    ///
    /// 预期：炸弹爆炸，整行清除，Chain 被破坏
    /// </summary>
    [Fact]
    public void HorizontalBomb_DamagesCoversInRow()
    {
        // Arrange
        var rng = new StubRandom();
        var state = new GameState(5, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        var bombTile = new Tile(2, TileType.Red, 1, 0);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(1, 0, bombTile);

        state.SetTile(2, 0, new Tile(3, TileType.Blue, 2, 0));

        var greenTile = new Tile(4, TileType.Green, 3, 0);
        state.SetTile(3, 0, greenTile);
        state.SetCover(new Position(3, 0), new Cover(CoverType.Chain, health: 1));

        state.SetTile(4, 0, new Tile(5, TileType.Yellow, 4, 0));

        var scoreSystem = new StubScoreSystem();
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        _output.WriteLine("初始状态:");
        PrintRow(ref state, 0);

        // Act: 激活水平炸弹
        powerUpHandler.ActivateBomb(ref state, new Position(1, 0));

        _output.WriteLine("爆炸后:");
        PrintRow(ref state, 0);

        // Assert: 没有覆盖物的格子被清除
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 0).Type); // 炸弹位置
        Assert.Equal(TileType.None, state.GetTile(2, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 0).Type);

        // 有 Chain 覆盖物的格子：覆盖物被破坏，但方块保留（被保护）
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 0)).Type);
        Assert.Equal(TileType.Green, state.GetTile(3, 0).Type); // 方块被 Chain 保护

        _output.WriteLine("水平炸弹正确伤害了覆盖物，被保护的方块保留");
    }

    /// <summary>
    /// 测试：垂直炸弹爆炸伤害同一列的覆盖物
    ///
    /// 场景：
    ///   0
    /// 0 R
    /// 1 [V]  <- 垂直炸弹
    /// 2 [B]  <- Blue 被 Cage 覆盖
    /// 3 G
    ///
    /// 预期：炸弹爆炸，整列清除，Cage 被破坏
    /// </summary>
    [Fact]
    public void VerticalBomb_DamagesCoversInColumn()
    {
        // Arrange
        var rng = new StubRandom();
        var state = new GameState(1, 4, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        var bombTile = new Tile(2, TileType.Red, 0, 1);
        bombTile.Bomb = BombType.Vertical;
        state.SetTile(0, 1, bombTile);

        var blueTile = new Tile(3, TileType.Blue, 0, 2);
        state.SetTile(0, 2, blueTile);
        state.SetCover(new Position(0, 2), new Cover(CoverType.Cage, health: 1));

        state.SetTile(0, 3, new Tile(4, TileType.Green, 0, 3));

        var scoreSystem = new StubScoreSystem();
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        _output.WriteLine("初始状态:");
        PrintColumn(ref state, 0);

        // Act: 激活垂直炸弹
        powerUpHandler.ActivateBomb(ref state, new Position(0, 1));

        _output.WriteLine("爆炸后:");
        PrintColumn(ref state, 0);

        // Assert: 没有覆盖物的格子被清除
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type); // 炸弹位置
        Assert.Equal(TileType.None, state.GetTile(0, 3).Type);

        // 有 Cage 覆盖物的格子：覆盖物被破坏，但方块保留（被保护）
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 2)).Type);
        Assert.Equal(TileType.Blue, state.GetTile(0, 2).Type); // 方块被 Cage 保护

        _output.WriteLine("垂直炸弹正确伤害了覆盖物，被保护的方块保留");
    }

    /// <summary>
    /// 测试：Square5x5 炸弹爆炸伤害周围的覆盖物
    ///
    /// 场景：3x3 区域，中心有 Square5x5 炸弹，四角有覆盖物
    /// </summary>
    [Fact]
    public void Square5x5Bomb_DamagesCoversInRadius()
    {
        // Arrange
        //   0 1 2
        // 0 [R] B [G]  <- [R] 和 [G] 被 Chain 覆盖
        // 1  B [S] B   <- [S] Square5x5 炸弹
        // 2 [Y] B [P]  <- [Y] 和 [P] 被 Chain 覆盖
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        // 第 0 行
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Chain, health: 1));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));
        state.SetCover(new Position(2, 0), new Cover(CoverType.Chain, health: 1));

        // 第 1 行
        state.SetTile(0, 1, new Tile(4, TileType.Blue, 0, 1));
        var areaBomb = new Tile(5, TileType.Red, 1, 1);
        areaBomb.Bomb = BombType.Square5x5;
        state.SetTile(1, 1, areaBomb);
        state.SetTile(2, 1, new Tile(6, TileType.Blue, 2, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(7, TileType.Yellow, 0, 2));
        state.SetCover(new Position(0, 2), new Cover(CoverType.Chain, health: 1));
        state.SetTile(1, 2, new Tile(8, TileType.Blue, 1, 2));
        state.SetTile(2, 2, new Tile(9, TileType.Purple, 2, 2));
        state.SetCover(new Position(2, 2), new Cover(CoverType.Chain, health: 1));

        var scoreSystem = new StubScoreSystem();
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        _output.WriteLine("初始状态:");
        PrintBoard(ref state);

        // Act: 激活 Wrapped 炸弹
        powerUpHandler.ActivateBomb(ref state, new Position(1, 1));

        _output.WriteLine("爆炸后:");
        PrintBoard(ref state);

        // Assert: 验证覆盖物被破坏
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 0)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 0)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 2)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 2)).Type);

        _output.WriteLine("Wrapped 炸弹正确伤害了周围的覆盖物");
    }

    #endregion

    #region Multi-HP Cover Tests

    /// <summary>
    /// 测试：多 HP 覆盖物需要多次爆炸才能破坏
    ///
    /// 场景：覆盖物有 2 HP，单次爆炸只减少 1 HP
    /// </summary>
    [Fact]
    public void MultiHPCover_RequiresMultipleHits()
    {
        // Arrange
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        var bombTile = new Tile(2, TileType.Red, 1, 0);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(1, 0, bombTile);

        state.SetTile(2, 0, new Tile(3, TileType.Blue, 2, 0));
        // 设置 2 HP 的覆盖物
        state.SetCover(new Position(2, 0), new Cover(CoverType.Chain, health: 2));

        var scoreSystem = new StubScoreSystem();
        var coverSystem = new CoverSystem();
        var events = new BufferedEventCollector();

        _output.WriteLine("初始状态:");
        _output.WriteLine($"Cover at (2,0): Type={state.GetCover(new Position(2, 0)).Type}, HP={state.GetCover(new Position(2, 0)).Health}");

        // Act: 第一次伤害
        coverSystem.TryDamageCover(ref state, new Position(2, 0), tick: 1, simTime: 0.1f, events);

        _output.WriteLine("第一次伤害后:");
        _output.WriteLine($"Cover at (2,0): Type={state.GetCover(new Position(2, 0)).Type}, HP={state.GetCover(new Position(2, 0)).Health}");

        // Assert: 覆盖物还在，HP 减少
        Assert.Equal(CoverType.Chain, state.GetCover(new Position(2, 0)).Type);
        Assert.Equal(1, state.GetCover(new Position(2, 0)).Health);

        // Act: 第二次伤害
        coverSystem.TryDamageCover(ref state, new Position(2, 0), tick: 2, simTime: 0.2f, events);

        _output.WriteLine("第二次伤害后:");
        _output.WriteLine($"Cover at (2,0): Type={state.GetCover(new Position(2, 0)).Type}");

        // Assert: 覆盖物被破坏
        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 0)).Type);

        // 应该有一个 CoverDestroyedEvent
        Assert.Single(events.GetEvents());

        _output.WriteLine("多 HP 覆盖物正确需要多次伤害才能破坏");
    }

    #endregion

    #region Chain Explosion Tests

    /// <summary>
    /// 测试：炸弹连锁爆炸时正确伤害覆盖物
    ///
    /// 场景：水平炸弹爆炸触发同行的垂直炸弹，垂直炸弹再伤害其列上的覆盖物
    /// </summary>
    [Fact]
    public void ChainExplosion_DamagesCoversInSequence()
    {
        // Arrange
        //   0 1 2 3 4
        // 0 [R] B Y G P   <- [R] 被 Chain 覆盖
        // 1  R [H] B [V] G <- [H] 水平炸弹, [V] 垂直炸弹
        // 2  B  G  Y  R  P
        // 3 [Y] P  B  G  R <- [Y] 被 Chain 覆盖
        var rng = new StubRandom();
        var state = new GameState(5, 4, 6, rng);

        // 第 0 行
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Chain, health: 1));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Yellow, 2, 0));
        state.SetTile(3, 0, new Tile(4, TileType.Green, 3, 0));
        state.SetTile(4, 0, new Tile(5, TileType.Purple, 4, 0));

        // 第 1 行 - 水平炸弹和垂直炸弹
        state.SetTile(0, 1, new Tile(6, TileType.Red, 0, 1));
        var hBomb = new Tile(7, TileType.Red, 1, 1);
        hBomb.Bomb = BombType.Horizontal;
        state.SetTile(1, 1, hBomb);
        state.SetTile(2, 1, new Tile(8, TileType.Blue, 2, 1));
        var vBomb = new Tile(9, TileType.Red, 3, 1);
        vBomb.Bomb = BombType.Vertical;
        state.SetTile(3, 1, vBomb);
        state.SetTile(4, 1, new Tile(10, TileType.Green, 4, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(11, TileType.Blue, 0, 2));
        state.SetTile(1, 2, new Tile(12, TileType.Green, 1, 2));
        state.SetTile(2, 2, new Tile(13, TileType.Yellow, 2, 2));
        state.SetTile(3, 2, new Tile(14, TileType.Red, 3, 2));
        state.SetTile(4, 2, new Tile(15, TileType.Purple, 4, 2));

        // 第 3 行
        state.SetTile(0, 3, new Tile(16, TileType.Yellow, 0, 3));
        state.SetCover(new Position(0, 3), new Cover(CoverType.Chain, health: 1));
        state.SetTile(1, 3, new Tile(17, TileType.Purple, 1, 3));
        state.SetTile(2, 3, new Tile(18, TileType.Blue, 2, 3));
        state.SetTile(3, 3, new Tile(19, TileType.Green, 3, 3));
        state.SetTile(4, 3, new Tile(20, TileType.Red, 4, 3));

        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, bombRegistry);

        _output.WriteLine("初始状态:");
        PrintBoard(ref state);

        // Act: 模拟匹配处理触发水平炸弹
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = TileType.Red,
                Positions = new HashSet<Position> { new(0, 1), new(1, 1), new(2, 1) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        _output.WriteLine("连锁爆炸后:");
        PrintBoard(ref state);

        // Assert:
        // 1. 水平炸弹应该清除第 1 行
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type);
        Assert.Equal(TileType.None, state.GetTile(4, 1).Type);

        // 2. 垂直炸弹被触发，应该清除第 3 列
        Assert.Equal(TileType.None, state.GetTile(3, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(3, 2).Type);
        Assert.Equal(TileType.None, state.GetTile(3, 3).Type);

        _output.WriteLine("炸弹连锁爆炸正确伤害了覆盖物");
    }

    #endregion

    #region Explosion + Gravity Integration

    /// <summary>
    /// 测试：爆炸后的重力下落与覆盖物交互
    ///
    /// 场景：
    /// 1. 炸弹爆炸清除一行
    /// 2. 上方方块下落
    /// 3. 被覆盖物保护的方块不下落
    /// </summary>
    [Fact]
    public void Explosion_GravityFall_CoverInteraction()
    {
        // Arrange
        //   0 1 2
        // 0 [R] G B   <- [R] 被 Cage 覆盖
        // 1  R [H] B  <- [H] 水平炸弹
        // 2  G  B  Y
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        // 第 0 行
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Cage, health: 1));
        var greenTile0 = new Tile(2, TileType.Green, 1, 0);
        greenTile0.Position = new Vector2(1, 0);
        state.SetTile(1, 0, greenTile0);
        var blueTile0 = new Tile(3, TileType.Blue, 2, 0);
        blueTile0.Position = new Vector2(2, 0);
        state.SetTile(2, 0, blueTile0);

        // 第 1 行
        state.SetTile(0, 1, new Tile(4, TileType.Red, 0, 1));
        var hBomb = new Tile(5, TileType.Red, 1, 1);
        hBomb.Bomb = BombType.Horizontal;
        state.SetTile(1, 1, hBomb);
        state.SetTile(2, 1, new Tile(6, TileType.Blue, 2, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(7, TileType.Green, 0, 2));
        state.SetTile(1, 2, new Tile(8, TileType.Blue, 1, 2));
        state.SetTile(2, 2, new Tile(9, TileType.Yellow, 2, 2));

        var scoreSystem = new StubScoreSystem();
        var powerUpHandler = new PowerUpHandler(scoreSystem);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // 记录 ID
        var greenId = state.GetTile(1, 0).Id;
        var blueId = state.GetTile(2, 0).Id;

        _output.WriteLine("初始状态:");
        PrintBoard(ref state);

        // Act 1: 激活水平炸弹
        powerUpHandler.ActivateBomb(ref state, new Position(1, 1));

        _output.WriteLine("爆炸后:");
        PrintBoard(ref state);

        // 验证第 1 行被清除
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 1).Type);
        Assert.Equal(TileType.None, state.GetTile(2, 1).Type);

        // Act 2: 运行重力
        int frameCount = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 120);

        _output.WriteLine($"重力下落后 ({frameCount} 帧):");
        PrintBoard(ref state);

        // Assert:
        // 1. 被 Cage 覆盖的 (0,0) 不应该移动
        Assert.Equal(TileType.Red, state.GetTile(0, 0).Type);
        Assert.Equal(CoverType.Cage, state.GetCover(new Position(0, 0)).Type);

        // 2. (1,0) 和 (2,0) 的方块应该下落到第 1 行
        Assert.Equal(greenId, state.GetTile(1, 1).Id);
        Assert.Equal(blueId, state.GetTile(2, 1).Id);

        _output.WriteLine("爆炸后重力下落正确处理了覆盖物");
    }

    #endregion

    #region Helper Methods

    private void PrintBoard(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            string row = $"Row {y}: ";
            for (int x = 0; x < state.Width; x++)
            {
                var t = state.GetTile(x, y);
                var c = state.GetCover(new Position(x, y));

                string symbol;
                if (t.Type == TileType.None)
                {
                    symbol = "_";
                }
                else if (t.Bomb != BombType.None)
                {
                    symbol = t.Bomb switch
                    {
                        BombType.Horizontal => "H",
                        BombType.Vertical => "V",
                        BombType.Square5x5 => "W",
                        BombType.Color => "C",
                        _ => t.Type.ToString()[0].ToString()
                    };
                }
                else
                {
                    symbol = t.Type.ToString()[0].ToString();
                }

                if (c.Type != CoverType.None)
                {
                    symbol = $"[{symbol}]";
                }
                row += symbol.PadRight(5);
            }
            _output.WriteLine(row);
        }
    }

    private void PrintRow(ref GameState state, int y)
    {
        string row = $"Row {y}: ";
        for (int x = 0; x < state.Width; x++)
        {
            var t = state.GetTile(x, y);
            var c = state.GetCover(new Position(x, y));

            string symbol = t.Type == TileType.None ? "_" : t.Type.ToString()[0].ToString();
            if (t.Bomb != BombType.None)
            {
                symbol = t.Bomb.ToString()[0].ToString();
            }
            if (c.Type != CoverType.None)
            {
                symbol = $"[{symbol}]";
            }
            row += symbol.PadRight(5);
        }
        _output.WriteLine(row);
    }

    private void PrintColumn(ref GameState state, int x)
    {
        for (int y = 0; y < state.Height; y++)
        {
            var t = state.GetTile(x, y);
            var c = state.GetCover(new Position(x, y));
            _output.WriteLine($"  ({x},{y}): Type={t.Type}, Bomb={t.Bomb}, Cover={c.Type}");
        }
    }

    #endregion
}
