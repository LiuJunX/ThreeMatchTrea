using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Core;

/// <summary>
/// AnimationSystem 单元测试
///
/// 职责：
/// - 管理 tile 的视觉位置插值
/// - 交换后动画移动到新位置
/// - 跳过正在掉落的 tile
/// </summary>
public class AnimationSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private Match3Config CreateConfig()
    {
        return new Match3Config
        {
            Width = 8,
            Height = 8,
            GravitySpeed = 10.0f
        };
    }

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = new Tile(id++, TileType.Red, x, y);
                tile.Position = new Vector2(x, y); // Visual position matches grid position
                state.SetTile(x, y, tile);
            }
        }
        return state;
    }

    #region Basic Animation Tests

    [Fact]
    public void Animate_TileAtTarget_ReturnsTrue()
    {
        // Arrange: tile 已经在目标位置
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // Act
        bool stable = system.Animate(ref state, 0.016f);

        // Assert: 应该是稳定的
        Assert.True(stable);
        Assert.True(system.IsVisuallyStable);
    }

    [Fact]
    public void Animate_TileNotAtTarget_ReturnsFalse()
    {
        // Arrange: tile 不在目标位置
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 把 (0,0) 的 tile 视觉位置设为 (1,0)
        var tile = state.GetTile(0, 0);
        tile.Position = new Vector2(1, 0);
        state.SetTile(0, 0, tile);

        // Act
        bool stable = system.Animate(ref state, 0.016f);

        // Assert: 不稳定，还在动画中
        Assert.False(stable);
        Assert.False(system.IsVisuallyStable);
    }

    [Fact]
    public void Animate_MovesTowardsTarget()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 把 (0,0) 的 tile 视觉位置设为 (2,0)
        var tile = state.GetTile(0, 0);
        tile.Position = new Vector2(2, 0);
        state.SetTile(0, 0, tile);
        float startX = tile.Position.X;

        // Act
        system.Animate(ref state, 0.1f);

        // Assert: 应该向目标 (0,0) 移动
        var updatedTile = state.GetTile(0, 0);
        Assert.True(updatedTile.Position.X < startX);
    }

    [Fact]
    public void Animate_SnapsToTarget_WhenClose()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 把 tile 放在非常接近目标的位置
        var tile = state.GetTile(0, 0);
        tile.Position = new Vector2(0.005f, 0);
        state.SetTile(0, 0, tile);

        // Act
        system.Animate(ref state, 0.016f);

        // Assert: 应该直接 snap 到目标
        var updatedTile = state.GetTile(0, 0);
        Assert.Equal(0, updatedTile.Position.X);
        Assert.Equal(0, updatedTile.Position.Y);
    }

    #endregion

    #region Swap Animation Tests

    [Fact]
    public void SwapTiles_KeepsVisualPositions_ForAnimation()
    {
        // Arrange: 模拟交换操作
        var state = CreateState();

        // 交换前
        var tileA = state.GetTile(0, 0); // Position = (0, 0)
        var tileB = state.GetTile(1, 0); // Position = (1, 0)
        Assert.Equal(new Vector2(0, 0), tileA.Position);
        Assert.Equal(new Vector2(1, 0), tileB.Position);

        // Act: 只交换网格数据，不交换视觉位置（正确的交换逻辑）
        var idxA = 0;
        var idxB = 1;
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // Assert: 交换后
        // Grid[0] 现在是原来的 tileB，但视觉位置仍是 (1, 0)
        // Grid[1] 现在是原来的 tileA，但视觉位置仍是 (0, 0)
        var newTileAtA = state.GetTile(0, 0);
        var newTileAtB = state.GetTile(1, 0);

        // 视觉位置应该保持不变（用于动画起点）
        Assert.Equal(new Vector2(1, 0), newTileAtA.Position); // 原 tileB 的位置
        Assert.Equal(new Vector2(0, 0), newTileAtB.Position); // 原 tileA 的位置
    }

    [Fact]
    public void SwapAnimation_TilesAnimateToNewPositions()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 模拟交换 (0,0) 和 (1,0)
        var idxA = 0;
        var idxB = 1;
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // 交换后，Grid[0] 的视觉位置是 (1,0)，需要动画到 (0,0)
        // 交换后，Grid[1] 的视觉位置是 (0,0)，需要动画到 (1,0)
        Assert.Equal(new Vector2(1, 0), state.Grid[idxA].Position);
        Assert.Equal(new Vector2(0, 0), state.Grid[idxB].Position);

        // Act: 运行动画
        bool stable = system.Animate(ref state, 0.016f);

        // Assert: 不稳定，正在动画
        Assert.False(stable);

        // Grid[0] 应该向 (0,0) 移动
        Assert.True(state.Grid[idxA].Position.X < 1.0f);
        // Grid[1] 应该向 (1,0) 移动
        Assert.True(state.Grid[idxB].Position.X > 0.0f);
    }

    [Fact]
    public void SwapAnimation_CompletesAfterEnoughTime()
    {
        // Arrange
        var config = CreateConfig();
        config.GravitySpeed = 20.0f; // 快速动画
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 模拟交换 (0,0) 和 (1,0)
        var idxA = 0;
        var idxB = 1;
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // Act: 运行足够多的动画帧
        bool stable = false;
        for (int i = 0; i < 100 && !stable; i++)
        {
            stable = system.Animate(ref state, 0.016f);
        }

        // Assert: 动画完成
        Assert.True(stable);
        Assert.Equal(new Vector2(0, 0), state.Grid[idxA].Position);
        Assert.Equal(new Vector2(1, 0), state.Grid[idxB].Position);
    }

    #endregion

    #region IsFalling Skip Tests

    [Fact]
    public void Animate_SkipsFallingTiles()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 设置 tile 为 falling 状态，且不在目标位置
        var tile = state.GetTile(0, 0);
        tile.Position = new Vector2(0, 5); // 视觉位置在 y=5
        tile.IsFalling = true;
        state.SetTile(0, 0, tile);

        // Act
        system.Animate(ref state, 0.1f);

        // Assert: falling tile 不应该被 AnimationSystem 移动
        var updatedTile = state.GetTile(0, 0);
        Assert.Equal(5, updatedTile.Position.Y); // 位置不变
    }

    [Fact]
    public void Animate_ProcessesNonFallingTiles()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // 设置两个 tile：一个 falling，一个不是
        var fallingTile = state.GetTile(0, 0);
        fallingTile.Position = new Vector2(0, 5);
        fallingTile.IsFalling = true;
        state.SetTile(0, 0, fallingTile);

        var normalTile = state.GetTile(1, 0);
        normalTile.Position = new Vector2(5, 0); // 不在目标位置
        normalTile.IsFalling = false;
        state.SetTile(1, 0, normalTile);

        // Act
        system.Animate(ref state, 0.1f);

        // Assert
        var updatedFalling = state.GetTile(0, 0);
        var updatedNormal = state.GetTile(1, 0);

        // falling tile 不变
        Assert.Equal(5, updatedFalling.Position.Y);
        // normal tile 向目标移动
        Assert.True(updatedNormal.Position.X < 5);
    }

    #endregion

    #region IsVisualAtTarget Tests

    [Fact]
    public void IsVisualAtTarget_ReturnsTrueWhenAtTarget()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        // Act
        bool atTarget = system.IsVisualAtTarget(in state, new Position(0, 0));

        // Assert
        Assert.True(atTarget);
    }

    [Fact]
    public void IsVisualAtTarget_ReturnsFalseWhenNotAtTarget()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        var tile = state.GetTile(0, 0);
        tile.Position = new Vector2(1, 1);
        state.SetTile(0, 0, tile);

        // Act
        bool atTarget = system.IsVisualAtTarget(in state, new Position(0, 0));

        // Assert
        Assert.False(atTarget);
    }

    [Fact]
    public void IsVisualAtTarget_ReturnsTrueForEmptyTile()
    {
        // Arrange
        var config = CreateConfig();
        var system = new AnimationSystem(config);
        var state = CreateState();

        state.SetTile(0, 0, new Tile(0, TileType.None, 0, 0));

        // Act
        bool atTarget = system.IsVisualAtTarget(in state, new Position(0, 0));

        // Assert: 空 tile 视为稳定
        Assert.True(atTarget);
    }

    #endregion
}
