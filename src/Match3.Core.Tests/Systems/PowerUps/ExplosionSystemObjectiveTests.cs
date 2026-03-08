using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

/// <summary>
/// Tests for ExplosionSystem integration with LevelObjectiveSystem.
/// Ensures that tiles destroyed by bomb explosions are tracked in objectives.
/// </summary>
public class ExplosionSystemObjectiveTests
{
    private GameState CreateGameState(int width, int height)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        // Fill with colored tiles
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var type = (ElementType)((x + y) % 6 + 1); // Red, Green, Blue, Yellow, Purple, Orange
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }
        }
        return state;
    }

    private GameState CreateGameStateWithUniformTiles(int width, int height, ElementType ElementType)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(id++, ElementType, x, y));
            }
        }
        return state;
    }

    #region Explosion Tile Destruction Tracking

    [Fact]
    public void Explosion_DestroysTiles_UpdatesObjectiveProgress()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        var state = CreateGameStateWithUniformTiles(10, 10, ElementType.Item1);
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 25
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(5, 5);
        int radius = 2; // 5x5 area = 25 tiles

        // Act
        explosionSystem.CreateExplosion(ref state, origin, radius);

        // Process all waves
        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert
        Assert.Equal(25, state.ObjectiveProgress[0].CurrentCount);
        Assert.True(state.ObjectiveProgress[0].IsCompleted);
    }

    [Fact]
    public void Explosion_DestroysMixedTiles_OnlyTracksMatchingType()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        // Create state with alternating Red and Blue tiles
        var state = new GameState(10, 10, 6, new StubRandom());
        int id = 1;
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }
        }

        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 50
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(5, 5);
        int radius = 2; // 5x5 area = 25 tiles

        // Act
        explosionSystem.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert - Should only count Red tiles (approximately half of 25)
        // In a 5x5 area centered at (5,5), the Red tiles are at even x+y positions
        Assert.True(state.ObjectiveProgress[0].CurrentCount > 0);
        Assert.True(state.ObjectiveProgress[0].CurrentCount < 25); // Not all tiles are Red
    }

    [Fact]
    public void Explosion_WithNoObjectiveSystem_DoesNotCrash()
    {
        // Arrange - ExplosionSystem without objective system
        var explosionSystem = new ExplosionSystem();
        var state = CreateGameStateWithUniformTiles(10, 10, ElementType.Item1);

        var events = new BufferedEventCollector();
        var origin = new Position(5, 5);
        int radius = 1;

        // Act & Assert - Should not throw
        explosionSystem.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Tiles should still be destroyed
        Assert.Equal(ElementType.None, state.GetTile(5, 5).Type);
    }

    [Fact]
    public void Explosion_EmitsProgressEvents()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        var state = CreateGameStateWithUniformTiles(5, 5, ElementType.Item2);
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item2,
            TargetCount = 100
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(2, 2);
        int radius = 1; // 3x3 area = 9 tiles

        // Act
        explosionSystem.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert - Should have ObjectiveProgressEvents
        var progressEvents = events.GetEvents().Where(e => e is ObjectiveProgressEvent).ToList();
        Assert.Equal(9, progressEvents.Count); // One for each destroyed tile
    }

    [Fact]
    public void Explosion_CompletesObjective_WhenTargetReached()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        var state = CreateGameStateWithUniformTiles(5, 5, ElementType.Item4);
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item4,
            TargetCount = 5 // Only need 5 tiles
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(2, 2);
        int radius = 1; // 3x3 area = 9 tiles

        // Act
        explosionSystem.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert
        Assert.True(state.ObjectiveProgress[0].IsCompleted);
        Assert.True(state.ObjectiveProgress[0].CurrentCount >= 5);
    }

    #endregion

    #region Targeted Explosion Tests

    [Fact]
    public void TargetedExplosion_DestroysTiles_UpdatesObjectiveProgress()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        var state = CreateGameStateWithUniformTiles(10, 10, ElementType.Item5);
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item5,
            TargetCount = 10
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(5, 5);
        var targets = new List<Position>
        {
            new Position(1, 1),
            new Position(2, 2),
            new Position(3, 3),
            new Position(7, 7),
            new Position(8, 8)
        };

        // Act
        explosionSystem.CreateTargetedExplosion(ref state, origin, targets);

        var triggeredBombs = new List<Position>();
        // Process enough waves to cover max distance
        for (int wave = 0; wave <= 5; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert
        Assert.Equal(5, state.ObjectiveProgress[0].CurrentCount);
    }

    #endregion

    #region Multiple Objectives Tests

    [Fact]
    public void Explosion_WithMultipleObjectives_UpdatesCorrectObjective()
    {
        // Arrange
        var objectiveSystem = new LevelObjectiveSystem();
        var explosionSystem = new ExplosionSystem(
            new CoverSystem(objectiveSystem),
            new GroundSystem(objectiveSystem),
            objectiveSystem);

        var state = CreateGameStateWithUniformTiles(10, 10, ElementType.Item6);
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1, // Not matching
            TargetCount = 10
        };
        config.Objectives[1] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item6, // Matching
            TargetCount = 10
        };
        objectiveSystem.Initialize(ref state, config);

        var events = new BufferedEventCollector();
        var origin = new Position(5, 5);
        int radius = 1; // 3x3 = 9 tiles

        // Act
        explosionSystem.CreateExplosion(ref state, origin, radius);

        var triggeredBombs = new List<Position>();
        for (int wave = 0; wave <= radius; wave++)
        {
            explosionSystem.Update(ref state, 0.1f, wave, wave * 0.1f, events, triggeredBombs);
        }

        // Assert
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount); // Red - not matched
        Assert.Equal(9, state.ObjectiveProgress[1].CurrentCount); // Orange - matched
    }

    #endregion
}

