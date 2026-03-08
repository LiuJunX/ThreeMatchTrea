using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class RealtimeRefillSystemTests
{

    private class FixedSpawnModel : ISpawnModel
    {
        public ElementType TypeToSpawn { get; set; } = ElementType.Item3;

        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            return TypeToSpawn;
        }
    }

    private class SequentialSpawnModel : ISpawnModel
    {
        private readonly ElementType[] _sequence;
        private int _index;

        public SequentialSpawnModel(params ElementType[] sequence)
        {
            _sequence = sequence;
            _index = 0;
        }

        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
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
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - All top row should have new tiles
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(ElementType.Item1, tile.Type);
            Assert.True(tile.IsFalling, $"Tile at ({x},0) should be falling");
        }
    }

    [Fact]
    public void Update_ShouldNotSpawnWhenTopIsOccupied()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        // Place existing tiles at top row
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - Tiles should remain unchanged
        for (int x = 0; x < 3; x++)
        {
            var tile = state.GetTile(x, 0);
            Assert.Equal(ElementType.Item2, tile.Type);
        }
    }

    [Fact]
    public void Update_ShouldSpawnOnlyInEmptyColumns()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        // Occupy column 1 only
        state.SetTile(1, 0, new Tile(1, ElementType.Item2, 1, 0));

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);   // Spawned
        Assert.Equal(ElementType.Item2, state.GetTile(1, 0).Type); // Unchanged
        Assert.Equal(ElementType.Item1, state.GetTile(2, 0).Type);   // Spawned
    }

    #endregion

    #region Spawn Position Tests

    [Fact]
    public void Update_SpawnedTile_ShouldHaveCorrectInitialPosition()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item3 };
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
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item3 };
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
        var state = new GameState(1, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        // Place a falling tile at (0, 1) with position partially through the cell
        var fallingTile = new Tile(1, ElementType.Item2, 0, 1)
        {
            Position = new Vector2(0, 0.5f), // Halfway through cell 0
            Velocity = new Vector2(0, 5.0f),
            IsFalling = true
        };
        state.SetTile(0, 1, fallingTile);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert - New tile always spawns at fixed position -1.0
        // (Gravity system handles following via GravityTargetResolver)
        var newTile = state.GetTile(0, 0);
        Assert.Equal(ElementType.Item1, newTile.Type);
        Assert.Equal(-1.0f, newTile.Position.Y, 0.01f);
    }

    #endregion

    #region Tile ID Tests

    [Fact]
    public void Update_ShouldAssignUniqueIds()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);
        state.NextTileId = 100;

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item3 };
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
        var state = new GameState(1, 2, 5, new StubRandom(0));
        ClearBoard(ref state);
        state.NextTileId = 1;

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item3 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act - First spawn
        refill.Update(ref state);
        var firstId = state.GetTile(0, 0).Id;

        // Clear and spawn again
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
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

        var state = new GameState(1, 3, 5, new StubRandom(0))
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

        var state = new GameState(1, 3, 5, new StubRandom(0))
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

        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            _onPredict(context);
            return ElementType.Item3;
        }
    }

    #endregion

    #region Irregular Board (Holes at Top)

    [Fact]
    public void Update_ShouldSpawnAtFirstNonHoleRow()
    {
        // Arrange: 3x3 board shaped like:
        //     X        ← row 0: only column 1 has a cell
        //   X X X      ← row 1
        //   X X X      ← row 2
        var state = new GameState(3, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        // Mark column 0 and 2, row 0 as holes
        state.Cells[0 * 3 + 0] = CellKind.Void; // (0,0)
        state.Cells[0 * 3 + 2] = CellKind.Void; // (2,0)

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert: column 0 spawns at row 1 (first non-hole)
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(0, 1).Type);
        Assert.True(state.GetTile(0, 1).IsFalling);
        Assert.Equal(0.0f, state.GetTile(0, 1).Position.Y); // spawnY - 1 = 0

        // Assert: column 1 spawns at row 0 (normal)
        Assert.Equal(ElementType.Item1, state.GetTile(1, 0).Type);
        Assert.Equal(-1.0f, state.GetTile(1, 0).Position.Y);

        // Assert: column 2 spawns at row 1 (first non-hole)
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(2, 1).Type);
        Assert.Equal(0.0f, state.GetTile(2, 1).Position.Y);
    }

    [Fact]
    public void Update_AllHolesColumn_ShouldSkip()
    {
        // Arrange: column 0 is entirely holes
        var state = new GameState(2, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        for (int y = 0; y < 3; y++)
            state.Cells[y * 2 + 0] = CellKind.Void;

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert: column 0 has no tiles anywhere
        for (int y = 0; y < 3; y++)
            Assert.Equal(ElementType.None, state.GetTile(0, y).Type);

        // Column 1 should be refilled normally
        Assert.Equal(ElementType.Item1, state.GetTile(1, 0).Type);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Update_EmptyBoard_ShouldFillTopRow()
    {
        // Arrange
        var state = new GameState(5, 5, 5, new StubRandom(0));
        ClearBoard(ref state);

        var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4, ElementType.Item5 };
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
        var state = new GameState(1, 5, 5, new StubRandom(0));
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item1 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);
    }

    [Fact]
    public void Update_WideBoard_ShouldFillAllColumns()
    {
        // Arrange
        var state = new GameState(10, 3, 5, new StubRandom(0));
        ClearBoard(ref state);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = ElementType.Item3 };
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert
        for (int x = 0; x < 10; x++)
        {
            Assert.Equal(ElementType.Item3, state.GetTile(x, 0).Type);
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
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));
            }
        }
    }

    #endregion
}


