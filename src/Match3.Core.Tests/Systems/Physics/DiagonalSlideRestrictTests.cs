using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class DiagonalSlideRestrictTests
{
    [Fact]
    public void NormalTile_ShouldNot_SlideOffOtherNormalTile()
    {
        // Scenario:
        // Col 0: Empty
        // Col 1: TileA (Top), TileB (Bottom)
        //
        // Even though Col 0 is empty, TileA should NOT slide into it
        // because TileB is a normal tile (not an obstacle).
        // Normal tiles should stack, not behave like liquid.
        
        var config = new Match3Config { GravitySpeed = 10f };
        var rng = StubRandom.WithFixedValue(0);
        var state = new GameState(2, 2, 5, rng);
        var gravity = new RealtimeGravitySystem(config, rng);

        // Setup
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
        state.SetTile(0, 1, new Tile(0, ElementType.None, 0, 1));
        
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0)); // TileA (Top)
        state.SetTile(1, 1, new Tile(2, ElementType.Item1, 1, 1)); // TileB (Bottom) - Acting as floor

        // Verify initial state
        Assert.Equal(ElementType.Item1, state.GetTile(1, 0).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(1, 1).Type);

        // Act
        gravity.Update(ref state, 0.02f);

        // Assert
        var tileAt10 = state.GetTile(1, 0);
        
        // TileA should still be at (1,0) or at least NOT moving horizontally
        Assert.Equal(1, (int)tileAt10.Position.X);
        Assert.Equal(0, tileAt10.Velocity.X); // Should be 0
        
        // Should NOT be in Col 0
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 1).Type);
    }

    [Fact]
    public void NormalTile_Should_SlideOffSuspendedTile()
    {
        // Scenario:
        // Col 0: Empty
        // Col 1: TileA (Top), Obstacle (Bottom)
        //
        // TileA SHOULD slide into Col 0 because it's blocked by an obstacle.

        var config = new Match3Config { GravitySpeed = 10f };
        var rng = StubRandom.WithFixedValue(0);
        var state = new GameState(2, 2, 5, rng);
        var gravity = new RealtimeGravitySystem(config, rng);

        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
        state.SetTile(0, 1, new Tile(0, ElementType.None, 0, 1));

        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0)); // TileA
        // Place obstacle at (1,1) — no tile, obstacle blocks direct fall
        state.SetTile(1, 1, new Tile(0, ElementType.None, 1, 1));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));

        // Act
        gravity.Update(ref state, 0.02f);

        // Assert
        var tileAt10 = state.GetTile(1, 0);

        // Should have moved or started moving
        // With 0.02f and speed 10, it might have moved slightly or fully depending on logic
        // But Velocity.X should be non-zero OR position changed

        // Check if it appeared in Col 0?
        // If it moved fully, (1,0) is None.

        if (state.GetTile(1, 0).Type != ElementType.None)
        {
            // Still in source cell, check if moving
            Assert.True(state.GetTile(1, 0).Position.X < 1.0f || state.GetTile(1, 0).Velocity.X != 0,
                "Tile should start sliding left off the obstacle");
        }
        else
        {
            // Moved fully to (0,0) or (0,1) depending on gravity
            // If it moved left, it should be in Col 0
            Assert.True(state.GetTile(0, 0).Type != ElementType.None || state.GetTile(0, 1).Type != ElementType.None,
                "Tile should have moved to Col 0");
        }
    }
}
