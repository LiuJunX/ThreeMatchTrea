using System;
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

public class ObstacleSystemTests
{
    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region TryHit — Direct hit path

    [Fact]
    public void TryHit_NoObstacle_ReturnsNoObstacle()
    {
        var state = CreateState();
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.NoObstacle, result);
    }

    [Fact]
    public void TryHit_Box_Bomb_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 4));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Damaged, result);
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void TryHit_Box_LastHP_Destroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Destroyed, result);
        Assert.False(state.HasObstacle(2, 2));
    }

    [Fact]
    public void TryHit_Safe_Match_Blocked()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 5));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.Equal(5, state.GetObstacle(2, 2).Stage); // undamaged
    }

    [Fact]
    public void TryHit_Safe_Bomb_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 5));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Damaged, result);
        Assert.Equal(4, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void TryHit_ColorBox_Match_Blocked()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void TryHit_ColorBox_Bomb_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Damaged, result);
        Assert.Equal(2, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void TryHit_ColorBox_ThreeHits_Destroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);
        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Projectile), 0, 0f, NullEventCollector.Instance);
        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Destroyed, result);
        Assert.False(state.HasObstacle(2, 2));
    }

    [Fact]
    public void TryHit_EmitsObstacleDamagedEvent()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 3));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 5, 1.5f, events);

        var allEvents = events.GetEvents();
        var evt = Assert.Single(allEvents, e => e is ObstacleDamagedEvent) as ObstacleDamagedEvent;
        Assert.NotNull(evt);
        Assert.Equal(new Position(2, 2), evt!.GridPosition);
        Assert.Equal(ObstacleType.Box, evt.Type);
        Assert.Equal(2, evt.RemainingStage);
        Assert.Equal(5, evt.Tick);
    }

    [Fact]
    public void TryHit_EmitsObstacleDestroyedEvent()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 1));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 5, 1.5f, events);

        var allEvents = events.GetEvents();
        var evt = Assert.Single(allEvents, e => e is ObstacleDestroyedEvent) as ObstacleDestroyedEvent;
        Assert.NotNull(evt);
        Assert.Equal(new Position(2, 2), evt!.GridPosition);
        Assert.Equal(ObstacleType.Box, evt.Type);
    }

    #endregion

    #region NotifyBatchElimination — Adjacent reaction path

    [Fact]
    public void NotifyBatch_AdjacentBox_DamagedOnce()
    {
        var state = CreateState();
        // Box at (2,2), tiles eliminated at (1,2) and (3,2) — both adjacent
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 4));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
            new(new Position(3, 2), new Tile(2, ElementType.Item1, 3, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Dedup: Box should only take 1 damage despite 2 adjacent eliminations
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void NotifyBatch_ColorBox_MatchingColor_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(2, state.GetObstacle(2, 2).Stage); // 3→2
    }

    [Fact]
    public void NotifyBatch_ColorBox_WrongColor_Unaffected()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item2, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(3, state.GetObstacle(2, 2).Stage); // undamaged
    }

    [Fact]
    public void NotifyBatch_ColorBox_ColorBombWildcard_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item3));
        var system = new ObstacleSystem();

        // ColorBomb tile at (1,2) — acts as wildcard
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.ColorBomb, 1, 2), ElimSource.ConsumeBomb),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(2, state.GetObstacle(2, 2).Stage); // 3→2
    }

    [Fact]
    public void NotifyBatch_ColorBox_ThreeColorMatches_Destroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        // Three separate batches of matching-color adjacent eliminations
        for (int i = 0; i < 3; i++)
        {
            Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
            {
                new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
            };
            system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);
        }

        Assert.False(state.HasObstacle(2, 2)); // destroyed after 3 hits
    }

    [Fact]
    public void NotifyBatch_ColorBox_BombSource_NoAdjacentDamage()
    {
        var state = CreateState();
        // Same-color tile eliminated by Bomb next to ColorBox — Bomb doesn't trigger adjacency
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Bomb),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(3, state.GetObstacle(2, 2).Stage); // undamaged — Bomb doesn't trigger adjacency
    }

    [Fact]
    public void NotifyBatch_Safe_NotAffectedByAdjacentMatch()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 5));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(5, state.GetObstacle(2, 2).Stage); // undamaged
    }

    [Fact]
    public void NotifyBatch_BombSource_DoesNotTriggerAdjacent()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 4));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Bomb),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(4, state.GetObstacle(2, 2).Stage); // undamaged — Bomb doesn't trigger adjacency
    }

    [Fact]
    public void NotifyBatch_NonAdjacentObstacle_Unaffected()
    {
        var state = CreateState();
        // Box at (4,4), tile eliminated at (1,1) — not adjacent
        state.SetObstacle(4, 4, new Obstacle(ObstacleType.Box, 4));
        var system = new ObstacleSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[]
        {
            new(new Position(1, 1), new Tile(1, ElementType.Item1, 1, 1), ElimSource.Match),
        };

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(4, state.GetObstacle(4, 4).Stage);
    }

    #endregion

    #region NotifyGlobalColorElimination — Curtain path

    [Fact]
    public void NotifyGlobal_Curtain_MatchingColor_Damaged()
    {
        var state = CreateState();
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Curtain, 2, (byte)ElementType.Item1));
        state.SetObstacle(4, 4, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(1, state.GetObstacle(0, 0).Stage);     // damaged
        Assert.False(state.HasObstacle(new Position(4, 4))); // destroyed
    }

    [Fact]
    public void NotifyGlobal_Curtain_WrongColor_Unaffected()
    {
        var state = CreateState();
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Curtain, 2, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item2, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(2, state.GetObstacle(0, 0).Stage); // undamaged
    }

    [Fact]
    public void NotifyGlobal_NonCurtainObstacle_Unaffected()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 4));
        var system = new ObstacleSystem();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(4, state.GetObstacle(2, 2).Stage);
    }

    #endregion

    #region CellEliminator integration — Obstacle guard

    [Fact]
    public void CellEliminator_ObstacleGuard_Box_Bomb_ReturnsDamaged()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 3));
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem(),
            obstacleSystem: new ObstacleSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Bomb, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.ObstacleDamaged, result.Outcome);
        Assert.Equal(2, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void CellEliminator_ObstacleGuard_Box_LastHP_ReturnsDestroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 1));
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem(),
            obstacleSystem: new ObstacleSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Bomb, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.ObstacleDestroyed, result.Outcome);
        Assert.False(state.HasObstacle(2, 2));
    }

    [Fact]
    public void CellEliminator_ObstacleGuard_Safe_Match_Blocked()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 5));
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem(),
            obstacleSystem: new ObstacleSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Match, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public void CellEliminator_NoObstacleSystem_SkipsGuard()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2));
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 3));
        // No obstacle system injected — guard is skipped
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Match, 0, 0f, NullEventCollector.Instance);

        // Falls through to tile elimination
        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
    }

    [Fact]
    public void CellEliminator_CoverAbsorbsBeforeObstacle()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2));
        state.SetCover(2, 2, new Cover(CoverType.Cage, 1));
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Box, 3));
        var eliminator = new CellEliminator(
            new CoverSystem(), new GroundSystem(),
            obstacleSystem: new ObstacleSystem());

        var result = eliminator.Eliminate(ref state, new Position(2, 2),
            ElimSource.Bomb, 0, 0f, NullEventCollector.Instance);

        // Cover absorbs first, obstacle untouched
        Assert.Equal(EliminateOutcome.Absorbed, result.Outcome);
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    #endregion
}
