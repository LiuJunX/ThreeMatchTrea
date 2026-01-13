using System.Text;
using Match3.Core.Config;
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
    public void Refill_ShouldSpawnTilesOneByOne()
    {
        // 1. Setup empty board
        int height = 5;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubTileGenerator());
        
        // 2. Run refill once
        refill.Update(ref state);

        // 3. Check that only the top slot is filled
        var topTile = state.GetTile(0, 0);
        Assert.NotEqual(TileType.None, topTile.Type);
        Assert.True(topTile.IsFalling);
        Assert.Equal(-1.0f, topTile.Position.Y, 0.01f);

        // The rest should be empty
        for (int y = 1; y < height; y++)
        {
            var tile = state.GetTile(0, y);
            Assert.Equal(TileType.None, tile.Type);
        }
    }

    [Fact]
    public void Refill_ShouldSpawnContinuouslyRelativeToTileBelow()
    {
        // 1. Setup board with a falling tile at (0, 1)
        int height = 5;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubTileGenerator());

        // Place a tile at (0, 1) that has fallen slightly
        // Logical position is (0, 1), Physical position is 0.5f
        var fallingTile = new Tile(100, TileType.Red, 0, 1);
        fallingTile.Position = new Vector2(0, 0.5f);
        fallingTile.IsFalling = true;
        state.SetTile(0, 1, fallingTile);

        // Ensure (0, 0) is empty
        state.SetTile(0, 0, new Tile(0, TileType.None, 0, 0));

        // 2. Run refill
        refill.Update(ref state);

        // 3. Check new tile at (0, 0)
        var newTile = state.GetTile(0, 0);
        Assert.NotEqual(TileType.None, newTile.Type);
        
        // Expected: 0.5f - 1.0f = -0.5f
        Assert.Equal(-0.5f, newTile.Position.Y, 0.001f);
    }

    [Fact]
    public void RefillAndGravity_ShouldFillBoardEventually()
    {
        // 1. Setup empty board
        int height = 10;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubTileGenerator());
        var gravity = new RealtimeGravitySystem(new Match3Config());

        // 2. Simulate loop
        float dt = 0.016f;
        int maxFrames = 1000; // Allow enough time for all tiles to spawn and fall
        
        for (int i = 0; i < maxFrames; i++)
        {
            // System Order: Refill -> Gravity
            refill.Update(ref state);
            gravity.Update(ref state, dt);
            
            if (IsBoardFullAndStable(state)) break;
        }

        // 3. Verify all tiles landed
        for (int y = 0; y < height; y++)
        {
            var tile = state.GetTile(0, y);
            Assert.NotEqual(TileType.None, tile.Type);
            Assert.False(tile.IsFalling, $"Tile at {y} is still falling at {tile.Position.Y}");
            Assert.Equal((float)y, tile.Position.Y, 0.1f);
        }
    }

    private bool IsBoardFullAndStable(GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                var t = state.GetTile(x, y);
                if (t.Type == TileType.None) return false;
                if (t.IsFalling) return false;
                if (Math.Abs(t.Position.Y - y) > 0.01f) return false;
            }
        }
        return true;
    }
}
