using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Core.Tests.Simulation;

public class SimulationMatchHandlerGoalTests
{
    private readonly SimulationMatchHandler _handler;
    private readonly FakeMatchFinder _matchFinder;
    private readonly FakeMatchProcessor _matchProcessor;
    private readonly BufferedEventCollector _eventCollector;

    public SimulationMatchHandlerGoalTests()
    {
        _matchFinder = new FakeMatchFinder();
        _matchProcessor = new FakeMatchProcessor();
        _eventCollector = new BufferedEventCollector();

        _handler = new SimulationMatchHandler(
            _matchFinder,
            _matchProcessor,
            new LevelObjectiveSystem()
        );
    }

    [Fact]
    public void ProcessStableMatches_SetsIsGoal_WhenTileIsObjectiveTarget()
    {
        // Arrange
        var state = new GameState(5, 5, 5, null!);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        // Setup Objective
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10,
            CurrentCount = 0
        };

        var matchGroup = new MatchGroup
        {
            Type = ElementType.Item1,
            Positions = new HashSet<Position> { new Position(0, 0) },
            Shape = MatchShape.Simple3
        };

        _matchFinder.SetMatches(new List<MatchGroup> { matchGroup });

        // Act
        _handler.ProcessStableMatches(ref state, 1, 0.1f, _eventCollector);

        // Assert
        var events = _eventCollector.GetEvents();
        var destroyEvent = events.OfType<TileDestroyedEvent>().FirstOrDefault();

        Assert.NotNull(destroyEvent);
        Assert.Equal(1, destroyEvent.TileId);
        Assert.True(destroyEvent.IsGoal);
    }

    [Fact]
    public void ProcessStableMatches_DoesNotSetIsGoal_WhenTileIsNotObjectiveTarget()
    {
        // Arrange
        var state = new GameState(5, 5, 5, null!);
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0)); // Item2

        // Setup Objective for Item1
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10,
            CurrentCount = 0
        };

        var matchGroup = new MatchGroup
        {
            Type = ElementType.Item2,
            Positions = new HashSet<Position> { new Position(0, 0) },
            Shape = MatchShape.Simple3
        };

        _matchFinder.SetMatches(new List<MatchGroup> { matchGroup });

        // Act
        _handler.ProcessStableMatches(ref state, 1, 0.1f, _eventCollector);

        // Assert
        var events = _eventCollector.GetEvents();
        var destroyEvent = events.OfType<TileDestroyedEvent>().FirstOrDefault();

        Assert.NotNull(destroyEvent);
        Assert.Equal(1, destroyEvent.TileId);
        Assert.False(destroyEvent.IsGoal);
    }

    [Fact]
    public void ProcessStableMatches_DoesNotSetIsGoal_WhenObjectiveCompleted()
    {
        // Arrange
        var state = new GameState(5, 5, 5, null!);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        
        // Setup Objective Completed
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10,
            CurrentCount = 10 // Completed
        };

        var matchGroup = new MatchGroup
        {
            Type = ElementType.Item1,
            Positions = new HashSet<Position> { new Position(0, 0) },
            Shape = MatchShape.Simple3
        };

        _matchFinder.SetMatches(new List<MatchGroup> { matchGroup });

        // Act
        _handler.ProcessStableMatches(ref state, 1, 0.1f, _eventCollector);

        // Assert
        var events = _eventCollector.GetEvents();
        var destroyEvent = events.OfType<TileDestroyedEvent>().FirstOrDefault();
        
        Assert.NotNull(destroyEvent);
        Assert.Equal(1, destroyEvent.TileId);
        Assert.False(destroyEvent.IsGoal);
    }
}

internal class FakeMatchFinder : IMatchFinder
{
    private List<MatchGroup> _groups = new();
    
    public void SetMatches(List<MatchGroup> groups) => _groups = groups;
    
    public bool HasMatches(in GameState state) => _groups.Count > 0;
    public List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null) => _groups;
    
    public bool HasMatchAt(in GameState state, Position p) => false;
    
    // Interface members not used in this test
    public List<MatchGroup> FindMatchGroups(GameState state, IEnumerable<Position>? foci = null) => _groups;
}

internal class FakeMatchProcessor : IMatchProcessor
{
    public int ProcessMatches(ref GameState state, List<MatchGroup> matches)
    {
        return 0;
    }

    public int ProcessMatches(ref GameState state, List<MatchGroup> groups, int tick, float simTime, IEventCollector events)
    {
        return 0;
    }
}
