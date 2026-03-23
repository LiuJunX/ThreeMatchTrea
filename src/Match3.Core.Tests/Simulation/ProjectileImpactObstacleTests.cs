using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Tests for ProcessProjectileImpacts interacting with obstacles.
/// Regression: UFO hitting Box/Cupboard was silently skipped because obstacle
/// cells have no tile and the old code did `if (tile == None) continue`.
/// </summary>
public class ProjectileImpactObstacleTests
{
    private readonly SimulationMatchHandler _handler;
    private readonly BufferedEventCollector _collector;

    public ProjectileImpactObstacleTests()
    {
        var matchFinder = new FakeMatchFinder();
        var matchProcessor = new FakeMatchProcessor();
        var objectiveSystem = new LevelObjectiveSystem();
        var obstacleSystem = new ObstacleSystem(objectiveSystem);
        var groundSystem = new GroundSystem();
        var coverSystem = new CoverSystem();
        var cellEliminator = new CellEliminator(coverSystem, groundSystem, objectiveSystem, obstacleSystem);

        _handler = new SimulationMatchHandler(
            matchFinder, matchProcessor, cellEliminator, objectiveSystem);
        _collector = new BufferedEventCollector();
    }

    #region Box obstacle

    [Fact]
    public void ProjectileImpact_OnBox_DamagesObstacle()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, stage: 3));

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // Box should be damaged (stage 3 → 2)
        var obstacle = state.GetObstacle(2, 2);
        Assert.Equal(ObstacleType.Box, obstacle.Type);
        Assert.Equal(2, obstacle.Stage);

        // Should emit ObstacleDamagedEvent
        var damageEvents = _collector.GetEvents().OfType<ObstacleDamagedEvent>().ToList();
        Assert.Single(damageEvents);
        Assert.Equal(new Position(2, 2), damageEvents[0].GridPosition);
        Assert.Equal(ObstacleType.Box, damageEvents[0].Type);
        Assert.Equal(2, damageEvents[0].RemainingStage);
    }

    [Fact]
    public void ProjectileImpact_OnBox_Stage1_DestroysObstacle()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, stage: 1));

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // Box should be destroyed
        Assert.False(state.HasObstacle(2, 2));

        // Should emit ObstacleDestroyedEvent
        var destroyEvents = _collector.GetEvents().OfType<ObstacleDestroyedEvent>().ToList();
        Assert.Single(destroyEvents);
        Assert.Equal(ObstacleType.Box, destroyEvents[0].Type);
    }

    [Fact]
    public void ProjectileImpact_OnBox_DoesNotTriggerBomb()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, stage: 2));

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        Assert.Empty(triggeredBombs);
    }

    #endregion

    #region Cupboard obstacle

    [Fact]
    public void ProjectileImpact_OnCupboard_DamagesObstacle()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetObstacle(3, 1, new Obstacle(ObstacleType.Cupboard, stage: 2));

        var affected = new HashSet<Position> { new Position(3, 1) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        var obstacle = state.GetObstacle(3, 1);
        Assert.Equal(ObstacleType.Cupboard, obstacle.Type);
        Assert.Equal(1, obstacle.Stage);

        var damageEvents = _collector.GetEvents().OfType<ObstacleDamagedEvent>().ToList();
        Assert.Single(damageEvents);
        Assert.Equal(ObstacleType.Cupboard, damageEvents[0].Type);
    }

    #endregion

    #region Mixed positions (tile + obstacle)

    [Fact]
    public void ProjectileImpact_MixedPositions_DamagesBothTileAndObstacle()
    {
        var state = new GameState(5, 5, 5, null!);
        // Tile at (1,1)
        state.SetTile(1, 1, new Tile(10, ElementType.Item1, 1, 1));
        // Obstacle at (3,3) — no tile here
        state.SetObstacle(3, 3, new Obstacle(ObstacleType.Box, stage: 2));

        var affected = new HashSet<Position> { new Position(1, 1), new Position(3, 3) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // Tile should be destroyed
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);

        // Obstacle should be damaged
        Assert.Equal(1, state.GetObstacle(3, 3).Stage);

        // Both events emitted
        var tileEvents = _collector.GetEvents().OfType<TileDestroyedEvent>().ToList();
        var obstacleEvents = _collector.GetEvents().OfType<ObstacleDamagedEvent>().ToList();
        Assert.Single(tileEvents);
        Assert.Single(obstacleEvents);
    }

    #endregion

    #region Empty cell

    [Fact]
    public void ProjectileImpact_OnEmptyCell_NoException()
    {
        var state = new GameState(5, 5, 5, null!);
        // No tile, no obstacle at (2,2)

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // No events, no crash
        Assert.Empty(_collector.GetEvents().OfType<ObstacleDamagedEvent>());
        Assert.Empty(_collector.GetEvents().OfType<TileDestroyedEvent>());
    }

    #endregion

    #region Objective tracking

    [Fact]
    public void ProjectileImpact_DestroyBox_UpdatesObstacleObjective()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, stage: 1));
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.Box,
            TargetCount = 4,
            CurrentCount = 0
        };

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // Objective should be updated
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount);

        // Destroy event should have IsGoal = true
        var destroyEvents = _collector.GetEvents().OfType<ObstacleDestroyedEvent>().ToList();
        Assert.Single(destroyEvents);
        Assert.True(destroyEvents[0].IsGoal);
    }

    #endregion

    #region Chain-activatable bomb at obstacle-free position

    [Fact]
    public void ProjectileImpact_OnChainActivatableBomb_TriggersInsteadOfDestroy()
    {
        var state = new GameState(5, 5, 5, null!);
        state.SetTile(2, 2, new Tile(10, ElementType.HorizontalRocket, 2, 2));

        var affected = new HashSet<Position> { new Position(2, 2) };
        var triggeredBombs = new List<Position>();

        _handler.ProcessProjectileImpacts(ref state, affected, 1, 0.1f, _collector, triggeredBombs);

        // Should be added to triggered bombs, not destroyed
        Assert.Contains(new Position(2, 2), triggeredBombs);
        // Tile should still exist
        Assert.Equal(ElementType.HorizontalRocket, state.GetTile(2, 2).Type);
    }

    #endregion
}
