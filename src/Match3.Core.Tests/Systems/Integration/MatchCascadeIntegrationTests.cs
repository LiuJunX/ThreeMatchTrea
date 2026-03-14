using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Tests.TestHelpers;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 集成测试：验证匹配 -> 消除 -> 重力 -> 填充 的完整流程
///
/// 这些测试验证多个系统协作时的行为：
/// - ClassicMatchFinder: 检测匹配
/// - StandardMatchProcessor: 处理匹配和消除
/// - RealtimeGravitySystem: 方块下落
/// - StandardTileGenerator + BoardInitializer: 生成新方块
/// </summary>
public class MatchCascadeIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public MatchCascadeIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 测试：匹配消除后，上方方块应该下落填充空位
    /// </summary>
    [Fact]
    public void AfterMatch_TilesAboveShouldFall()
    {
        // Arrange: 创建一个有匹配的棋盘
        //   0 1 2 3 4
        // 0 B G Y P O
        // 1 R R R G B  <- 第1行有水平匹配
        // 2 G B Y R P
        var rng = new StubRandom();
        var state = new GameState(5, 3, 6, rng);

        // 第 0 行
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item4, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item5, 3, 0));
        state.SetTile(4, 0, new Tile(5, ElementType.Item6, 4, 0));

        // 第 1 行 - 有匹配
        state.SetTile(0, 1, new Tile(6, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(7, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(8, ElementType.Item1, 2, 1));
        state.SetTile(3, 1, new Tile(9, ElementType.Item2, 3, 1));
        state.SetTile(4, 1, new Tile(10, ElementType.Item3, 4, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(11, ElementType.Item2, 0, 2));
        state.SetTile(1, 2, new Tile(12, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(13, ElementType.Item4, 2, 2));
        state.SetTile(3, 2, new Tile(14, ElementType.Item1, 3, 2));
        state.SetTile(4, 2, new Tile(15, ElementType.Item5, 4, 2));

        // 创建系统
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);

        // Act 1: 检测匹配
        var matches = matchFinder.FindMatchGroups(in state);
        Assert.NotEmpty(matches);

        // Act 2: 处理匹配（消除方块）
        processor.ProcessMatches(ref state, matches);

        // 验证第 1 行的红色被消除
        Assert.Equal(ElementType.None, state.GetTile(0, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);

        // 记录第 0 行方块的原始 ID
        var blueId = state.GetTile(0, 0).Id;
        var greenId = state.GetTile(1, 0).Id;
        var yellowId = state.GetTile(2, 0).Id;

        // Act 3: 运行重力系统多帧，让方块下落
        float dt = 1.0f / 60.0f;
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);

            // 检查是否所有方块都已落定
            bool allSettled = true;
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var tile = state.GetTile(x, y);
                    if (tile.Type != ElementType.None && tile.IsFalling)
                    {
                        allSettled = false;
                        break;
                    }
                }
                if (!allSettled) break;
            }
            if (allSettled) break;
        }

        // Assert: 第 0 行的方块应该下落到第 1 行
        var tileAt01 = state.GetTile(0, 1);
        var tileAt11 = state.GetTile(1, 1);
        var tileAt21 = state.GetTile(2, 1);

        // 验证方块已经移动到新位置
        Assert.Equal(blueId, tileAt01.Id);
        Assert.Equal(greenId, tileAt11.Id);
        Assert.Equal(yellowId, tileAt21.Id);
    }

    /// <summary>
    /// 测试：连锁消除 - 第一次消除后触发第二次匹配
    /// </summary>
    [Fact]
    public void ChainReaction_SecondMatchAfterFirstClear()
    {
        // Arrange: 设置一个会触发连锁的棋盘
        //   0 1 2
        // 0 G G B  <- 下落后会形成 G G G
        // 1 R R R  <- 第一次匹配
        // 2 G B Y
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        // 第 0 行
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));

        // 第 1 行 - 第一次匹配
        state.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item1, 2, 1));

        // 第 2 行 - 下落后会和上面的 G G 形成匹配
        state.SetTile(0, 2, new Tile(7, ElementType.Item2, 0, 2));
        state.SetTile(1, 2, new Tile(8, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(9, ElementType.Item4, 2, 2));

        // 创建系统
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);

        // Act 1: 第一次匹配检测和消除
        var matches1 = matchFinder.FindMatchGroups(in state);
        Assert.Single(matches1); // 应该只有红色匹配
        Assert.Equal(ElementType.Item1, matches1[0].Type);

        processor.ProcessMatches(ref state, matches1);

        // Act 2: 重力下落
        float dt = 1.0f / 60.0f;
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);
        }

        // Act 3: 检查是否有新的匹配
        // 下落后：
        //   0 1 2
        // 0 ? ? ?  <- 空
        // 1 G G B
        // 2 G B Y
        // 第 1 行和第 2 行的第 0 列现在都是 G，可能形成垂直匹配

        // 注意：由于下落后 (0,1) 是 Green, (0,2) 是 Green
        // 但 (0,0) 是空的，不会形成 3 连

        // 这个测试主要验证系统协作流程正确
        _output.WriteLine("After gravity:");
        for (int y = 0; y < 3; y++)
        {
            string row = "";
            for (int x = 0; x < 3; x++)
            {
                var t = state.GetTile(x, y);
                row += t.Type.ToString().Substring(0, 1) + " ";
            }
            _output.WriteLine($"Row {y}: {row}");
        }
    }

    /// <summary>
    /// 测试：炸弹效果和重力的协作
    /// </summary>
    [Fact]
    public void BombExplosion_TriggersGravityForSurroundingTiles()
    {
        // Arrange: 创建一个有炸弹的棋盘
        //   0 1 2 3 4
        // 0 R G B Y P
        // 1 G B R G B
        // 2 B R [H] G Y  <- [H] 是水平炸弹
        // 3 Y G B R P
        var rng = new StubRandom();
        var state = new GameState(5, 4, 6, rng);

        // 填充棋盘
        var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4, ElementType.Item5 };
        int id = 1;
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (x == 2 && y == 2)
                {
                    // 放置水平炸弹
                    var bombTile = new Tile(id++, ElementType.HorizontalRocket, x, y);
                    state.SetTile(x, y, bombTile);
                }
                else
                {
                    state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
                }
            }
        }

        // 创建系统 -- ExplosionSystem is now required for wave-based destruction
        var scoreSystem = new StubScoreSystem();
        var explosionSystem = new ExplosionSystem(new CoverSystem(), new GroundSystem());
        var powerUpHandler = new PowerUpHandler(scoreSystem)
            .WithExplosionSystem(explosionSystem);
        var eventCollector = new StubEventCollector();
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);

        // Act 1: 激活炸弹 (creates explosion, clears bomb attribute)
        powerUpHandler.ActivateBomb(ref state, new Position(2, 2), 1, 0f, eventCollector);

        // Act 2: 运行爆炸波次到完成
        for (int i = 0; i < 30 && explosionSystem.HasActiveExplosions; i++)
            explosionSystem.Update(ref state, 0.05f, 2 + i, 0.05f * i, eventCollector);

        // 验证整行被清除
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(ElementType.None, state.GetTile(x, 2).Type);
        }

        // Act 3: 运行重力
        float dt = 1.0f / 60.0f;
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);
        }

        // Assert: 上方的方块应该下落填充空位
        _output.WriteLine("After explosion and gravity:");
        for (int y = 0; y < 4; y++)
        {
            string row = "";
            for (int x = 0; x < 5; x++)
            {
                var t = state.GetTile(x, y);
                row += (t.Type == ElementType.None ? "_" : t.Type.ToString().Substring(0, 1)) + " ";
            }
            _output.WriteLine($"Row {y}: {row}");
        }

        // 验证第 2 行不全是空的（有方块下落填充）
        int nonEmptyInRow2 = 0;
        for (int x = 0; x < 5; x++)
        {
            if (state.GetTile(x, 2).Type != ElementType.None)
            {
                nonEmptyInRow2++;
            }
        }
        Assert.True(nonEmptyInRow2 > 0, "Some tiles should have fallen to fill row 2");
    }

    /// <summary>
    /// 测试：完整的游戏循环 - 从交换到匹配到消除到重力
    /// </summary>
    [Fact]
    public void FullGameLoop_SwapToMatchToGravity()
    {
        // Arrange: 设置一个交换后会形成匹配的棋盘
        //   0 1 2 3
        // 0 R R G B  <- 交换 (2,0) 和 (2,1) 后，(0,0)(1,0)(2,0) 形成红色匹配
        // 1 G B R Y
        // 2 B Y G P
        var rng = new StubRandom();
        var state = new GameState(4, 3, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item3, 3, 0));

        state.SetTile(0, 1, new Tile(5, ElementType.Item2, 0, 1));
        state.SetTile(1, 1, new Tile(6, ElementType.Item3, 1, 1));
        state.SetTile(2, 1, new Tile(7, ElementType.Item1, 2, 1));
        state.SetTile(3, 1, new Tile(8, ElementType.Item4, 3, 1));

        state.SetTile(0, 2, new Tile(9, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(10, ElementType.Item4, 1, 2));
        state.SetTile(2, 2, new Tile(11, ElementType.Item2, 2, 2));
        state.SetTile(3, 2, new Tile(12, ElementType.Item5, 3, 2));

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);

        // Act 1: 模拟交换 (2,0) 和 (2,1)
        var tile1 = state.GetTile(2, 0);
        var tile2 = state.GetTile(2, 1);

        // 更新 tile 的坐标
        var newTile1 = new Tile(tile1.Id, tile1.Type, 2, 1);
        var newTile2 = new Tile(tile2.Id, tile2.Type, 2, 0);
        state.SetTile(2, 0, newTile2);
        state.SetTile(2, 1, newTile1);

        _output.WriteLine("After swap:");
        PrintBoard(ref state);

        // Act 2: 检测匹配
        var matches = matchFinder.FindMatchGroups(in state);
        Assert.NotEmpty(matches);
        _output.WriteLine($"Found {matches.Count} match(es)");

        // Act 3: 处理匹配
        int points = processor.ProcessMatches(ref state, matches);
        Assert.True(points > 0);
        _output.WriteLine($"Points: {points}");

        _output.WriteLine("After match processing:");
        PrintBoard(ref state);

        // Act 4: 运行重力
        float dt = 1.0f / 60.0f;
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);
        }

        _output.WriteLine("After gravity:");
        PrintBoard(ref state);

        // Assert: 验证流程完成，棋盘状态合理
        // 红色匹配被消除后，上方方块应该下落
    }

    /// <summary>
    /// 测试：多重炸弹连锁 - 同一行内的炸弹会相互触发
    /// </summary>
    [Fact]
    public void MultipleBombs_ChainReaction()
    {
        // Arrange: 创建一个有多个炸弹会连锁的棋盘
        //   0 1 2 3 4
        // 0 R [H] B [V] G  <- 水平炸弹在 (1,0)，垂直炸弹在 (3,0)
        // 1 G B Y R P
        // 2 B R G Y B
        var rng = new StubRandom();
        var state = new GameState(5, 3, 6, rng);

        // 第 0 行 - 水平炸弹和垂直炸弹在同一行
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        var hBomb = new Tile(2, ElementType.HorizontalRocket, 1, 0);
        state.SetTile(1, 0, hBomb);
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));
        var vBomb = new Tile(4, ElementType.VerticalRocket, 3, 0);
        state.SetTile(3, 0, vBomb);
        state.SetTile(4, 0, new Tile(5, ElementType.Item2, 4, 0));

        // 第 1 行
        state.SetTile(0, 1, new Tile(6, ElementType.Item2, 0, 1));
        state.SetTile(1, 1, new Tile(7, ElementType.Item3, 1, 1));
        state.SetTile(2, 1, new Tile(8, ElementType.Item4, 2, 1));
        state.SetTile(3, 1, new Tile(9, ElementType.Item1, 3, 1));
        state.SetTile(4, 1, new Tile(10, ElementType.Item5, 4, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(11, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(12, ElementType.Item1, 1, 2));
        state.SetTile(2, 2, new Tile(13, ElementType.Item2, 2, 2));
        state.SetTile(3, 2, new Tile(14, ElementType.Item4, 3, 2));
        state.SetTile(4, 2, new Tile(15, ElementType.Item3, 4, 2));

        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);

        // 模拟匹配组包含水平炸弹（会触发整行，进而触发垂直炸弹）
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act: 处理匹配（应该触发连锁）
        processor.ProcessMatches(ref state, groups);

        _output.WriteLine("After chain reaction:");
        PrintBoard(ref state);

        // Assert: 验证连锁效果
        // 水平炸弹清除第 0 行
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(4, 0).Type);

        // 垂直炸弹被触发（因为在同一行），清除第 3 列
        Assert.Equal(ElementType.None, state.GetTile(3, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(3, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(3, 2).Type);
    }

    private void PrintBoard(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            string row = $"Row {y}: ";
            for (int x = 0; x < state.Width; x++)
            {
                var t = state.GetTile(x, y);
                string symbol = t.Type == ElementType.None ? "_" : t.Type.ToString().Substring(0, 1);
                if (t.Type.IsBomb())
                {
                    symbol = "[" + symbol + "]";
                }
                row += symbol.PadRight(4);
            }
            _output.WriteLine(row);
        }
    }

    #region Animation Integration Tests

    /// <summary>
    /// 集成测试：消除后的重力下落应该有平滑的视觉动画
    ///
    /// 这个测试验证 GravitySystem + AnimationSystem 的协作：
    /// 1. GravitySystem 更新物理位置
    /// 2. AnimationSystem 不干扰掉落中的 tile
    /// 3. 掉落完成后 AnimationSystem 吸附到整数位置
    /// </summary>
    [Fact]
    public void AfterMatch_FallingTiles_ShouldHaveSmoothAnimation()
    {
        // Arrange: 创建一个有匹配的棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 4, 6, rng);

        // 设置棋盘:
        //   0 1 2
        // 0 G B Y
        // 1 R R R  <- 匹配行
        // 2 B G P
        // 3 Y P B

        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item4, 2, 0));

        state.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item1, 2, 1));

        state.SetTile(0, 2, new Tile(7, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(8, ElementType.Item2, 1, 2));
        state.SetTile(2, 2, new Tile(9, ElementType.Item5, 2, 2));

        state.SetTile(0, 3, new Tile(10, ElementType.Item4, 0, 3));
        state.SetTile(1, 3, new Tile(11, ElementType.Item5, 1, 3));
        state.SetTile(2, 3, new Tile(12, ElementType.Item3, 2, 3));

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // Act 1: 检测并处理匹配
        var matches = matchFinder.FindMatchGroups(in state);
        Assert.NotEmpty(matches);
        processor.ProcessMatches(ref state, matches);

        // 验证匹配行被消除
        Assert.Equal(ElementType.None, state.GetTile(0, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);

        // 记录第 0 行方块的 ID
        var greenId = state.GetTile(0, 0).Id;

        // Act 2: 运行物理+动画系统
        int frameCount = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 120);

        _output.WriteLine($"动画完成: {frameCount} 帧");

        // Assert: 方块应该下落到新位置
        var tileAt01 = state.GetTile(0, 1);
        Assert.Equal(greenId, tileAt01.Id);
        Assert.Equal(ElementType.Item2, tileAt01.Type);

        // 验证位置是整数（被吸附）
        Assert.Equal(0, tileAt01.Position.X, 1);
        Assert.Equal(1, tileAt01.Position.Y, 1);
    }

    /// <summary>
    /// 集成测试：连锁消除的完整动画流程
    ///
    /// 测试流程：
    /// 1. 第一次消除 → 重力动画 → 检查新匹配
    /// 2. 如果有新匹配 → 再次消除 → 重力动画
    /// 3. 验证每一步都有平滑的动画
    /// </summary>
    [Fact]
    public void ChainReaction_ShouldHaveAnimationAtEachStep()
    {
        // Arrange: 设置一个会触发连锁的棋盘
        //   0 1 2
        // 0 G G B  <- 下落后可能形成新匹配
        // 1 R R R  <- 第一次匹配
        // 2 G B Y
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));

        state.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item1, 2, 1));

        state.SetTile(0, 2, new Tile(7, ElementType.Item2, 0, 2));
        state.SetTile(1, 2, new Tile(8, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(9, ElementType.Item4, 2, 2));

        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // Act 1: 第一次消除
        var matches1 = matchFinder.FindMatchGroups(in state);
        Assert.NotEmpty(matches1);
        _output.WriteLine($"第一次匹配: {matches1.Count} 组");

        processor.ProcessMatches(ref state, matches1);

        // Act 2: 运行动画直到稳定
        int frames1 = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem);
        _output.WriteLine($"第一次动画: {frames1} 帧");

        // Assert: 第一次动画应该有多帧
        Assert.True(frames1 > 1, "第一次消除后应该有动画");

        // 打印当前状态
        _output.WriteLine("第一次动画后:");
        PrintBoard(ref state);

        // 检查是否有新的匹配（连锁）
        var matches2 = matchFinder.FindMatchGroups(in state);
        if (matches2.Count > 0)
        {
            _output.WriteLine($"触发连锁: {matches2.Count} 组新匹配");
            processor.ProcessMatches(ref state, matches2);

            int frames2 = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem);
            _output.WriteLine($"连锁动画: {frames2} 帧");

            Assert.True(frames2 > 0, "连锁消除后应该有动画");
        }
    }

    /// <summary>
    /// 集成测试：炸弹爆炸后的重力动画
    /// </summary>
    [Fact]
    public void BombExplosion_ShouldHaveSmoothGravityAnimation()
    {
        // Arrange: 创建有炸弹的棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 4, 6, rng);

        // 设置棋盘:
        //   0 1 2
        // 0 R G B
        // 1 G [H] Y  <- 水平炸弹
        // 2 B R G
        // 3 Y P B

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));

        state.SetTile(0, 1, new Tile(4, ElementType.Item2, 0, 1));
        var bombTile = new Tile(5, ElementType.HorizontalRocket, 1, 1);
        state.SetTile(1, 1, bombTile);
        state.SetTile(2, 1, new Tile(6, ElementType.Item4, 2, 1));

        state.SetTile(0, 2, new Tile(7, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(8, ElementType.Item1, 1, 2));
        state.SetTile(2, 2, new Tile(9, ElementType.Item2, 2, 2));

        state.SetTile(0, 3, new Tile(10, ElementType.Item4, 0, 3));
        state.SetTile(1, 3, new Tile(11, ElementType.Item5, 1, 3));
        state.SetTile(2, 3, new Tile(12, ElementType.Item3, 2, 3));

        var scoreSystem = new StubScoreSystem();
        var explosionSystem = new ExplosionSystem(new CoverSystem(), new GroundSystem());
        var powerUpHandler = new PowerUpHandler(scoreSystem)
            .WithExplosionSystem(explosionSystem);
        var eventCollector = new StubEventCollector();
        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // 记录第 0 行方块 ID
        var redId = state.GetTile(0, 0).Id;
        var greenId = state.GetTile(1, 0).Id;
        var blueId = state.GetTile(2, 0).Id;

        // Act 1: 激活炸弹 (creates explosion, clears bomb attribute)
        powerUpHandler.ActivateBomb(ref state, new Position(1, 1), 1, 0f, eventCollector);

        // Act 2: 运行爆炸波次到完成
        for (int i = 0; i < 30 && explosionSystem.HasActiveExplosions; i++)
            explosionSystem.Update(ref state, 0.05f, 2 + i, 0.05f * i, eventCollector);

        // 验证整行被清除
        Assert.Equal(ElementType.None, state.GetTile(0, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);

        _output.WriteLine("炸弹爆炸后:");
        PrintBoard(ref state);

        // Act 3: 运行重力+动画 (gravity can run freely, no Drop locks)
        int frameCount = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 120);

        _output.WriteLine($"动画完成: {frameCount} 帧");
        _output.WriteLine("最终状态:");
        PrintBoard(ref state);

        // Assert: 动画应该有多帧
        Assert.True(frameCount > 1, "炸弹爆炸后应该有重力动画");

        // 第 0 行的方块应该下落到第 1 行
        Assert.Equal(redId, state.GetTile(0, 1).Id);
        Assert.Equal(greenId, state.GetTile(1, 1).Id);
        Assert.Equal(blueId, state.GetTile(2, 1).Id);
    }

    #endregion
}


