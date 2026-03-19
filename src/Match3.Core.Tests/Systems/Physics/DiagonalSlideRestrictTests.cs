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
        // Scenario (2x2):
        //   T O     TileA (1,0), Obstacle (0,0) makes (0,1) dead zone
        //   O .     Obstacle (1,1) blocks vertical
        // TileA slides right to (0,1) which is a dead zone.
        // Wait — 2x2 grid, col 0 obstacle at row 0 means (0,1) is dead zone.
        // Actually for a 2-wide grid: tile at col 1, obstacle at (1,1).
        // Need (0,1) to be dead zone → obstacle at (0,0).

        var config = new Match3Config { GravitySpeed = 10f };
        var rng = StubRandom.WithFixedValue(0);
        var state = new GameState(2, 2, 5, rng);
        var gravity = new RealtimeGravitySystem(config, rng);

        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0)); // TileA
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1)); // blocks vertical
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Box, 1)); // makes (0,1) dead zone

        // Act — run enough frames for the slide to complete
        for (int i = 0; i < 30; i++)
            gravity.Update(ref state, 0.02f);

        // Assert: tile should have slid to (0,1)
        Assert.True(
            state.GetTile(0, 1).Type != ElementType.None,
            "Tile should have slid to dead zone (0,1)");
    }
}
