using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Input;
using Match3.Core.Utility;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Input;

/// <summary>
/// InteractionSystem 单元测试
///
/// 职责：
/// - 处理点击交互（选择、取消选择、交换）
/// - 处理滑动交互
/// - 管理选择状态
/// </summary>
public class InteractionSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class StubLogger : IGameLogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogInfo<T>(string message, T args) { }
        public void LogInfo<T1, T2>(string message, T1 arg1, T2 arg2) { }
        public void LogInfo<T1, T2, T3>(string message, T1 arg1, T2 arg2, T3 arg3) { }
        public void LogWarning<T>(string message, T args) { }
    }

    private InteractionSystem CreateInteractionSystem()
    {
        return new InteractionSystem(new StubLogger());
    }

    private GameState CreateEmptyState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        state.SelectedPosition = Position.Invalid;
        return state;
    }

    #region TryHandleTap Tests - Selection

    [Fact]
    public void TryHandleTap_NoSelection_SelectsTile()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var position = new Position(3, 3);

        // Act
        bool result = system.TryHandleTap(ref state, position, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result); // 首次点击不产生移动
        Assert.Null(move);
        Assert.Equal(position, state.SelectedPosition);
    }

    [Fact]
    public void TryHandleTap_SamePositionTwice_DeselectsTile()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var position = new Position(3, 3);

        // 首次点击选中
        system.TryHandleTap(ref state, position, isBoardInteractive: true, out _);

        // Act: 再次点击同一位置
        bool result = system.TryHandleTap(ref state, position, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
        Assert.Equal(Position.Invalid, state.SelectedPosition);
    }

    [Fact]
    public void TryHandleTap_NeighborPosition_ReturnsMove()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var pos1 = new Position(3, 3);
        var pos2 = new Position(4, 3); // 右邻居

        // 首次点击选中
        system.TryHandleTap(ref state, pos1, isBoardInteractive: true, out _);

        // Act: 点击邻居
        bool result = system.TryHandleTap(ref state, pos2, isBoardInteractive: true, out var move);

        // Assert
        Assert.True(result);
        Assert.NotNull(move);
        Assert.Equal(pos1, move.Value.From);
        Assert.Equal(pos2, move.Value.To);
        Assert.Equal(Position.Invalid, state.SelectedPosition); // 选择被清除
    }

    [Fact]
    public void TryHandleTap_NonNeighborPosition_ChangesSelection()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var pos1 = new Position(3, 3);
        var pos2 = new Position(6, 6); // 不是邻居

        // 首次点击选中
        system.TryHandleTap(ref state, pos1, isBoardInteractive: true, out _);

        // Act: 点击非邻居
        bool result = system.TryHandleTap(ref state, pos2, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
        Assert.Equal(pos2, state.SelectedPosition); // 选择变更为新位置
    }

    #endregion

    #region TryHandleTap Tests - Board Interactive State

    [Fact]
    public void TryHandleTap_BoardNotInteractive_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var position = new Position(3, 3);

        // Act
        bool result = system.TryHandleTap(ref state, position, isBoardInteractive: false, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
        Assert.Equal(Position.Invalid, state.SelectedPosition); // 状态不变
    }

    [Fact]
    public void TryHandleTap_InvalidPosition_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var position = new Position(-1, 0); // 无效位置

        // Act
        bool result = system.TryHandleTap(ref state, position, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
    }

    [Fact]
    public void TryHandleTap_OutOfBoundsPosition_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var position = new Position(100, 100); // 超出边界

        // Act
        bool result = system.TryHandleTap(ref state, position, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
    }

    #endregion

    #region TryHandleSwipe Tests

    [Fact]
    public void TryHandleSwipe_ValidSwipe_ReturnsMove()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var from = new Position(3, 3);

        // Act
        bool result = system.TryHandleSwipe(ref state, from, Direction.Right, isBoardInteractive: true, out var move);

        // Assert
        Assert.True(result);
        Assert.NotNull(move);
        Assert.Equal(from, move.Value.From);
        Assert.Equal(new Position(4, 3), move.Value.To);
    }

    [Fact]
    public void TryHandleSwipe_ClearsExistingSelection()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        state.SelectedPosition = new Position(1, 1); // 有现有选择
        var from = new Position(3, 3);

        // Act
        system.TryHandleSwipe(ref state, from, Direction.Down, isBoardInteractive: true, out _);

        // Assert
        Assert.Equal(Position.Invalid, state.SelectedPosition);
    }

    [Fact]
    public void TryHandleSwipe_BoardNotInteractive_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var from = new Position(3, 3);

        // Act
        bool result = system.TryHandleSwipe(ref state, from, Direction.Right, isBoardInteractive: false, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
    }

    [Fact]
    public void TryHandleSwipe_FromInvalidPosition_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var from = new Position(-1, 0);

        // Act
        bool result = system.TryHandleSwipe(ref state, from, Direction.Right, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
    }

    [Fact]
    public void TryHandleSwipe_ToInvalidPosition_ReturnsFalse()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var from = new Position(0, 0);

        // Act: 向左滑动会超出边界
        bool result = system.TryHandleSwipe(ref state, from, Direction.Left, isBoardInteractive: true, out var move);

        // Assert
        Assert.False(result);
        Assert.Null(move);
    }

    [Fact]
    public void TryHandleSwipe_AllDirections_ReturnCorrectTarget()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var center = new Position(3, 3);

        // Act & Assert - Right
        system.TryHandleSwipe(ref state, center, Direction.Right, true, out var moveRight);
        Assert.Equal(new Position(4, 3), moveRight!.Value.To);

        // Act & Assert - Left
        system.TryHandleSwipe(ref state, center, Direction.Left, true, out var moveLeft);
        Assert.Equal(new Position(2, 3), moveLeft!.Value.To);

        // Act & Assert - Down
        system.TryHandleSwipe(ref state, center, Direction.Down, true, out var moveDown);
        Assert.Equal(new Position(3, 4), moveDown!.Value.To);

        // Act & Assert - Up
        system.TryHandleSwipe(ref state, center, Direction.Up, true, out var moveUp);
        Assert.Equal(new Position(3, 2), moveUp!.Value.To);
    }

    #endregion

    #region Neighbor Detection Tests

    [Fact]
    public void TryHandleTap_AllNeighborDirections_ReturnMove()
    {
        var system = CreateInteractionSystem();
        var center = new Position(3, 3);

        var neighbors = new[]
        {
            new Position(4, 3), // Right
            new Position(2, 3), // Left
            new Position(3, 4), // Down
            new Position(3, 2), // Up
        };

        foreach (var neighbor in neighbors)
        {
            var state = CreateEmptyState();
            system.TryHandleTap(ref state, center, true, out _); // 选中中心
            bool result = system.TryHandleTap(ref state, neighbor, true, out var move);

            Assert.True(result, $"Should return move for neighbor at {neighbor}");
            Assert.NotNull(move);
        }
    }

    [Fact]
    public void TryHandleTap_DiagonalPosition_IsNotNeighbor()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();
        var center = new Position(3, 3);
        var diagonal = new Position(4, 4); // 对角线

        // 选中中心
        system.TryHandleTap(ref state, center, true, out _);

        // Act
        bool result = system.TryHandleTap(ref state, diagonal, true, out var move);

        // Assert: 对角线不是邻居
        Assert.False(result);
        Assert.Null(move);
        Assert.Equal(diagonal, state.SelectedPosition); // 选择变更
    }

    #endregion

    #region Status Message Tests

    [Fact]
    public void TryHandleTap_SelectTile_UpdatesStatusMessage()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();

        // Act
        system.TryHandleTap(ref state, new Position(3, 3), true, out _);

        // Assert
        Assert.Equal("Select destination", system.StatusMessage);
    }

    [Fact]
    public void TryHandleTap_DeselectTile_UpdatesStatusMessage()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();

        system.TryHandleTap(ref state, new Position(3, 3), true, out _);

        // Act
        system.TryHandleTap(ref state, new Position(3, 3), true, out _);

        // Assert
        Assert.Equal("Selection Cleared", system.StatusMessage);
    }

    [Fact]
    public void TryHandleTap_SwapTiles_UpdatesStatusMessage()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();

        system.TryHandleTap(ref state, new Position(3, 3), true, out _);

        // Act
        system.TryHandleTap(ref state, new Position(4, 3), true, out _);

        // Assert
        Assert.Equal("Swapping...", system.StatusMessage);
    }

    [Fact]
    public void TryHandleSwipe_ValidSwipe_UpdatesStatusMessage()
    {
        // Arrange
        var system = CreateInteractionSystem();
        var state = CreateEmptyState();

        // Act
        system.TryHandleSwipe(ref state, new Position(3, 3), Direction.Right, true, out _);

        // Assert
        Assert.Equal("Swapping...", system.StatusMessage);
    }

    #endregion
}
