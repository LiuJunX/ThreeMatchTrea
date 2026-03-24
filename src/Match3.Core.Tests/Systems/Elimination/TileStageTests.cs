using System;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Elimination;

file sealed class NullCoverSystem : ICoverSystem
{
    public bool IsTileProtected(in GameState state, Position pos) => false;
    public bool TryDamageCover(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) => false;
    public void SyncDynamicCovers(ref GameState state, Position from, Position to) { }
    public void NotifyBatchElimination(ref GameState state, ReadOnlySpan<EliminatedTileInfo> eliminated, int tick, float simTime, IEventCollector events) { }
}

file sealed class NullGroundSystem : IGroundSystem
{
    public void OnTileDestroyed(ref GameState state, Position pos, int tick, float simTime, IEventCollector events) { }
}

/// <summary>
/// Tests for Tile Stage infrastructure: multi-stage tiles, TileDamagedEvent,
/// elimination source restrictions, and swap guard.
/// </summary>
public class TileStageTests
{
    private static GameState CreateState(int width = 5, int height = 5)
        => new GameState(width, height, 5, new StubRandom());

    private static CellEliminator CreateEliminator()
        => new CellEliminator(new NullCoverSystem(), new NullGroundSystem());

    #region Normal tile (Stage=1) backward compatibility

    [Fact]
    public void NormalTile_Stage1_EliminatedOnFirstHit()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2));
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, events);

        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
        Assert.Single(events.GetEvents().OfType<TileDestroyedEvent>());
        Assert.Empty(events.GetEvents().OfType<TileDamagedEvent>());
    }

    [Fact]
    public void NormalTile_DefaultStageIs1()
    {
        var tile = new Tile(1, ElementType.Item1, 0, 0);
        Assert.Equal(1, tile.Stage);
    }

    #endregion

    #region Multi-stage tile (Stage > 1)

    [Fact]
    public void MultiStage_FirstHit_DamagesButDoesNotEliminate()
    {
        var state = CreateState();
        var tile = new Tile(1, ElementType.Item1, 2, 2) { Stage = 3 };
        state.SetTile(2, 2, tile);
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, events);

        Assert.Equal(EliminateOutcome.Absorbed, result.Outcome);
        Assert.Equal(ElementType.Item1, state.GetTile(2, 2).Type);
        Assert.Equal(2, state.GetTile(2, 2).Stage);
        var dmg = events.GetEvents().OfType<TileDamagedEvent>().Single();
        Assert.Equal(1, dmg.TileId);
        Assert.Equal(2, dmg.RemainingStage);
        Assert.Empty(events.GetEvents().OfType<TileDestroyedEvent>());
    }

    [Fact]
    public void MultiStage_FinalHit_Eliminates()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { Stage = 1 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        var result = elim.Eliminate(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, events);

        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
        Assert.Single(events.GetEvents().OfType<TileDestroyedEvent>());
    }

    [Fact]
    public void MultiStage_SequentialHits_FullLifecycle()
    {
        var state = CreateState();
        state.SetTile(2, 2, new Tile(1, ElementType.Item1, 2, 2) { Stage = 3 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        // Hit 1: 3→2
        elim.Eliminate(ref state, new Position(2, 2), new ElimContext(ElimSource.Bomb), 0, 0f, events);
        Assert.Equal(2, state.GetTile(2, 2).Stage);

        // Hit 2: 2→1
        elim.Eliminate(ref state, new Position(2, 2), new ElimContext(ElimSource.Bomb), 1, 0.1f, events);
        Assert.Equal(1, state.GetTile(2, 2).Stage);

        // Hit 3: 1→0 (eliminated)
        var result = elim.Eliminate(ref state, new Position(2, 2), new ElimContext(ElimSource.Bomb), 2, 0.2f, events);
        Assert.Equal(EliminateOutcome.Eliminated, result.Outcome);

        Assert.Equal(2, events.GetEvents().OfType<TileDamagedEvent>().Count());
        Assert.Single(events.GetEvents().OfType<TileDestroyedEvent>());
    }

    #endregion

    #region TileRules

    [Fact]
    public void TileRules_DefaultCanEliminate_AllSourcesAllowed()
    {
        foreach (ElimSource source in Enum.GetValues(typeof(ElimSource)))
            Assert.True(TileRules.CanEliminate(ElementType.Item1, source));
    }

    [Fact]
    public void TileRules_GetDefaultStage_Is1()
    {
        Assert.Equal(1, TileRules.GetDefaultStage(ElementType.Item1));
    }

    #endregion

    #region Swap guard

    [Fact]
    public void SwapCommand_MultiStageTile_CannotSwap()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1) { Stage = 2 });
        state.SetTile(2, 1, new Tile(2, ElementType.Item2, 2, 1));

        var cmd = new Match3.Core.Commands.SwapCommand { From = new Position(1, 1), To = new Position(2, 1) };
        Assert.False(cmd.CanExecute(in state));
    }

    [Fact]
    public void SwapCommand_NormalTiles_CanSwap()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
        state.SetTile(2, 1, new Tile(2, ElementType.Item2, 2, 1));

        var cmd = new Match3.Core.Commands.SwapCommand { From = new Position(1, 1), To = new Position(2, 1) };
        Assert.True(cmd.CanExecute(in state));
    }

    #endregion

    #region LevelConfig TileStages

    [Fact]
    public void BoardInitializer_AppliesTileStagesFromConfig()
    {
        var config = new LevelConfig(5, 5);
        int idx = 2 * 5 + 2;
        config.Grid[idx] = ElementType.Item1;
        config.TileStages[idx] = 3;

        var state = CreateState();
        new BoardInitializer(new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(3, state.GetTile(2, 2).Stage);
    }

    [Fact]
    public void BoardInitializer_DefaultStageIs1_WhenNotConfigured()
    {
        var config = new LevelConfig(5, 5);
        config.Grid[2 * 5 + 2] = ElementType.Item2;

        var state = CreateState();
        new BoardInitializer(new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2))
            .Initialize(ref state, config);

        Assert.Equal(1, state.GetTile(2, 2).Stage);
    }

    #endregion

    #region TileDamagedEvent

    [Fact]
    public void TileDamagedEvent_HasCorrectFields()
    {
        var state = CreateState();
        state.SetTile(3, 3, new Tile(42, ElementType.Item4, 3, 3) { Stage = 2 });
        var elim = CreateEliminator();
        var events = new BufferedEventCollector();

        elim.Eliminate(ref state, new Position(3, 3), new ElimContext(ElimSource.Bomb), 5, 1.5f, events);

        var dmg = events.GetEvents().OfType<TileDamagedEvent>().Single();
        Assert.Equal(42, dmg.TileId);
        Assert.Equal(new Position(3, 3), dmg.GridPosition);
        Assert.Equal(ElementType.Item4, dmg.Type);
        Assert.Equal(1, dmg.RemainingStage);
        Assert.Equal(ElimSource.Bomb, dmg.Reason);
    }

    #endregion
}
