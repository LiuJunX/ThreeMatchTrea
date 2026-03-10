using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.View;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Core;

public class AsyncGameLoopTests
{
    private class StubPhysics : IPhysicsSimulation
    {
        public void Update(ref GameState state, float deltaTime) { }
        public bool IsStable(in GameState state) => true;
        public IPhysicsSimulation CloneForSimulation(IRandom newRandom) => this;
    }

    private class StubSpawnModel : ISpawnModel
    {
        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context) => ElementType.Item3;
    }

    private class MockMatchFinder : IMatchFinder
    {
        public List<MatchGroup> GroupsToReturn = new();
        public List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null)
        {
            // Return a copy since caller may release the list (mimics pool behavior)
            return new List<MatchGroup>(GroupsToReturn);
        }
        public bool HasMatchAt(in GameState state, Position p) => false;
        public bool HasMatches(in GameState state) => false;
    }

    private class SpyMatchProcessor : IMatchProcessor
    {
        public bool ProcessMatchesCalled { get; private set; }
        public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
        {
            ProcessMatchesCalled = true;
            return 0;
        }

        public int ProcessMatches(ref GameState state, List<MatchGroup> groups, int tick, float simTime, IEventCollector events)
        {
            ProcessMatchesCalled = true;
            return 0;
        }
    }
    
    private class StubPowerUp : IPowerUpHandler
    {
        public bool ActivateBombCalled { get; private set; }
        public Position LastActivatedPosition { get; private set; }

        public void ActivateBomb(ref GameState state, Position p) { }
        public void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events) { }
        public void HandlePowerUp(ref GameState state, Position p, ElementType bomb) { }
        public bool TryActivate(ref GameState state, Position p) => false;
        public void ProcessSpecialMove(ref GameState state, Position a, Position b, out int score) { score = 0; }
        public void ProcessSpecialMove(ref GameState state, Position a, Position b, int tick, float simTime, IEventCollector events, out int score) { score = 0; }
        
        public void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events, bool isChainReaction)
        {
            ActivateBombCalled = true;
            LastActivatedPosition = p;
        }
        
        public IPowerUpHandler WithExplosionSystem(IExplosionSystem? explosionSystem) => this;
        public IPowerUpHandler WithProjectileSystem(IProjectileSystem? projectileSystem) => this;
    }

    #region Basic Update Tests

    [Fact]
    public void Update_ShouldProcessMatches_WhenFinderReturnsGroups()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // Setup match
        var group = new MatchGroup { Type = ElementType.Item1 };
        group.Positions.Add(new Position(0, 0));
        finder.GroupsToReturn.Add(group);

        // Pre-fill board to ensure stability
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        // Act
        loop.Update(ref state, 0.1f);

        // Assert
        Assert.False(state.GetTile(0,0).IsFalling, "Tile should not be falling");
        Assert.True(processor.ProcessMatchesCalled, "MatchProcessor should be called");
    }

    [Fact]
    public void Update_ShouldNotProcessMatches_WhenNoMatchesFound()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder(); // Empty GroupsToReturn
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);
        FillBoard(ref state);

        // Act
        loop.Update(ref state, 0.1f);

        // Assert
        Assert.False(processor.ProcessMatchesCalled);
    }

    #endregion

    #region Stability Tests

    [Fact]
    public void Update_ShouldNotProcessMatches_WhenTilesAreFalling()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // Setup falling tile at match position
        var fallingTile = new Tile(1, ElementType.Item1, 0, 0) { IsFalling = true };
        state.SetTile(0, 0, fallingTile);

        // Setup match group containing falling tile
        var group = new MatchGroup { Type = ElementType.Item1 };
        group.Positions.Add(new Position(0, 0));
        finder.GroupsToReturn.Add(group);

        // Act
        loop.Update(ref state, 0.1f);

        // Assert - Match should not be processed because tile is falling
        Assert.False(processor.ProcessMatchesCalled,
            "MatchProcessor should not be called for falling tiles");
    }

    [Fact]
    public void Update_ShouldProcessOnlyStableMatches_WhenMixedStability()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new CountingMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // Setup stable tiles
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0) { IsFalling = false });
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0) { IsFalling = false });
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0) { IsFalling = false });

        // Setup falling tiles
        state.SetTile(0, 1, new Tile(4, ElementType.Item3, 0, 1) { IsFalling = true });
        state.SetTile(1, 1, new Tile(5, ElementType.Item3, 1, 1) { IsFalling = true });
        state.SetTile(2, 1, new Tile(6, ElementType.Item3, 2, 1) { IsFalling = true });

        // Stable group
        var stableGroup = new MatchGroup { Type = ElementType.Item1 };
        stableGroup.Positions.Add(new Position(0, 0));
        stableGroup.Positions.Add(new Position(1, 0));
        stableGroup.Positions.Add(new Position(2, 0));

        // Falling group
        var fallingGroup = new MatchGroup { Type = ElementType.Item3 };
        fallingGroup.Positions.Add(new Position(0, 1));
        fallingGroup.Positions.Add(new Position(1, 1));
        fallingGroup.Positions.Add(new Position(2, 1));

        finder.GroupsToReturn.Add(stableGroup);
        finder.GroupsToReturn.Add(fallingGroup);

        // Act
        loop.Update(ref state, 0.1f);

        // Assert - Only stable group should be processed
        Assert.True(processor.ProcessMatchesCalled);
        Assert.Equal(1, processor.GroupsProcessed);
    }

    private class CountingMatchProcessor : IMatchProcessor
    {
        public bool ProcessMatchesCalled { get; private set; }
        public int GroupsProcessed { get; private set; }

        public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
        {
            ProcessMatchesCalled = true;
            GroupsProcessed = groups.Count;
            return groups.Count;
        }

        public int ProcessMatches(ref GameState state, List<MatchGroup> groups, int tick, float simTime, IEventCollector events)
        {
            ProcessMatchesCalled = true;
            GroupsProcessed = groups.Count;
            return groups.Count;
        }
    }

    #endregion

    #region System Execution Order Tests

    [Fact]
    public void Update_ShouldCallPhysicsSystem()
    {
        // Arrange
        var physics = new SpyPhysicsSimulation();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);
        var state = new GameState(8, 8, 5, new StubRandom());
        FillBoard(ref state);

        // Act
        loop.Update(ref state, 0.1f);

        // Assert
        Assert.True(physics.UpdateCalled, "Physics Update should be called");
        Assert.Equal(0.1f, physics.LastDeltaTime, 0.001f);
    }

    private class SpyPhysicsSimulation : IPhysicsSimulation
    {
        public bool UpdateCalled { get; private set; }
        public float LastDeltaTime { get; private set; }

        public void Update(ref GameState state, float deltaTime)
        {
            UpdateCalled = true;
            LastDeltaTime = deltaTime;
        }

        public bool IsStable(in GameState state) => true;
        public IPhysicsSimulation CloneForSimulation(IRandom newRandom) => this;
    }

    #endregion

    #region ActivateBomb Tests

    [Fact]
    public void ActivateBomb_ShouldDelegateToPowerUpHandler()
    {
        // Arrange
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new SpyPowerUpHandler();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);
        var state = new GameState(8, 8, 5, new StubRandom());
        FillBoard(ref state);

        var position = new Position(3, 3);

        // Act
        loop.ActivateBomb(ref state, position);

        // Assert
        Assert.True(powerUp.ActivateBombCalled);
        Assert.Equal(position, powerUp.LastActivatedPosition);
    }

    private class SpyPowerUpHandler : IPowerUpHandler
    {
        public bool ActivateBombCalled { get; private set; }
        public Position LastActivatedPosition { get; private set; }

        public void ActivateBomb(ref GameState state, Position p)
        {
            ActivateBombCalled = true;
            LastActivatedPosition = p;
        }

        public void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events, bool isChainReaction)
        {
            ActivateBombCalled = true;
            LastActivatedPosition = p;
        }

        public void HandlePowerUp(ref GameState state, Position p, ElementType bomb) { }
        public bool TryActivate(ref GameState state, Position p) => false;
        public void ProcessSpecialMove(ref GameState state, Position a, Position b, out int score) { score = 0; }
        public void ProcessSpecialMove(ref GameState state, Position a, Position b, int tick, float simTime, IEventCollector events, out int score) { score = 0; }

        public IPowerUpHandler WithExplosionSystem(IExplosionSystem? explosionSystem) => this;
        public IPowerUpHandler WithProjectileSystem(IProjectileSystem? projectileSystem) => this;
    }

    #endregion

    #region Multi-Frame Update Tests

    [Fact]
    public void Update_MultipleFrames_ShouldProcessMatchesEachFrame()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new FrameCountingProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // Setup stable match
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        var group = new MatchGroup { Type = ElementType.Item1 };
        group.Positions.Add(new Position(0, 0));
        finder.GroupsToReturn.Add(group);

        // Act - Run multiple frames
        for (int i = 0; i < 5; i++)
        {
            loop.Update(ref state, 0.016f);
        }

        // Assert
        Assert.Equal(5, processor.FrameCount);
    }

    private class FrameCountingProcessor : IMatchProcessor
    {
        public int FrameCount { get; private set; }

        public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
        {
            FrameCount++;
            return groups.Count;
        }

        public int ProcessMatches(ref GameState state, List<MatchGroup> groups, int tick, float simTime, IEventCollector events)
        {
            FrameCount++;
            return groups.Count;
        }
    }

    [Fact]
    public void Update_WithDeltaTime_ShouldPassToPhysics()
    {
        // Arrange
        var physics = new DeltaTimeCapturingPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);
        var state = new GameState(8, 8, 5, new StubRandom());
        FillBoard(ref state);

        // Act
        loop.Update(ref state, 0.033f);

        // Assert
        Assert.Equal(0.033f, physics.LastDeltaTime, 0.0001f);
    }

    private class DeltaTimeCapturingPhysics : IPhysicsSimulation
    {
        public float LastDeltaTime { get; private set; }

        public void Update(ref GameState state, float deltaTime)
        {
            LastDeltaTime = deltaTime;
        }

        public bool IsStable(in GameState state) => true;
        public IPhysicsSimulation CloneForSimulation(IRandom newRandom) => this;
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Update_EmptyBoard_ShouldNotThrow()
    {
        // Arrange
        var state = new GameState(8, 8, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // Clear board
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => loop.Update(ref state, 0.1f));
        Assert.Null(exception);
    }

    [Fact]
    public void Update_AllTilesFalling_ShouldNotProcessAnyMatches()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        var physics = new StubPhysics();
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var finder = new MockMatchFinder();
        var processor = new SpyMatchProcessor();
        var powerUp = new StubPowerUp();

        var loop = new AsyncGameLoopSystem(physics, refill, finder, processor, powerUp);

        // All tiles falling
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var tile = new Tile(y * 3 + x + 1, ElementType.Item1, x, y) { IsFalling = true };
                state.SetTile(x, y, tile);
            }
        }

        // Setup match groups for all positions
        var group = new MatchGroup { Type = ElementType.Item1 };
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                group.Positions.Add(new Position(x, y));
        finder.GroupsToReturn.Add(group);

        // Act
        loop.Update(ref state, 0.1f);

        // Assert
        Assert.False(processor.ProcessMatchesCalled,
            "Should not process matches when all tiles are falling");
    }

    #endregion

    #region Helper Methods

    private static void FillBoard(ref GameState state)
    {
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4 };
        int id = 1;
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = types[(x + y * 2) % types.Length];
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }
        }
    }

    #endregion
}

