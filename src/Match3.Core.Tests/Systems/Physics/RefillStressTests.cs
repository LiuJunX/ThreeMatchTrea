using System.Text;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Xunit;
using Xunit.Abstractions;
using System.Numerics;

namespace Match3.Core.Tests.Systems.Physics;

public class RefillStressTests
{
    private readonly ITestOutputHelper _output;

    public RefillStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubTileGenerator : ITileGenerator
    {
        public TileType GenerateNonMatchingTile(ref GameState state, int x, int y) => TileType.Blue;
    }

    private class StubRandom : Match3.Random.IRandom
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

    private Tile GetTileById(GameState state, long id)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var t = state.GetTile(x, y);
                if (t.Id == id) return t;
            }
        }
        return default;
    }

    [Fact]
    public void Refill_ShouldSpawnTilesCorrectlyStacked()
    {
        // 1. Setup empty board
        int height = 5;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubTileGenerator());
        
        // 2. Run refill once
        refill.Update(ref state);

        // 3. Check initial positions of spawned tiles
        // Logic:
        // y=4 (bottom empty) -> filled first -> pos -1
        // y=3 -> filled second -> pos -2
        // ...
        // y=0 (top) -> filled last -> pos -5

        for (int y = 0; y < height; y++)
        {
            var tile = state.GetTile(0, y);
            Assert.NotEqual(TileType.None, tile.Type);
            Assert.True(tile.IsFalling);
            
            // Expected StartY calculation: -1.0f - (deepestEmptyY - y)
            // deepestEmptyY was 4.
            // y=4: -1 - (4-4) = -1
            // y=0: -1 - (4-0) = -5
            float expectedY = -1.0f - (4 - y);
            Assert.Equal(expectedY, tile.Position.Y, 0.01f);
        }
    }

    [Fact]
    public void RefillAndGravity_ShouldFallSmoothlyToBottom()
    {
        // 1. Setup empty board
        int height = 10;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubTileGenerator());
        var gravity = new RealtimeGravitySystem();

        // 2. Run refill
        refill.Update(ref state);

        // 3. Simulate frames
        float dt = 0.016f;
        int frames = 200; // Enough time to fall 10+5 units

        // Track the bottom-most tile (originally at y=height-1)
        // Its ID should be... depends on iteration order.
        // Refill iterates y from deepest(9) to 0.
        // So ID order: 1, 2, ... 10
        // y=9 gets ID 1.
        
        var bottomTileStart = state.GetTile(0, 9); 
        long bottomTileId = bottomTileStart.Id;

        for (int i = 0; i < frames; i++)
        {
            gravity.Update(ref state, dt);
            
            var t = GetTileById(state, bottomTileId);
            
            // Check if it stops prematurely
            if (t.IsFalling && t.Velocity.Y < 0.1f && t.Position.Y < 8.0f && i > 10)
            {
                 Assert.Fail($"Tile stuck at {t.Position.Y} with velocity {t.Velocity.Y} at frame {i}");
            }
        }

        // 4. Verify all tiles landed
        for (int y = 0; y < height; y++)
        {
            var tile = state.GetTile(0, y);
            Assert.False(tile.IsFalling, $"Tile at {y} is still falling at {tile.Position.Y}");
            Assert.Equal((float)y, tile.Position.Y, 0.1f);
        }
    }
}
