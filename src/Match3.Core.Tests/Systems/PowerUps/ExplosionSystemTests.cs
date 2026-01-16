using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

public class ExplosionSystemTests : IDisposable
{
    private readonly ExplosionSystem _sut;
    private readonly StubEventCollector _eventCollector;

    public ExplosionSystemTests()
    {
        _sut = new ExplosionSystem();
        _eventCollector = new StubEventCollector();
    }

    public void Dispose()
    {
        _sut.Reset();
    }

    [Fact]
    public void CreateExplosion_SuspendsTilesInRange()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        int radius = 2;

        // Act
        _sut.CreateExplosion(ref state, origin, radius);

        // Assert
        Assert.True(_sut.HasActiveExplosions);

        // Check tiles in range [3..7, 3..7]
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                var tile = state.GetTile(x, y);
                // Chebyshev distance
                int dist = Math.Max(Math.Abs(x - origin.X), Math.Abs(y - origin.Y));
                bool inRange = dist <= radius;
                
                if (inRange)
                {
                    Assert.True(tile.IsSuspended, $"Tile at {x},{y} should be suspended");
                }
                else
                {
                    Assert.False(tile.IsSuspended, $"Tile at {x},{y} should NOT be suspended");
                }
            }
        }
    }

    [Fact]
    public void Update_AdvancesWavesAndDestroysTiles()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        int radius = 2;
        _sut.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        float deltaTime = 0.1f; // Exactly one wave interval
        long tick = 1;
        float simTime = 1.0f;

        // Act 1: First Update (Wave 0 - Center)
        _sut.Update(ref state, deltaTime, tick, simTime, _eventCollector, triggeredBombs);

        // Assert 1: Center destroyed
        var centerTile = state.GetTile(origin.X, origin.Y);
        Assert.Equal(TileType.None, centerTile.Type);
        Assert.Contains(_eventCollector.EmittedEvents, e => e is TileDestroyedEvent tde && tde.GridPosition.Equals(origin));

        // Act 2: Second Update (Wave 1)
        _sut.Update(ref state, deltaTime, tick + 1, simTime + 0.1f, _eventCollector, triggeredBombs);

        // Assert 2: Wave 1 destroyed (Chebyshev distance 1)
        // e.g. (4,4), (4,5), (4,6), (5,4)...
        var wave1Pos = new Position(4, 5); // Distance 1
        Assert.Equal(TileType.None, state.GetTile(wave1Pos.X, wave1Pos.Y).Type);

        // Wave 2 should still be suspended
        var wave2Pos = new Position(3, 5); // Distance 2
        Assert.NotEqual(TileType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);
        Assert.True(state.GetTile(wave2Pos.X, wave2Pos.Y).IsSuspended);

        // Act 3: Third Update (Wave 2)
        _sut.Update(ref state, deltaTime, tick + 2, simTime + 0.2f, _eventCollector, triggeredBombs);
        
        // Assert 3: Wave 2 destroyed
        Assert.Equal(TileType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);
        
        // Explosion should be finished (radius 2 has 3 waves: 0, 1, 2)
        Assert.False(_sut.HasActiveExplosions);
    }

    [Fact]
    public void Update_IdentifiesTriggeredBombs_DoesNotDestroyImmediately()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        var bombPos = new Position(6, 5); // Distance 1
        
        // Place a bomb
        var bombTile = new Tile(100, TileType.Red, bombPos.X, bombPos.Y) { Bomb = BombType.Horizontal };
        state.SetTile(bombPos.X, bombPos.Y, bombTile);
        
        _sut.CreateExplosion(ref state, origin, 2);
        var triggeredBombs = new List<Position>();

        // Act 1: Wave 0 (Center)
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, triggeredBombs);
        
        // Assert 1: Bomb not triggered yet
        Assert.Empty(triggeredBombs);
        var tileBefore = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(BombType.Horizontal, tileBefore.Bomb);
        Assert.True(tileBefore.IsSuspended); // Should be suspended

        // Act 2: Wave 1 (Hits bomb)
        _sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector, triggeredBombs);

        // Assert 2: Bomb triggered but NOT destroyed
        Assert.Single(triggeredBombs);
        Assert.Equal(bombPos, triggeredBombs[0]);
        var tileAfter = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(BombType.Horizontal, tileAfter.Bomb);
        Assert.NotEqual(TileType.None, tileAfter.Type);
        // Suspended flag is cleared when bomb is triggered (BombActivationSystem will handle it)
        Assert.False(tileAfter.IsSuspended);
    }

    [Fact]
    public void Update_ClearsSuspendedStatus_WhenDestroying()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        _sut.CreateExplosion(ref state, origin, 1);
        
        // Verify suspended initially
        Assert.True(state.GetTile(5, 5).IsSuspended);

        // Act
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, new List<Position>());

        // Assert
        var tile = state.GetTile(5, 5);
        Assert.Equal(TileType.None, tile.Type);
        Assert.False(tile.IsSuspended); // Default Tile has IsSuspended = false
    }

    private GameState CreateGameState(int width, int height)
    {
        var state = new GameState(width, height, 5, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x + 1, TileType.Red, x, y));
            }
        }
        return state;
    }

    private class StubRandom : Match3.Random.IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class StubEventCollector : IEventCollector
    {
        public List<GameEvent> EmittedEvents { get; } = new();

        public bool IsEnabled => true;

        public void Emit(GameEvent evt)
        {
            EmittedEvents.Add(evt);
        }

        public void EmitBatch(IEnumerable<GameEvent> events)
        {
            EmittedEvents.AddRange(events);
        }
    }
}
