using System;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit.Abstractions;

namespace Match3.Core.Tests.TestHelpers;

/// <summary>
/// 动画测试辅助类 - 提供多帧测试的可复用方法
///
/// 用于测试需要多帧才能完成的异步行为：
/// - 交换动画
/// - 重力掉落
/// - 回退动画
/// </summary>
public class AnimationTestHelper
{
    private readonly ITestOutputHelper? _output;

    public AnimationTestHelper(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    #region Frame Simulation

    /// <summary>
    /// 运行动画直到稳定（所有 tile 到达目标位置）
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="animationSystem">动画系统</param>
    /// <param name="maxFrames">最大帧数限制</param>
    /// <param name="dt">每帧时间间隔</param>
    /// <returns>实际运行的帧数</returns>
    public int AnimateUntilStable(
        ref GameState state,
        IAnimationSystem animationSystem,
        int maxFrames = 100,
        float dt = 1f / 60f)
    {
        int frameCount = 0;
        bool stable = false;

        while (!stable && frameCount < maxFrames)
        {
            stable = animationSystem.Animate(ref state, dt);
            frameCount++;
        }

        _output?.WriteLine($"AnimateUntilStable: {frameCount} frames, stable={stable}");
        return frameCount;
    }

    /// <summary>
    /// 运行物理+动画系统直到稳定
    ///
    /// 重要：稳定性检测需要同时检查：
    /// 1. GravitySystem 的 IsStable（没有掉落中的 tile）
    /// 2. AnimationSystem 的返回值（没有需要插值的 tile）
    /// </summary>
    public int UpdateUntilStable(
        ref GameState state,
        IPhysicsSimulation? physics,
        IAnimationSystem animationSystem,
        int maxFrames = 100,
        float dt = 1f / 60f)
    {
        int frameCount = 0;
        bool stable = false;

        while (!stable && frameCount < maxFrames)
        {
            physics?.Update(ref state, dt);
            bool animStable = animationSystem.Animate(ref state, dt);

            // 同时检查物理稳定性和动画稳定性
            bool physicsStable = physics?.IsStable(in state) ?? true;
            stable = animStable && physicsStable;

            frameCount++;
        }

        _output?.WriteLine($"UpdateUntilStable: {frameCount} frames, stable={stable}");
        return frameCount;
    }

    /// <summary>
    /// 运行指定帧数并收集每帧数据
    /// </summary>
    public FrameData[] CollectFrameData(
        ref GameState state,
        IAnimationSystem animationSystem,
        int frameCount,
        float dt = 1f / 60f)
    {
        var frames = new FrameData[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            bool stable = animationSystem.Animate(ref state, dt);
            frames[i] = new FrameData
            {
                FrameIndex = i,
                IsStable = stable,
                TilePositions = CaptureTilePositions(in state)
            };
        }

        return frames;
    }

    #endregion

    #region Swap Animation Helpers

    /// <summary>
    /// 执行交换并返回动画过程数据
    /// </summary>
    public SwapAnimationResult SimulateSwap(
        ref GameState state,
        IAnimationSystem animationSystem,
        int indexA,
        int indexB,
        int maxFrames = 100,
        float dt = 1f / 60f)
    {
        var result = new SwapAnimationResult();

        // 记录交换前状态
        result.BeforeSwap = new TileSnapshot
        {
            TileA = state.Grid[indexA],
            TileB = state.Grid[indexB],
            PositionA = state.Grid[indexA].Position,
            PositionB = state.Grid[indexB].Position
        };

        // 执行交换
        var temp = state.Grid[indexA];
        state.Grid[indexA] = state.Grid[indexB];
        state.Grid[indexB] = temp;

        // 记录交换后（动画前）状态
        result.AfterSwapBeforeAnimation = new TileSnapshot
        {
            TileA = state.Grid[indexA],
            TileB = state.Grid[indexB],
            PositionA = state.Grid[indexA].Position,
            PositionB = state.Grid[indexB].Position
        };

        // 运行动画
        result.FrameCount = AnimateUntilStable(ref state, animationSystem, maxFrames, dt);

        // 记录动画后状态
        result.AfterAnimation = new TileSnapshot
        {
            TileA = state.Grid[indexA],
            TileB = state.Grid[indexB],
            PositionA = state.Grid[indexA].Position,
            PositionB = state.Grid[indexB].Position
        };

        return result;
    }

    /// <summary>
    /// 模拟无效交换（交换 → 动画 → 回退 → 动画）
    /// </summary>
    public InvalidSwapResult SimulateInvalidSwap(
        ref GameState state,
        IAnimationSystem animationSystem,
        int indexA,
        int indexB,
        int maxFrames = 100,
        float dt = 1f / 60f)
    {
        var result = new InvalidSwapResult();

        // 第一次交换
        result.SwapResult = SimulateSwap(ref state, animationSystem, indexA, indexB, maxFrames, dt);

        // 回退交换
        result.RevertResult = SimulateSwap(ref state, animationSystem, indexA, indexB, maxFrames, dt);

        return result;
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// 验证 tile 在动画中（位置在起点和终点之间）
    /// </summary>
    public bool IsTileAnimating(Vector2 currentPos, Vector2 startPos, Vector2 targetPos, float epsilon = 0.01f)
    {
        float distFromStart = Vector2.Distance(currentPos, startPos);
        float distFromTarget = Vector2.Distance(currentPos, targetPos);

        return distFromStart > epsilon && distFromTarget > epsilon;
    }

    /// <summary>
    /// 验证 tile 已到达目标位置
    /// </summary>
    public bool IsTileAtTarget(Vector2 currentPos, Vector2 targetPos, float epsilon = 0.01f)
    {
        return Vector2.Distance(currentPos, targetPos) <= epsilon;
    }

    #endregion

    #region Private Helpers

    private Vector2[] CaptureTilePositions(in GameState state)
    {
        var positions = new Vector2[state.Grid.Length];
        for (int i = 0; i < state.Grid.Length; i++)
        {
            positions[i] = state.Grid[i].Position;
        }
        return positions;
    }

    #endregion

    #region Data Classes

    public class FrameData
    {
        public int FrameIndex { get; set; }
        public bool IsStable { get; set; }
        public Vector2[] TilePositions { get; set; } = Array.Empty<Vector2>();
    }

    public class TileSnapshot
    {
        public Tile TileA { get; set; }
        public Tile TileB { get; set; }
        public Vector2 PositionA { get; set; }
        public Vector2 PositionB { get; set; }
    }

    public class SwapAnimationResult
    {
        public TileSnapshot BeforeSwap { get; set; } = new();
        public TileSnapshot AfterSwapBeforeAnimation { get; set; } = new();
        public TileSnapshot AfterAnimation { get; set; } = new();
        public int FrameCount { get; set; }
    }

    public class InvalidSwapResult
    {
        public SwapAnimationResult SwapResult { get; set; } = new();
        public SwapAnimationResult RevertResult { get; set; } = new();
        public int TotalFrameCount => SwapResult.FrameCount + RevertResult.FrameCount;
    }

    #endregion
}
