using System;
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
/// Tests for MagicHat generator obstacle: permanent, accumulates 3 adjacent hits to spawn Diamond.
/// </summary>
public class MagicHatTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region ObstacleRules

    [Fact]
    public void IsGenerator_MagicHat_ReturnsTrue()
    {
        Assert.True(ObstacleRules.IsGenerator(ObstacleType.MagicHat));
    }

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.ChainReaction)]
    public void CanHit_MagicHat_ReturnsFalseForAllSources(ElimSource source)
    {
        var obstacle = new Obstacle(ObstacleType.MagicHat, 1);
        Assert.False(ObstacleRules.CanHit(in obstacle, new ElimContext(source)));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.Item2)]
    [InlineData(ElementType.ColorBomb)]
    public void CanReactAdjacent_MagicHat_ReturnsTrueForAllTypes(ElementType triggerType)
    {
        var obstacle = new Obstacle(ObstacleType.MagicHat, 1);
        Assert.True(ObstacleRules.CanReactAdjacent(in obstacle, triggerType));
    }

    [Fact]
    public void MagicHat_CannotBeDirectHit()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);
    }

    #endregion

    #region Accumulation — State increments

    [Fact]
    public void Accumulate_OneHit_StateIncremented()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();

        TriggerAdjacentMatch(ref state, system, new Position(1, 2));

        Assert.Equal(1, state.GetObstacle(2, 2).State);
        Assert.Equal(1, state.GetObstacle(2, 2).Stage); // Stage unchanged
        AssertNoDiamond(in state);
    }

    [Fact]
    public void Accumulate_TwoHits_StateIncremented()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();

        TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2)); // clear for next batch
        TriggerAdjacentMatch(ref state, system, new Position(3, 2));

        Assert.Equal(2, state.GetObstacle(2, 2).State);
        AssertNoDiamond(in state);
    }

    [Fact]
    public void Accumulate_ThreeHits_SpawnsDiamond()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));
        TriggerAdjacentMatch(ref state, system, new Position(3, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));
        TriggerAdjacentMatch(ref state, system, new Position(1, 2));

        Assert.True(FindDiamond(in state, new Position(2, 2)),
            "Diamond should be spawned at an adjacent empty cell after 3 hits");
    }

    [Fact]
    public void Accumulate_ThreeHits_StateResets()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));
        TriggerAdjacentMatch(ref state, system, new Position(3, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));
        TriggerAdjacentMatch(ref state, system, new Position(1, 2));

        Assert.Equal(0, state.GetObstacle(2, 2).State);
    }

    [Fact]
    public void Accumulate_SixHits_TwoDiamonds()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // 6 separate batches — enough empty neighbors (4 slots) for 2 Diamonds
        for (int i = 0; i < 6; i++)
        {
            TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        }

        int diamondCount = CountDiamonds(in state);
        Assert.Equal(2, diamondCount);
    }

    #endregion

    #region No empty slot — State preserved

    [Fact]
    public void Accumulate_NoEmptySlot_StatePreserved()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        // Fill all neighbors with tiles
        state.SetTile(2, 1, new Tile(10, ElementType.Item2, 2, 1));
        state.SetTile(3, 2, new Tile(11, ElementType.Item3, 3, 2));
        state.SetTile(2, 3, new Tile(12, ElementType.Item4, 2, 3));
        state.SetTile(1, 2, new Tile(13, ElementType.Item5, 1, 2));
        var system = new ObstacleSystem();

        // Trigger 3 times (using trick: NotifyBatchElimination doesn't check tile presence in state)
        for (int i = 0; i < 3; i++)
        {
            Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
            {
                new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
            };
            system.NotifyBatchElimination(ref state, eliminated, i, i * 0.1f, NullEventCollector.Instance);
        }

        // State should be 3 (threshold reached but no empty slot)
        Assert.Equal(3, state.GetObstacle(2, 2).State);
        AssertNoDiamond(in state);
    }

    [Fact]
    public void Accumulate_NoEmptySlot_NextHitRetries()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.SetTile(2, 1, new Tile(10, ElementType.Item2, 2, 1));
        state.SetTile(3, 2, new Tile(11, ElementType.Item3, 3, 2));
        state.SetTile(2, 3, new Tile(12, ElementType.Item4, 2, 3));
        state.SetTile(1, 2, new Tile(13, ElementType.Item5, 1, 2));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Accumulate to threshold with all slots blocked
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };
        for (int i = 0; i < 3; i++)
        {
            system.NotifyBatchElimination(ref state, eliminated, i, i * 0.1f, NullEventCollector.Instance);
        }

        Assert.Equal(3, state.GetObstacle(2, 2).State);

        // Free up a slot
        state.SetTile(2, 1, default);

        // Next activation should spawn Diamond
        Span<EliminatedTileInfo> elim = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };
        system.NotifyBatchElimination(ref state, elim, 3, 0.3f, NullEventCollector.Instance);

        // State should NOT increment further (was already at threshold), should reset after spawn
        Assert.Equal(0, state.GetObstacle(2, 2).State);
        Assert.Equal(ElementType.Diamond, state.GetTile(2, 1).Type);
    }

    #endregion

    #region MagicHat never destroyed

    [Fact]
    public void MagicHat_NeverDestroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // 10 adjacent hits
        for (int i = 0; i < 10; i++)
        {
            ClearSpawnedTiles(ref state, new Position(2, 2));
            TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        }

        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(ObstacleType.MagicHat, state.GetObstacle(2, 2).Type);
        Assert.Equal(1, state.GetObstacle(2, 2).Stage); // Stage never decremented
    }

    #endregion

    #region Bomb/Projectile sources also trigger accumulation (Pass 2)

    [Fact]
    public void BombAdjacentElimination_TriggersAccumulation()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Bomb),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(1, state.GetObstacle(2, 2).State);
    }

    #endregion

    #region Diamond properties

    [Fact]
    public void Diamond_IsCollectible()
    {
        Assert.True(ElementType.Diamond.IsCollectible());
    }

    [Fact]
    public void Diamond_IsNotMatchable()
    {
        Assert.False(ElementType.Diamond.IsMatchable());
        Assert.False(ElementType.Diamond.IsColor());
    }

    [Fact]
    public void Diamond_IsNotBomb()
    {
        Assert.False(ElementType.Diamond.IsBomb());
    }

    #endregion

    #region Events

    [Fact]
    public void EmitsDamagedEvent_WithAccumulationCount()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();
        var collector = new BufferedEventCollector();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 5, 1.0f, collector);

        var events = collector.GetEvents();
        var dmgEvt = Assert.Single(events.OfType<ObstacleDamagedEvent>());
        Assert.Equal(ObstacleType.MagicHat, dmgEvt.Type);
        Assert.Equal(new Position(2, 2), dmgEvt.GridPosition);
        Assert.Equal(1, dmgEvt.RemainingStage); // accumulation count = 1
        Assert.False(dmgEvt.IsGoal);
    }

    [Fact]
    public void EmitsGeneratorActivatedEvent_OnSpawn()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();
        var collector = new BufferedEventCollector();

        // Accumulate to 3
        for (int i = 0; i < 2; i++)
        {
            TriggerAdjacentMatch(ref state, system, new Position(1, 2));
            ClearSpawnedTiles(ref state, new Position(2, 2));
        }

        // Third hit — should produce
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };
        system.NotifyBatchElimination(ref state, eliminated, 10, 2.0f, collector);

        var events = collector.GetEvents();
        var genEvt = Assert.Single(events.OfType<GeneratorActivatedEvent>());
        Assert.Equal(new Position(2, 2), genEvt.GridPosition);
        Assert.Equal(ObstacleType.MagicHat, genEvt.ObstacleType);
        Assert.Equal(ElementType.Diamond, genEvt.ProductType);

        var spawnEvt = Assert.Single(events.OfType<TileSpawnedEvent>());
        Assert.Equal(ElementType.Diamond, spawnEvt.Type);
        Assert.Equal(genEvt.ProductPosition, spawnEvt.GridPosition);
    }

    [Fact]
    public void ThirdHit_EmitsBothDamagedAndGeneratorEvents()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Accumulate to 2
        for (int i = 0; i < 2; i++)
        {
            TriggerAdjacentMatch(ref state, system, new Position(1, 2));
            ClearSpawnedTiles(ref state, new Position(2, 2));
        }

        var collector = new BufferedEventCollector();
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };
        system.NotifyBatchElimination(ref state, eliminated, 10, 2.0f, collector);

        var events = collector.GetEvents();
        // Should emit both: DamagedEvent (State→3 progress) + GeneratorActivatedEvent (spawn)
        Assert.Single(events.OfType<ObstacleDamagedEvent>());
        Assert.Single(events.OfType<GeneratorActivatedEvent>());
    }

    [Fact]
    public void Diamond_HasProtectUntil()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();
        float simTime = 1.0f;

        // Accumulate to 3 and spawn
        TriggerAdjacentMatch(ref state, system, new Position(1, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));
        TriggerAdjacentMatch(ref state, system, new Position(3, 2));
        ClearSpawnedTiles(ref state, new Position(2, 2));

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };
        system.NotifyBatchElimination(ref state, eliminated, 0, simTime, NullEventCollector.Instance);

        // Find spawned Diamond and verify protection
        Position[] neighbors = { new(2, 1), new(3, 2), new(2, 3), new(1, 2) };
        foreach (var n in neighbors)
        {
            var tile = state.GetTile(n);
            if (tile.Type == ElementType.Diamond)
            {
                Assert.True(tile.ProtectUntil > simTime,
                    "Spawned Diamond should have protection to prevent same-frame elimination");
                return;
            }
        }
        Assert.Fail("No Diamond found");
    }

    #endregion

    #region LevelConfig serialization

    [Fact]
    public void LevelConfig_WithMagicHat_RoundTrip_PreservesData()
    {
        var original = new LevelConfig(5, 5) { MoveLimit = 15 };
        int idx = 2 * 5 + 3;
        original.Obstacles[idx] = ObstacleType.MagicHat;
        original.ObstacleStages[idx] = 1;

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.MagicHat, parsed.Obstacles[idx]);
        Assert.Equal(1, parsed.ObstacleStages[idx]);
    }

    #endregion

    #region Dedup — same batch multiple adjacencies

    [Fact]
    public void Dedup_MultipleAdjacentEliminations_OnlyOneAccumulation()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.MagicHat, 1));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
            new(new Position(3, 2), new Tile(2, ElementType.Item1, 3, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Dedup: MagicHat should only accumulate once per batch
        Assert.Equal(1, state.GetObstacle(2, 2).State);
    }

    #endregion

    #region Full pipeline

    [Fact]
    public void FullPipeline_MagicHatNeverDestroyed()
    {
        var config = CreateMagicHatLevelConfig();

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

        for (int move = 0; move < 20; move++)
        {
            SimulationTestHelper.SettleCompletely(engine);
            if (!SimulationTestHelper.TryApplyRandomMove(engine, move))
                break;
            SimulationTestHelper.SettleCompletely(engine);

            Assert.True(engine.State.HasObstacle(3, 3),
                $"MagicHat at (3,3) should survive after move {move}");
        }
    }

    [Fact]
    public void FullPipeline_Determinism_SameSeed_SameResult()
    {
        var config = CreateMagicHatLevelConfig();

        var runs = new GameState[2];
        for (int r = 0; r < 2; r++)
        {
            var session = SimulationTestHelper.CreateSession(42, config, tileTypesCount: 5);
            var engine = session.Engine;

            for (int move = 0; move < 15; move++)
            {
                SimulationTestHelper.SettleCompletely(engine);
                if (!SimulationTestHelper.TryApplyRandomMove(engine, move))
                    break;
            }
            SimulationTestHelper.SettleCompletely(engine);
            runs[r] = engine.State;
        }

        SimulationTestHelper.AssertStateEqual(runs[0], runs[1], "MagicHat determinism");
    }

    #endregion

    #region Helpers

    private static void TriggerAdjacentMatch(ref GameState state, ObstacleSystem system, Position elimPos)
    {
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(elimPos, new Tile(1, ElementType.Item1, elimPos.X, elimPos.Y), ElimSource.Match),
        };
        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);
    }

    private static void ClearSpawnedTiles(ref GameState state, Position center)
    {
        Position[] neighbors = { new(center.X, center.Y - 1), new(center.X + 1, center.Y),
                                 new(center.X, center.Y + 1), new(center.X - 1, center.Y) };
        foreach (var n in neighbors)
        {
            if (state.IsValid(n.X, n.Y) && state.GetTile(n).Type != ElementType.None)
                state.SetTile(n.X, n.Y, default);
        }
    }

    private static bool FindDiamond(in GameState state, Position center)
    {
        Position[] neighbors = { new(center.X, center.Y - 1), new(center.X + 1, center.Y),
                                 new(center.X, center.Y + 1), new(center.X - 1, center.Y) };
        foreach (var n in neighbors)
        {
            if (state.IsValid(n.X, n.Y) && state.GetTile(n).Type == ElementType.Diamond)
                return true;
        }
        return false;
    }

    private static int CountDiamonds(in GameState state)
    {
        int count = 0;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                if (state.GetTile(x, y).Type == ElementType.Diamond)
                    count++;
        return count;
    }

    private static void AssertNoDiamond(in GameState state)
    {
        Assert.Equal(0, CountDiamonds(in state));
    }

    private static LevelConfig CreateMagicHatLevelConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        void PlaceMagicHat(int x, int y)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.MagicHat;
            config.ObstacleStages[i] = 1;
        }

        PlaceMagicHat(3, 3);
        PlaceMagicHat(5, 5);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Diamond,
                TargetCount = 3
            },
            default,
            default,
            default
        ];

        return config;
    }

    #endregion
}
