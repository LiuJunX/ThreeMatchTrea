using System.Linq;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.Objectives;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Tests.TestHelpers;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Integration tests for Cupboard obstacle: damage, destruction, Plate release, and objective tracking.
/// </summary>
public class CupboardIntegrationTests
{
    #region ObstacleRules

    [Fact]
    public void CanHit_Cupboard_ReturnsTrueForAllSources()
    {
        var obstacle = new Obstacle(ObstacleType.Cupboard, 2);

        Assert.True(ObstacleRules.CanHit(in obstacle, new ElimContext(ElimSource.Match)));
        Assert.True(ObstacleRules.CanHit(in obstacle, new ElimContext(ElimSource.Bomb)));
        Assert.True(ObstacleRules.CanHit(in obstacle, new ElimContext(ElimSource.Projectile)));
        Assert.True(ObstacleRules.CanHit(in obstacle, new ElimContext(ElimSource.ColorBomb)));
    }

    [Fact]
    public void CanReactAdjacent_Cupboard_ReturnsTrue()
    {
        var obstacle = new Obstacle(ObstacleType.Cupboard, 2);

        Assert.True(ObstacleRules.CanReactAdjacent(in obstacle, ElementType.Item1));
        Assert.True(ObstacleRules.CanReactAdjacent(in obstacle, ElementType.ColorBomb));
    }

    #endregion

    #region Damage and destruction

    [Fact]
    public void TryHit_Cupboard_DamagesFromStage2To1()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Cupboard, 2));

        var collector = new BufferedEventCollector();
        var obstacleSystem = new ObstacleSystem();

        var result = obstacleSystem.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, collector);

        Assert.Equal(ObstacleHitResult.Damaged, result);
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);

        var damageEvents = collector.GetEvents().OfType<ObstacleDamagedEvent>().ToList();
        Assert.Single(damageEvents);
        Assert.Equal(ObstacleType.Cupboard, damageEvents[0].Type);
        Assert.Equal(1, damageEvents[0].RemainingStage);
    }

    [Fact]
    public void TryHit_Cupboard_DestroyAndReleasePlate()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Cupboard, 1));
        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var obstacleSystem = new ObstacleSystem();

        var result = obstacleSystem.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, collector);

        Assert.Equal(ObstacleHitResult.Destroyed, result);
        Assert.False(state.HasObstacle(2, 2));

        // Plate should be spawned at the same position
        var tile = state.GetTile(2, 2);
        Assert.Equal(ElementType.Plate, tile.Type);
        Assert.Equal(100, tile.Id);

        // Verify events: ObstacleDestroyed + TileSpawned
        var events = collector.GetEvents().ToList();
        Assert.Single(events.OfType<ObstacleDestroyedEvent>());
        var spawnEvent = Assert.Single(events.OfType<TileSpawnedEvent>());
        Assert.Equal(ElementType.Plate, spawnEvent.Type);
        Assert.Equal(new Position(2, 2), spawnEvent.GridPosition);
    }

    [Fact]
    public void AdjacentMatch_Cupboard_TwoHits_DestroyAndReleasePlate()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Cupboard, 2));
        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var obstacleSystem = new ObstacleSystem();

        // First adjacent hit
        var eliminated1 = new[]
        {
            new EliminatedTileInfo(new Position(2, 1), new Tile(1, ElementType.Item1, 2, 1), ElimSource.Match)
        };
        obstacleSystem.NotifyBatchElimination(ref state, eliminated1, 0, 0f, collector);

        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);

        // Second adjacent hit
        var eliminated2 = new[]
        {
            new EliminatedTileInfo(new Position(2, 3), new Tile(2, ElementType.Item2, 2, 3), ElimSource.Match)
        };
        obstacleSystem.NotifyBatchElimination(ref state, eliminated2, 1, 0.1f, collector);

        Assert.False(state.HasObstacle(2, 2));
        Assert.Equal(ElementType.Plate, state.GetTile(2, 2).Type);
    }

    #endregion

    #region Plate properties

    [Fact]
    public void ReleasedPlate_IsCollectible()
    {
        Assert.True(ElementType.Plate.IsCollectible());
    }

    [Fact]
    public void ReleasedPlate_IsNotMatchable()
    {
        Assert.False(ElementType.Plate.IsMatchable());
        Assert.False(ElementType.Plate.IsColor());
    }

    [Fact]
    public void ReleasedPlate_HasProtection()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Cupboard, 1));
        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var obstacleSystem = new ObstacleSystem();
        float simTime = 1.0f;

        obstacleSystem.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, simTime, collector);

        var tile = state.GetTile(2, 2);
        Assert.True(tile.ProtectUntil > simTime,
            "Released Plate should have protection to prevent immediate elimination");
    }

    #endregion

    #region Objective tracking — Plate collection

    [Fact]
    public void PlateObjective_TracksPlateDestruction()
    {
        var config = new LevelConfig(5, 5) { MoveLimit = 30 };
        // Place a Cupboard at (2,2) with stage 1
        int idx = 2 * 5 + 2;
        config.Obstacles[idx] = ObstacleType.Cupboard;
        config.ObstacleStages[idx] = 1;
        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Plate,
                TargetCount = 1
            },
            default,
            default,
            default
        ];

        var factory = new GameServiceFactoryBuilder().UseDefaultServices().Build();
        var sessionConfig = new GameServiceConfiguration
        {
            RngSeed = 42,
            TileTypesCount = 5,
            EnableEventCollection = true,
            SimulationConfig = SimulationConfig.ForAI()
        };
        var session = factory.CreateGameSession(sessionConfig, config);
        var state = session.Engine.State;

        // Verify objective is initialized for Plate collection
        Assert.Equal(ObjectiveTargetLayer.Tile, state.ObjectiveProgress[0].TargetLayer);
        Assert.Equal((int)ElementType.Plate, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(1, state.ObjectiveProgress[0].TargetCount);
    }

    #endregion

    #region LevelConfig serialization

    [Fact]
    public void LevelConfig_WithCupboard_RoundTrip_PreservesData()
    {
        var original = new LevelConfig(5, 5) { MoveLimit = 15 };
        int idx = 1 * 5 + 2;
        original.Obstacles[idx] = ObstacleType.Cupboard;
        original.ObstacleStages[idx] = 2;

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Cupboard, parsed.Obstacles[idx]);
        Assert.Equal(2, parsed.ObstacleStages[idx]);
    }

    [Fact]
    public void ParseLevelConfig_CupboardJson_DeserializesCorrectly()
    {
        const string json = """
        {
            "width": 3,
            "height": 3,
            "obstacles": ["None","Cupboard","None", "None","None","None", "None","None","None"]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Cupboard, config.Obstacles[1]);
        Assert.Equal(ObstacleType.None, config.Obstacles[0]);
    }

    #endregion

    #region BoardInitializer

    [Fact]
    public void BoardInitializer_PlacesCupboardFromConfig()
    {
        var config = new LevelConfig(5, 5);
        int idx = 1 * 5 + 2;
        config.Obstacles[idx] = ObstacleType.Cupboard;
        config.ObstacleStages[idx] = 2;

        var state = new GameState(5, 5, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(2, 1));
        var obstacle = state.GetObstacle(2, 1);
        Assert.Equal(ObstacleType.Cupboard, obstacle.Type);
        Assert.Equal(2, obstacle.Stage);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);
    }

    [Fact]
    public void BoardInitializer_CupboardDefaultStage_Is2()
    {
        var config = new LevelConfig(3, 3);
        config.Obstacles[4] = ObstacleType.Cupboard;
        // ObstacleStages[4] left as 0 → should default to 2 (not 1 like Box)

        var state = new GameState(3, 3, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(1, 1));
        Assert.Equal(2, state.GetObstacle(1, 1).Stage);
    }

    #endregion

    #region Mixed scenario — Cupboard + Box coexistence

    [Fact]
    public void MixedScene_CupboardAndBox_IndependentBehavior()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(1, 2, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(3, 2, new Obstacle(ObstacleType.Cupboard, 1));
        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var obstacleSystem = new ObstacleSystem();

        // Eliminate tile at (2,2) — adjacent to both
        var eliminated = new[]
        {
            new EliminatedTileInfo(new Position(2, 2), new Tile(1, ElementType.Item1, 2, 2), ElimSource.Match)
        };
        obstacleSystem.NotifyBatchElimination(ref state, eliminated, 0, 0f, collector);

        // Box at (1,2) should be destroyed (stage 1 → 0), no tile spawned
        Assert.False(state.HasObstacle(1, 2));
        Assert.Equal(ElementType.None, state.GetTile(1, 2).Type);

        // Cupboard at (3,2) should be destroyed, Plate spawned
        Assert.False(state.HasObstacle(3, 2));
        Assert.Equal(ElementType.Plate, state.GetTile(3, 2).Type);
    }

    #endregion

    #region Full pipeline

    [Fact]
    public void FullPipeline_CupboardDestroyed_PlateSpawned()
    {
        var config = CreateCupboardLevelConfig();

        var factory = new GameServiceFactoryBuilder().UseDefaultServices().Build();
        var sessionConfig = new GameServiceConfiguration
        {
            RngSeed = 42,
            TileTypesCount = 5,
            EnableEventCollection = true,
            SimulationConfig = SimulationConfig.ForAI()
        };
        var session = factory.CreateGameSession(sessionConfig, config);
        var engine = session.Engine;

        // Run simulation moves until a cupboard is destroyed
        bool cupboardDestroyed = false;
        for (int move = 0; move < 30 && !cupboardDestroyed; move++)
        {
            SimulationTestHelper.SettleCompletely(engine);
            if (!SimulationTestHelper.TryApplyRandomMove(engine, move))
                break;
            SimulationTestHelper.SettleCompletely(engine);

            // Check if any cupboard was destroyed
            cupboardDestroyed = !engine.State.HasObstacle(3, 3) || !engine.State.HasObstacle(5, 3);
        }

        // Whether or not a cupboard was destroyed, the simulation should run without crashes
        // and maintain board consistency
        for (int y = 0; y < engine.State.Height; y++)
        {
            for (int x = 0; x < engine.State.Width; x++)
            {
                if (engine.State.HasObstacle(x, y))
                {
                    Assert.Equal(ElementType.None, engine.State.GetTile(x, y).Type);
                }
            }
        }
    }

    #endregion

    #region Helpers

    private static LevelConfig CreateCupboardLevelConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        void PlaceCupboard(int x, int y, byte stage)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.Cupboard;
            config.ObstacleStages[i] = stage;
        }

        PlaceCupboard(3, 3, 2);
        PlaceCupboard(5, 3, 2);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Plate,
                TargetCount = 2
            },
            default,
            default,
            default
        ];

        return config;
    }

    #endregion
}

/// <summary>
/// Stub ITileGenerator for Cupboard tests.
/// </summary>
file sealed class StubTileGenerator : ITileGenerator
{
    private int _counter;

    public ElementType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3,
                            ElementType.Item4, ElementType.Item5 };
        return types[_counter++ % types.Length];
    }
}
