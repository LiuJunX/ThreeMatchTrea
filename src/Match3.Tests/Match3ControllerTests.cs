using System.Collections.Generic;
using Match3.Core;
using Match3.Core.Structs;
using Match3.Core.Logic;
using Xunit;

namespace Match3.Tests;

public class Match3ControllerTests
{
    [Fact]
    public void TrySwap_ValidSwap_ResolvesMatches()
    {
        // Arrange
        var rng = new TestRandomGenerator(); 
        var view = new MockGameView();
        var controller = new Match3Controller(4, 4, 5, rng, view);

        // Setup a specific board state
        // Row 0: R R G R  (Swap G<->R at x=2,3 to make R R R R)
        controller.DebugSetTile(new Position(0, 0), TileType.Red);
        controller.DebugSetTile(new Position(1, 0), TileType.Red);
        controller.DebugSetTile(new Position(2, 0), TileType.Green);
        controller.DebugSetTile(new Position(3, 0), TileType.Red);
        
        // Clear others to avoid accidental matches
        for(int y=0; y<4; y++) 
        {
            for(int x=0; x<4; x++) 
            {
                if(y > 0) controller.DebugSetTile(new Position(x, y), TileType.None);
            }
        }
        
        // Act
        // Swap (2,0) <-> (3,0)
        controller.OnTap(new Position(2, 0));
        controller.OnTap(new Position(3, 0));

        // Assert
        Assert.Equal("Swapping...", controller.StatusMessage);
        
        // ShowSwap is called ONLY after animation finishes and logic is resolved.
        Assert.False(view.SwapSuccess.HasValue, "View should NOT be notified of swap result yet (animation pending)");

        // Now we need to pump the update loop to trigger resolution
        // We need to simulate time passing so tiles move to swap positions.
        // SwapSpeed is 10.0f. Distance is 1. Time = 1/10 = 0.1s.
        // Let's update for 0.2s to be safe.
        // Note: AnimateTiles moves tiles. We need enough time for them to reach target.
        // Distance is 1. Speed 10. Time 0.1s.
        // Update(0.2f) might cover it if single step.
        // But AnimateTiles steps by dt.
        
        // Let's loop until idle or timeout to be robust
        int maxSteps = 100;
        while(!controller.IsIdle && maxSteps-- > 0)
        {
            controller.Update(0.016f); // 60fps
        }

        Assert.True(view.SwapSuccess.HasValue, "Swap should be visualized as success after animation");
        Assert.True(view.SwapSuccess.Value, "Swap should be successful");
        
        // After swap animation finishes, it checks for matches.
        // If matches found -> Resolving state.
        
        // Verify matches were detected
        // Matches are shown via ShowMatches
        Assert.NotEmpty(view.AllMatches); // Should be one set of matches
        var matchSet = view.AllMatches[0];
        
        // 0,0 1,0 2,0 should match (after swap 2,0 becomes Red)
        // 3,0 becomes Green.
        Assert.Contains(new Position(0, 0), matchSet);
        Assert.Contains(new Position(1, 0), matchSet);
        Assert.Contains(new Position(2, 0), matchSet); // The one that moved from 3,0 to 2,0 (Red)
    }

    [Fact]
    public void TrySwap_InvalidSwap_Reverts()
    {
        // Arrange
        var rng = new TestRandomGenerator();
        var view = new MockGameView();
        var controller = new Match3Controller(4, 4, 5, rng, view);

        // Clear
        for(int y=0; y<4; y++) for(int x=0; x<4; x++) controller.DebugSetTile(new Position(x, y), TileType.None);

        // R G
        controller.DebugSetTile(new Position(0, 0), TileType.Red);
        controller.DebugSetTile(new Position(1, 0), TileType.Green);
        
        // Act
        // Swap (0,0) <-> (1,0) -> G R. No match.
        controller.OnTap(new Position(0, 0));
        controller.OnTap(new Position(1, 0));

        // Assert
        Assert.Equal("Swapping...", controller.StatusMessage);
        
        // Pump update to finish swap animation
        int maxSteps = 100;
        while(maxSteps-- > 0)
        {
            controller.Update(0.016f);
            if(controller.IsIdle) break;
        }
        
        // Now it should have checked matches, found none, and started AnimateRevert.
        // We need to pump update again for revert animation?
        // Wait, IsIdle is true only after everything is done.
        // If it reverts, it goes Idle -> AnimateSwap -> Resolving (no match) -> AnimateRevert -> Idle.
        // So checking IsIdle above might be enough to catch the end of Revert too?
        // Let's trace:
        // Update:
        //  AnimateSwap (Stable) -> HasMatches? No -> Swap Back -> AnimateRevert.
        //  Next Update:
        //  AnimateRevert (Not Stable) -> ...
        //  AnimateRevert (Stable) -> ShowSwap(false) -> Idle.
        
        // So the loop above will run until it is completely back to Idle.
        
        // So ShowSwap is called ONLY after the tiles physically moved (visual swap).
        Assert.True(view.SwapSuccess.HasValue, "View should be notified of swap result");
        Assert.False(view.SwapSuccess.Value, "Swap should be reported as failed (false) to the view");
        
        // Verify state is reverted
        
        // Check tiles
        // We need a way to check state.
        // Using DebugSetTile we can't get.
        // But we can check State property.
        // State returns a copy.
        Assert.Equal(TileType.Red, controller.State.GetTile(0, 0).Type);
        Assert.Equal(TileType.Green, controller.State.GetTile(1, 0).Type);
    }
}

public class MockGameView : IGameView
{
    public bool? SwapSuccess { get; private set; }
    public List<List<Position>> AllMatches { get; } = new();

    public void RenderBoard(TileType[,] board) { }
    
    public void ShowSwap(Position a, Position b, bool success) 
    {
        SwapSuccess = success;
    }
    
    public void ShowMatches(IReadOnlyCollection<Position> matched) 
    {
        AllMatches.Add(new List<Position>(matched));
    }
    
    public void ShowGravity(IEnumerable<TileMove> moves) { }
    public void ShowRefill(IEnumerable<TileMove> newTiles) { }
}
