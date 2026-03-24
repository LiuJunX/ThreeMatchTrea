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

namespace Match3.Core.Tests.Systems.Elimination;

file sealed class NullCoverSystem3 : ICoverSystem
{
    public bool IsTileProtected(in GameState state, Position pos) => false;
    public bool TryDamageCover(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) => false;
    public void SyncDynamicCovers(ref GameState state, Position from, Position to) { }
    public void NotifyBatchElimination(ref GameState state, ReadOnlySpan<EliminatedTileInfo> eliminated, int tick, float simTime, IEventCollector events) { }
}

file sealed class NullGroundSystem3 : IGroundSystem
{
    public void OnTileDestroyed(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) { }
}

/// <summary>
/// Tests for Oyster (3-stage, releases Pearl) and Flowerpot (2-stage, spreads 3×3 Grass).
/// </summary>
public class OysterFlowerpotTests
{
    private static GameState CreateState(int w = 7, int h = 7)
        => new(w, h, 5, new StubRandom());

    private static CellEliminator CreateEliminator()
        => new(new NullCoverSystem3(), new NullGroundSystem3());

    #region TileRules

    [Fact]
    public void GetDefaultStage_Oyster_3()
    {
        Assert.Equal(3, TileRules.GetDefaultStage(ElementType.Oyster));
    }

    [Fact]
    public void GetDefaultStage_Flowerpot_2()
    {
        Assert.Equal(2, TileRules.GetDefaultStage(ElementType.Flowerpot));
    }

    [Fact]
    public void CanEliminate_Oyster_AnySource()
    {
        Assert.True(TileRules.CanEliminate(ElementType.Oyster, ElimSource.Match));
        Assert.True(TileRules.CanEliminate(ElementType.Oyster, ElimSource.Bomb));
    }

    [Fact]
    public void CanEliminate_Flowerpot_AnySource()
    {
        Assert.True(TileRules.CanEliminate(ElementType.Flowerpot, ElimSource.Match));
        Assert.True(TileRules.CanEliminate(ElementType.Flowerpot, ElimSource.Bomb));
    }

    #endregion

    #region Oyster — multi-stage + Pearl death effect

    [Fact]
    public void Oyster_ThreeHits_Destroyed()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Oyster, 3, 3) { Stage = 3 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        // Hit 1: 3→2
        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, events);
        Assert.Equal(2, state.GetTile(3, 3).Stage);

        // Hit 2: 2→1
        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 1, 0.1f, events);
        Assert.Equal(1, state.GetTile(3, 3).Stage);

        // Hit 3: 1→0 (destroyed → Pearl released at same position)
        var result = elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 2, 0.2f, events);
        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
        Assert.Equal(ElementType.Pearl, state.GetTile(3, 3).Type); // death effect

        Assert.Equal(2, events.GetEvents().OfType<TileDamagedEvent>().Count());
        Assert.Single(events.GetEvents().OfType<TileDestroyedEvent>());
    }

    [Fact]
    public void Oyster_DeathEffect_ReleasesPearl()
    {
        var state = CreateState();
        // Stage=1 so next hit destroys it
        state.SetTile(3, 3, new Tile(1, ElementType.Oyster, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // Pearl should appear at the same position
        var tile = state.GetTile(3, 3);
        Assert.Equal(ElementType.Pearl, tile.Type);
    }

    [Fact]
    public void Oyster_Pearl_HasProtectUntil()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Oyster, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();
        float simTime = 1.0f;

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, simTime, NullEventCollector.Instance);

        var pearl = state.GetTile(3, 3);
        Assert.Equal(ElementType.Pearl, pearl.Type);
        Assert.True(pearl.ProtectUntil > simTime); // protected from immediate destruction
    }

    [Fact]
    public void Oyster_Pearl_EmitsTileSpawnedEvent()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Oyster, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, events);

        var spawn = events.GetEvents().OfType<TileSpawnedEvent>().SingleOrDefault();
        Assert.NotNull(spawn);
        Assert.Equal(ElementType.Pearl, spawn.Type);
        Assert.Equal(new Position(3, 3), spawn.GridPosition);
    }

    #endregion

    #region Flowerpot — multi-stage + 3×3 Grass death effect

    [Fact]
    public void Flowerpot_TwoHits_Destroyed()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 2 });
        var elim = CreateEliminator();

        // Hit 1: 2→1
        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Match), 0, 0f, NullEventCollector.Instance);
        Assert.Equal(1, state.GetTile(3, 3).Stage);

        // Hit 2: 1→0
        var result = elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Match), 1, 0.1f, NullEventCollector.Instance);
        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
    }

    [Fact]
    public void Flowerpot_DeathEffect_SpreadsGrass3x3()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // 3×3 = 9 cells should have Grass (including center)
        int grassCount = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (state.GetGround(3 + dx, 3 + dy).Type == GroundType.Grass)
                    grassCount++;

        Assert.Equal(9, grassCount);
    }

    [Fact]
    public void Flowerpot_SpreadSkipsExistingGround()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        // Pre-place Grass at one neighbor
        state.SetGround(2, 2, new Ground(GroundType.Grass, 1));
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // (2,2) should still have its original Grass (not overwritten)
        Assert.Equal(GroundType.Grass, state.GetGround(2, 2).Type);
        // Other 8 cells should have new Grass
        int grassCount = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (state.GetGround(3 + dx, 3 + dy).Type == GroundType.Grass)
                    grassCount++;
        Assert.Equal(9, grassCount); // all 9 have Grass (old + new)
    }

    [Fact]
    public void Flowerpot_SpreadSkipsObstacles()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        // Place obstacle at one neighbor
        state.SetObstacle(4, 3, new Obstacle(ObstacleType.Box, 2));
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // (4,3) should NOT have Grass (obstacle blocks)
        Assert.False(state.GetGround(4, 3).HasGround);
        // Other 8 cells should have Grass
        int grassCount = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (state.GetGround(3 + dx, 3 + dy).Type == GroundType.Grass)
                    grassCount++;
        Assert.Equal(8, grassCount);
    }

    [Fact]
    public void Flowerpot_SpreadSkipsVoid()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        // Set one neighbor to Void
        state.SetCell(2, 3, CellKind.Void);
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.False(state.GetGround(2, 3).HasGround);
    }

    [Fact]
    public void Flowerpot_Grass_HasProtectUntil()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();
        float simTime = 1.0f;

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, simTime, NullEventCollector.Instance);

        var grass = state.GetGround(3, 3);
        Assert.Equal(GroundType.Grass, grass.Type);
        Assert.True(grass.ProtectUntil > simTime);
    }

    [Fact]
    public void Flowerpot_EmitsGroundSpawnedEvents()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.Flowerpot, 3, 3) { Stage = 1 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, events);

        var groundEvents = events.GetEvents().OfType<GroundSpawnedEvent>().ToList();
        Assert.Equal(9, groundEvents.Count); // 3×3
    }

    #endregion

    #region Shared behavior

    [Fact]
    public void Oyster_IsMovingObstacle()
    {
        Assert.True(ElementType.Oyster.IsMovingObstacle());
    }

    [Fact]
    public void Flowerpot_IsMovingObstacle()
    {
        Assert.True(ElementType.Flowerpot.IsMovingObstacle());
    }

    [Fact]
    public void BothNotMatchable()
    {
        Assert.False(ElementType.Oyster.IsMatchable());
        Assert.False(ElementType.Flowerpot.IsMatchable());
    }

    [Fact]
    public void BothCannotSwap()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Oyster, 1, 1));
        state.SetTile(2, 1, new Tile(2, ElementType.Item1, 2, 1));

        var cmd = new Match3.Core.Commands.SwapCommand { From = new Position(1, 1), To = new Position(2, 1) };
        Assert.False(cmd.CanExecute(in state));

        state.SetTile(1, 1, new Tile(3, ElementType.Flowerpot, 1, 1));
        cmd = new Match3.Core.Commands.SwapCommand { From = new Position(1, 1), To = new Position(2, 1) };
        Assert.False(cmd.CanExecute(in state));
    }

    [Fact]
    public void BoardInitializer_Oyster_DefaultStage3()
    {
        var config = new Match3.Core.Config.LevelConfig(5, 5);
        config.Grid[2 * 5 + 2] = ElementType.Oyster;

        var state = new GameState(5, 5, 5, new StubRandom());
        new Match3.Core.Systems.Generation.BoardInitializer(
            new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(3, state.GetTile(2, 2).Stage);
    }

    [Fact]
    public void BoardInitializer_Flowerpot_DefaultStage2()
    {
        var config = new Match3.Core.Config.LevelConfig(5, 5);
        config.Grid[2 * 5 + 2] = ElementType.Flowerpot;

        var state = new GameState(5, 5, 5, new StubRandom());
        new Match3.Core.Systems.Generation.BoardInitializer(
            new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(2, state.GetTile(2, 2).Stage);
    }

    #endregion

    #region No death effect for non-death-effect types

    [Fact]
    public void Flowerpot_AtCorner_SpreadsOnlyValidCells()
    {
        var state = CreateState(); // 7×7
        state.SetTile(0, 0, new Tile(1, ElementType.Flowerpot, 0, 0) { Stage = 1 });
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(0, 0), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        // Corner: only 4 cells valid in 3×3 (0,0), (1,0), (0,1), (1,1)
        int grassCount = 0;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                if (state.GetGround(x, y).Type == GroundType.Grass)
                    grassCount++;

        Assert.Equal(4, grassCount);
    }

    [Fact]
    public void RoyalEgg_NoPearl_NoGrass()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(1, ElementType.RoyalEgg, 3, 3));
        var elim = CreateEliminator();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ElementType.None, state.GetTile(3, 3).Type);
        Assert.False(state.GetGround(3, 3).HasGround);
    }

    #endregion
}
