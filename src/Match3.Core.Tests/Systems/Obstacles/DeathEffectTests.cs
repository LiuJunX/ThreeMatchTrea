using System;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Tests for D4: obstacle death effects (spread ground, release tile, ProtectUntil).
/// </summary>
public class DeathEffectTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region Bush → Grass spread

    [Fact]
    public void Bush_Death_SpreadsGrassToFourNeighbors()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 1.0f, NullEventCollector.Instance);

        // Bush destroyed → 4 neighbors should have Grass
        Assert.Equal(GroundType.Grass, state.GetGround(1, 2).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(2, 1).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(2, 3).Type);
    }

    [Fact]
    public void Bush_Damaged_NoGrassSpread()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 3));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // Still alive — no grass
        Assert.False(state.GetGround(1, 2).HasGround);
        Assert.False(state.GetGround(3, 2).HasGround);
    }

    [Fact]
    public void Bush_Death_SkipsVoidCells()
    {
        var state = CreateState();
        state.SetCell(1, 2, CellKind.Void);
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.False(state.GetGround(1, 2).HasGround); // Void — skipped
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type); // Slot — spread
    }

    [Fact]
    public void Bush_Death_SkipsExistingGround()
    {
        var state = CreateState();
        state.SetGround(1, 2, new Ground(GroundType.Ice, 1));
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // Ice is not overwritten
        Assert.Equal(GroundType.Ice, state.GetGround(1, 2).Type);
    }

    [Fact]
    public void Bush_Death_SkipsObstacleOccupiedCells()
    {
        var state = CreateState();
        state.SetObstacle(1, 2, new Obstacle(ObstacleType.Box, 2));
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // (1,2) has Box — Grass not placed
        Assert.False(state.GetGround(1, 2).HasGround);
        // (3,2) empty — Grass placed
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type);
    }

    [Fact]
    public void Bush_Death_AtEdge_SkipsOutOfBounds()
    {
        var state = CreateState();
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(0, 0),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // Only 2 valid neighbors: (1,0) and (0,1)
        Assert.Equal(GroundType.Grass, state.GetGround(1, 0).Type);
        Assert.Equal(GroundType.Grass, state.GetGround(0, 1).Type);
    }

    [Fact]
    public void Bush_Death_EmitsGroundSpawnedEvents()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 5, 1.0f, events);

        var allEvents = events.GetEvents();
        var spawnEvents = allEvents.OfType<GroundSpawnedEvent>().ToList();
        Assert.Equal(4, spawnEvents.Count);
        Assert.All(spawnEvents, e =>
        {
            Assert.Equal(GroundType.Grass, e.Type);
            Assert.Equal(new Position(2, 2), e.SourcePosition);
            Assert.Equal(5, e.Tick);
        });
    }

    [Fact]
    public void Bush_Death_AdjacentPath_AlsoTriggersEffect()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        // Trigger via adjacent reaction (not direct hit)
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Bush destroyed via adjacency → Grass spread should trigger
        Assert.False(state.HasObstacle(2, 2));
        Assert.Equal(GroundType.Grass, state.GetGround(3, 2).Type);
    }

    #endregion

    #region Grass ProtectUntil

    [Fact]
    public void Grass_ProtectUntil_BlocksDamage()
    {
        var state = CreateState();
        // Simulate Bush death at simTime=1.0
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 1.0f, NullEventCollector.Instance);

        // Grass at (1,2) should have ProtectUntil = 1.0 + 0.4 = 1.4
        ref var grass = ref state.GetGround(1, 2);
        Assert.Equal(GroundType.Grass, grass.Type);
        Assert.True(grass.ProtectUntil > 1.0f);

        // Try to damage ground at simTime=1.2 (within protection)
        var groundSystem = new GroundSystem();
        groundSystem.OnTileDestroyed(ref state, new Position(1, 2), 1, 1.2f, NullEventCollector.Instance);

        // Grass should survive
        Assert.Equal(GroundType.Grass, state.GetGround(1, 2).Type);
        Assert.Equal(1, state.GetGround(1, 2).Health);
    }

    [Fact]
    public void Grass_ProtectUntil_DamageAfterExpiry()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 1.0f, NullEventCollector.Instance);

        // Try to damage ground at simTime=2.0 (after protection expires)
        var groundSystem = new GroundSystem();
        groundSystem.OnTileDestroyed(ref state, new Position(1, 2), 1, 2.0f, NullEventCollector.Instance);

        // Grass should be destroyed
        Assert.False(state.GetGround(1, 2).HasGround);
    }

    [Fact]
    public void Ice_NoProtection_DamagedNormally()
    {
        var state = CreateState();
        state.SetGround(2, 2, new Ground(GroundType.Ice, 1));
        var groundSystem = new GroundSystem();

        groundSystem.OnTileDestroyed(ref state, new Position(2, 2), 0, 0f, NullEventCollector.Instance);

        // Ice should be destroyed (no protection)
        Assert.False(state.GetGround(2, 2).HasGround);
    }

    #endregion

    #region Tile ProtectUntil — matching immunity

    [Fact]
    public void ProtectedTile_ExcludedFromMatching()
    {
        var state = CreateState();
        state.SimulationTime = 1.0f;

        // Place a protected tile (simulates released Pearl)
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { ProtectUntil = 1.5f });

        // CanMatch should return false during protection
        Assert.False(state.CanMatch(2, 2));
    }

    [Fact]
    public void ProtectedTile_MatchableAfterExpiry()
    {
        var state = CreateState();
        state.SimulationTime = 2.0f;

        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { ProtectUntil = 1.5f });

        // After protection expires, tile is matchable
        Assert.True(state.CanMatch(2, 2));
    }

    [Fact]
    public void UnprotectedTile_AlwaysMatchable()
    {
        var state = CreateState();
        state.SimulationTime = 0f;

        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2));

        // Default ProtectUntil=0, SimulationTime=0 → 0 > 0 is false → matchable
        Assert.True(state.CanMatch(2, 2));
    }

    [Fact]
    public void ProtectedTile_MatchableAtExactExpiry()
    {
        var state = CreateState();
        state.SimulationTime = 1.5f;

        // ProtectUntil == SimulationTime → exclusive upper bound → matchable
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { ProtectUntil = 1.5f });

        Assert.True(state.CanMatch(2, 2));
    }

    #endregion

    #region Tile ProtectUntil — CellEliminator guard

    [Fact]
    public void ProtectedTile_BlockedByCellEliminator()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { ProtectUntil = 1.5f });
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Bomb, 0, 1.0f, NullEventCollector.Instance);

        // Protected — should be blocked
        Assert.Equal(EliminateOutcome.Blocked, result.Outcome);
        Assert.Equal(ElementType.Item1, state.GetTile(2, 2).Type); // Tile still there
    }

    [Fact]
    public void ProtectedTile_DestroyableAfterExpiry()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { ProtectUntil = 1.5f });
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Bomb, 0, 2.0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
    }

    #endregion

    #region ReleaseToSelf

    [Fact]
    public void ReleaseToSelf_CreatesProtectedTile()
    {
        var state = CreateState();
        state.NextTileId = 100;

        ObstacleSystem.ReleaseToSelf(ref state, new Position(2, 2),
            ElementType.Pearl, 1.0f, 5, NullEventCollector.Instance);

        var tile = state.GetTile(2, 2);
        Assert.Equal(ElementType.Pearl, tile.Type);
        Assert.Equal(100, tile.Id);
        Assert.Equal(1.0f + ObstacleSystem.TileProtectDuration, tile.ProtectUntil);
        Assert.Equal(101, state.NextTileId); // Incremented
    }

    [Fact]
    public void ReleaseToSelf_SkipsOccupiedCell()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2));

        ObstacleSystem.ReleaseToSelf(ref state, new Position(2, 2),
            ElementType.Pearl, 1.0f, 0, NullEventCollector.Instance);

        // Tile not overwritten
        Assert.Equal(ElementType.Item1, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void ReleaseToSelf_EmitsTileSpawnedEvent()
    {
        var state = CreateState();
        state.NextTileId = 50;
        var events = new BufferedEventCollector();

        ObstacleSystem.ReleaseToSelf(ref state, new Position(2, 2),
            ElementType.Plate, 1.0f, 5, events);

        var allEvents = events.GetEvents();
        var spawnEvt = Assert.Single(allEvents.OfType<TileSpawnedEvent>());
        Assert.Equal(50, spawnEvt.TileId);
        Assert.Equal(new Position(2, 2), spawnEvt.GridPosition);
        Assert.Equal(ElementType.Plate, spawnEvt.Type);
    }

    #endregion

    #region Two adjacent Bushes die — dedup & no overwrite

    [Fact]
    public void TwoAdjacentBushes_SharedNeighborGetsGrassOnce()
    {
        var state = CreateState();
        // Two Bushes at (1,2) and (3,2) — shared neighbor is (2,2)
        state.SetObstacle(1, 2, new Obstacle(ObstacleType.Bush, 1));
        state.SetObstacle(3, 2, new Obstacle(ObstacleType.Bush, 1));
        var system = new ObstacleSystem();

        // Kill both via direct hit
        system.TryHit(ref state, new Position(1, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);
        system.TryHit(ref state, new Position(3, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // (2,2) should have Grass exactly once (from whichever Bush died first)
        Assert.Equal(GroundType.Grass, state.GetGround(2, 2).Type);
        Assert.Equal(1, state.GetGround(2, 2).Health);
    }

    #endregion

    #region Non-Bush obstacles don't spread

    [Fact]
    public void Box_Death_NoDeathEffect()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // No ground spawned
        Assert.False(state.GetGround(1, 2).HasGround);
        Assert.False(state.GetGround(3, 2).HasGround);
    }

    [Fact]
    public void Safe_Death_NoDeathEffect()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.False(state.GetGround(1, 2).HasGround);
    }

    #endregion

    #region GroundRules — new types

    [Fact]
    public void GroundRules_GrassDefaultHealth_Is1()
    {
        Assert.Equal(1, GroundRules.GetDefaultHealth(GroundType.Grass));
    }

    [Fact]
    public void GroundRules_LeavesDefaultHealth_Is1()
    {
        Assert.Equal(1, GroundRules.GetDefaultHealth(GroundType.Leaves));
    }

    #endregion

    #region ElementType extensions — new collectibles

    [Fact]
    public void Pearl_IsCollectible()
    {
        Assert.True(ElementType.Pearl.IsCollectible());
        Assert.False(ElementType.Pearl.IsColor());
        Assert.False(ElementType.Pearl.IsBomb());
        Assert.False(ElementType.Pearl.IsMatchable());
    }

    [Fact]
    public void Plate_IsCollectible()
    {
        Assert.True(ElementType.Plate.IsCollectible());
    }

    #endregion
}
