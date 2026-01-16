using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class GravityTargetResolverTests
{
    private class StubRandom : IRandom
    {
        public int ReturnValue { get; set; } = 0;

        public float NextFloat() => 0f;
        public int Next(int max) => ReturnValue % max;
        public int Next(int min, int max) => min + (ReturnValue % (max - min));
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    #region Vertical Movement Tests

    [Fact]
    public void DetermineTarget_EmptyBelow_ShouldReturnLowestEmpty()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        // Place a tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should target the bottom of the board
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(4f, result.Position.Y); // Bottom row
        Assert.False(result.FoundDynamicTarget);
    }

    [Fact]
    public void DetermineTarget_BlockedBelow_ShouldStayPut()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Place tiles
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        state.SetTile(1, 1, new Tile(2, TileType.Blue, 1, 1)); // Blocking tile

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should stay at current position
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
        Assert.False(result.FoundDynamicTarget);
    }

    [Fact]
    public void DetermineTarget_PartiallyEmpty_ShouldFindCorrectFloor()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        // Place a tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));
        // Place a blocking tile at (1, 3)
        state.SetTile(1, 3, new Tile(2, TileType.Blue, 1, 3));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should stop above the blocking tile
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(2f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_AtBottom_ShouldStayPut()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Place tile at bottom
        state.SetTile(1, 2, new Tile(1, TileType.Red, 1, 2));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 2);

        // Assert - Should stay at bottom
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(2f, result.Position.Y);
    }

    #endregion

    #region Following Falling Tile Tests

    [Fact]
    public void DetermineTarget_FallingTileBelow_ShouldFollowWhenFalling()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        // Place a falling tile at (1, 0) - current tile is also falling
        var currentTile = new Tile(1, TileType.Red, 1, 0) { IsFalling = true };
        state.SetTile(1, 0, currentTile);

        // Place a falling tile at (1, 1)
        var fallingBelow = new Tile(2, TileType.Blue, 1, 1)
        {
            Position = new Vector2(1, 1.5f),
            Velocity = new Vector2(0, 10f),
            IsFalling = true
        };
        state.SetTile(1, 1, fallingBelow);

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should follow the falling tile
        Assert.True(result.FoundDynamicTarget);
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0.5f, result.Position.Y, 0.01f); // 1.5 - 1.0 = 0.5
        Assert.Equal(10f, result.InheritedVelocityY, 0.01f);
    }

    [Fact]
    public void DetermineTarget_FallingTileBelowNotCleared_ShouldWait()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        // Place a non-falling tile at (1, 0)
        var currentTile = new Tile(1, TileType.Red, 1, 0) { IsFalling = false };
        state.SetTile(1, 0, currentTile);

        // Place a falling tile at (1, 1) that hasn't cleared the midpoint
        var fallingBelow = new Tile(2, TileType.Blue, 1, 1)
        {
            Position = new Vector2(1, 1.3f), // < 1.5 midpoint
            Velocity = new Vector2(0, 10f),
            IsFalling = true
        };
        state.SetTile(1, 1, fallingBelow);

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should wait (stay at current position)
        Assert.False(result.FoundDynamicTarget);
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    #endregion

    #region Diagonal Slide Tests

    [Fact]
    public void DetermineTarget_SuspendedBelow_CanSlideLeft_ShouldSlideLeft()
    {
        // Arrange
        var random = new StubRandom { ReturnValue = 0 }; // Will choose left
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));

        // Suspended tile at (1, 1) - blocks direct fall
        var suspended = new Tile(2, TileType.Blue, 1, 1) { IsSuspended = true };
        state.SetTile(1, 1, suspended);

        // Left diagonal open: (0, 1) empty, (0, 0) empty (overhead clear)
        // Right diagonal blocked: (2, 1) has tile
        state.SetTile(2, 1, new Tile(3, TileType.Green, 2, 1));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should slide left
        Assert.Equal(0f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_SuspendedBelow_CanSlideRight_ShouldSlideRight()
    {
        // Arrange
        var random = new StubRandom { ReturnValue = 1 }; // Will choose right
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));

        // Suspended tile at (1, 1)
        var suspended = new Tile(2, TileType.Blue, 1, 1) { IsSuspended = true };
        state.SetTile(1, 1, suspended);

        // Left diagonal blocked: (0, 1) has tile
        state.SetTile(0, 1, new Tile(3, TileType.Green, 0, 1));

        // Right diagonal open: (2, 1) empty, (2, 0) empty (overhead clear)

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should slide right
        Assert.Equal(2f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_BothDiagonalsOpen_ShouldRandomlyChoose()
    {
        // Arrange - First call returns left (0)
        var random = new StubRandom { ReturnValue = 0 };
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));

        // Suspended tile at (1, 1)
        var suspended = new Tile(2, TileType.Blue, 1, 1) { IsSuspended = true };
        state.SetTile(1, 1, suspended);

        // Both diagonals open

        // Act
        resolver.ClearReservations();
        var result1 = resolver.DetermineTarget(ref state, 1, 0);

        // Change random to return right
        random.ReturnValue = 1;
        resolver.ClearReservations();
        var result2 = resolver.DetermineTarget(ref state, 1, 0);

        // Assert
        Assert.Equal(0f, result1.Position.X); // Left
        Assert.Equal(2f, result2.Position.X); // Right
    }

    [Fact]
    public void DetermineTarget_DiagonalBlocked_OverheadNotClear_ShouldNotSlide()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (1, 0)
        state.SetTile(1, 0, new Tile(1, TileType.Red, 1, 0));

        // Suspended tile at (1, 1)
        var suspended = new Tile(2, TileType.Blue, 1, 1) { IsSuspended = true };
        state.SetTile(1, 1, suspended);

        // Left diagonal target (0, 1) is open, but overhead (0, 0) is blocked
        state.SetTile(0, 0, new Tile(3, TileType.Green, 0, 0));

        // Right diagonal blocked at target
        state.SetTile(2, 1, new Tile(4, TileType.Yellow, 2, 1));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Should stay put (no valid diagonal)
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_AtLeftEdge_ShouldOnlyConsiderRight()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (0, 0) - left edge
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        // Suspended tile at (0, 1)
        var suspended = new Tile(2, TileType.Blue, 0, 1) { IsSuspended = true };
        state.SetTile(0, 1, suspended);

        // Right diagonal open

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 0, 0);

        // Assert - Should slide right
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_AtRightEdge_ShouldOnlyConsiderLeft()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        // Tile at (2, 0) - right edge
        state.SetTile(2, 0, new Tile(1, TileType.Red, 2, 0));

        // Suspended tile at (2, 1)
        var suspended = new Tile(2, TileType.Blue, 2, 1) { IsSuspended = true };
        state.SetTile(2, 1, suspended);

        // Left diagonal open

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 2, 0);

        // Assert - Should slide left
        Assert.Equal(1f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    #endregion

    #region Reservation Tests

    [Fact]
    public void DetermineTarget_MultipleCallsWithoutClear_ShouldRespectReservations()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        // Place two tiles at top
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));

        // Act - First tile reserves bottom
        resolver.ClearReservations();
        var result1 = resolver.DetermineTarget(ref state, 0, 0);

        // Second call without clearing - same column
        // But actually different column, so it should work
        var result2 = resolver.DetermineTarget(ref state, 1, 0);

        // Assert - Both should target the bottom of their respective columns
        Assert.Equal(0f, result1.Position.X);
        Assert.Equal(4f, result1.Position.Y);
        Assert.Equal(1f, result2.Position.X);
        Assert.Equal(4f, result2.Position.Y);
    }

    [Fact]
    public void DetermineTarget_SameColumn_ShouldStackReservations()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 5, 5, random);
        ClearBoard(ref state);

        // Place only one tile at top - this will reserve the bottom slot
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        // Act
        resolver.ClearReservations();
        var result1 = resolver.DetermineTarget(ref state, 0, 0); // Reserves (0, 4)

        // Now place another tile at (0, 0) and query again (simulating multiple tiles needing targets)
        // Since (0, 4) is reserved, next target should be (0, 3)
        var result2 = resolver.DetermineTarget(ref state, 0, 0); // Should reserve (0, 3)

        // Assert - Should stack reservations properly
        Assert.Equal(4f, result1.Position.Y); // First gets bottom
        Assert.Equal(3f, result2.Position.Y); // Second gets one above
    }

    [Fact]
    public void ClearReservations_ShouldAllowReuse()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 5, 5, random);
        ClearBoard(ref state);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        // Act
        resolver.ClearReservations();
        var result1 = resolver.DetermineTarget(ref state, 0, 0);

        resolver.ClearReservations();
        var result2 = resolver.DetermineTarget(ref state, 0, 0);

        // Assert - Both should target the same position after clear
        Assert.Equal(result1.Position.Y, result2.Position.Y);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetermineTarget_SingleCellBoard_ShouldStayPut()
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 1, 5, random);
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 0, 0);

        // Assert
        Assert.Equal(0f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Theory]
    [InlineData(0)] // Left edge
    [InlineData(4)] // Middle
    [InlineData(9)] // Right edge
    public void DetermineTarget_AllColumns_ShouldWork(int column)
    {
        // Arrange
        var random = new StubRandom();
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(10, 5, 5, random);
        ClearBoard(ref state);

        state.SetTile(column, 0, new Tile(1, TileType.Red, column, 0));

        // Act
        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, column, 0);

        // Assert
        Assert.Equal((float)column, result.Position.X);
        Assert.Equal(4f, result.Position.Y); // Bottom
    }

    #endregion

    #region Helper Methods

    private static void ClearBoard(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));
            }
        }
    }

    #endregion
}
