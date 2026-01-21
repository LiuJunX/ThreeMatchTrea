using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestHelpers;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 集成测试：验证 Refill + Animation 系统协作
///
/// 覆盖的核心场景：
/// 1. 新生成方块的动画从顶部开始
/// 2. 新生成方块与现有方块的下落协调
/// 3. 连续填充时的动画平滑性
/// 4. 多列同时填充的动画同步
/// </summary>
public class RefillAnimationIntegrationTests
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

    /// <summary>
    /// 简单的 SpawnModel，按固定颜色顺序生成方块
    /// </summary>
    private class SequentialSpawnModel : ISpawnModel
    {
        private readonly TileType[] _sequence;
        private int _index = 0;

        public SequentialSpawnModel(params TileType[] sequence)
        {
            _sequence = sequence;
        }

        public TileType Predict(ref GameState state, int column, in SpawnContext context)
        {
            var type = _sequence[_index % _sequence.Length];
            _index++;
            return type;
        }
    }

    public RefillAnimationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Basic Refill Animation Tests

    /// <summary>
    /// 测试：新生成的方块从顶部 (-1) 位置开始动画
    ///
    /// 场景：顶行为空，触发填充
    /// 预期：新方块从 Y=-1 开始，平滑下落到 Y=0
    /// </summary>
    [Fact]
    public void NewTile_StartsFromAboveBoard()
    {
        // Arrange: 创建一个顶行为空的棋盘
        var rng = new StubRandom();
        var state = new GameState(1, 3, 6, rng);

        // 顶行为空
        state.SetTile(0, 0, new Tile(1, TileType.None, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.Blue, 0, 2));

        var spawnModel = new SequentialSpawnModel(TileType.Green);
        var refillSystem = new RealtimeRefillSystem(spawnModel);

        // Act: 触发填充
        refillSystem.Update(ref state);

        // Assert: 新方块应该在 (0, 0) 但位置从 Y=-1 开始
        var newTile = state.GetTile(0, 0);
        Assert.Equal(TileType.Green, newTile.Type);
        Assert.Equal(-1.0f, newTile.Position.Y, 1);
        Assert.True(newTile.IsFalling, "新生成的方块应该处于下落状态");

        _output.WriteLine($"新方块: Type={newTile.Type}, Position.Y={newTile.Position.Y}, IsFalling={newTile.IsFalling}");
    }

    /// <summary>
    /// 测试：新生成方块的完整下落动画
    ///
    /// 场景：空棋盘，触发填充后运行动画直到稳定
    /// 预期：方块平滑下落到目标位置
    /// </summary>
    [Fact]
    public void NewTile_AnimatesToTargetPosition()
    {
        // Arrange: 创建一个顶行为空的棋盘
        var rng = new StubRandom();
        var state = new GameState(1, 2, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.None, 0, 0));
        var redTile = new Tile(2, TileType.Red, 0, 1);
        redTile.Position = new Vector2(0, 1);
        state.SetTile(0, 1, redTile);

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Green);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // Act 1: 触发填充
        refillSystem.Update(ref state);

        var newTile = state.GetTile(0, 0);
        Assert.Equal(-1.0f, newTile.Position.Y, 1);
        _output.WriteLine($"填充后: Position.Y={newTile.Position.Y}");

        // Act 2: 运行重力+动画
        int frameCount = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 120);

        // Assert: 方块应该到达目标位置
        var finalTile = state.GetTile(0, 0);
        Assert.Equal(0.0f, finalTile.Position.Y, 1);
        Assert.False(finalTile.IsFalling, "动画完成后方块不应该在下落");

        _output.WriteLine($"动画完成 ({frameCount} 帧): Position.Y={finalTile.Position.Y}, IsFalling={finalTile.IsFalling}");
    }

    /// <summary>
    /// 测试：新生成方块的位置单调递增（无回弹）
    ///
    /// 场景：跟踪每帧的位置变化
    /// 预期：Y 坐标应该单调递增，不能出现回弹
    /// </summary>
    [Fact]
    public void NewTile_PositionMonotonicallyIncreasing()
    {
        // Arrange
        var rng = new StubRandom();
        var state = new GameState(1, 2, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.None, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Green);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        // Act: 触发填充
        refillSystem.Update(ref state);

        float prevY = state.GetTile(0, 0).Position.Y;
        int regressionCount = 0;
        float dt = 1.0f / 60.0f;

        // 运行动画并检查每帧
        for (int frame = 0; frame < 60; frame++)
        {
            gravitySystem.Update(ref state, dt);
            animationSystem.Animate(ref state, dt);

            var tile = state.GetTile(0, 0);
            float currentY = tile.Position.Y;

            if (currentY < prevY - 0.001f)
            {
                _output.WriteLine($"Frame {frame}: 位置回退! {prevY:F3} -> {currentY:F3}");
                regressionCount++;
            }

            prevY = currentY;

            if (!tile.IsFalling && currentY >= -0.01f)
            {
                _output.WriteLine($"Frame {frame}: 动画完成, Position.Y={currentY:F3}");
                break;
            }
        }

        // Assert: 不应该有回弹
        Assert.Equal(0, regressionCount);
    }

    #endregion

    #region Multi-Column Refill Tests

    /// <summary>
    /// 测试：多列同时填充的动画协调
    ///
    /// 场景：多列顶行同时为空
    /// 预期：所有列同时填充，动画同步进行
    /// </summary>
    [Fact]
    public void MultiColumn_SimultaneousRefill()
    {
        // Arrange: 创建一个顶行全空的棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 2, 6, rng);

        // 顶行全空
        for (int x = 0; x < 3; x++)
        {
            state.SetTile(x, 0, new Tile(x, TileType.None, x, 0));
            var tile = new Tile(x + 3, TileType.Red, x, 1);
            tile.Position = new Vector2(x, 1);
            state.SetTile(x, 1, tile);
        }

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Green, TileType.Blue, TileType.Yellow);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        // Act 1: 触发填充
        refillSystem.Update(ref state);

        _output.WriteLine("填充后:");
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            _output.WriteLine($"  Column {x}: Type={tile.Type}, Position.Y={tile.Position.Y}");
        }

        // 验证所有新方块都从 Y=-1 开始
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(-1.0f, tile.Position.Y, 1);
            Assert.True(tile.IsFalling);
        }

        // Act 2: 运行动画
        int frameCount = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 120);

        _output.WriteLine($"动画完成 ({frameCount} 帧):");
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            _output.WriteLine($"  Column {x}: Type={tile.Type}, Position.Y={tile.Position.Y}");
        }

        // Assert: 所有方块都应该到达目标位置
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(0.0f, tile.Position.Y, 1);
            Assert.False(tile.IsFalling);
        }
    }

    /// <summary>
    /// 测试：不同列不同空位数量的填充
    ///
    /// 场景：
    /// - Column 0: 1 个空位
    /// - Column 1: 2 个空位
    /// - Column 2: 3 个空位
    ///
    /// 预期：各列独立填充，动画正确完成
    /// </summary>
    [Fact]
    public void DifferentColumns_DifferentEmptyCount()
    {
        // Arrange
        var rng = new StubRandom();
        var state = new GameState(3, 4, 6, rng);

        // Column 0: 1 个空位 (0,0)
        state.SetTile(0, 0, new Tile(1, TileType.None, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.Red, 0, 2));
        state.SetTile(0, 3, new Tile(4, TileType.Red, 0, 3));

        // Column 1: 2 个空位 (1,0), (1,1)
        state.SetTile(1, 0, new Tile(5, TileType.None, 1, 0));
        state.SetTile(1, 1, new Tile(6, TileType.None, 1, 1));
        state.SetTile(1, 2, new Tile(7, TileType.Blue, 1, 2));
        state.SetTile(1, 3, new Tile(8, TileType.Blue, 1, 3));

        // Column 2: 3 个空位 (2,0), (2,1), (2,2)
        state.SetTile(2, 0, new Tile(9, TileType.None, 2, 0));
        state.SetTile(2, 1, new Tile(10, TileType.None, 2, 1));
        state.SetTile(2, 2, new Tile(11, TileType.None, 2, 2));
        state.SetTile(2, 3, new Tile(12, TileType.Green, 2, 3));

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Yellow, TileType.Purple, TileType.Orange);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        _output.WriteLine("初始状态:");
        PrintBoard(ref state);

        // Act: 运行完整的填充+重力+动画循环
        int totalFrames = 0;
        int maxIterations = 10;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 检查是否有空位需要填充
            bool hasEmpty = false;
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetTile(x, 0).Type == TileType.None)
                {
                    hasEmpty = true;
                    break;
                }
            }

            if (!hasEmpty)
            {
                // 检查是否稳定
                bool stable = gravitySystem.IsStable(in state);
                if (stable)
                {
                    _output.WriteLine($"迭代 {iter}: 系统稳定，退出");
                    break;
                }
            }

            // 触发填充
            refillSystem.Update(ref state);

            // 运行动画
            int frames = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 60);
            totalFrames += frames;

            _output.WriteLine($"迭代 {iter}: {frames} 帧");
        }

        _output.WriteLine($"总帧数: {totalFrames}");
        _output.WriteLine("最终状态:");
        PrintBoard(ref state);

        // Assert: 所有位置都应该被填充
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                Assert.NotEqual(TileType.None, tile.Type);
                Assert.False(tile.IsFalling, $"({x},{y}) 不应该在下落");
            }
        }
    }

    #endregion

    #region Cascade Refill Tests

    /// <summary>
    /// 测试：连续消除后的连续填充动画
    ///
    /// 场景：清除一行后，触发填充，新方块下落后可能触发新的消除
    /// 预期：每次填充的动画都是平滑的
    /// </summary>
    [Fact]
    public void CascadeRefill_SmoothAnimation()
    {
        // Arrange: 清除第 1 行后的状态
        //   0 1 2
        // 0 G B Y   <- 第 0 行会下落
        // 1 _ _ _   <- 被清除的行
        // 2 R P B
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        // 第 0 行
        var greenTile = new Tile(1, TileType.Green, 0, 0);
        greenTile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, greenTile);
        var blueTile = new Tile(2, TileType.Blue, 1, 0);
        blueTile.Position = new Vector2(1, 0);
        state.SetTile(1, 0, blueTile);
        var yellowTile = new Tile(3, TileType.Yellow, 2, 0);
        yellowTile.Position = new Vector2(2, 0);
        state.SetTile(2, 0, yellowTile);

        // 第 1 行 - 空
        state.SetTile(0, 1, new Tile(4, TileType.None, 0, 1));
        state.SetTile(1, 1, new Tile(5, TileType.None, 1, 1));
        state.SetTile(2, 1, new Tile(6, TileType.None, 2, 1));

        // 第 2 行
        state.SetTile(0, 2, new Tile(7, TileType.Red, 0, 2));
        state.SetTile(1, 2, new Tile(8, TileType.Purple, 1, 2));
        state.SetTile(2, 2, new Tile(9, TileType.Blue, 2, 2));

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Orange, TileType.Red, TileType.Green);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        _output.WriteLine("初始状态:");
        PrintBoard(ref state);

        // Act: 运行完整循环
        int iteration = 0;
        int totalFrames = 0;

        while (iteration < 10)
        {
            // Step 1: 运行重力让现有方块下落
            int gravityFrames = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 60);
            totalFrames += gravityFrames;

            _output.WriteLine($"迭代 {iteration} - 重力: {gravityFrames} 帧");

            // Step 2: 检查并填充
            bool needsRefill = false;
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetTile(x, 0).Type == TileType.None)
                {
                    needsRefill = true;
                    break;
                }
            }

            if (!needsRefill)
            {
                _output.WriteLine($"迭代 {iteration}: 不需要填充，退出");
                break;
            }

            refillSystem.Update(ref state);
            _output.WriteLine($"迭代 {iteration}: 触发填充");

            iteration++;
        }

        _output.WriteLine($"总帧数: {totalFrames}");
        _output.WriteLine("最终状态:");
        PrintBoard(ref state);

        // Assert: 所有位置都应该被填充且稳定
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                Assert.NotEqual(TileType.None, tile.Type);
            }
        }

        Assert.True(gravitySystem.IsStable(in state), "最终状态应该是稳定的");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// 测试：全空棋盘的完整填充
    ///
    /// 场景：整个棋盘都是空的
    /// 预期：所有位置都被正确填充
    /// </summary>
    [Fact]
    public void EmptyBoard_FullRefill()
    {
        // Arrange: 全空棋盘
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));
            }
        }

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(
            TileType.Red, TileType.Blue, TileType.Green,
            TileType.Yellow, TileType.Purple, TileType.Orange
        );
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        _output.WriteLine("初始状态: 全空棋盘");

        // Act: 运行完整填充循环
        int iteration = 0;
        while (iteration < 20)
        {
            // 填充顶行
            refillSystem.Update(ref state);

            // 运行重力和动画
            int frames = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 60);

            // 检查是否还有空位
            bool hasEmpty = false;
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    if (state.GetTile(x, y).Type == TileType.None)
                    {
                        hasEmpty = true;
                        break;
                    }
                }
                if (hasEmpty) break;
            }

            _output.WriteLine($"迭代 {iteration}: {frames} 帧, 还有空位: {hasEmpty}");

            if (!hasEmpty && gravitySystem.IsStable(in state))
            {
                break;
            }

            iteration++;
        }

        _output.WriteLine("最终状态:");
        PrintBoard(ref state);

        // Assert: 所有位置都应该被填充
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                Assert.NotEqual(TileType.None, tile.Type);
                Assert.False(tile.IsFalling);
            }
        }
    }

    /// <summary>
    /// 测试：单列连续空位的填充
    ///
    /// 场景：一列全空
    /// 预期：方块依次填充并下落到正确位置
    /// </summary>
    [Fact]
    public void SingleColumn_ContinuousEmpty()
    {
        // Arrange: 单列全空
        var rng = new StubRandom();
        var state = new GameState(1, 4, 6, rng);

        for (int y = 0; y < 4; y++)
        {
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));
        }

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var spawnModel = new SequentialSpawnModel(TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow);
        var refillSystem = new RealtimeRefillSystem(spawnModel);
        var gravitySystem = new RealtimeGravitySystem(config, rng);
        var animationSystem = new AnimationSystem(config);

        var helper = new AnimationTestHelper(_output);

        _output.WriteLine("初始状态: 单列全空");

        // Act: 运行完整填充循环
        int iteration = 0;
        while (iteration < 10)
        {
            refillSystem.Update(ref state);

            int frames = helper.UpdateUntilStable(ref state, gravitySystem, animationSystem, maxFrames: 60);

            bool hasEmpty = false;
            for (int y = 0; y < state.Height; y++)
            {
                if (state.GetTile(0, y).Type == TileType.None)
                {
                    hasEmpty = true;
                    break;
                }
            }

            _output.WriteLine($"迭代 {iteration}: {frames} 帧");
            PrintColumnDetail(ref state);

            if (!hasEmpty && gravitySystem.IsStable(in state))
            {
                break;
            }

            iteration++;
        }

        // Assert
        for (int y = 0; y < state.Height; y++)
        {
            var tile = state.GetTile(0, y);
            Assert.NotEqual(TileType.None, tile.Type);
            Assert.Equal(y, tile.Position.Y, 1);
            Assert.False(tile.IsFalling);
        }
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
                string symbol = t.Type == TileType.None ? "_" : t.Type.ToString()[0].ToString();
                row += symbol.PadRight(3);
            }
            _output.WriteLine(row);
        }
    }

    private void PrintColumnDetail(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            var t = state.GetTile(0, y);
            _output.WriteLine($"  (0,{y}): Type={t.Type}, Pos.Y={t.Position.Y:F2}, IsFalling={t.IsFalling}");
        }
    }

    #endregion
}
