using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Tests.TestFixtures;
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
                // Chebyshev distance
                int dist = Math.Max(Math.Abs(x - origin.X), Math.Abs(y - origin.Y));
                bool inRange = dist <= radius;

                if (inRange)
                {
                    Assert.True(state.IsLocked(x, y, CellLockType.Drop), $"Tile at {x},{y} should be suspended");
                }
                else
                {
                    Assert.False(state.IsLocked(x, y, CellLockType.Drop), $"Tile at {x},{y} should NOT be suspended");
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
        int tick = 1;
        float simTime = 1.0f;

        // Act 1: First Update (Wave 0 - Center)
        _sut.Update(ref state, deltaTime, tick, simTime, _eventCollector, triggeredBombs);

        // Assert 1: Center destroyed
        var centerTile = state.GetTile(origin.X, origin.Y);
        Assert.Equal(ElementType.None, centerTile.Type);
        Assert.Contains(_eventCollector.Events, e => e is TileDestroyedEvent tde && tde.GridPosition.Equals(origin));

        // Act 2: Second Update (Wave 1)
        _sut.Update(ref state, deltaTime, tick + 1, simTime + 0.1f, _eventCollector, triggeredBombs);

        // Assert 2: Wave 1 destroyed (Chebyshev distance 1)
        // e.g. (4,4), (4,5), (4,6), (5,4)...
        var wave1Pos = new Position(4, 5); // Distance 1
        Assert.Equal(ElementType.None, state.GetTile(wave1Pos.X, wave1Pos.Y).Type);

        // Wave 2 should still be suspended
        var wave2Pos = new Position(3, 5); // Distance 2
        Assert.NotEqual(ElementType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);
        Assert.True(state.IsLocked(wave2Pos.X, wave2Pos.Y, CellLockType.Drop));

        // Act 3: Third Update (Wave 2)
        _sut.Update(ref state, deltaTime, tick + 2, simTime + 0.2f, _eventCollector, triggeredBombs);
        
        // Assert 3: Wave 2 destroyed
        Assert.Equal(ElementType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);
        
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
        var bombTile = new Tile(100, ElementType.HorizontalRocket, bombPos.X, bombPos.Y);
        state.SetTile(bombPos.X, bombPos.Y, bombTile);
        
        _sut.CreateExplosion(ref state, origin, 2);
        var triggeredBombs = new List<Position>();

        // Act 1: Wave 0 (Center)
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, triggeredBombs);
        
        // Assert 1: Bomb not triggered yet
        Assert.Empty(triggeredBombs);
        var tileBefore = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(ElementType.HorizontalRocket, tileBefore.Type);
        Assert.True(state.IsLocked(bombPos.X, bombPos.Y, CellLockType.Drop)); // Should be suspended

        // Act 2: Wave 1 (Hits bomb)
        _sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector, triggeredBombs);

        // Assert 2: Bomb triggered but NOT destroyed
        Assert.Single(triggeredBombs);
        Assert.Equal(bombPos, triggeredBombs[0]);
        var tileAfter = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(ElementType.HorizontalRocket, tileAfter.Type);
        Assert.NotEqual(ElementType.None, tileAfter.Type);
        // Suspended flag is cleared when bomb is triggered (BombActivationSystem will handle it)
        Assert.False(state.IsLocked(bombPos.X, bombPos.Y, CellLockType.Drop));
    }

    [Fact]
    public void Update_ClearsSuspendedStatus_WhenDestroying()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        _sut.CreateExplosion(ref state, origin, 1);
        
        // Verify suspended initially
        Assert.True(state.IsLocked(5, 5, CellLockType.Drop));

        // Act
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, new List<Position>());

        // Assert
        var tile = state.GetTile(5, 5);
        Assert.Equal(ElementType.None, tile.Type);
        Assert.False(state.IsLocked(5, 5, CellLockType.Drop)); // Default cell has no Drop lock
    }

    [Fact]
    public void CreateTargetedExplosion_LocksNoneTilesInTargetSet()
    {
        // Arrange: position (2,2) is None (simulates ClearBombAttribute clearing a bomb)
        var state = CreateGameState(5, 5);
        var clearedPos = new Position(2, 2);
        state.SetTile(2, 2, new Tile(0, ElementType.None, 2, 2));

        var lockScheduler = new LockScheduler();
        var sut = new ExplosionSystem(new StubCoverSystem(), new StubGroundSystem(), null, lockScheduler);

        var targets = new HashSet<Position>
        {
            clearedPos,
            new Position(1, 2), // normal tile
            new Position(3, 2), // normal tile
        };

        // Act
        sut.CreateTargetedExplosion(ref state, new Position(0, 2), targets);

        // Assert: even the None position should have a Drop lock
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "None tile in target set should still get Drop lock");
        Assert.True(state.IsLocked(1, 2, CellLockType.Drop));
        Assert.True(state.IsLocked(3, 2, CellLockType.Drop));
    }

    [Fact]
    public void CreateTargetedExplosion_NonePosition_DropLockReleasedOnWave()
    {
        // Arrange: (1,0) is None, origin is (0,0)
        var state = CreateGameState(3, 3);
        state.SetTile(1, 0, new Tile(0, ElementType.None, 1, 0));

        var lockScheduler = new LockScheduler();
        var sut = new ExplosionSystem(new StubCoverSystem(), new StubGroundSystem(), null, lockScheduler);

        var targets = new HashSet<Position> { new Position(0, 0), new Position(1, 0), new Position(2, 0) };
        sut.CreateTargetedExplosion(ref state, new Position(0, 0), targets);

        // Wave 0: processes (0,0) — normal tile destroyed
        sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, new List<Position>());

        // Wave 1: processes (1,0) — None tile, Drop lock released
        sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector, new List<Position>());

        Assert.False(state.IsLocked(1, 0, CellLockType.Drop),
            "Drop lock on None position should be released after wave processes it");
    }

    private GameState CreateGameState(int width, int height)
    {
        var state = new GameState(width, height, 5, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x + 1, ElementType.Item1, x, y));
            }
        }
        return state;
    }

    private class StubCoverSystem : ICoverSystem
    {
        public bool IsTileProtected(in GameState state, Position pos) => false;
        public bool TryDamageCover(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) => false;
        public void SyncDynamicCovers(ref GameState state, Position from, Position to) { }
    }

    private class StubGroundSystem : IGroundSystem
    {
        public void OnTileDestroyed(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) { }
    }

}

