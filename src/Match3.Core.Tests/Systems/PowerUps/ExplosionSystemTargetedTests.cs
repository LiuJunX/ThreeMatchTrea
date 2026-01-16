using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

public class ExplosionSystemTargetedTests : IDisposable
{
    private readonly ExplosionSystem _sut;
    private readonly StubEventCollector _eventCollector;

    public ExplosionSystemTargetedTests()
    {
        _sut = new ExplosionSystem();
        _eventCollector = new StubEventCollector();
    }

    public void Dispose()
    {
        _sut.Reset();
    }

    [Fact]
    public void CreateTargetedExplosion_SuspendsOnlyTargetTiles()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        var targets = new List<Position>
        {
            new Position(5, 6), // Distance 1
            new Position(5, 7), // Distance 2
            new Position(9, 9)  // Distance 4
        };

        // Act
        _sut.CreateTargetedExplosion(ref state, origin, targets);

        // Assert
        Assert.True(_sut.HasActiveExplosions);

        // Verify targets are suspended
        foreach (var pos in targets)
        {
            Assert.True(state.GetTile(pos.X, pos.Y).IsSuspended, $"Target at {pos} should be suspended");
        }

        // Verify others are NOT suspended
        Assert.False(state.GetTile(5, 5).IsSuspended, "Origin should not be suspended unless in targets");
        Assert.False(state.GetTile(0, 0).IsSuspended, "Random tile should not be suspended");
    }

    [Fact]
    public void Update_ClearsTargetedTilesInWaves()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        var targets = new List<Position>
        {
            new Position(5, 6), // Distance 1
            new Position(5, 7), // Distance 2
            new Position(5, 8)  // Distance 3
        };

        _sut.CreateTargetedExplosion(ref state, origin, targets);
        var triggeredBombs = new List<Position>();
        float deltaTime = 0.1f; 

        // Act 1: Wave 0 (Distance 0)
        _sut.Update(ref state, deltaTime, 1, 1f, _eventCollector, triggeredBombs);
        
        // Assert 1: Nothing cleared yet (targets start at Distance 1)
        Assert.NotEqual(TileType.None, state.GetTile(5, 6).Type);
        Assert.True(state.GetTile(5, 6).IsSuspended);

        // Act 2: Wave 1 (Distance 1)
        _sut.Update(ref state, deltaTime, 2, 1.1f, _eventCollector, triggeredBombs);

        // Assert 2: Target at Distance 1 cleared
        Assert.Equal(TileType.None, state.GetTile(5, 6).Type);
        Assert.NotEqual(TileType.None, state.GetTile(5, 7).Type);

        // Act 3: Wave 2 (Distance 2)
        _sut.Update(ref state, deltaTime, 3, 1.2f, _eventCollector, triggeredBombs);

        // Assert 3: Target at Distance 2 cleared
        Assert.Equal(TileType.None, state.GetTile(5, 7).Type);
        Assert.NotEqual(TileType.None, state.GetTile(5, 8).Type);

        // Act 4: Wave 3 (Distance 3)
        _sut.Update(ref state, deltaTime, 4, 1.3f, _eventCollector, triggeredBombs);

        // Assert 4: Target at Distance 3 cleared
        Assert.Equal(TileType.None, state.GetTile(5, 8).Type);
        Assert.False(_sut.HasActiveExplosions);
    }

    private GameState CreateGameState(int width, int height)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x + 1, TileType.Red, x, y));
            }
        }
        return state;
    }

    private class StubRandom : IRandom
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
        public void Emit(GameEvent evt) => EmittedEvents.Add(evt);
        public void EmitBatch(IEnumerable<GameEvent> events) => EmittedEvents.AddRange(events);
    }
}
