using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class DuplicateIdTests
{
    [Fact]
    public void TilesWithDuplicateIds_ShouldAllFall()
    {
        // Scenario: 
        // 2 Columns, Height 3.
        // Row 0: TileA (Id=0), TileB (Id=0)
        // Row 1: Empty, Empty
        // Row 2: Empty, Empty
        
        // If the system relies on Unique IDs, one of these might be skipped.
        
        var config = new Match3Config { GravitySpeed = 10f };
        var rng = StubRandom.WithFixedValue(0);
        var state = new GameState(2, 3, 5, rng);
        var gravity = new RealtimeGravitySystem(config, rng);

        // Initialize tiles with ID = 0
        state.SetTile(0, 0, new Tile(0, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(0, ElementType.Item1, 1, 0));

        // Act
        // Run enough frames for them to fall at least one cell
        // Speed 10. Distance 1. Need 0.1s.
        // We run one frame to check if logic starts.
        
        gravity.Update(ref state, 0.02f); // Frame 1
        gravity.Update(ref state, 0.02f); // Frame 2

        // Assert
        // Both tiles should have moved down (or at least started falling)
        var tileA = state.GetTile(0, 0);
        var tileB = state.GetTile(1, 0);
        
        // If they started falling, their Y position should be > 0
        // Or if they moved to next cell, they should be at (0, 1) and (1, 1)
        
        // Let's check if they are "Falling" or moved.
        
        // Get the tiles (they might have moved to y=1)
        var tileA_Pos = state.GetTile(0, 0).Type == ElementType.None ? state.GetTile(0, 1) : state.GetTile(0, 0);
        var tileB_Pos = state.GetTile(1, 0).Type == ElementType.None ? state.GetTile(1, 1) : state.GetTile(1, 0);

        // Both should be valid tiles
        Assert.NotEqual(ElementType.None, tileA_Pos.Type);
        Assert.NotEqual(ElementType.None, tileB_Pos.Type);

        // Both should have non-zero velocity or position change
        bool aMoved = tileA_Pos.Position.Y > 0.01f;
        bool bMoved = tileB_Pos.Position.Y > 0.01f;

        Assert.True(aMoved, "Tile A (Col 0) should move");
        Assert.True(bMoved, "Tile B (Col 1) should move");
    }
}
