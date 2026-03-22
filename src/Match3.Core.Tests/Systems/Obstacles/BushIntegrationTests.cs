using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Integration tests for Bush obstacle: default stage, multi-hit lifecycle,
/// and death effect → Grass spread.
/// </summary>
public class BushIntegrationTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region GetDefaultStage

    [Fact]
    public void GetDefaultStage_Bush_Returns5()
    {
        Assert.Equal(5, ObstacleRules.GetDefaultStage(ObstacleType.Bush));
    }

    [Fact]
    public void GetDefaultStage_Safe_Returns5()
    {
        Assert.Equal(5, ObstacleRules.GetDefaultStage(ObstacleType.Safe));
    }

    [Fact]
    public void BoardInitializer_BushDefaultStage_Is5()
    {
        var config = new LevelConfig(3, 3);
        config.Obstacles[4] = ObstacleType.Bush;
        // ObstacleStages[4] left as 0 → should default to 5

        var state = new GameState(3, 3, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(1, 1));
        Assert.Equal(5, state.GetObstacle(1, 1).Stage);
    }

    #endregion

    #region Multi-hit lifecycle: 5 → 4 → 3 → 2 → 1 → 0 (destroy)

    [Fact]
    public void Bush_FiveHits_DestroysAndSpreadsGrass()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 5));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        // Hits 1–4: damage only, no grass
        for (int i = 0; i < 4; i++)
        {
            var result = system.TryHit(ref state, new Position(2, 2),
                new ElimContext(ElimSource.Bomb), i, 0f, events);
            Assert.Equal(ObstacleHitResult.Damaged, result);
            Assert.True(state.HasObstacle(2, 2));
            Assert.Equal((byte)(4 - i), state.GetObstacle(2, 2).Stage);
        }

        // No grass yet
        Assert.False(state.GetGround(1, 2).HasGround);

        // Hit 5: destroys Bush → Grass spread
        var finalResult = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 4, 0f, events);
        Assert.Equal(ObstacleHitResult.Destroyed, finalResult);
        Assert.False(state.HasObstacle(2, 2));

        // Grass now present in 4 neighbors
        Assert.Equal(GroundType.Grass, state.GetGround(1, 2).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(2, 1).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(2, 3).Type);
    }

    [Fact]
    public void Bush_FiveHits_EmitsCorrectEventSequence()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 5));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        for (int i = 0; i < 5; i++)
        {
            system.TryHit(ref state, new Position(2, 2),
                new ElimContext(ElimSource.Bomb), i, 0f, events);
        }

        var allEvents = events.GetEvents();

        // 4 damage events + 1 destroy event
        var damageEvents = allEvents.OfType<ObstacleDamagedEvent>().ToList();
        Assert.Equal(4, damageEvents.Count);
        Assert.Equal(4, damageEvents[0].RemainingStage);
        Assert.Equal(3, damageEvents[1].RemainingStage);
        Assert.Equal(2, damageEvents[2].RemainingStage);
        Assert.Equal(1, damageEvents[3].RemainingStage);

        var destroyEvents = allEvents.OfType<ObstacleDestroyedEvent>().ToList();
        Assert.Single(destroyEvents);
        Assert.Equal(ObstacleType.Bush, destroyEvents[0].Type);

        // 4 grass spawn events (from death effect)
        var grassEvents = allEvents.OfType<GroundSpawnedEvent>().ToList();
        Assert.Equal(4, grassEvents.Count);
    }

    #endregion

    #region Adjacent damage path — multi-hit via NotifyBatchElimination

    [Fact]
    public void Bush_AdjacentDamage_MultipleRounds_Destroys()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 3));
        var system = new ObstacleSystem();

        // 3 adjacent eliminations to destroy a stage-3 Bush
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        for (int i = 0; i < 3; i++)
        {
            eliminated[0] = new EliminatedTileInfo(
                new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);
            system.NotifyBatchElimination(ref state, eliminated, i, 0f, NullEventCollector.Instance);
        }

        Assert.False(state.HasObstacle(2, 2));
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type);
    }

    #endregion
}
