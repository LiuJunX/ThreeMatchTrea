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

file sealed class NullCoverSystem2 : ICoverSystem
{
    public bool IsTileProtected(in GameState state, Position pos) => false;
    public bool TryDamageCover(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) => false;
    public void SyncDynamicCovers(ref GameState state, Position from, Position to) { }
    public void NotifyBatchElimination(ref GameState state, ReadOnlySpan<EliminatedTileInfo> eliminated, int tick, float simTime, IEventCollector events) { }
}

file sealed class NullGroundSystem2 : IGroundSystem
{
    public void OnTileDestroyed(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) { }
}

/// <summary>
/// Tests for Moving Obstacle tiles: RoyalEgg, Vase, PorcelainPiggy.
/// Covers TileRules, CellEliminator Stage guard, swap guard, and match exclusion.
/// </summary>
public class MovingObstacleTests
{
    private static GameState CreateState(int w = 5, int h = 5)
        => new(w, h, 5, new StubRandom());

    private static CellEliminator CreateEliminator()
        => new(new NullCoverSystem2(), new NullGroundSystem2());

    #region TileRules — CanEliminate

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ColorBomb)]
    public void CanEliminate_RoyalEgg_AnySource_True(ElimSource source)
    {
        Assert.True(TileRules.CanEliminate(ElementType.RoyalEgg, source));
    }

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ColorBomb)]
    public void CanEliminate_Vase_AnySource_True(ElimSource source)
    {
        Assert.True(TileRules.CanEliminate(ElementType.Vase, source));
    }

    [Fact]
    public void CanEliminate_PorcelainPiggy_Match_False()
    {
        Assert.False(TileRules.CanEliminate(ElementType.PorcelainPiggy, ElimSource.Match));
    }

    [Theory]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ChainReaction)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.SideItem)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanEliminate_PorcelainPiggy_AllPowerUps_True(ElimSource source)
    {
        Assert.True(TileRules.CanEliminate(ElementType.PorcelainPiggy, source));
    }

    #endregion

    #region TileRules — GetDefaultStage

    [Fact]
    public void GetDefaultStage_Vase_2()
    {
        Assert.Equal(2, TileRules.GetDefaultStage(ElementType.Vase));
    }

    [Fact]
    public void GetDefaultStage_RoyalEgg_1()
    {
        Assert.Equal(1, TileRules.GetDefaultStage(ElementType.RoyalEgg));
    }

    [Fact]
    public void GetDefaultStage_PorcelainPiggy_1()
    {
        Assert.Equal(1, TileRules.GetDefaultStage(ElementType.PorcelainPiggy));
    }

    #endregion

    #region CellEliminator — direct hit

    [Fact]
    public void BombDirect_RoyalEgg_Destroyed()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.RoyalEgg, 2, 2));
        var elim = CreateEliminator();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void BombDirect_Vase_Damaged()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Vase, 2, 2) { Stage = 2 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, events);

        Assert.Equal(EliminateOutcome.Absorbed, result.Outcome);
        Assert.Equal(1, state.GetTile(2, 2).Stage);
        Assert.Single(events.GetEvents().OfType<TileDamagedEvent>());
    }

    [Fact]
    public void BombDirect_PorcelainPiggy_Destroyed()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.PorcelainPiggy, 2, 2));
        var elim = CreateEliminator();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
    }

    [Fact]
    public void MatchDirect_PorcelainPiggy_Blocked()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.PorcelainPiggy, 2, 2));
        var elim = CreateEliminator();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateOutcome.Blocked, result.Outcome);
        Assert.Equal(ElementType.PorcelainPiggy, state.GetTile(2, 2).Type);
    }

    [Fact]
    public void Vase_TwoHits_Destroyed()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Vase, 2, 2) { Stage = 2 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        // Hit 1: 2→1
        elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, events);
        Assert.Equal(1, state.GetTile(2, 2).Stage);

        // Hit 2: 1→0 (destroyed)
        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 1, 0.1f, events);
        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);

        Assert.Single(events.GetEvents().OfType<TileDamagedEvent>());
        Assert.Single(events.GetEvents().OfType<TileDestroyedEvent>());
    }

    #endregion

    #region Swap guard

    [Fact]
    public void MovingObstacle_CannotSwap_Stage1()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.RoyalEgg, 1, 1)); // Stage=1
        state.SetTile(2, 1, new Tile(2, ElementType.Item1, 2, 1));

        var cmd = new Match3.Core.Commands.SwapCommand
        {
            From = new Position(1, 1),
            To = new Position(2, 1)
        };

        Assert.False(cmd.CanExecute(in state));
    }

    [Fact]
    public void PorcelainPiggy_CannotSwap()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.PorcelainPiggy, 1, 1));
        state.SetTile(2, 1, new Tile(2, ElementType.Item2, 2, 1));

        var cmd = new Match3.Core.Commands.SwapCommand
        {
            From = new Position(1, 1),
            To = new Position(2, 1)
        };

        Assert.False(cmd.CanExecute(in state));
    }

    #endregion

    #region Match exclusion

    [Fact]
    public void MovingObstacle_NotMatchable()
    {
        Assert.False(ElementType.RoyalEgg.IsMatchable());
        Assert.False(ElementType.Vase.IsMatchable());
        Assert.False(ElementType.PorcelainPiggy.IsMatchable());
    }

    [Fact]
    public void MovingObstacle_NotCollectible()
    {
        Assert.False(ElementType.RoyalEgg.IsCollectible());
        Assert.False(ElementType.Vase.IsCollectible());
        Assert.False(ElementType.PorcelainPiggy.IsCollectible());
    }

    [Fact]
    public void IsMovingObstacle_True()
    {
        Assert.True(ElementType.RoyalEgg.IsMovingObstacle());
        Assert.True(ElementType.Vase.IsMovingObstacle());
        Assert.True(ElementType.PorcelainPiggy.IsMovingObstacle());
    }

    [Fact]
    public void IsMovingObstacle_NormalTile_False()
    {
        Assert.False(ElementType.Item1.IsMovingObstacle());
        Assert.False(ElementType.ColorBomb.IsMovingObstacle());
    }

    #endregion

    #region BoardInitializer — default stage

    [Fact]
    public void BoardInitializer_Vase_DefaultStage2()
    {
        var config = new Match3.Core.Config.LevelConfig(5, 5);
        int idx = 2 * 5 + 2;
        config.Grid[idx] = ElementType.Vase;
        // TileStages NOT set — should use TileRules.GetDefaultStage(Vase) = 2

        var state = CreateState();
        new Match3.Core.Systems.Generation.BoardInitializer(
            new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(ElementType.Vase, state.GetTile(2, 2).Type);
        Assert.Equal(2, state.GetTile(2, 2).Stage);
    }

    [Fact]
    public void BoardInitializer_RoyalEgg_DefaultStage1()
    {
        var config = new Match3.Core.Config.LevelConfig(5, 5);
        int idx = 2 * 5 + 2;
        config.Grid[idx] = ElementType.RoyalEgg;

        var state = CreateState();
        new Match3.Core.Systems.Generation.BoardInitializer(
            new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(1, state.GetTile(2, 2).Stage);
    }

    #endregion
}
