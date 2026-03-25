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
/// Tests for Mailbox generator obstacle: indestructible, spawns Envelope on adjacent elimination.
/// </summary>
public class MailboxTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region ObstacleRules

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.ChainReaction)]
    public void CanHit_Mailbox_ReturnsFalseForAllSources(ElimSource source)
    {
        var obstacle = new Obstacle(ObstacleType.Mailbox, 1);
        Assert.False(ObstacleRules.CanHit(in obstacle, new ElimContext(source)));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.Item2)]
    [InlineData(ElementType.ColorBomb)]
    [InlineData(ElementType.HorizontalRocket)]
    public void CanReactAdjacent_Mailbox_ReturnsTrueForAllTypes(ElementType triggerType)
    {
        var obstacle = new Obstacle(ObstacleType.Mailbox, 1);
        Assert.True(ObstacleRules.CanReactAdjacent(in obstacle, triggerType));
    }

    [Fact]
    public void IsGenerator_Mailbox_ReturnsTrue()
    {
        Assert.True(ObstacleRules.IsGenerator(ObstacleType.Mailbox));
    }

    [Theory]
    [InlineData(ObstacleType.Box)]
    [InlineData(ObstacleType.Bush)]
    [InlineData(ObstacleType.Safe)]
    [InlineData(ObstacleType.Cupboard)]
    public void IsGenerator_NonGenerators_ReturnsFalse(ObstacleType type)
    {
        Assert.False(ObstacleRules.IsGenerator(type));
    }

    [Fact]
    public void GetDefaultStage_Mailbox_Returns1()
    {
        Assert.Equal(1, ObstacleRules.GetDefaultStage(ObstacleType.Mailbox));
    }

    #endregion

    #region TryHit — Mailbox is indestructible

    [Fact]
    public void TryHit_Mailbox_Blocked()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage); // undamaged
    }

    #endregion

    #region Adjacent match → spawn Envelope

    [Fact]
    public void AdjacentMatch_SpawnsEnvelopeAtEmptyNeighbor()
    {
        var state = CreateState();
        // Mailbox at (2,2), tile eliminated at (1,2) — adjacent
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 1.0f, NullEventCollector.Instance);

        // Mailbox should still exist with Stage=1
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);

        // An Envelope should be spawned somewhere adjacent to (2,2)
        bool envelopeFound = false;
        Position[] neighbors = { new(2, 1), new(3, 2), new(2, 3), new(1, 2) };
        foreach (var n in neighbors)
        {
            if (state.GetTile(n).Type == ElementType.Envelope)
            {
                envelopeFound = true;
                break;
            }
        }
        Assert.True(envelopeFound, "Envelope should be spawned at an adjacent empty cell");
    }

    [Fact]
    public void AdjacentMatch_MailboxStageUnchanged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Mailbox is permanent — never decremented
        Assert.Equal(ObstacleType.Mailbox, state.GetObstacle(2, 2).Type);
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);
    }

    #endregion

    #region No empty neighbor → no spawn

    [Fact]
    public void ThreeNeighborsOccupied_SpawnsAtOnlyEmptySlot()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        // Fill 3 neighbors with tiles, leave (1,2) empty via elimination
        state.SetTile(2, 1, new Tile(10, ElementType.Item2, 2, 1));
        state.SetTile(3, 2, new Tile(11, ElementType.Item3, 3, 2));
        state.SetTile(2, 3, new Tile(12, ElementType.Item4, 2, 3));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Eliminate from (1,2) — the only empty neighbor after elimination
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(13, ElementType.Item5, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.True(state.HasObstacle(2, 2)); // Mailbox still present
        // Envelope spawned at (1,2) — the only available slot
        Assert.Equal(ElementType.Envelope, state.GetTile(1, 2).Type);
    }

    [Fact]
    public void ThreeNeighborsObstacles_SpawnsAtOnlyEmptySlot()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.SetObstacle(2, 1, new Obstacle(ObstacleType.Box, 2));
        state.SetObstacle(3, 2, new Obstacle(ObstacleType.Box, 2));
        state.SetObstacle(2, 3, new Obstacle(ObstacleType.Box, 2));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // (1,2) is the only empty neighbor
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ElementType.Envelope, state.GetTile(1, 2).Type);
    }

    #endregion

    #region Dedup — multiple eliminated tiles adjacent to same Mailbox

    [Fact]
    public void Dedup_MultipleAdjacentEliminations_OnlyOneEnvelope()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Two tiles eliminated adjacent to the same Mailbox in the same batch
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
            new(new Position(3, 2), new Tile(2, ElementType.Item1, 3, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Dedup: Mailbox at (2,2) should only produce 1 Envelope
        int envelopeCount = 0;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                if (state.GetTile(x, y).Type == ElementType.Envelope)
                    envelopeCount++;

        Assert.Equal(1, envelopeCount);
    }

    #endregion

    #region Envelope properties

    [Fact]
    public void Envelope_IsCollectible()
    {
        Assert.True(ElementType.Envelope.IsCollectible());
    }

    [Fact]
    public void Envelope_IsNotMatchable()
    {
        Assert.False(ElementType.Envelope.IsMatchable());
        Assert.False(ElementType.Envelope.IsColor());
    }

    [Fact]
    public void Envelope_IsNotBomb()
    {
        Assert.False(ElementType.Envelope.IsBomb());
    }

    [Fact]
    public void Envelope_HasProtectUntil()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();
        float simTime = 1.0f;

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, simTime, NullEventCollector.Instance);

        // Find the spawned Envelope
        Position[] neighbors = { new(2, 1), new(3, 2), new(2, 3), new(1, 2) };
        foreach (var n in neighbors)
        {
            var tile = state.GetTile(n);
            if (tile.Type == ElementType.Envelope)
            {
                Assert.True(tile.ProtectUntil > simTime,
                    "Spawned Envelope should have protection to prevent same-frame elimination");
                return;
            }
        }
        Assert.Fail("No Envelope found");
    }

    #endregion

    #region Events

    [Fact]
    public void AdjacentMatch_EmitsGeneratorActivatedAndTileSpawnedEvents()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();
        var collector = new BufferedEventCollector();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 5, 1.5f, collector);

        var events = collector.GetEvents();

        var genEvt = Assert.Single(events.OfType<GeneratorActivatedEvent>());
        Assert.Equal(new Position(2, 2), genEvt.GridPosition);
        Assert.Equal(ObstacleType.Mailbox, genEvt.ObstacleType);
        Assert.Equal(ElementType.Envelope, genEvt.ProductType);
        Assert.Equal(5, genEvt.Tick);

        var spawnEvt = Assert.Single(events.OfType<TileSpawnedEvent>());
        Assert.Equal(ElementType.Envelope, spawnEvt.Type);
        Assert.Equal(genEvt.ProductPosition, spawnEvt.GridPosition);
        Assert.Equal(100, spawnEvt.TileId);
    }

    [Fact]
    public void NoEmptyNeighbor_NoEventsEmitted()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.SetTile(2, 1, new Tile(10, ElementType.Item2, 2, 1));
        state.SetTile(3, 2, new Tile(11, ElementType.Item3, 3, 2));
        state.SetTile(2, 3, new Tile(12, ElementType.Item4, 2, 3));
        state.SetTile(1, 2, new Tile(13, ElementType.Item5, 1, 2));
        var system = new ObstacleSystem();
        var collector = new BufferedEventCollector();

        // To trigger: we need adjacency but all neighbors occupied.
        // We'll use a trick: state has tiles, but the eliminated tile info is separate.
        // The NotifyBatchElimination doesn't check if the tile is actually gone from state.
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(13, ElementType.Item5, 1, 2), ElimSource.Match),
        };
        // (1,2) still has the tile in state, so FindEmptyAdjacentSlot won't find a slot

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, collector);

        Assert.Empty(collector.GetEvents().OfType<GeneratorActivatedEvent>());
        Assert.Empty(collector.GetEvents().OfType<TileSpawnedEvent>());
    }

    #endregion

    #region Edge / corner — Mailbox at board edge

    [Fact]
    public void Mailbox_AtCorner_SpawnsInAvailableNeighbor()
    {
        var state = CreateState();
        // Mailbox at corner (0,0) — only 2 neighbors: (1,0) and (0,1)
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Eliminate from (1,0) — adjacent to (0,0)
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 0), new Tile(1, ElementType.Item1, 1, 0), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Envelope should spawn at one of the 2 valid neighbors
        bool found = state.GetTile(1, 0).Type == ElementType.Envelope
                  || state.GetTile(0, 1).Type == ElementType.Envelope;
        Assert.True(found, "Envelope should spawn at an available neighbor of corner Mailbox");
    }

    [Fact]
    public void Mailbox_AtEdge_FewerNeighbors_NoOutOfBounds()
    {
        var state = CreateState();
        // Mailbox at top edge (2,0)
        state.SetObstacle(2, 0, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 0), new Tile(1, ElementType.Item1, 1, 0), ElimSource.Match),
        };

        // Should not throw
        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);
        Assert.True(state.HasObstacle(2, 0));
    }

    #endregion

    #region Multiple triggers — separate batches → separate Envelopes

    [Fact]
    public void TwoSeparateBatches_TwoEnvelopes()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Batch 1
        var eliminated1 = new[]
        {
            new EliminatedTileInfo(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match)
        };
        system.NotifyBatchElimination(ref state, eliminated1, 0, 0f, NullEventCollector.Instance);

        // Batch 2 — from a different adjacent position
        var eliminated2 = new[]
        {
            new EliminatedTileInfo(new Position(3, 2), new Tile(2, ElementType.Item2, 3, 2), ElimSource.Match)
        };
        system.NotifyBatchElimination(ref state, eliminated2, 1, 0.1f, NullEventCollector.Instance);

        // Count Envelopes
        int envelopeCount = 0;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                if (state.GetTile(x, y).Type == ElementType.Envelope)
                    envelopeCount++;

        Assert.Equal(2, envelopeCount);
    }

    #endregion

    #region Power-up sources also trigger generator (Pass 2)

    [Fact]
    public void BombSource_TriggersGenerator()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        // Bomb source — not an AdjacentSource, but should trigger generator via Pass 2
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Bomb),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Envelope should be spawned
        bool envelopeFound = false;
        Position[] neighbors = { new(2, 1), new(3, 2), new(2, 3), new(1, 2) };
        foreach (var n in neighbors)
        {
            if (state.GetTile(n).Type == ElementType.Envelope)
            {
                envelopeFound = true;
                break;
            }
        }
        Assert.True(envelopeFound, "Bomb source should trigger generator via Pass 2");
    }

    [Fact]
    public void ProjectileSource_TriggersGenerator()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Projectile),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        bool envelopeFound = false;
        Position[] neighbors = { new(2, 1), new(3, 2), new(2, 3), new(1, 2) };
        foreach (var n in neighbors)
        {
            if (state.GetTile(n).Type == ElementType.Envelope)
            {
                envelopeFound = true;
                break;
            }
        }
        Assert.True(envelopeFound, "Projectile source should trigger generator via Pass 2");
    }

    [Fact]
    public void MatchSource_NoDoubleActivation()
    {
        // Match source triggers via Pass 1 (adjacent reactions).
        // Pass 2 should skip because the generator was already in the dedup set.
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Only 1 Envelope — not double-activated
        int envelopeCount = 0;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                if (state.GetTile(x, y).Type == ElementType.Envelope)
                    envelopeCount++;

        Assert.Equal(1, envelopeCount);
    }

    #endregion

    #region FindEmptyAdjacentSlot

    [Fact]
    public void FindEmptyAdjacentSlot_PrefersUpRightDownLeft()
    {
        var state = CreateState();
        // All 4 neighbors empty — should return "up" (center.Y - 1)
        var result = ObstacleSystem.FindEmptyAdjacentSlot(in state, new Position(2, 2));
        Assert.NotNull(result);
        Assert.Equal(new Position(2, 1), result!.Value); // up
    }

    [Fact]
    public void FindEmptyAdjacentSlot_SkipsVoidCells()
    {
        var state = CreateState();
        state.SetCell(2, 1, CellKind.Void); // up is void
        var result = ObstacleSystem.FindEmptyAdjacentSlot(in state, new Position(2, 2));
        Assert.NotNull(result);
        Assert.Equal(new Position(3, 2), result!.Value); // next: right
    }

    [Fact]
    public void FindEmptyAdjacentSlot_SkipsTilesAndObstacles()
    {
        var state = CreateState();
        state.SetTile(2, 1, new Tile(1, ElementType.Item1, 2, 1));    // up has tile
        state.SetObstacle(3, 2, new Obstacle(ObstacleType.Box, 1));    // right has obstacle
        var result = ObstacleSystem.FindEmptyAdjacentSlot(in state, new Position(2, 2));
        Assert.NotNull(result);
        Assert.Equal(new Position(2, 3), result!.Value); // down
    }

    [Fact]
    public void FindEmptyAdjacentSlot_AllBlocked_ReturnsNull()
    {
        var state = CreateState();
        state.SetTile(2, 1, new Tile(1, ElementType.Item1, 2, 1));
        state.SetTile(3, 2, new Tile(2, ElementType.Item2, 3, 2));
        state.SetTile(2, 3, new Tile(3, ElementType.Item3, 2, 3));
        state.SetTile(1, 2, new Tile(4, ElementType.Item4, 1, 2));
        var result = ObstacleSystem.FindEmptyAdjacentSlot(in state, new Position(2, 2));
        Assert.Null(result);
    }

    #endregion

    #region LevelConfig serialization

    [Fact]
    public void LevelConfig_WithMailbox_RoundTrip_PreservesData()
    {
        var original = new LevelConfig(5, 5) { MoveLimit = 15 };
        int idx = 2 * 5 + 3;
        original.Obstacles[idx] = ObstacleType.Mailbox;
        original.ObstacleStages[idx] = 1;

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Mailbox, parsed.Obstacles[idx]);
        Assert.Equal(1, parsed.ObstacleStages[idx]);
    }

    [Fact]
    public void ParseLevelConfig_MailboxJson_DeserializesCorrectly()
    {
        const string json = """
        {
            "width": 3,
            "height": 3,
            "obstacles": ["None","Mailbox","None", "None","None","None", "None","None","None"]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObstacleType.Mailbox, config.Obstacles[1]);
        Assert.Equal(ObstacleType.None, config.Obstacles[0]);
    }

    #endregion

    #region BoardInitializer

    [Fact]
    public void BoardInitializer_PlacesMailboxFromConfig()
    {
        var config = new LevelConfig(5, 5);
        int idx = 2 * 5 + 3;
        config.Obstacles[idx] = ObstacleType.Mailbox;
        config.ObstacleStages[idx] = 1;

        var state = new GameState(5, 5, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(3, 2));
        var obstacle = state.GetObstacle(3, 2);
        Assert.Equal(ObstacleType.Mailbox, obstacle.Type);
        Assert.Equal(1, obstacle.Stage);
        Assert.Equal(ElementType.None, state.GetTile(3, 2).Type);
    }

    [Fact]
    public void BoardInitializer_MailboxDefaultStage_Is1()
    {
        var config = new LevelConfig(3, 3);
        config.Obstacles[4] = ObstacleType.Mailbox;
        // ObstacleStages[4] left as 0 → should default to 1

        var state = new GameState(3, 3, 5, new StubRandom());
        var initializer = new BoardInitializer(new StubTileGenerator());
        initializer.Initialize(ref state, config);

        Assert.True(state.HasObstacle(1, 1));
        Assert.Equal(1, state.GetObstacle(1, 1).Stage);
    }

    #endregion

    #region Mixed scenario — Mailbox + Box coexistence

    [Fact]
    public void MixedScene_MailboxAndBox_IndependentBehavior()
    {
        var state = CreateState();
        state.SetObstacle(1, 2, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(3, 2, new Obstacle(ObstacleType.Mailbox, 1));
        state.NextTileId = 100;

        var collector = new BufferedEventCollector();
        var system = new ObstacleSystem();

        // Eliminate tile at (2,2) — adjacent to both
        var eliminated = new[]
        {
            new EliminatedTileInfo(new Position(2, 2), new Tile(1, ElementType.Item1, 2, 2), ElimSource.Match)
        };
        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, collector);

        // Box at (1,2) should be destroyed (stage 1 → 0)
        Assert.False(state.HasObstacle(1, 2));

        // Mailbox at (3,2) should still exist, Stage unchanged
        Assert.True(state.HasObstacle(3, 2));
        Assert.Equal(1, state.GetObstacle(3, 2).Stage);

        // Envelope should be spawned adjacent to Mailbox
        var events = collector.GetEvents();
        Assert.Single(events.OfType<ObstacleDestroyedEvent>());
        Assert.Single(events.OfType<GeneratorActivatedEvent>());
    }

    #endregion

    #region Envelope objective tracking

    [Fact]
    public void EnvelopeObjective_InitializesCorrectly()
    {
        var config = CreateMailboxLevelConfig();

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

        Assert.Equal(ObjectiveTargetLayer.Tile, state.ObjectiveProgress[0].TargetLayer);
        Assert.Equal((int)ElementType.Envelope, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(2, state.ObjectiveProgress[0].TargetCount);
    }

    #endregion

    #region Full pipeline — determinism & invariant

    [Fact]
    [Trait("Category", "Slow")]
    public void FullPipeline_MailboxNeverDestroyed_EnvelopesSpawned()
    {
        var config = CreateMailboxLevelConfig();

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

            // Invariant: Mailboxes are never destroyed
            Assert.True(engine.State.HasObstacle(3, 3),
                $"Mailbox at (3,3) should survive after move {move}");
            Assert.True(engine.State.HasObstacle(5, 5),
                $"Mailbox at (5,5) should survive after move {move}");

            // Invariant: no tile at obstacle position
            Assert.Equal(ElementType.None, engine.State.GetTile(3, 3).Type);
            Assert.Equal(ElementType.None, engine.State.GetTile(5, 5).Type);
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void FullPipeline_Determinism_SameSeed_SameResult()
    {
        var config = CreateMailboxLevelConfig();

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

        SimulationTestHelper.AssertStateEqual(runs[0], runs[1], "Mailbox determinism");
    }

    #endregion

    #region Helpers

    private static LevelConfig CreateMailboxLevelConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        void PlaceMailbox(int x, int y)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.Mailbox;
            config.ObstacleStages[i] = 1;
        }

        PlaceMailbox(3, 3);
        PlaceMailbox(5, 5);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Envelope,
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
