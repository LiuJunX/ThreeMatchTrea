using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Integration tests for Curtain obstacle: global color elimination,
/// color isolation, multi-HP chains, direct-hit immunity, and objective tracking.
/// </summary>
public class CurtainIntegrationTests
{
    private static GameState CreateState(int width = 9, int height = 9)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    #region Global color elimination — core mechanic

    [Fact]
    public void SingleHP_MatchDestroysImmediately()
    {
        var state = CreateState();
        state.SetObstacle(4, 4, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, events);

        Assert.False(state.HasObstacle(4, 4));
        Assert.Single(events.GetEvents().OfType<ObstacleDestroyedEvent>());
    }

    [Fact]
    public void MultiColorCurtain_OnlyMatchingColorDamaged()
    {
        var state = CreateState();
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1)); // Red
        state.SetObstacle(7, 7, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item3)); // Blue
        var system = new ObstacleSystem();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, NullEventCollector.Instance);

        Assert.False(state.HasObstacle(1, 1));  // Red destroyed
        Assert.True(state.HasObstacle(7, 7));   // Blue unaffected
        Assert.Equal(1, state.GetObstacle(7, 7).Stage);
    }

    [Fact]
    public void MultipleCurtains_SameColor_AllDamaged()
    {
        var state = CreateState();
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item2));
        state.SetObstacle(4, 4, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item2));
        state.SetObstacle(8, 8, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item2));
        var system = new ObstacleSystem();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item2, 0, 0f, NullEventCollector.Instance);

        Assert.False(state.HasObstacle(0, 0));
        Assert.False(state.HasObstacle(4, 4));
        Assert.False(state.HasObstacle(8, 8));
    }

    [Fact]
    public void MultiHitPerTurn_ChainDestroysMultiHP()
    {
        var state = CreateState();
        state.SetObstacle(3, 3, new Obstacle(ObstacleType.Curtain, 2, (byte)ElementType.Item1));
        var system = new ObstacleSystem();
        var events = new BufferedEventCollector();

        // First red match in the same turn
        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, events);
        Assert.True(state.HasObstacle(3, 3));
        Assert.Equal(1, state.GetObstacle(3, 3).Stage);

        // Second red match (chain) in the same turn
        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0.1f, events);
        Assert.False(state.HasObstacle(3, 3));

        var allEvents = events.GetEvents();
        Assert.Single(allEvents.OfType<ObstacleDamagedEvent>());
        Assert.Single(allEvents.OfType<ObstacleDestroyedEvent>());
    }

    #endregion

    #region Immunity — direct hit and adjacent reaction

    [Fact]
    public void Curtain_NoDirectHit()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void Curtain_NoAdjacentReaction()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1));
        var system = new ObstacleSystem();

        // Eliminate a tile adjacent to the Curtain
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(1, state.GetObstacle(2, 2).Stage);
    }

    #endregion

    #region ColorBomb path

    [Fact]
    public void ColorBomb_TriggersCurtainDamage()
    {
        // Verify NotifyGlobalColorElimination works for ColorBomb path
        // (same method, but confirms the color parameter flows correctly)
        var state = CreateState();
        state.SetObstacle(5, 5, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item4));
        var system = new ObstacleSystem();

        // Simulate ColorBomb targeting Item4 (yellow)
        system.NotifyGlobalColorElimination(ref state, ElementType.Item4, 0, 0f, NullEventCollector.Instance);

        Assert.False(state.HasObstacle(5, 5));
    }

    #endregion

    #region Objective tracking

    [Fact]
    public void Curtain_Objective_IsGoal()
    {
        var state = CreateState();
        state.SetObstacle(3, 3, new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1));

        // Set objective: destroy Curtain obstacles
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.Curtain,
            TargetCount = 1,
            CurrentCount = 0
        };

        var objectiveSystem = new LevelObjectiveSystem();
        var system = new ObstacleSystem(objectiveSystem);
        var events = new BufferedEventCollector();

        system.NotifyGlobalColorElimination(ref state, ElementType.Item1, 0, 0f, events);

        Assert.False(state.HasObstacle(3, 3));

        var destroyEvt = events.GetEvents().OfType<ObstacleDestroyedEvent>().Single();
        Assert.True(destroyEvt.IsGoal);
    }

    #endregion
}
