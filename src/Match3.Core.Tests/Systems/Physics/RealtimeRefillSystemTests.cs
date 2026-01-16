using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class RealtimeRefillSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class FixedSpawnModel : ISpawnModel
    {
        public TileType TypeToSpawn { get; set; } = TileType.Blue;

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            return TypeToSpawn;
        }
    }

    private class SequentialSpawnModel : ISpawnModel
    {
        private readonly TileType[] _sequence;
        private int _index;

        public SequentialSpawnModel(params TileType[] sequence)
        {
            _sequence = sequence;
            _index = 0;
        }

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            var type = _sequence[_index % _sequence.Length];
            _index++;
            return type;
        }
    }

    #region Basic Spawn Tests

    [Fact]
    public void Update_ShouldSpawnTileWhenTopIsEmpty()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Red };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - All top row should have new tiles
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(TileType.Red, tile.Type);
            Assert.True(tile.IsFalling, $"Tile at ({x},0) should be falling");
        }
    }

    [Fact]
    public void Update_ShouldNotSpawnWhenTopIsOccupied()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);

        // Place existing tiles at top row
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Red };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - Tiles should remain unchanged
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(TileType.Green, tile.Type);
        }
    }

    [Fact]
    public void Update_ShouldSpawnOnlyInEmptyColumns()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);

        // Occupy column 1 only
        state.SetTile(1, 0, new Tile(1, TileType.Green, 1, 0));

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Red };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.Equal(TileType.Red, state.GetTile(0, 0).Type);   // Spawned
        Assert.Equal(TileType.Green, state.GetTile(1, 0).Type); // Unchanged
        Assert.Equal(TileType.Red, state.GetTile(2, 0).Type);   // Spawned
    }

    #endregion

    #region Spawn Position Tests

    [Fact]
    public void Update_SpawnedTile_ShouldHaveCorrectInitialPosition()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Blue };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - Spawned tiles should start above the board
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(-1.0f, tile.Position.Y);
            Assert.Equal((float)x, tile.Position.X);
        }
    }

    [Fact]
    public void Update_SpawnedTile_ShouldHaveDownwardVelocity()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Blue };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.True(tile.Velocity.Y > 0, "Spawned tile should have downward velocity");
        }
    }

    [Fact]
    public void Update_SpawnedTile_ShouldSpawnAtFixedPosition()
    {
        // Arrange
        var state = new GameState(1, 3, 5, new StubRandom());
        ClearBoard(ref state);

        // Place a falling tile at (0, 1) with position partially through the cell
        var fallingTile = new Tile(1, TileType.Green, 0, 1)
        {
            Position = new Vector2(0, 0.5f), // Halfway through cell 0
            Velocity = new Vector2(0, 5.0f),
            IsFalling = true
        };
        state.SetTile(0, 1, fallingTile);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Red };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - New tile always spawns at fixed position -1.0
        // (Gravity system handles following via GravityTargetResolver)
        var newTile = state.GetTile(0, 0);
        Assert.Equal(TileType.Red, newTile.Type);
        Assert.Equal(-1.0f, newTile.Position.Y, 0.01f);
    }

    #endregion

    #region Tile ID Tests

    [Fact]
    public void Update_ShouldAssignUniqueIds()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        ClearBoard(ref state);
        state.NextTileId = 100;

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Blue };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - Each tile should have unique ID
        var ids = new HashSet<long>();
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.True(ids.Add(tile.Id), $"Duplicate ID found: {tile.Id}");
        }

        // NextTileId should have been incremented
        Assert.Equal(103, state.NextTileId);
    }

    [Fact]
    public void Update_MultipleUpdates_ShouldContinueUniqueIds()
    {
        // Arrange
        var state = new GameState(1, 2, 5, new StubRandom());
        ClearBoard(ref state);
        state.NextTileId = 1;

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Blue };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act - First spawn
        refill.Update(ref state);
        var firstId = state.GetTile(0, 0).Id;

        // Clear and spawn again
        state.SetTile(0, 0, new Tile(0, TileType.None, 0, 0));
        refill.Update(ref state);
        var secondId = state.GetTile(0, 0).Id;

        // Assert
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(firstId + 1, secondId);
    }

    #endregion

    #region SpawnContext Tests

    [Fact]
    public void Update_ShouldPassCorrectSpawnContext()
    {
        // Arrange
        SpawnContext? capturedContext = null;
        var capturingModel = new CapturingSpawnModel(ctx => capturedContext = ctx);

        var state = new GameState(1, 3, 5, new StubRandom())
        {
            TargetDifficulty = 0.7f,
            MoveLimit = 20,
            MoveCount = 5
        };
        ClearBoard(ref state);

        var refill = new RealtimeRefillSystem(capturingModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(0.7f, capturedContext.Value.TargetDifficulty, 0.001f);
        Assert.Equal(15, capturedContext.Value.RemainingMoves); // 20 - 5
    }

    [Fact]
    public void Update_RemainingMoves_ShouldNotBeNegative()
    {
        // Arrange
        SpawnContext? capturedContext = null;
        var capturingModel = new CapturingSpawnModel(ctx => capturedContext = ctx);

        var state = new GameState(1, 3, 5, new StubRandom())
        {
            MoveLimit = 10,
            MoveCount = 15 // Exceeded limit
        };
        ClearBoard(ref state);

        var refill = new RealtimeRefillSystem(capturingModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(0, capturedContext.Value.RemainingMoves); // Should clamp to 0
    }

    private class CapturingSpawnModel : ISpawnModel
    {
        private readonly Action<SpawnContext> _onPredict;

        public CapturingSpawnModel(Action<SpawnContext> onPredict)
        {
            _onPredict = onPredict;
        }

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            _onPredict(context);
            return TileType.Blue;
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Update_EmptyBoard_ShouldFillTopRow()
    {
        // Arrange
        var state = new GameState(5, 5, 5, new StubRandom());
        ClearBoard(ref state);

        var types = new[] { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple };
        var spawnModel = new SequentialSpawnModel(types);
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - Top row should be filled with different types
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(types[x], state.GetTile(x, 0).Type);
        }
    }

    [Fact]
    public void Update_SingleColumnBoard_ShouldWork()
    {
        // Arrange
        var state = new GameState(1, 5, 5, new StubRandom());
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Red };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.Equal(TileType.Red, state.GetTile(0, 0).Type);
    }

    [Fact]
    public void Update_WideBoard_ShouldFillAllColumns()
    {
        // Arrange
        var state = new GameState(10, 3, 5, new StubRandom());
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Blue };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        for (int x = 0; x < 10; x++)
        {
            Assert.Equal(TileType.Blue, state.GetTile(x, 0).Type);
        }
    }

    #endregion

    #region Helper Methods

    private static void ClearBoard(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));
            }
        }
    }

    #endregion
}
