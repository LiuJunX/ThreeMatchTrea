using System.Linq;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Layers;

/// <summary>
/// Integration tests for Grass ground element: damage, destruction, events, objectives, protection.
/// </summary>
public class GrassIntegrationTests
{
    private readonly GroundSystem _groundSystem = new();

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.Item1, x, y));
            }
        }
        return state;
    }

    #region Single-HP Grass

    [Fact]
    public void Grass_HP1_SingleElimination_Destroys()
    {
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 1));
        var events = new BufferedEventCollector();

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(GroundType.Grass, evt.Type);
        Assert.Equal(pos, evt.GridPosition);
    }

    #endregion

    #region Multi-HP Grass (Deep Variant)

    [Fact]
    public void Grass_HP2_FirstHit_EmitsGroundDamagedEvent()
    {
        var state = CreateState();
        var pos = new Position(4, 4);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 2));
        var events = new BufferedEventCollector();

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Still alive
        Assert.Equal(GroundType.Grass, state.GetGround(pos).Type);
        Assert.Equal(1, state.GetGround(pos).Health);

        // Emits GroundDamagedEvent (not destroyed)
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDamagedEvent>(events.GetEvents()[0]);
        Assert.Equal(GroundType.Grass, evt.Type);
        Assert.Equal(pos, evt.GridPosition);
        Assert.Equal(1, evt.RemainingHealth);
    }

    [Fact]
    public void Grass_HP2_SecondHit_EmitsGroundDestroyedEvent()
    {
        var state = CreateState();
        var pos = new Position(4, 4);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 2));
        var events = new BufferedEventCollector();

        // First hit — damages
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);
        Assert.IsType<GroundDamagedEvent>(events.GetEvents()[0]);

        // Second hit — destroys
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 2, simTime: 0.2f, events);

        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Equal(2, events.GetEvents().Count);
        var destroyEvt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[1]);
        Assert.Equal(GroundType.Grass, destroyEvt.Type);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Grass_ValidHPRange_DestroyedAfterExactHits(byte hp)
    {
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Grass, health: hp));
        var events = new BufferedEventCollector();

        for (int i = 0; i < hp; i++)
        {
            _groundSystem.OnTileDestroyed(ref state, pos, tick: i + 1, simTime: 0.1f * (i + 1), events);
        }

        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        // Last event should be GroundDestroyedEvent
        var lastEvt = events.GetEvents().Last();
        Assert.IsType<GroundDestroyedEvent>(lastEvt);
    }

    #endregion

    #region Protection (Death-Effect Spawned Grass)

    [Fact]
    public void ProtectedGrass_ImmuneBeforeProtectUntil()
    {
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 1, protectUntil: 1.0f));
        var events = new BufferedEventCollector();

        // Hit at simTime 0.5 — protected
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.5f, events);

        Assert.Equal(GroundType.Grass, state.GetGround(pos).Type);
        Assert.Equal(1, state.GetGround(pos).Health);
        Assert.Empty(events.GetEvents());
    }

    [Fact]
    public void ProtectedGrass_VulnerableAfterProtectUntil()
    {
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 1, protectUntil: 1.0f));
        var events = new BufferedEventCollector();

        // Hit at simTime 1.5 — protection expired
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 1.5f, events);

        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
    }

    #endregion

    #region Event Data Correctness

    [Fact]
    public void GroundDamagedEvent_ContainsCorrectTickAndSimTime()
    {
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 2));
        var events = new BufferedEventCollector();

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 42, simTime: 1.5f, events);

        var evt = Assert.IsType<GroundDamagedEvent>(events.GetEvents()[0]);
        Assert.Equal(42, evt.Tick);
        Assert.Equal(1.5f, evt.SimulationTime);
    }

    #endregion

    #region Objective Tracking

    [Fact]
    public void Grass_Destruction_ReportsIsGoal_WhenObjectiveTargets()
    {
        // Use GroundSystem with objective system that considers Grass a target
        var objectiveSystem = new LevelObjectiveSystem();
        var groundSystem = new GroundSystem(objectiveSystem);

        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Grass, health: 1));

        // Set up objective targeting Grass
        state.ObjectiveProgress[0] = new Match3.Core.Models.Gameplay.ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Grass,
            TargetCount = 5,
            CurrentCount = 0
        };

        var events = new BufferedEventCollector();
        groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.True(evt.IsGoal);
    }

    #endregion

    #region No Ground — No Effect

    [Fact]
    public void Grass_NoGround_NoChange()
    {
        var state = CreateState();
        var pos = new Position(5, 5);
        var events = new BufferedEventCollector();

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Empty(events.GetEvents());
    }

    #endregion
}
