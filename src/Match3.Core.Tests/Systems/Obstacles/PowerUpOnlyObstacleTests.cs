using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.Objectives;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Parameterized tests for Power-up Only obstacles: Safe, Owl, Stone.
/// All share the same CanHit whitelist and CanReactAdjacent=false rules.
/// </summary>
public class PowerUpOnlyObstacleTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region CanHit — Match blocked

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    [InlineData(ObstacleType.Safe, 5)]
    public void MatchSource_Blocked(ObstacleType type, byte stage)
    {
        var obs = new Obstacle(type, stage);
        Assert.False(ObstacleRules.CanHit(in obs, new ElimContext(ElimSource.Match)));
    }

    #endregion

    #region CanHit — All PowerUp sources allowed

    [Theory]
    [InlineData(ObstacleType.Owl, 1, ElimSource.Bomb)]
    [InlineData(ObstacleType.Owl, 1, ElimSource.Projectile)]
    [InlineData(ObstacleType.Owl, 1, ElimSource.ChainReaction)]
    [InlineData(ObstacleType.Owl, 1, ElimSource.ColorBomb)]
    [InlineData(ObstacleType.Owl, 1, ElimSource.SideItem)]
    [InlineData(ObstacleType.Owl, 1, ElimSource.ConsumeBomb)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.Bomb)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.Projectile)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.ChainReaction)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.ColorBomb)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.SideItem)]
    [InlineData(ObstacleType.Stone, 3, ElimSource.ConsumeBomb)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.Bomb)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.Projectile)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.ChainReaction)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.ColorBomb)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.SideItem)]
    [InlineData(ObstacleType.Safe, 5, ElimSource.ConsumeBomb)]
    public void AllPowerUpSources_CanHit(ObstacleType type, byte stage, ElimSource source)
    {
        var obs = new Obstacle(type, stage);
        Assert.True(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    #endregion

    #region CanReactAdjacent — always false

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    [InlineData(ObstacleType.Safe, 5)]
    public void AdjacentMatch_NoEffect(ObstacleType type, byte stage)
    {
        var obs = new Obstacle(type, stage);
        Assert.False(ObstacleRules.CanReactAdjacent(in obs, ElementType.Item1));
        Assert.False(ObstacleRules.CanReactAdjacent(in obs, ElementType.ColorBomb));
    }

    #endregion

    #region Full destruction — requires N hits

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    [InlineData(ObstacleType.Safe, 5)]
    public void FullDestruction_RequiresNHits(ObstacleType type, byte stage)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(type, stage));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();
        var pos = new Position(2, 2);

        for (int i = 0; i < stage; i++)
        {
            system.TryHit(ref state, pos, new ElimContext(ElimSource.Bomb),
                i, i * 0.1f, events);
        }

        Assert.Equal(ObstacleType.None, state.GetObstacle(2, 2).Type);
        Assert.Contains(events.GetEvents(), e => e is ObstacleDestroyedEvent);
    }

    #endregion

    #region No death effect

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    [InlineData(ObstacleType.Safe, 5)]
    public void NoDeathEffect(ObstacleType type, byte stage)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(type, 1)); // 1 HP for quick destroy
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // No ground spread or tile release
        Assert.False(state.GetGround(1, 2).HasGround);
        Assert.False(state.GetGround(3, 2).HasGround);
        Assert.False(state.GetGround(2, 1).HasGround);
        Assert.False(state.GetGround(2, 3).HasGround);
    }

    #endregion

    #region GetDefaultStage

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    [InlineData(ObstacleType.Safe, 5)]
    public void GetDefaultStage_ReturnsExpected(ObstacleType type, byte expectedStage)
    {
        Assert.Equal(expectedStage, ObstacleRules.GetDefaultStage(type));
    }

    #endregion

    #region Objective — IsGoal

    [Theory]
    [InlineData(ObstacleType.Owl)]
    [InlineData(ObstacleType.Stone)]
    [InlineData(ObstacleType.Safe)]
    public void Objective_IsGoal(ObstacleType type)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(type, 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)type,
            TargetCount = 1
        };
        objectiveSystem.Initialize(ref state, config);

        var system = new ObstacleSystem(objectiveSystem);
        var events = new BufferedEventCollector();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, events);

        var evt = events.GetEvents().OfType<ObstacleDestroyedEvent>().Single();
        Assert.True(evt.IsGoal);
    }

    #endregion

    #region Owl-specific: never emits DamagedEvent

    [Fact]
    public void Owl_NeverEmitsDamagedEvent()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Owl, 1));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, events);

        // Only DestroyedEvent, no DamagedEvent
        Assert.DoesNotContain(events.GetEvents(), e => e is ObstacleDamagedEvent);
        Assert.Contains(events.GetEvents(), e => e is ObstacleDestroyedEvent);
    }

    #endregion

    #region Stone-specific: emits DamagedEvent at each stage

    [Fact]
    public void Stone_EmitsDamagedEvent_AtEachStage()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Stone, 3));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();
        var pos = new Position(2, 2);

        // Hit 1: 3→2
        system.TryHit(ref state, pos, new ElimContext(ElimSource.Bomb), 1, 0.1f, events);
        var damaged1 = events.GetEvents().OfType<ObstacleDamagedEvent>().Single();
        Assert.Equal(2, damaged1.RemainingStage);

        // Hit 2: 2→1
        system.TryHit(ref state, pos, new ElimContext(ElimSource.Bomb), 2, 0.2f, events);
        var damaged2 = events.GetEvents().OfType<ObstacleDamagedEvent>().Last();
        Assert.Equal(1, damaged2.RemainingStage);

        // Hit 3: 1→destroyed
        system.TryHit(ref state, pos, new ElimContext(ElimSource.Bomb), 3, 0.3f, events);
        Assert.Contains(events.GetEvents(), e => e is ObstacleDestroyedEvent);
        Assert.Equal(2, events.GetEvents().OfType<ObstacleDamagedEvent>().Count());
    }

    #endregion

    #region Integration — Bomb full pipeline damages Owl/Stone

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    public void BombExplosion_FullPipeline_DamagesAndDestroys(ObstacleType type, byte stage)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(type, stage));
        var objectiveSystem = new LevelObjectiveSystem();
        var obstacleSystem = new ObstacleSystem(objectiveSystem);
        var events = new BufferedEventCollector();
        var pos = new Position(2, 2);

        // Hit until destroyed
        for (int i = 0; i < stage; i++)
        {
            obstacleSystem.TryHit(ref state, pos,
                new ElimContext(ElimSource.Bomb), i, i * 0.1f, events);
        }

        Assert.False(state.HasObstacle(2, 2));
        Assert.Single(events.GetEvents().OfType<ObstacleDestroyedEvent>());
    }

    #endregion

    #region Integration — Adjacent match does NOT damage Owl/Stone

    [Theory]
    [InlineData(ObstacleType.Owl, 1)]
    [InlineData(ObstacleType.Stone, 3)]
    public void AdjacentMatch_FullPipeline_DoesNotDamage(ObstacleType type, byte stage)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(type, stage));
        // Place a tile adjacent to obstacle for elimination
        state.SetTile(2, 1, new Tile(1, ElementType.Item1, 2, 1));

        var obstacleSystem = new ObstacleSystem();
        var events = new BufferedEventCollector();

        var eliminated = new[]
        {
            new EliminatedTileInfo(new Position(2, 1), new Tile(1, ElementType.Item1, 2, 1), ElimSource.Match)
        };

        obstacleSystem.NotifyBatchElimination(ref state, eliminated, 0, 0f, events);

        // Obstacle unchanged
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(stage, state.GetObstacle(2, 2).Stage);
        Assert.Empty(events.GetEvents().OfType<ObstacleDamagedEvent>());
        Assert.Empty(events.GetEvents().OfType<ObstacleDestroyedEvent>());
    }

    #endregion
}
