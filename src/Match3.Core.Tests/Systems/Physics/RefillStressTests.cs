using System.Numerics;
using System.Text;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.View;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Physics;

public class RefillStressTests
{
    private readonly ITestOutputHelper _output;

    public RefillStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubSpawnModel : ISpawnModel
    {
        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context) => TileType.Blue;
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
        var refill = new RealtimeRefillSystem(new StubSpawnModel());
        
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
    public void Refill_ShouldSpawnAtFixedPosition()
    {
        // 1. Setup board with a falling tile at (0, 1)
        int height = 5;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubSpawnModel());

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

        // New behavior: Always spawn at -1.0f, gravity system handles following
        Assert.Equal(-1.0f, newTile.Position.Y, 0.001f);
    }

    [Fact]
    public void RefillAndGravity_ShouldFillBoardEventually()
    {
        // 1. Setup empty board
        int height = 10;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubSpawnModel());
        var gravity = new RealtimeGravitySystem(new Match3Config(), new StubRandom());

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

    [Fact]
    public void Refill_NewTileFollowsAfterHalfCell()
    {
        // 1. Setup board with a falling tile at (0, 1)
        int height = 5;
        var state = new GameState(1, height, 3, new StubRandom());
        var refill = new RealtimeRefillSystem(new StubSpawnModel());
        var gravity = new RealtimeGravitySystem(new Match3Config(), new StubRandom());

        // Place a tile at (0, 1) that has already crossed 0.5 cells
        // Position.Y = 1.6 means it has moved 0.6 cells from row 1
        var fallingTile = new Tile(100, TileType.Red, 0, 1);
        fallingTile.Position = new Vector2(0, 1.6f);
        fallingTile.Velocity = new Vector2(0, 10.0f);
        fallingTile.IsFalling = true;
        state.SetTile(0, 1, fallingTile);

        // Ensure (0, 0) is empty
        state.SetTile(0, 0, new Tile(0, TileType.None, 0, 0));

        // 2. Run Refill to spawn the new tile
        refill.Update(ref state);
        var newTile = state.GetTile(0, 0);

        // New tile always spawns at -1.0f
        Assert.NotEqual(TileType.None, newTile.Type);
        Assert.Equal(-1.0f, newTile.Position.Y, 0.001f);

        // 3. Run gravity - the new tile should start falling immediately
        // because the tile below has already crossed the 0.5 threshold
        gravity.Update(ref state, 0.02f);

        // Find new tile again (may have moved)
        var updatedTile = state.GetTile(0, 0);
        Assert.True(updatedTile.IsFalling, "New tile should start falling after gravity update");
        Assert.True(updatedTile.Position.Y > -1.0f, "New tile should have moved from spawn position");
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

    #region Integration Tests with RuleBasedSpawnModel

    [Fact]
    public void Refill_WithRuleBasedSpawnModel_UsesSpawnContext()
    {
        // Arrange: Create state with difficulty settings
        var state = new GameState(3, 3, 6, new StubRandom());
        state.TargetDifficulty = 0.5f;
        state.MoveLimit = 20;
        state.MoveCount = 5;

        var spawnModel = new RuleBasedSpawnModel(new StubRandom());
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act: Run refill
        refill.Update(ref state);

        // Assert: Top row should have spawned tiles
        for (int x = 0; x < state.Width; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.NotEqual(TileType.None, tile.Type);
            Assert.True(tile.IsFalling);
        }
    }

    [Fact]
    public void Refill_WithRuleBasedSpawnModel_RemainingMovesCalculatedCorrectly()
    {
        // Arrange: MoveLimit=20, MoveCount=18 => RemainingMoves=2 (should trigger Help)
        var state = new GameState(5, 5, 6, new StubRandom());
        state.TargetDifficulty = 0.5f;
        state.MoveLimit = 20;
        state.MoveCount = 18;

        // Setup potential match: if Red spawned at column 2, it would match
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(0, TileType.None, 2, 0)); // Empty spawn point

        var spawnModel = new RuleBasedSpawnModel(new StubRandom());
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert: With only 2 moves left and GoalProgress=0, Help mode should trigger
        // Help mode tries to spawn colors that create matches
        var spawnedTile = state.GetTile(2, 0);
        Assert.NotEqual(TileType.None, spawnedTile.Type);
        // In Help mode, Red should be spawned to complete the match
        Assert.Equal(TileType.Red, spawnedTile.Type);
    }

    #endregion
}
