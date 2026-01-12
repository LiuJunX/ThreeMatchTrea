using System;
using System.Numerics;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class GravityBugTests
{
    private class StubTileGenerator : ITileGenerator
    {
        public TileType GenerateNonMatchingTile(ref GameState state, int x, int y) => TileType.Red;
    }

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    [Fact]
    public void StackedTiles_ShouldFallTogether_Start()
    {
        // Arrange
        var state = new GameState(1, 5, 3, new StubRandom());
        var gravity = new RealtimeGravitySystem();
        
        // Setup:
        // Y=4: Ground
        // Y=1: Tile B (Bottom)
        // Y=0: Tile A (Top)
        
        state.SetTile(0, 4, new Tile(99, TileType.Blue, 0, 4)); // Floor
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        
        // Act - Frame 1
        gravity.Update(ref state, 0.1f);
        
        var tileA = state.GetTile(0, 0);
        var tileB = state.GetTile(0, 1);
        
        // Check B
        Assert.True(tileB.IsFalling, "Tile B should be falling");
        Assert.True(tileB.Position.Y > 1.0f, "Tile B should have moved down");
        
        // Check A
        Assert.True(tileA.IsFalling, "Tile A should be falling together with B");
        Assert.True(tileA.Position.Y > 0.0f, "Tile A should have moved down");
    }

    [Fact]
    public void StackedTiles_ShouldNotSnapToGrid_WhenFloating()
    {
        // Arrange
        var state = new GameState(1, 10, 3, new StubRandom());
        var gravity = new RealtimeGravitySystem();

        // Setup:
        // Tile B at Y=3.5 (Falling). Logically at Y=3.
        // Tile A at Y=2.5 (Falling). Logically at Y=2.
        // They are falling together.
        
        var tileB = new Tile(2, TileType.Red, 0, 3);
        tileB.Position = new Vector2(0, 3.5f);
        tileB.IsFalling = true;
        tileB.Velocity = new Vector2(0, 5.0f); // Moving down

        var tileA = new Tile(1, TileType.Red, 0, 2);
        tileA.Position = new Vector2(0, 2.5f);
        tileA.IsFalling = true;
        tileA.Velocity = new Vector2(0, 5.0f); // Moving down

        state.SetTile(0, 3, tileB);
        state.SetTile(0, 2, tileA);
        
        // Empty space below B
        state.SetTile(0, 4, new Tile(0, TileType.None, 0, 4));
        state.SetTile(0, 5, new Tile(0, TileType.None, 0, 5));

        // Act
        // Gravity update
        // tileB should fall to ~3.5 + 0.5 = 4.0.
        // tileA should fall to ~2.5 + 0.5 = 3.0.
        // BUT we want to ensure tileA doesn't snap to 2.0 or 3.0 prematurely if blocked?
        // Actually, let's test the "Stop Halfway" case.
        // Suppose tileB is BLOCKED at 3.5 (e.g. hit something? No, B is falling freely).
        
        // Let's simulate B falling slightly.
        float dt = 0.01f; 
        // 5.0 * 0.01 = 0.05.
        // B -> 3.55.
        // A -> 2.55.
        
        // Logic check:
        // For A (at 2):
        // targetY calculation:
        // Below is B (at 3). B is falling.
        // targetY = B.Position.Y - 1 = 3.55 - 1 = 2.55.
        // A.Position.Y (2.5) + vel*dt (0.05) = 2.55.
        // A reaches targetY (2.55).
        // Since A.Position >= targetY (approx), it enters "else".
        // It snaps A to... ?
        // If it snaps to Y=2. A moves 2.55 -> 2.0. ERROR.
        
        gravity.Update(ref state, dt);

        var newA = state.GetTile(0, 2); 
        if (newA.Id != tileA.Id)
        {
            // If it swapped, check next slot
            newA = state.GetTile(0, 3);
        }
        
        Assert.Equal(tileA.Id, newA.Id); // Ensure we found A
        
        // Assert
        Assert.True(newA.IsFalling, "Tile A should still be falling");
        Assert.True(newA.Position.Y > 2.5f, $"Tile A should advance (Was 2.5, New {newA.Position.Y})");
        Assert.NotEqual(2.0f, newA.Position.Y); // Should NOT snap to 2
        
        // Ensure velocity is maintained (approx 5.0)
        // If it hit the target, it should match B's velocity (5 + gravity*dt)
        // 5 + 35 * 0.01 = 5.35.
        // Or if B didn't accelerate much.
        Assert.True(newA.Velocity.Y > 4.0f, $"Velocity should be maintained (Was {newA.Velocity.Y})");
    }
}
