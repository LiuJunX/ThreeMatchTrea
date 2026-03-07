using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Physics;

/// <summary>
/// 诊断测试：用于分析掉落过程中的位置和速度变化
/// </summary>
public class FallSmoothnessDiagnosticTests
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

    public FallSmoothnessDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 诊断测试：单个方块在完全空的列中掉落
    /// 记录每帧的位置、速度和加速度变化
    /// </summary>
    [Fact]
    public void Diagnostic_SingleTileFall_TrackPositionAndVelocity()
    {
        // Arrange: 1列6行的棋盘，顶部有一个方块，下面全空
        var state = new GameState(1, 6, 5, new StubRandom());

        // 清空棋盘
        for (int y = 0; y < 6; y++)
        {
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));
        }

        // 在 (0, 0) 放置一个红色方块
        var tile = new Tile(100, ElementType.Item1, 0, 0);
        tile.Position = new Vector2(0, 0);
        tile.Velocity = new Vector2(0, 0);
        state.SetTile(0, 0, tile);

        var config = new Match3Config
        {
            GravitySpeed = 20.0f,
            MaxFallSpeed = 25.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        float dt = 1.0f / 60.0f; // 60fps

        var records = new List<(int frame, float posY, float velY, float deltaPos, float deltaVel, int gridY)>();

        float prevPosY = 0f;
        float prevVelY = 0f;

        _output.WriteLine("=== 单方块掉落诊断 ===");
        _output.WriteLine($"配置: GravitySpeed={config.GravitySpeed}, MaxFallSpeed={config.MaxFallSpeed}, dt={dt:F4}");
        _output.WriteLine("");
        _output.WriteLine("Frame | GridY | Position.Y | Velocity.Y | DeltaPos | DeltaVel | 备注");
        _output.WriteLine("------|-------|------------|------------|----------|----------|-----");

        // Act: 模拟掉落过程
        for (int frame = 0; frame < 100; frame++)
        {
            physics.Update(ref state, dt);

            // 找到方块当前位置
            Tile currentTile = default;
            int gridY = -1;
            for (int y = 0; y < 6; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == ElementType.Item1)
                {
                    currentTile = t;
                    gridY = y;
                    break;
                }
            }

            if (gridY == -1)
            {
                _output.WriteLine($"Frame {frame}: 方块丢失!");
                break;
            }

            float deltaPos = currentTile.Position.Y - prevPosY;
            float deltaVel = currentTile.Velocity.Y - prevVelY;

            string notes = "";

            // 检测异常情况
            if (frame > 0)
            {
                // 检测位置突变
                if (Math.Abs(deltaPos) > 0.5f)
                {
                    notes += "[位置跳变!] ";
                }

                // 检测速度突变
                if (Math.Abs(deltaVel) > config.GravitySpeed * dt * 1.5f && currentTile.IsFalling)
                {
                    notes += "[速度突变!] ";
                }

                // 检测格子边界跨越
                if (gridY != records[frame - 1].gridY)
                {
                    notes += $"[跨格子: {records[frame - 1].gridY}->{gridY}] ";
                }

                // 检测速度重置
                if (prevVelY > 1.0f && currentTile.Velocity.Y < 0.1f)
                {
                    notes += "[速度重置!] ";
                }
            }

            if (!currentTile.IsFalling)
            {
                notes += "[已停止] ";
            }

            records.Add((frame, currentTile.Position.Y, currentTile.Velocity.Y, deltaPos, deltaVel, gridY));

            _output.WriteLine($"{frame,5} | {gridY,5} | {currentTile.Position.Y,10:F4} | {currentTile.Velocity.Y,10:F4} | {deltaPos,8:F4} | {deltaVel,8:F4} | {notes}");

            prevPosY = currentTile.Position.Y;
            prevVelY = currentTile.Velocity.Y;

            // 如果到达底部且停止，结束测试
            if (!currentTile.IsFalling && currentTile.Position.Y >= 4.9f)
            {
                _output.WriteLine("");
                _output.WriteLine($"方块在第 {frame} 帧到达底部");
                break;
            }
        }

        // 分析结果
        _output.WriteLine("");
        _output.WriteLine("=== 分析 ===");

        // 找出速度变化最大的帧
        float maxDeltaVel = 0;
        int maxDeltaVelFrame = 0;

        for (int i = 1; i < records.Count; i++)
        {
            if (Math.Abs(records[i].deltaVel) > Math.Abs(maxDeltaVel))
            {
                maxDeltaVel = records[i].deltaVel;
                maxDeltaVelFrame = records[i].frame;
            }
        }

        _output.WriteLine($"最大速度变化: 帧 {maxDeltaVelFrame}, DeltaVel = {maxDeltaVel:F4}");

        // 检查是否有不平滑的情况
        bool foundIssue = false;
        for (int i = 2; i < records.Count - 1; i++)
        {
            // 如果方块还在掉落中
            if (records[i].velY > 0.1f)
            {
                // 检查加速度是否突然变化（应该是恒定的 gravity * dt）
                float expectedDeltaVel = config.GravitySpeed * dt;
                float actualDeltaVel = records[i].deltaVel;

                // 如果速度增量偏差超过 50%，说明有问题
                if (Math.Abs(actualDeltaVel - expectedDeltaVel) > expectedDeltaVel * 0.5f
                    && records[i].velY < config.MaxFallSpeed - 1)
                {
                    _output.WriteLine($"异常: 帧 {records[i].frame} 加速度不一致 (期望: {expectedDeltaVel:F4}, 实际: {actualDeltaVel:F4})");
                    foundIssue = true;
                }
            }
        }

        if (!foundIssue)
        {
            _output.WriteLine("未发现明显的加速度异常");
        }

        // Assert: 至少应该完成掉落
        Assert.True(records.Count > 5, "方块应该经历多帧掉落");
    }

    /// <summary>
    /// 测试：验证积分顺序是否正确
    /// 正确的物理应该是: x_new = x_old + v_old * dt, 然后 v_new = v_old + a * dt
    /// </summary>
    [Fact]
    public void Diagnostic_CheckIntegrationOrder()
    {
        var state = new GameState(1, 10, 5, new StubRandom());

        // 清空棋盘
        for (int y = 0; y < 10; y++)
        {
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));
        }

        // 放置方块
        var tile = new Tile(100, ElementType.Item1, 0, 0);
        state.SetTile(0, 0, tile);

        var config = new Match3Config
        {
            GravitySpeed = 20.0f,
            MaxFallSpeed = 25.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        float dt = 1.0f / 60.0f;

        // 第一帧更新
        physics.Update(ref state, dt);

        var t1 = state.GetTile(0, 0);
        float v1 = t1.Velocity.Y;
        float p1 = t1.Position.Y;

        // 理论计算 (正确的显式欧拉):
        // v1 = 0 + 20 * dt = 0.333
        // p1 = 0 + 0 * dt = 0 (使用旧速度)

        // 如果代码错误地使用新速度:
        // p1 = 0 + 0.333 * dt = 0.00556

        _output.WriteLine($"第一帧后: Position.Y = {p1:F6}, Velocity.Y = {v1:F6}");
        _output.WriteLine($"理论值 (正确积分): Position.Y = 0, Velocity.Y = {config.GravitySpeed * dt:F6}");
        _output.WriteLine($"理论值 (错误积分): Position.Y = {config.GravitySpeed * dt * dt:F6}");

        // 如果位置不为0，说明使用了新速度来更新位置（顺序错误）
        if (p1 > 0.001f)
        {
            _output.WriteLine("");
            _output.WriteLine("*** 发现问题: 积分顺序错误! 先更新了速度，然后用新速度更新位置 ***");
            _output.WriteLine("这会导致加速效果被放大");
        }
    }

    /// <summary>
    /// 测试：分析位置变化的平滑性
    /// 每帧的位置增量应该是平滑增加的（因为速度在增加）
    /// </summary>
    [Fact]
    public void Diagnostic_PositionDeltaSmoothness()
    {
        var state = new GameState(1, 10, 5, new StubRandom());

        for (int y = 0; y < 10; y++)
        {
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));
        }

        var tile = new Tile(100, ElementType.Item1, 0, 0);
        state.SetTile(0, 0, tile);

        var config = new Match3Config
        {
            GravitySpeed = 20.0f,
            MaxFallSpeed = 25.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        float dt = 1.0f / 60.0f;

        var deltas = new List<float>();
        float prevPos = 0;

        _output.WriteLine("帧 | 位置增量 | 备注");
        _output.WriteLine("----|----------|-----");

        for (int frame = 0; frame < 50; frame++)
        {
            physics.Update(ref state, dt);

            // 找到方块
            float currentPos = 0;
            for (int y = 0; y < 10; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == ElementType.Item1)
                {
                    currentPos = t.Position.Y;
                    break;
                }
            }

            float delta = currentPos - prevPos;
            deltas.Add(delta);

            string note = "";
            if (frame > 0 && delta < deltas[frame - 1] - 0.001f)
            {
                note = "[减速!]";
            }

            _output.WriteLine($"{frame,3} | {delta,8:F5} | {note}");

            prevPos = currentPos;
        }

        // 检查是否有突然减速的情况（排除到达最大速度后、停止后和落地减速的情况）
        int issueCount = 0;
        for (int i = 2; i < deltas.Count - 1; i++)
        {
            // 跳过方块停止后的帧（delta 接近 0 表示停止）
            if (deltas[i] < 0.001f) continue;

            // 跳过落地前的减速帧（前一帧 delta 接近最大速度时，当前帧减速是正常的落地行为）
            if (deltas[i - 1] >= config.MaxFallSpeed * dt * 0.8f) continue;

            if (deltas[i] < deltas[i - 1] - 0.001f)
            {
                issueCount++;
            }
        }

        _output.WriteLine("");
        _output.WriteLine($"发现 {issueCount} 次异常减速");

        // 不应该有异常减速
        Assert.Equal(0, issueCount);
    }
}

