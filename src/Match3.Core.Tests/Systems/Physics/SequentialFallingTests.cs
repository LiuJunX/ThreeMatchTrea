using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

/// <summary>
/// Tests for sequential (cell-by-cell) falling behavior.
/// Verifies that tiles fall one cell at a time, waiting for the cell below to be vacated.
/// </summary>
public class SequentialFallingTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    /// <summary>
    /// Scenario: A-B-C vertical stack, C is eliminated.
    /// Expected: B starts falling first, A waits until B's data moves to next cell.
    /// </summary>
    [Fact]
    public void SequentialFall_TopTileWaitsForMiddleTileToCrossHalfway()
    {
        // Arrange: 1 column, 4 rows
        // y=0: A (Red)
        // y=1: B (Blue)
        // y=2: Empty (C was eliminated)
        // y=3: Floor (Green, static)
        var state = new GameState(1, 4, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));   // A
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));  // B
        state.SetTile(0, 2, new Tile(0, ElementType.None, 0, 2));  // Empty (C eliminated)
        state.SetTile(0, 3, new Tile(3, ElementType.Item2, 0, 3)); // Floor

        var config = new Match3Config { GravitySpeed = 35.0f, MaxFallSpeed = 20.0f };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        // Act & Assert: Run physics frame by frame
        const float dt = 0.016f;
        bool bStartedFalling = false;
        bool aStartedFalling = false;
        bool bCrossedMidpoint = false;

        for (int frame = 0; frame < 100; frame++)
        {
            physics.Update(ref state, dt);

            // Find where A and B are now
            Tile? tileA = null;
            Tile? tileB = null;

            for (int y = 0; y < 4; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == ElementType.Item1) tileA = t;
                if (t.Type == ElementType.Item3) tileB = t;
            }

            Assert.NotNull(tileA);
            Assert.NotNull(tileB);

            // Track B's state
            if (tileB.Value.IsFalling)
            {
                bStartedFalling = true;
            }

            // Check if B has crossed midpoint (Position.Y >= 1.5 means data moves to y=2)
            if (tileB.Value.Position.Y >= 1.5f)
            {
                bCrossedMidpoint = true;
            }

            // Track A's state
            if (tileA.Value.IsFalling)
            {
                aStartedFalling = true;

                // Key assertion: A should NOT start falling before B crosses midpoint
                Assert.True(bCrossedMidpoint,
                    $"Frame {frame}: A started falling (Pos.Y={tileA.Value.Position.Y:F3}) " +
                    $"before B crossed midpoint (B.Pos.Y={tileB.Value.Position.Y:F3})");
            }

            // Exit early if both have settled
            if (!tileA.Value.IsFalling && !tileB.Value.IsFalling &&
                tileA.Value.Position.Y >= 0.99f && tileB.Value.Position.Y >= 1.99f)
            {
                break;
            }
        }

        Assert.True(bStartedFalling, "B should have started falling");
        Assert.True(aStartedFalling, "A should have started falling after B moved");
    }

    /// <summary>
    /// Verifies that when B is falling but hasn't crossed midpoint,
    /// A remains stationary (Position.Y stays at 0, IsFalling = false).
    /// </summary>
    [Fact]
    public void SequentialFall_TopTileStaysStationaryWhileMiddleTileInTransit()
    {
        // Arrange
        var state = new GameState(1, 3, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));   // A at top
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));  // B in middle
        state.SetTile(0, 2, new Tile(0, ElementType.None, 0, 2));  // Empty at bottom

        var config = new Match3Config { GravitySpeed = 10.0f, MaxFallSpeed = 5.0f }; // Slow fall
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        // Act: Run a few frames (not enough for B to cross midpoint)
        const float dt = 0.016f;

        for (int frame = 0; frame < 5; frame++)
        {
            physics.Update(ref state, dt);

            var tileA = state.GetTile(0, 0);
            var tileB = state.GetTile(0, 1);

            // B should be falling but not yet at midpoint
            if (tileB.Type == ElementType.Item3 && tileB.Position.Y < 1.5f)
            {
                // Assert: A should still be at y=0 and not falling
                Assert.Equal(ElementType.Item1, tileA.Type);
                Assert.Equal(0.0f, tileA.Position.Y, 0.001f);
                Assert.False(tileA.IsFalling, $"Frame {frame}: A should not be falling while B is in transit");
            }
        }
    }

    /// <summary>
    /// Verifies the final state after sequential falling completes.
    /// A-B stack over empty cell should result in B at bottom, A above B.
    /// </summary>
    [Fact]
    public void SequentialFall_FinalStateIsCorrect()
    {
        // Arrange: 1 column, 3 rows
        // y=0: A (Red)
        // y=1: B (Blue)
        // y=2: Empty
        var state = new GameState(1, 3, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));
        state.SetTile(0, 2, new Tile(0, ElementType.None, 0, 2));

        var config = new Match3Config { GravitySpeed = 35.0f, MaxFallSpeed = 20.0f };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        // Act: Run until stable
        const float dt = 0.016f;
        for (int i = 0; i < 100; i++)
        {
            physics.Update(ref state, dt);
            if (physics.IsStable(in state)) break;
        }

        // Assert: Final positions
        var tile0 = state.GetTile(0, 0);
        var tile1 = state.GetTile(0, 1);
        var tile2 = state.GetTile(0, 2);

        Assert.Equal(ElementType.None, tile0.Type); // y=0 should be empty
        Assert.Equal(ElementType.Item1, tile1.Type);  // A should be at y=1
        Assert.Equal(ElementType.Item3, tile2.Type); // B should be at y=2

        Assert.Equal(1.0f, tile1.Position.Y, 0.001f);
        Assert.Equal(2.0f, tile2.Position.Y, 0.001f);
        Assert.False(tile1.IsFalling);
        Assert.False(tile2.IsFalling);
    }

    /// <summary>
    /// Tests a longer chain: A-B-C-D with D eliminated.
    /// Each tile should wait for the one below to clear its cell.
    /// </summary>
    [Fact]
    public void SequentialFall_LongChain_EachTileWaitsForPrevious()
    {
        // Arrange: 1 column, 5 rows
        // y=0: A, y=1: B, y=2: C, y=3: Empty (D eliminated), y=4: Floor
        var state = new GameState(1, 5, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));    // A
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));   // B
        state.SetTile(0, 2, new Tile(3, ElementType.Item2, 0, 2));  // C
        state.SetTile(0, 3, new Tile(0, ElementType.None, 0, 3));   // Empty
        state.SetTile(0, 4, new Tile(4, ElementType.Item4, 0, 4)); // Floor

        var config = new Match3Config { GravitySpeed = 35.0f, MaxFallSpeed = 20.0f };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        // Act: Run until stable
        const float dt = 0.016f;
        for (int i = 0; i < 150; i++)
        {
            physics.Update(ref state, dt);
            if (physics.IsStable(in state)) break;
        }

        // Assert: Final positions - each tile moved down by 1
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);   // Empty
        Assert.Equal(ElementType.Item1, state.GetTile(0, 1).Type);    // A at y=1
        Assert.Equal(ElementType.Item3, state.GetTile(0, 2).Type);   // B at y=2
        Assert.Equal(ElementType.Item2, state.GetTile(0, 3).Type);  // C at y=3
        Assert.Equal(ElementType.Item4, state.GetTile(0, 4).Type); // Floor unchanged
    }
}

