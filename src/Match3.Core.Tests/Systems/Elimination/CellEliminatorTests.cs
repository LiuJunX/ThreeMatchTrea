using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Elimination;

public class CellEliminatorTests
{
    private static GameState CreateState(int width = 3, int height = 3)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    [Fact]
    public void EmptyCell_ReturnsBlocked()
    {
        var state = CreateState();
        // Cell (1,1) has no tile (default is Type=None)
        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Match, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateResult.Blocked, result);
    }

    [Fact]
    public void CoverProtected_ReturnsAbsorbed_AndDamagesCover()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
        state.SetCover(1, 1, new Cover(CoverType.Cage, 1));

        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.BombEffect, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateResult.Absorbed, result);
        // Tile should survive
        Assert.Equal(ElementType.Item1, state.GetTile(1, 1).Type);
        // Cover should be destroyed (HP was 1)
        Assert.Equal(CoverType.None, state.GetCover(1, 1).Type);
    }

    [Fact]
    public void IndestructibleLock_ReturnsBlocked()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
        state.Lock(1, 1, CellLockType.Indestructible);

        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Match, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateResult.Blocked, result);
        // Tile should survive
        Assert.Equal(ElementType.Item1, state.GetTile(1, 1).Type);
    }

    [Fact]
    public void NormalTile_ReturnsEliminated_ClearsTile_NotifiesGround()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(42, ElementType.Item3, 1, 1));
        state.SetGround(1, 1, new Ground(GroundType.Ice, 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var eliminator = new CellEliminator(
            new CoverSystem(objectiveSystem), new GroundSystem(objectiveSystem), objectiveSystem);

        var events = new BufferedEventCollector();
        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Match, 5, 1.5f, events);

        Assert.Equal(EliminateResult.Eliminated, result);
        // Tile should be cleared
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
        // Ground should be damaged (HP was 1 → destroyed)
        Assert.Equal(GroundType.None, state.GetGround(1, 1).Type);
        // Should have TileDestroyedEvent
        var allEvents = events.GetEvents();
        Assert.Contains(allEvents, e => e is TileDestroyedEvent);
    }

    [Fact]
    public void NormalTile_EmitsTileDestroyedEvent_WithCorrectFields()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(42, ElementType.Item3, 1, 1));

        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());
        var events = new BufferedEventCollector();

        eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Projectile, 10, 2.5f, events);

        var allEvents = events.GetEvents();
        var tde = Assert.Single(allEvents, e => e is TileDestroyedEvent) as TileDestroyedEvent;
        Assert.NotNull(tde);
        Assert.Equal(42, tde!.TileId);
        Assert.Equal(new Position(1, 1), tde.GridPosition);
        Assert.Equal(ElementType.Item3, tde.Type);
        Assert.Equal(DestroyReason.Projectile, tde.Reason);
        Assert.Equal(10, tde.Tick);
        Assert.Equal(2.5f, tde.SimulationTime);
    }

    [Fact]
    public void NullEventCollector_NoEventsEmitted()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));

        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Match, 0, 0f, NullEventCollector.Instance);

        // Should still eliminate successfully
        Assert.Equal(EliminateResult.Eliminated, result);
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
    }

    [Fact]
    public void CoverWithMultipleHP_ReturnsAbsorbed_CoverSurvives()
    {
        var state = CreateState();
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
        state.SetCover(1, 1, new Cover(CoverType.Cage, 2)); // 2 HP

        var eliminator = new CellEliminator(new CoverSystem(), new GroundSystem());

        var result = eliminator.Eliminate(
            ref state, new Position(1, 1), DestroyReason.Match, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(EliminateResult.Absorbed, result);
        // Tile survives
        Assert.Equal(ElementType.Item1, state.GetTile(1, 1).Type);
        // Cover survives with 1 HP
        Assert.Equal(CoverType.Cage, state.GetCover(1, 1).Type);
        Assert.Equal(1, state.GetCover(1, 1).Health);
    }
}
