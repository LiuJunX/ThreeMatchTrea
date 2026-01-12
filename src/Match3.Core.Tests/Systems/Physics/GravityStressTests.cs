using System;
using System.Text;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;
using System.Numerics; // Fix for Vector2

namespace Match3.Core.Tests.Systems.Physics;

public class GravityStressTests
{
    private readonly ITestOutputHelper _output;

    public GravityStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubTileGenerator : ITileGenerator
    {
        public TileType GenerateNonMatchingTile(ref GameState state, int x, int y) => TileType.Blue;
    }

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
        public bool NextBool() => false;
        public T PickRandom<T>(System.Collections.Generic.IList<T> items) => items[0];
        public void Shuffle<T>(System.Collections.Generic.IList<T> list) { }
    }

    [Fact]
    public void ColumnFall_ShouldBeFastAndSmooth()
    {
        // Setup a tall grid
        int height = 10;
        var state = new GameState(1, height, 3, new StubRandom());
        var gravity = new RealtimeGravitySystem();
        
        // Fill top 3 with tiles, bottom 7 empty
        // Y=0,1,2 occupied. 3..9 empty.
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.Red, 0, 2));

        for (int y = 3; y < height; y++)
        {
            state.SetTile(0, y, new Tile(0, TileType.None, 0, y));
        }

        // Simulate 60 frames (1 second)
        float dt = 0.016f;
        
        StringBuilder sb = new StringBuilder();
        
        for (int i = 0; i < 100; i++)
        {
            gravity.Update(ref state, dt);
            
            var t0 = GetTileById(state, 1);
            var t1 = GetTileById(state, 2);
            var t2 = GetTileById(state, 3);
            
            // sb.AppendLine($"Frame {i}: T2(Bot)={t2.Position.Y:F2}(v={t2.Velocity.Y:F2}) T1(Mid)={t1.Position.Y:F2}(v={t1.Velocity.Y:F2}) T0(Top)={t0.Position.Y:F2}(v={t0.Velocity.Y:F2})");
            
            // Check for stalling
            // If tiles are falling, velocity should increase or be high
            if (t2.IsFalling && t2.Velocity.Y < 0.1f && i > 5)
            {
                 // It stopped?
                 // Unless it hit bottom.
                 if (t2.Position.Y < height - 1 - 0.1f)
                 {
                     _output.WriteLine(sb.ToString());
                     Assert.Fail($"Tile 2 stopped prematurely at {t2.Position.Y}");
                 }
            }
        }
        
        var finalT2 = GetTileById(state, 3);
        // Should be at bottom (9)
        Assert.Equal(9.0f, finalT2.Position.Y, 0.1f);
        
        // Others should be stacked on top
        var finalT1 = GetTileById(state, 2);
        Assert.Equal(8.0f, finalT1.Position.Y, 0.1f);
        
        var finalT0 = GetTileById(state, 1);
        Assert.Equal(7.0f, finalT0.Position.Y, 0.1f);
    }

    [Fact]
    public void VelocityInheritance_ShouldNotSlowDown_WhenStacked()
    {
        var state = new GameState(1, 10, 3, new StubRandom());
        var gravity = new RealtimeGravitySystem();

        // Tile A (Bottom) falling at speed 10
        var tileA = new Tile(1, TileType.Red, 0, 5);
        tileA.Position = new Vector2(0, 5.0f);
        tileA.Velocity = new Vector2(0, 10.0f);
        tileA.IsFalling = true;
        state.SetTile(0, 5, tileA);

        // Tile B (Top) falling at speed 15 (catching up)
        var tileB = new Tile(2, TileType.Blue, 0, 4);
        tileB.Position = new Vector2(0, 4.05f); // Very close to A (Gap 0.95 < 1.0) -> Should collide
        tileB.Velocity = new Vector2(0, 15.0f);
        tileB.IsFalling = true;
        state.SetTile(0, 4, tileB);

        float dt = 0.016f;
        gravity.Update(ref state, dt);

        var newA = state.GetTile(0, 5);
        var newB = state.GetTile(0, 4);
        
        // A should accelerate. 10 + 35*0.016 = 10.56.
        // Pos A: 5.0 + 10*0.016 = 5.16. (approx)
        
        // B should collide with A
        // TargetY for B is A.Pos - 1 = 4.16.
        // B.Pos was 4.05.
        // B physics: 4.05 + 15*0.016 = 4.29.
        // 4.29 > 4.16. Snap to 4.16.
        
        Assert.Equal(newA.Position.Y - 1.0f, newB.Position.Y, 0.01f);
        
        // Crucial: Velocity should be A's velocity (10.56)
        // My code sets it to A.Velocity (10.56).
        Assert.Equal(newA.Velocity.Y, newB.Velocity.Y, 0.01f);
        Assert.True(newB.IsFalling);
    }

    private Tile GetTileById(GameState state, long id)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Id == id) return state.Grid[i];
        }
        return new Tile(); // Should not happen
    }
}
