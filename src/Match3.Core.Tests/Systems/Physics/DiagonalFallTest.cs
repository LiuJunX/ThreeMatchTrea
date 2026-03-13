using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;


namespace Match3.Core.Tests.Systems.Physics;

public class DiagonalFallTest
{
    private readonly ITestOutputHelper _output;

    public DiagonalFallTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SlideAndFall_ShouldBeContinuous()
    {
        var config = new Match3Config {
            GravitySpeed = 35f, // Use default
            MaxFallSpeed = 20f
        };
        var rng = StubRandom.WithFixedValue(0);
        // Width 2, Height 5
        var state = new GameState(2, 5, 5, rng);
        var gravity = new RealtimeGravitySystem(config, rng);

        // Setup
        // (0,0): Falling Tile
        // (0,1): Obstacle (Suspended)
        // Col 1: Empty
        
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item1, 0, 1));
        state.Lock(0, 1, CellLockType.Drop);
        
        // Log positions frame by frame
        StringBuilder log = new StringBuilder();
        log.AppendLine("Frame, X, Y, VelX, VelY, IsFalling, GridX, GridY");

        float dt = 0.02f;
        int frames = 0;
        bool crossedBoundary = false;
        float lastY = 0;

        // Run for enough frames to slide and fall
        for (int i = 0; i < 100; i++)
        {
            // Capture tile state
            // Note: Tile might move from (0,0) to (1,0) or (1,1)
            Tile currentTile = FindTile(state, 1);
            
            log.AppendLine($"{i}, {currentTile.Position.X:F3}, {currentTile.Position.Y:F3}, {currentTile.Velocity.X:F3}, {currentTile.Velocity.Y:F3}, {currentTile.IsFalling}, {state.GetTileIndex(1).X}, {state.GetTileIndex(1).Y}");

            if (currentTile.Position.X > 0.5f && !crossedBoundary)
            {
                crossedBoundary = true;
                _output.WriteLine($"Crossed boundary at frame {i}. Pos: {currentTile.Position}");
            }

            if (i > 0)
            {
                // Check for teleportation (large Y jump)
                float deltaY = currentTile.Position.Y - lastY;
                if (deltaY > 0.5f) // Jumped more than 0.5 grid in one frame?
                {
                    _output.WriteLine(log.ToString());
                    Assert.Fail($"Teleportation detected at frame {i}. Y jumped from {lastY} to {currentTile.Position.Y}");
                }
            }
            lastY = currentTile.Position.Y;

            gravity.Update(ref state, dt);
            frames++;

            // Stop if reached bottom
            if (currentTile.Position.Y > 3.9f && currentTile.Velocity.Y == 0)
                break;
        }

        _output.WriteLine(log.ToString());
    }

    private Tile FindTile(GameState state, int id)
    {
        for(int x=0; x<state.Width; x++)
        {
            for(int y=0; y<state.Height; y++)
            {
                var t = state.GetTile(x, y);
                if (t.Id == id) return t;
            }
        }
        return new Tile(); // Not found
    }
}

// Helper extension to find tile grid index
public static class StateExtensions
{
    public static (int X, int Y) GetTileIndex(this GameState state, int id)
    {
         for(int x=0; x<state.Width; x++)
        {
            for(int y=0; y<state.Height; y++)
            {
                var t = state.GetTile(x, y);
                if (t.Id == id) return (x, y);
            }
        }
        return (-1, -1);
    }
}
