using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
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
    public void CreateExplosion_RegistersActiveExplosion()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        int radius = 2;

        // Act
        _sut.CreateExplosion(ref state, origin, radius);

        // Assert: explosion is registered but no pre-locking occurs
        Assert.True(_sut.HasActiveExplosions);

        // No Drop locks should be placed (pre-locking removed)
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Assert.False(state.IsLocked(x, y, CellLockType.Drop), $"Tile at {x},{y} should NOT have Drop lock");
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

        float deltaTime = 0.1f; // Exactly one wave interval
        int tick = 1;
        float simTime = 1.0f;

        // Act 1: First Update (Wave 0 - Center)
        _sut.Update(ref state, deltaTime, tick, simTime, _eventCollector);

        // Assert 1: Center destroyed
        var centerTile = state.GetTile(origin.X, origin.Y);
        Assert.Equal(ElementType.None, centerTile.Type);
        Assert.Contains(_eventCollector.Events, e => e is TileDestroyedEvent tde && tde.GridPosition.Equals(origin));

        // Act 2: Second Update (Wave 1)
        _sut.Update(ref state, deltaTime, tick + 1, simTime + 0.1f, _eventCollector);

        // Assert 2: Wave 1 destroyed (Chebyshev distance 1)
        var wave1Pos = new Position(4, 5); // Distance 1
        Assert.Equal(ElementType.None, state.GetTile(wave1Pos.X, wave1Pos.Y).Type);

        // Wave 2 should still exist (not yet processed)
        var wave2Pos = new Position(3, 5); // Distance 2
        Assert.NotEqual(ElementType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);

        // Act 3: Third Update (Wave 2)
        _sut.Update(ref state, deltaTime, tick + 2, simTime + 0.2f, _eventCollector);

        // Assert 3: Wave 2 destroyed
        Assert.Equal(ElementType.None, state.GetTile(wave2Pos.X, wave2Pos.Y).Type);

        // Explosion should be finished (radius 2 has 3 waves: 0, 1, 2)
        Assert.False(_sut.HasActiveExplosions);
    }

    [Fact]
    public void Update_HandlesTriggeredBombs_Internally()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        var bombPos = new Position(6, 5); // Distance 1

        // Place a bomb
        var bombTile = new Tile(100, ElementType.HorizontalRocket, bombPos.X, bombPos.Y);
        state.SetTile(bombPos.X, bombPos.Y, bombTile);

        _sut.CreateExplosion(ref state, origin, 2);

        // Act 1: Wave 0 (Center)
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector);

        // Assert 1: Bomb not triggered yet (wave hasn't reached it)
        var tileBefore = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(ElementType.HorizontalRocket, tileBefore.Type);

        // Act 2: Wave 1 (Hits bomb) -- ExplosionSystem handles chain reactions internally
        _sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector);

        // Assert 2: Bomb was eliminated by the wave
        var tileAfter = state.GetTile(bombPos.X, bombPos.Y);
        Assert.Equal(ElementType.None, tileAfter.Type);

        // A new explosion should have been created for the chain reaction
        Assert.True(_sut.HasActiveExplosions);
    }

    [Fact]
    public void Update_WithTriggeredBombsList_CollectsBombsInsteadOfChaining()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        var bombPos = new Position(6, 5); // Distance 1

        var bombTile = new Tile(100, ElementType.HorizontalRocket, bombPos.X, bombPos.Y);
        state.SetTile(bombPos.X, bombPos.Y, bombTile);

        _sut.CreateExplosion(ref state, origin, 2);

        // Wave 0: center
        var triggeredBombs = new List<(Position Pos, Tile Tile)>();
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector, triggeredBombs);

        Assert.Empty(triggeredBombs); // bomb not reached yet

        // Wave 1: hits bomb at (6,5)
        triggeredBombs.Clear();
        _sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector, triggeredBombs);

        // Assert: bomb collected in list, NOT internally chain-reacted
        Assert.Single(triggeredBombs);
        Assert.Equal(bombPos, triggeredBombs[0].Pos);
        Assert.Equal(ElementType.HorizontalRocket, triggeredBombs[0].Tile.Type);
        Assert.Equal(100, triggeredBombs[0].Tile.Id);

        // Tile should still be eliminated from the grid
        Assert.Equal(ElementType.None, state.GetTile(bombPos.X, bombPos.Y).Type);

        // No internal chain explosion should have been created (only the original explosion remains)
        // After wave 2, the original explosion finishes
        _sut.Update(ref state, 0.1f, 3, 1.2f, _eventCollector);
        Assert.False(_sut.HasActiveExplosions);
    }

    [Fact]
    public void Update_DestroysTile_WhenWaveReachesIt()
    {
        // Arrange
        var state = CreateGameState(10, 10);
        var origin = new Position(5, 5);
        _sut.CreateExplosion(ref state, origin, 1);

        // No pre-locking -- tile should not have Drop lock
        Assert.False(state.IsLocked(5, 5, CellLockType.Drop));

        // Act
        _sut.Update(ref state, 0.1f, 1, 1f, _eventCollector);

        // Assert: tile destroyed
        var tile = state.GetTile(5, 5);
        Assert.Equal(ElementType.None, tile.Type);
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

        // Wave 0: processes (0,0) -- normal tile destroyed
        sut.Update(ref state, 0.1f, 1, 1f, _eventCollector);

        // Wave 1: processes (1,0) -- None tile
        sut.Update(ref state, 0.1f, 2, 1.1f, _eventCollector);

        Assert.False(state.IsLocked(1, 0, CellLockType.Drop),
            "No Drop lock should exist on None position");
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
        public void NotifyBatchElimination(ref GameState state, ReadOnlySpan<EliminatedTileInfo> eliminated, int tick, float simTime, IEventCollector events) { }
    }

    private class StubGroundSystem : IGroundSystem
    {
        public void OnTileDestroyed(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) { }
    }

}
