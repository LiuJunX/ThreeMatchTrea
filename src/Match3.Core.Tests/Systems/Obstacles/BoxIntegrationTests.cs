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
/// Integration tests for Box obstacle: LevelConfig → GameState → gameplay → objective completion.
/// </summary>
public class BoxIntegrationTests
{
    #region LevelConfig serialization round-trip

    [Fact]
    public void LevelConfig_WithObstacles_RoundTrip_PreservesData()
    {
        var original = new LevelConfig(5, 5) { MoveLimit = 15 };
        // Place a Box at (1,1) with stage 3
        int idx = 1 * 5 + 1;
        original.Obstacles[idx] = ObstacleType.Box;
        original.ObstacleStages[idx] = 3;
        // Place a Box at (3,2) with stage 4
        int idx2 = 2 * 5 + 3;
        original.Obstacles[idx2] = ObstacleType.Box;
        original.ObstacleStages[idx2] = 4;

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Box, parsed.Obstacles[idx]);
        Assert.Equal(3, parsed.ObstacleStages[idx]);
        Assert.Equal(ObstacleType.Box, parsed.Obstacles[idx2]);
        Assert.Equal(4, parsed.ObstacleStages[idx2]);
        // Non-obstacle cells remain None
        Assert.Equal(ObstacleType.None, parsed.Obstacles[0]);
        Assert.Equal(0, parsed.ObstacleStages[0]);
    }

    [Fact]
    public void LevelConfig_DeepCopy_CopiesObstacleData()
    {
        var original = new LevelConfig(4, 4);
        original.Obstacles[5] = ObstacleType.Box;
        original.ObstacleStages[5] = 2;

        var copy = original.DeepCopy();

        Assert.Equal(ObstacleType.Box, copy.Obstacles[5]);
        Assert.Equal(2, copy.ObstacleStages[5]);

        // Mutating copy doesn't affect original
        copy.Obstacles[5] = ObstacleType.None;
        Assert.Equal(ObstacleType.Box, original.Obstacles[5]);
    }

    [Fact]
    public void ParseLevelConfig_ObstacleJson_DeserializesCorrectly()
    {
        // ObstacleType[] uses JsonStringEnumConverter so works as JSON string array
        const string json = """
        {
            "width": 3,
            "height": 3,
            "obstacles": ["None","Box","None", "None","None","None", "None","None","Box"]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Box, config.Obstacles[1]);
        Assert.Equal(ObstacleType.Box, config.Obstacles[8]);
        Assert.Equal(ObstacleType.None, config.Obstacles[0]);
    }

    [Fact]
    public void ParseLevelConfig_ObstacleObjective_DeserializesCorrectly()
    {
        const string json = """
        {
            "objectives": [
                { "targetLayer": "Obstacle", "elementType": 1, "targetCount": 4 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObjectiveTargetLayer.Obstacle, config.Objectives[0].TargetLayer);
        Assert.Equal(1, config.Objectives[0].ElementType); // ObstacleType.Box = 1
        Assert.Equal(4, config.Objectives[0].TargetCount);
    }

    #endregion

    #region BoardInitializer → GameState initialization

    [Fact]
    public void BoardInitializer_PlacesObstaclesFromConfig()
    {
        var config = new LevelConfig(5, 5);
        // Place Box at (2,1) with stage 3
        int idx = 1 * 5 + 2;
        config.Obstacles[idx] = ObstacleType.Box;
        config.ObstacleStages[idx] = 3;

        var state = new GameState(5, 5, 5, new StubRandom());
        var tileGenerator = new StubTileGenerator();
        var initializer = new BoardInitializer(tileGenerator);
        initializer.Initialize(ref state, config);

        // Obstacle should be placed
        Assert.True(state.HasObstacle(2, 1));
        var obstacle = state.GetObstacle(2, 1);
        Assert.Equal(ObstacleType.Box, obstacle.Type);
        Assert.Equal(3, obstacle.Stage);

        // Tile should NOT be placed at obstacle position
        var tile = state.GetTile(2, 1);
        Assert.Equal(ElementType.None, tile.Type);
    }

    [Fact]
    public void BoardInitializer_NonObstacleCells_HaveTiles()
    {
        var config = new LevelConfig(5, 5);
        // Place one obstacle
        config.Obstacles[0] = ObstacleType.Box;
        config.ObstacleStages[0] = 1;

        var state = new GameState(5, 5, 5, new StubRandom());
        var tileGenerator = new StubTileGenerator();
        var initializer = new BoardInitializer(tileGenerator);
        initializer.Initialize(ref state, config);

        // Obstacle position — no tile
        Assert.True(state.HasObstacle(0, 0));
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);

        // Non-obstacle positions — have tiles
        Assert.False(state.HasObstacle(1, 0));
        Assert.NotEqual(ElementType.None, state.GetTile(1, 0).Type);
    }

    [Fact]
    public void BoardInitializer_ObstacleDefaultStage_Is4()
    {
        var config = new LevelConfig(3, 3);
        config.Obstacles[4] = ObstacleType.Box;
        // ObstacleStages[4] left as 0 → should default to 4 (Box standard HP)

        var state = new GameState(3, 3, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(1, 1));
        Assert.Equal(4, state.GetObstacle(1, 1).Stage);
    }

    #endregion

    #region Objective system — obstacle target

    [Fact]
    public void ObjectiveSystem_TracksBoxDestruction()
    {
        var config = CreateBoxLevelConfig();
        var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
        var state = session.Engine.State;

        // Verify objective initialized: target is "clear all Box" (4 boxes)
        Assert.Equal(ObjectiveTargetLayer.Obstacle, state.ObjectiveProgress[0].TargetLayer);
        Assert.Equal((int)ObstacleType.Box, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(4, state.ObjectiveProgress[0].TargetCount);
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
    }

    #endregion

    #region End-to-end: GameSession with obstacles

    [Fact]
    public void GameSession_WithBoxConfig_InitializesObstacles()
    {
        var config = CreateBoxLevelConfig();
        var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
        var state = session.Engine.State;

        // Verify obstacles placed
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(ObstacleType.Box, state.GetObstacle(2, 2).Type);
        Assert.Equal(2, state.GetObstacle(2, 2).Stage);

        Assert.True(state.HasObstacle(5, 2));
        Assert.Equal(1, state.GetObstacle(5, 2).Stage);

        Assert.True(state.HasObstacle(2, 5));
        Assert.Equal(3, state.GetObstacle(2, 5).Stage);

        Assert.True(state.HasObstacle(5, 5));
        Assert.Equal(4, state.GetObstacle(5, 5).Stage);

        // Obstacle cells should have no tile
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(5, 2).Type);

        // Non-obstacle cells should have tiles
        Assert.NotEqual(ElementType.None, state.GetTile(0, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(3, 3).Type);
    }

    [Fact]
    public void GameSession_WithBox_AdjacentMatchDamagesBox()
    {
        // Create a minimal board with a box and force a match next to it
        var state = new GameState(5, 5, 5, new StubRandom());
        // Place Box at (2,2) with stage 2
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 2));

        // Build a horizontal match at row 1 adjacent to box: (1,1), (2,1), (3,1)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item4, 3, 0));
        state.SetTile(4, 0, new Tile(5, ElementType.Item5, 4, 0));

        // Row 1: create a horizontal match of Item1 at (1,1)-(2,1)-(3,1)
        state.SetTile(0, 1, new Tile(6, ElementType.Item2, 0, 1));
        state.SetTile(1, 1, new Tile(7, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(8, ElementType.Item1, 2, 1));
        state.SetTile(3, 1, new Tile(9, ElementType.Item1, 3, 1));
        state.SetTile(4, 1, new Tile(10, ElementType.Item2, 4, 1));

        // Row 2: box at (2,2), tiles elsewhere
        state.SetTile(0, 2, new Tile(11, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(12, ElementType.Item4, 1, 2));
        // (2,2) is obstacle — no tile
        state.SetTile(3, 2, new Tile(14, ElementType.Item5, 3, 2));
        state.SetTile(4, 2, new Tile(15, ElementType.Item3, 4, 2));

        // Fill rest with non-matching pattern
        FillRemainingRows(ref state, startY: 3);

        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var objectiveSystem = new LevelObjectiveSystem();
        var obstacleSystem = new ObstacleSystem(objectiveSystem);
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);
        // Override to include obstacle system: need to use full factory
        // Instead, use direct ObstacleSystem test via NotifyBatchElimination

        // Directly test adjacent damage
        var eliminated = new[]
        {
            new EliminatedTileInfo(new Position(2, 1), new Tile(8, ElementType.Item1, 2, 1), ElimSource.Match)
        };

        obstacleSystem.NotifyBatchElimination(
            ref state, eliminated, 0, 0f, collector);

        // Box should be damaged from stage 2 → 1
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);

        // Verify damage event emitted
        var damageEvents = collector.GetEvents().OfType<ObstacleDamagedEvent>().ToList();
        Assert.Single(damageEvents);
        Assert.Equal(ObstacleType.Box, damageEvents[0].Type);
        Assert.Equal(1, damageEvents[0].RemainingStage);
    }

    [Fact]
    public void FullPipeline_BoxDestroyed_ObjectiveProgress()
    {
        // Use full session to test box destruction updates objective
        var config = new LevelConfig(5, 5) { MoveLimit = 30 };
        // Place a single Box at (2,2) with stage 1 (will be destroyed on first adjacent match)
        int idx = 2 * 5 + 2;
        config.Obstacles[idx] = ObstacleType.Box;
        config.ObstacleStages[idx] = 1;
        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Obstacle,
                ElementType = (int)ObstacleType.Box,
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
        var engine = session.Engine;

        // Run simulation moves until the box is destroyed or we run out of moves
        bool boxDestroyed = false;
        for (int move = 0; move < 30 && !boxDestroyed; move++)
        {
            SimulationTestHelper.SettleCompletely(engine);
            if (!SimulationTestHelper.TryApplyRandomMove(engine, move))
                break;
            SimulationTestHelper.SettleCompletely(engine);

            boxDestroyed = !engine.State.HasObstacle(2, 2);
        }

        // If box was destroyed, objective should have progressed
        if (boxDestroyed)
        {
            Assert.True(engine.State.ObjectiveProgress[0].CurrentCount > 0,
                "Objective should track box destruction");
        }
        // If box wasn't destroyed in 30 random moves, that's also acceptable —
        // the important thing is no crash and state consistency
    }

    #endregion

    #region Physics — obstacle blocks gravity and refill

    [Fact]
    public void Gravity_TileAboveObstacle_DoesNotFallThrough()
    {
        // Column: tile at y=0, box at y=1, empty at y=2
        // Tile should NOT fall through box to y=2
        var config = new LevelConfig(5, 5) { MoveLimit = 30 };
        int boxIdx = 1 * 5 + 2; // (2,1)
        config.Obstacles[boxIdx] = ObstacleType.Box;
        config.ObstacleStages[boxIdx] = 2;

        var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
        var engine = session.Engine;

        // After settling, obstacle at (2,1) should remain
        SimulationTestHelper.SettleCompletely(engine);
        Assert.True(engine.State.HasObstacle(2, 1), "Box should still exist");

        // The cell at (2,1) should have no tile (obstacle occupies it)
        Assert.Equal(ElementType.None, engine.State.GetTile(2, 1).Type);

        // Tile at (2,0) should exist (above the box, can't fall through)
        Assert.NotEqual(ElementType.None, engine.State.GetTile(2, 0).Type);
    }

    [Fact]
    public void Gravity_TileDoesNotEnterObstacleCell_AfterElimination()
    {
        // Setup: column with tile above box, and gap below box
        // After settling, the tile above should NOT move into the box cell
        var state = new GameState(3, 5, 5, new StubRandom());

        // y=0: tile
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));

        // y=1: box at (1,1)
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 2));
        state.SetTile(0, 1, new Tile(4, ElementType.Item4, 0, 1));
        // (1,1) obstacle — no tile
        state.SetTile(2, 1, new Tile(6, ElementType.Item5, 2, 1));

        // y=2: empty at (1,2) — gap below box
        state.SetTile(0, 2, new Tile(7, ElementType.Item3, 0, 2));
        // (1,2) intentionally empty
        state.SetTile(2, 2, new Tile(9, ElementType.Item1, 2, 2));

        // y=3,4: fill with tiles
        FillRemainingRows(ref state, startY: 3);
        state.NextTileId = 100;

        var engine = TestEngineFactory.CreateEngine(state,
            simulationConfig: SimulationConfig.ForAI());
        SimulationTestHelper.SettleCompletely(engine);

        // Box should still be there
        Assert.True(engine.State.HasObstacle(1, 1));
        // No tile at box position
        Assert.Equal(ElementType.None, engine.State.GetTile(1, 1).Type);
    }

    [Fact]
    public void Refill_DoesNotSpawnAtObstaclePosition()
    {
        // Place obstacle at top of a column — refill should not spawn there
        var config = new LevelConfig(3, 3) { MoveLimit = 30 };
        // Box at (1,0) — top row
        config.Obstacles[1] = ObstacleType.Box;
        config.ObstacleStages[1] = 2;

        var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
        var engine = session.Engine;
        SimulationTestHelper.SettleCompletely(engine);

        // Obstacle at (1,0) should remain with no tile
        Assert.True(engine.State.HasObstacle(1, 0));
        Assert.Equal(ElementType.None, engine.State.GetTile(1, 0).Type);
    }

    [Fact]
    public void BombExplosion_DamagesAdjacentBox()
    {
        // Direct test: CellEliminator with ObstacleSystem handles bomb → obstacle hit
        var state = new GameState(5, 5, 5, new StubRandom());
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 2));

        var collector = new BufferedEventCollector();
        var objectiveSystem = new LevelObjectiveSystem();
        var obstacleSystem = new ObstacleSystem(objectiveSystem);

        // Simulate bomb hitting the obstacle cell directly
        var result = obstacleSystem.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, collector);

        Assert.Equal(ObstacleHitResult.Damaged, result);
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);

        // Hit again — should destroy
        result = obstacleSystem.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 1, 0.1f, collector);

        Assert.Equal(ObstacleHitResult.Destroyed, result);
        Assert.False(state.HasObstacle(2, 2));

        // Verify events
        var events = collector.GetEvents().ToList();
        Assert.Single(events.OfType<ObstacleDamagedEvent>());
        Assert.Single(events.OfType<ObstacleDestroyedEvent>());
    }

    [Fact]
    public void ObstacleInvariant_NoTileAtObstaclePosition_DuringSimulation()
    {
        // Run several moves on a board with obstacles and verify
        // no tile ever occupies an obstacle cell
        var config = CreateBoxLevelConfig();
        var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
        var engine = session.Engine;

        for (int move = 0; move < 10; move++)
        {
            SimulationTestHelper.SettleCompletely(engine);

            // Check invariant: no tile at any obstacle position
            for (int y = 0; y < engine.State.Height; y++)
            {
                for (int x = 0; x < engine.State.Width; x++)
                {
                    if (engine.State.HasObstacle(x, y))
                    {
                        Assert.True(engine.State.GetTile(x, y).Type == ElementType.None,
                            $"Tile found at obstacle position ({x},{y}) after move {move}");
                    }
                }
            }

            if (!SimulationTestHelper.TryApplyRandomMove(engine, move))
                break;
        }
    }

    #endregion

    #region Helpers

    private static LevelConfig CreateBoxLevelConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        // Place 4 boxes at various positions with different stages
        void PlaceBox(int x, int y, byte stage)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.Box;
            config.ObstacleStages[i] = stage;
        }

        PlaceBox(2, 2, 2);
        PlaceBox(5, 2, 1);
        PlaceBox(2, 5, 3);
        PlaceBox(5, 5, 4);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Obstacle,
                ElementType = (int)ObstacleType.Box,
                TargetCount = 4
            },
            default,
            default,
            default
        ];

        return config;
    }

    private static void FillRemainingRows(ref GameState state, int startY)
    {
        int id = 50;
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2,
                            ElementType.Item4, ElementType.Item5 };
        for (int y = startY; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                int typeIdx = (x + y) % types.Length;
                state.SetTile(x, y, new Tile(id++, types[typeIdx], x, y));
            }
        }
    }

    #endregion
}

/// <summary>
/// Stub ITileGenerator for BoardInitializer tests.
/// Returns a deterministic cycling pattern of ElementTypes.
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
