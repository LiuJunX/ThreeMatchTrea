using System;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestHelpers;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 集成测试：验证 RealtimeGravitySystem 和 AnimationSystem 协作时的行为
///
/// 背景：两个系统独立工作时都正确，但协作时可能产生冲突：
/// - GravitySystem 使用物理模拟更新 tile.Position 为浮点数
/// - AnimationSystem 将 tile.Position 插值到网格整数位置
///
/// 如果 AnimationSystem 处理了正在掉落的 tile，会导致位置被重置，
/// 造成视觉上的"不平滑"或"回弹"效果。
/// </summary>
public class GravityAnimationIntegrationTests
{
    private readonly ITestOutputHelper _output;

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    public GravityAnimationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 核心测试：验证 GravitySystem 和 AnimationSystem 协作时位置不会被重置
    ///
    /// 测试场景：
    /// 1. GravitySystem 更新 tile 位置到 3.3
    /// 2. AnimationSystem 运行
    /// 3. 验证位置没有被重置回 3.0
    /// </summary>
    [Fact]
    public void FallingTile_PositionShouldNotBeReset_WhenAnimationSystemRuns()
    {
        // Arrange
        var state = new GameState(1, 6, 5, new StubRandom());

        // 清空棋盘
        for (int y = 0; y < 6; y++)
        {
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));
        }

        // 在 (0, 0) 放置一个红色方块
        var tile = new Tile(100, TileType.Red, 0, 0);
        state.SetTile(0, 0, tile);

        var config = new Match3Config
        {
            GravitySpeed = 20.0f,
            MaxFallSpeed = 25.0f
        };

        var gravitySystem = new RealtimeGravitySystem(config, new StubRandom());
        var animationSystem = new AnimationSystem(config);

        float dt = 1.0f / 60.0f;

        // Act: 模拟多帧更新，交替运行两个系统
        for (int frame = 0; frame < 30; frame++)
        {
            // 记录 GravitySystem 更新前的位置
            var tileBeforeGravity = FindTile(ref state, TileType.Red);
            float posBeforeGravity = tileBeforeGravity.Position.Y;

            // GravitySystem 更新
            gravitySystem.Update(ref state, dt);

            var tileAfterGravity = FindTile(ref state, TileType.Red);
            float posAfterGravity = tileAfterGravity.Position.Y;

            // AnimationSystem 更新
            animationSystem.Animate(ref state, dt);

            var tileAfterAnimation = FindTile(ref state, TileType.Red);
            float posAfterAnimation = tileAfterAnimation.Position.Y;

            _output.WriteLine($"Frame {frame}: Before={posBeforeGravity:F3} -> AfterGravity={posAfterGravity:F3} -> AfterAnimation={posAfterAnimation:F3} | IsFalling={tileAfterGravity.IsFalling}");

            // Assert: 如果 tile 正在掉落，AnimationSystem 不应该重置位置
            if (tileAfterGravity.IsFalling)
            {
                // 位置应该保持 GravitySystem 更新后的值，不应该被 AnimationSystem 拉回
                Assert.True(
                    Math.Abs(posAfterAnimation - posAfterGravity) < 0.01f,
                    $"Frame {frame}: AnimationSystem 不应修改掉落中 tile 的位置！" +
                    $"GravitySystem 更新后 Y={posAfterGravity:F3}，但 AnimationSystem 后 Y={posAfterAnimation:F3}"
                );
            }

            // 如果 tile 已停止，结束测试
            if (!tileAfterAnimation.IsFalling && posAfterAnimation >= 4.9f)
            {
                _output.WriteLine($"Tile 在第 {frame} 帧到达底部并停止");
                break;
            }
        }
    }

    /// <summary>
    /// 测试：验证掉落过程中位置是连续递增的（无回弹）
    /// </summary>
    [Fact]
    public void FallingTile_PositionShouldBeMonotonicallyIncreasing()
    {
        // Arrange
        var state = new GameState(1, 8, 5, new StubRandom());

        for (int y = 0; y < 8; y++)
        {
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));
        }

        var tile = new Tile(100, TileType.Red, 0, 0);
        state.SetTile(0, 0, tile);

        var config = new Match3Config
        {
            GravitySpeed = 20.0f,
            MaxFallSpeed = 25.0f
        };

        var gravitySystem = new RealtimeGravitySystem(config, new StubRandom());
        var animationSystem = new AnimationSystem(config);

        float dt = 1.0f / 60.0f;
        float prevPosY = 0f;
        int regressionCount = 0;

        // Act & Assert
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);
            animationSystem.Animate(ref state, dt);

            var currentTile = FindTile(ref state, TileType.Red);
            float currentPosY = currentTile.Position.Y;

            // 位置应该单调递增（或停止时保持不变）
            if (currentPosY < prevPosY - 0.001f)
            {
                _output.WriteLine($"Frame {frame}: 位置回退! {prevPosY:F3} -> {currentPosY:F3}");
                regressionCount++;
            }

            prevPosY = currentPosY;

            if (!currentTile.IsFalling && currentPosY >= 6.9f)
            {
                break;
            }
        }

        Assert.Equal(0, regressionCount);
    }

    /// <summary>
    /// 测试：静止的 tile 应该被 AnimationSystem 吸附到整数位置
    /// </summary>
    [Fact]
    public void StoppedTile_ShouldBeSnappedToIntegerPosition_ByAnimationSystem()
    {
        // Arrange
        var state = new GameState(1, 3, 5, new StubRandom());

        for (int y = 0; y < 3; y++)
        {
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));
        }

        // 放置一个已停止的 tile，位置略微偏离整数
        var tile = new Tile(100, TileType.Red, 0, 2);
        tile.Position = new Vector2(0, 2.05f); // 略微偏离
        tile.Velocity = Vector2.Zero;
        tile.IsFalling = false;
        state.SetTile(0, 2, tile);

        var config = new Match3Config { GravitySpeed = 20.0f };
        var animationSystem = new AnimationSystem(config);

        // Act
        animationSystem.Animate(ref state, 1.0f / 60.0f);

        // Assert
        var resultTile = state.GetTile(0, 2);
        Assert.Equal(2.0f, resultTile.Position.Y, 2); // 应该被吸附到整数位置
    }

    private Tile FindTile(ref GameState state, TileType type)
    {
        for (int y = 0; y < state.Height; y++)
        {
            var t = state.GetTile(0, y);
            if (t.Type == type)
            {
                return t;
            }
        }
        return default;
    }

    #region Swap Animation Integration Tests

    /// <summary>
    /// 关键测试：验证无效交换的"交换-回退"动画流程
    ///
    /// 这个测试覆盖了之前遗漏的 bug：
    /// - 旧代码：两次 SwapTiles 在同一帧执行，没有动画
    /// - 新代码：先交换动画 → 检查 match → 回退动画
    ///
    /// 测试验证：
    /// 1. 交换后第 1 帧：tiles 应该在动画中（不在目标位置）
    /// 2. 动画完成前：tiles 应该逐渐移动
    /// 3. 动画完成后：如果无 match，应该触发回退
    /// </summary>
    [Fact]
    public void SwapAnimation_InvalidSwap_ShouldShowSwapThenRevert()
    {
        // Arrange: 创建 2x1 棋盘
        var state = new GameState(2, 1, 5, new StubRandom());

        var redTile = new Tile(1, TileType.Red, 0, 0);
        redTile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, redTile);

        var blueTile = new Tile(2, TileType.Blue, 1, 0);
        blueTile.Position = new Vector2(1, 0);
        state.SetTile(1, 0, blueTile);

        var config = new Match3Config { GravitySpeed = 10.0f };
        var animationSystem = new AnimationSystem(config);

        // Act 1: 模拟交换（只交换引用，保持视觉位置）
        var temp = state.Grid[0];
        state.Grid[0] = state.Grid[1];
        state.Grid[1] = temp;

        // Assert 1: 交换后第 1 帧 - tiles 应该在动画中
        bool stableAfterSwap = animationSystem.Animate(ref state, 0.016f);
        Assert.False(stableAfterSwap, "交换后应该有动画在进行");

        // 记录动画中的位置
        float blueXDuringAnimation = state.Grid[0].Position.X;
        float redXDuringAnimation = state.Grid[1].Position.X;

        _output.WriteLine($"动画中: Blue.X={blueXDuringAnimation:F3}, Red.X={redXDuringAnimation:F3}");

        // Assert 2: tiles 应该在移动中（不在起点也不在终点）
        Assert.True(blueXDuringAnimation < 1.0f && blueXDuringAnimation > 0.0f,
            "Blue 应该在移动中（0 < X < 1）");
        Assert.True(redXDuringAnimation > 0.0f && redXDuringAnimation < 1.0f,
            "Red 应该在移动中（0 < X < 1）");

        // Act 2: 继续动画直到完成
        bool stable = false;
        for (int i = 0; i < 100 && !stable; i++)
        {
            stable = animationSystem.Animate(ref state, 0.016f);
        }

        // Assert 3: 动画完成，tiles 到达交换后的位置
        Assert.True(stable, "动画应该完成");
        Assert.Equal(0, state.Grid[0].Position.X, 2); // Blue 到达 (0,0)
        Assert.Equal(1, state.Grid[1].Position.X, 2); // Red 到达 (1,0)

        _output.WriteLine($"交换动画完成: Blue 在 X={state.Grid[0].Position.X:F3}, Red 在 X={state.Grid[1].Position.X:F3}");

        // Act 3: 模拟"无 match，需要回退"- 再次交换
        temp = state.Grid[0];
        state.Grid[0] = state.Grid[1];
        state.Grid[1] = temp;

        // Assert 4: 回退交换后也应该有动画
        bool stableAfterRevert = animationSystem.Animate(ref state, 0.016f);
        Assert.False(stableAfterRevert, "回退交换后也应该有动画");

        // Act 4: 继续动画直到回退完成
        stable = false;
        for (int i = 0; i < 100 && !stable; i++)
        {
            stable = animationSystem.Animate(ref state, 0.016f);
        }

        // Assert 5: 回退完成，tiles 回到原始位置
        Assert.True(stable, "回退动画应该完成");
        Assert.Equal(0, state.Grid[0].Position.X, 2); // Red 回到 (0,0)
        Assert.Equal(1, state.Grid[1].Position.X, 2); // Blue 回到 (1,0)
        Assert.Equal(TileType.Red, state.Grid[0].Type);  // 类型也应该正确
        Assert.Equal(TileType.Blue, state.Grid[1].Type);

        _output.WriteLine($"回退动画完成: Red 在 X={state.Grid[0].Position.X:F3}, Blue 在 X={state.Grid[1].Position.X:F3}");
    }

    /// <summary>
    /// 集成测试：验证交换后 AnimationSystem 能正确产生动画
    ///
    /// 这是对 Match3Engine.SwapTiles 修复的端到端验证：
    /// - SwapTiles 只交换 Grid 引用，保持视觉位置不变
    /// - AnimationSystem 将 tile 从原视觉位置动画到新网格位置
    /// </summary>
    [Fact]
    public void SwapAnimation_Integration_TilesShouldAnimateToNewPositions()
    {
        // Arrange: 创建 2x1 棋盘
        var state = new GameState(2, 1, 5, new StubRandom());

        // 设置两个不同颜色的 tile
        var redTile = new Tile(1, TileType.Red, 0, 0);
        redTile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, redTile);

        var blueTile = new Tile(2, TileType.Blue, 1, 0);
        blueTile.Position = new Vector2(1, 0);
        state.SetTile(1, 0, blueTile);

        var config = new Match3Config { GravitySpeed = 10.0f };
        var animationSystem = new AnimationSystem(config);

        // 验证初始状态
        Assert.True(animationSystem.Animate(ref state, 0.016f), "初始状态应该是稳定的");

        // Act: 模拟 SwapTiles（只交换引用，保持视觉位置不变）
        var temp = state.Grid[0];
        state.Grid[0] = state.Grid[1];
        state.Grid[1] = temp;

        // 交换后验证视觉位置保持不变
        Assert.Equal(new Vector2(1, 0), state.Grid[0].Position); // Blue 仍在视觉位置 (1,0)
        Assert.Equal(new Vector2(0, 0), state.Grid[1].Position); // Red 仍在视觉位置 (0,0)

        // Act: 运行动画
        bool stable = animationSystem.Animate(ref state, 0.016f);

        // Assert: 动画应该正在进行
        Assert.False(stable, "交换后应该有动画在进行");

        // Grid[0] (Blue) 应该从 (1,0) 向 (0,0) 移动
        Assert.True(state.Grid[0].Position.X < 1.0f, "Blue 应该向左移动");
        Assert.True(state.Grid[0].Position.X > 0.0f, "Blue 还没到达目标");

        // Grid[1] (Red) 应该从 (0,0) 向 (1,0) 移动
        Assert.True(state.Grid[1].Position.X > 0.0f, "Red 应该向右移动");
        Assert.True(state.Grid[1].Position.X < 1.0f, "Red 还没到达目标");

        _output.WriteLine($"动画第1帧: Blue.X={state.Grid[0].Position.X:F3}, Red.X={state.Grid[1].Position.X:F3}");

        // 继续运行动画直到完成
        int maxFrames = 100;
        for (int i = 0; i < maxFrames && !stable; i++)
        {
            stable = animationSystem.Animate(ref state, 0.016f);
        }

        // Assert: 动画应该完成，tile 到达目标位置
        Assert.True(stable, "动画应该最终完成");
        Assert.Equal(0, state.Grid[0].Position.X, 2); // Blue 到达 (0,0)
        Assert.Equal(1, state.Grid[1].Position.X, 2); // Red 到达 (1,0)

        _output.WriteLine($"动画完成: Blue 在 ({state.Grid[0].Position.X:F3}, {state.Grid[0].Position.Y:F3})");
        _output.WriteLine($"动画完成: Red 在 ({state.Grid[1].Position.X:F3}, {state.Grid[1].Position.Y:F3})");
    }

    /// <summary>
    /// 回归测试：如果 SwapTiles 错误地也更新了视觉位置，动画就不会产生
    ///
    /// 这个测试模拟了之前的 bug：交换后立即更新视觉位置到目标位置
    /// </summary>
    [Fact]
    public void SwapAnimation_BuggySwap_WouldNotProduceAnimation()
    {
        // Arrange
        var state = new GameState(2, 1, 5, new StubRandom());

        var redTile = new Tile(1, TileType.Red, 0, 0);
        redTile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, redTile);

        var blueTile = new Tile(2, TileType.Blue, 1, 0);
        blueTile.Position = new Vector2(1, 0);
        state.SetTile(1, 0, blueTile);

        var config = new Match3Config { GravitySpeed = 10.0f };
        var animationSystem = new AnimationSystem(config);

        // Act: 模拟 BUGGY 的 SwapTiles（交换引用后也更新视觉位置）
        var temp = state.Grid[0];
        state.Grid[0] = state.Grid[1];
        state.Grid[1] = temp;

        // BUG: 错误地将视觉位置也更新到目标位置
        state.Grid[0].Position = new Vector2(0, 0); // Blue 直接跳到 (0,0)
        state.Grid[1].Position = new Vector2(1, 0); // Red 直接跳到 (1,0)

        // Act: 运行动画
        bool stable = animationSystem.Animate(ref state, 0.016f);

        // Assert: 因为视觉位置已经在目标位置，所以"稳定"（没有动画）
        Assert.True(stable, "Buggy 实现会导致没有动画（这是 bug 的表现）");

        _output.WriteLine("此测试展示了 bug 的表现：交换后没有动画");
    }

    /// <summary>
    /// 使用 AnimationTestHelper 简化的测试示例
    /// </summary>
    [Fact]
    public void SwapAnimation_UsingHelper_SimplifiedTest()
    {
        // Arrange
        var state = new GameState(2, 1, 5, new StubRandom());

        var redTile = new Tile(1, TileType.Red, 0, 0);
        redTile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, redTile);

        var blueTile = new Tile(2, TileType.Blue, 1, 0);
        blueTile.Position = new Vector2(1, 0);
        state.SetTile(1, 0, blueTile);

        var config = new Match3Config { GravitySpeed = 10.0f };
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // Act: 使用辅助类模拟无效交换
        var result = helper.SimulateInvalidSwap(ref state, animationSystem, 0, 1);

        // Assert
        Assert.True(result.SwapResult.FrameCount > 0, "交换应该有动画帧");
        Assert.True(result.RevertResult.FrameCount > 0, "回退应该有动画帧");
        Assert.Equal(TileType.Red, state.Grid[0].Type);   // 回到原始位置
        Assert.Equal(TileType.Blue, state.Grid[1].Type);

        _output.WriteLine($"交换动画: {result.SwapResult.FrameCount} 帧");
        _output.WriteLine($"回退动画: {result.RevertResult.FrameCount} 帧");
        _output.WriteLine($"总计: {result.TotalFrameCount} 帧");
    }

    #endregion
}
