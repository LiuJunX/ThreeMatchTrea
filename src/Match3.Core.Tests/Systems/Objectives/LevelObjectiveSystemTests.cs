using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Objectives;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Objectives;

/// <summary>
/// LevelObjectiveSystem unit tests.
///
/// Responsibilities:
/// - Initializing objectives from level configuration
/// - Tracking progress when tiles/covers/grounds are destroyed
/// - Determining level completion (victory/defeat)
/// - Emitting objective-related events
/// </summary>
public class LevelObjectiveSystemTests
{
    private readonly LevelObjectiveSystem _system = new();

    private GameState CreateState(int width = 8, int height = 8)
    {
        return new GameState(width, height, 6, new StubRandom());
    }

    private LevelConfig CreateConfig(params LevelObjective[] objectives)
    {
        var config = new LevelConfig();
        for (int i = 0; i < objectives.Length && i < 4; i++)
        {
            config.Objectives[i] = objectives[i];
        }
        return config;
    }

    #region Initialize Tests

    [Fact]
    public void Initialize_WithSingleObjective_SetsProgressCorrectly()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10
        });

        // Act
        _system.Initialize(ref state, config);

        // Assert
        Assert.Equal(ObjectiveTargetLayer.Tile, state.ObjectiveProgress[0].TargetLayer);
        Assert.Equal((int)ElementType.Item1, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(10, state.ObjectiveProgress[0].TargetCount);
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
        Assert.True(state.ObjectiveProgress[0].IsActive);
        Assert.False(state.ObjectiveProgress[0].IsCompleted);
    }

    [Fact]
    public void Initialize_WithMultipleObjectives_SetsAllProgressCorrectly()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 5 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Cover, ElementType = (int)CoverType.Cage, TargetCount = 3 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Ground, ElementType = (int)GroundType.Ice, TargetCount = 8 }
        );

        // Act
        _system.Initialize(ref state, config);

        // Assert
        Assert.True(state.ObjectiveProgress[0].IsActive);
        Assert.True(state.ObjectiveProgress[1].IsActive);
        Assert.True(state.ObjectiveProgress[2].IsActive);
        Assert.False(state.ObjectiveProgress[3].IsActive); // No fourth objective
    }

    [Fact]
    public void Initialize_WithNoObjectives_AllSlotsInactive()
    {
        // Arrange
        var state = CreateState();
        var config = new LevelConfig(); // No objectives set

        // Act
        _system.Initialize(ref state, config);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            Assert.False(state.ObjectiveProgress[i].IsActive);
            Assert.Equal(ObjectiveTargetLayer.None, state.ObjectiveProgress[i].TargetLayer);
        }
    }

    [Fact]
    public void Initialize_SetsLevelStatusToInProgress()
    {
        // Arrange
        var state = CreateState();
        state.LevelStatus = LevelStatus.Victory; // Pre-set to something else
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item3,
            TargetCount = 5
        });

        // Act
        _system.Initialize(ref state, config);

        // Assert
        Assert.Equal(LevelStatus.InProgress, state.LevelStatus);
    }

    #endregion

    #region OnTileDestroyed Tests

    [Fact]
    public void OnTileDestroyed_MatchingObjective_IncrementsProgress()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount);
    }

    [Fact]
    public void OnTileDestroyed_NonMatchingType_DoesNotIncrement()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item3, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
    }

    [Fact]
    public void OnTileDestroyed_NonMatchingLayer_DoesNotIncrement()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover, // Tracking covers, not tiles
            ElementType = (int)CoverType.Cage,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
    }

    [Fact]
    public void OnTileDestroyed_EmitsProgressEvent()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item2,
            TargetCount = 3
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item2, tick: 42, simTime: 1.5f, events);

        // Assert
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<ObjectiveProgressEvent>(events.GetEvents()[0]);
        Assert.Equal(0, evt.ObjectiveIndex);
        Assert.Equal(0, evt.PreviousCount);
        Assert.Equal(1, evt.CurrentCount);
        Assert.Equal(3, evt.TargetCount);
        Assert.False(evt.IsCompleted);
        Assert.Equal(42, evt.Tick);
        Assert.Equal(1.5f, evt.SimulationTime);
    }

    [Fact]
    public void OnTileDestroyed_CompletesObjective_SetsIsCompletedInEvent()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item4,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item4, tick: 1, simTime: 0.1f, events);

        // Assert
        var evt = Assert.IsType<ObjectiveProgressEvent>(events.GetEvents()[0]);
        Assert.True(evt.IsCompleted);
        Assert.True(state.ObjectiveProgress[0].IsCompleted);
    }

    [Fact]
    public void OnTileDestroyed_AlreadyCompleted_DoesNotEmitEvent()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);
        events = new BufferedEventCollector(); // Reset events

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 2, simTime: 0.2f, events);

        // Assert
        Assert.Empty(events.GetEvents());
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount); // Not incremented
    }

    [Fact]
    public void OnTileDestroyed_DisabledEvents_NoEventEmitted()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount); // Progress still updated
    }

    #endregion

    #region OnCoverDestroyed Tests

    [Fact]
    public void OnCoverDestroyed_MatchingObjective_IncrementsProgress()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Cage,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnCoverDestroyed(ref state, CoverType.Cage, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount);
    }

    [Fact]
    public void OnCoverDestroyed_NonMatchingType_DoesNotIncrement()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Cage,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnCoverDestroyed(ref state, CoverType.Chain, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
    }

    [Theory]
    [InlineData(CoverType.Cage)]
    [InlineData(CoverType.Chain)]
    [InlineData(CoverType.Bubble)]
    public void OnCoverDestroyed_AllCoverTypes_TrackedCorrectly(CoverType coverType)
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)coverType,
            TargetCount = 3
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnCoverDestroyed(ref state, coverType, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount);
    }

    #endregion

    #region OnGroundDestroyed Tests

    [Fact]
    public void OnGroundDestroyed_MatchingObjective_IncrementsProgress()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.OnGroundDestroyed(ref state, GroundType.Ice, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount);
    }

    [Fact]
    public void OnGroundDestroyed_NonMatchingType_DoesNotIncrement()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 5
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act - Use a different GroundType value (simulating a non-matching type)
        _system.OnGroundDestroyed(ref state, GroundType.None, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
    }

    #endregion

    #region IsLevelComplete Tests

    [Fact]
    public void IsLevelComplete_AllObjectivesCompleted_ReturnsTrue()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 1 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Cover, ElementType = (int)CoverType.Cage, TargetCount = 1 }
        );
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;

        // Complete all objectives
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);
        _system.OnCoverDestroyed(ref state, CoverType.Cage, tick: 2, simTime: 0.2f, events);

        // Act
        bool isComplete = _system.IsLevelComplete(in state);

        // Assert
        Assert.True(isComplete);
    }

    [Fact]
    public void IsLevelComplete_SomeObjectivesNotCompleted_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 1 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Cover, ElementType = (int)CoverType.Cage, TargetCount = 5 }
        );
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;

        // Only complete first objective
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);

        // Act
        bool isComplete = _system.IsLevelComplete(in state);

        // Assert
        Assert.False(isComplete);
    }

    [Fact]
    public void IsLevelComplete_NoActiveObjectives_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        var config = new LevelConfig(); // No objectives
        _system.Initialize(ref state, config);

        // Act
        bool isComplete = _system.IsLevelComplete(in state);

        // Assert
        Assert.False(isComplete); // No objectives means nothing to complete
    }

    #endregion

    #region IsLevelFailed Tests

    [Fact]
    public void IsLevelFailed_OutOfMovesAndNotComplete_ReturnsTrue()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 5;
        state.MoveCount = 5; // Out of moves
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10 // Not achievable
        });
        _system.Initialize(ref state, config);

        // Act
        bool isFailed = _system.IsLevelFailed(in state);

        // Assert
        Assert.True(isFailed);
    }

    [Fact]
    public void IsLevelFailed_OutOfMovesButComplete_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 5;
        state.MoveCount = 5; // Out of moves
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events); // Complete objective

        // Act
        bool isFailed = _system.IsLevelFailed(in state);

        // Assert
        Assert.False(isFailed);
    }

    [Fact]
    public void IsLevelFailed_HasMovesRemaining_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 10;
        state.MoveCount = 5; // Still has moves
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10
        });
        _system.Initialize(ref state, config);

        // Act
        bool isFailed = _system.IsLevelFailed(in state);

        // Assert
        Assert.False(isFailed);
    }

    [Fact]
    public void IsLevelFailed_NoActiveObjectives_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 5;
        state.MoveCount = 5;
        var config = new LevelConfig(); // No objectives
        _system.Initialize(ref state, config);

        // Act
        bool isFailed = _system.IsLevelFailed(in state);

        // Assert
        Assert.False(isFailed); // No objectives means no failure condition
    }

    #endregion

    #region UpdateLevelStatus Tests

    [Fact]
    public void UpdateLevelStatus_AllObjectivesComplete_SetsVictory()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);
        events = new BufferedEventCollector(); // Reset

        // Act
        _system.UpdateLevelStatus(ref state, tick: 2, simTime: 0.2f, events);

        // Assert
        Assert.Equal(LevelStatus.Victory, state.LevelStatus);
    }

    [Fact]
    public void UpdateLevelStatus_Victory_EmitsLevelCompletedEvent()
    {
        // Arrange
        var state = CreateState();
        state.Score = 1000;
        state.MoveCount = 3;
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);
        events = new BufferedEventCollector(); // Reset

        // Act
        _system.UpdateLevelStatus(ref state, tick: 100, simTime: 5.0f, events);

        // Assert
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<LevelCompletedEvent>(events.GetEvents()[0]);
        Assert.True(evt.IsVictory);
        Assert.Equal(1000, evt.FinalScore);
        Assert.Equal(3, evt.MovesUsed);
        Assert.Equal(100, evt.Tick);
        Assert.Equal(5.0f, evt.SimulationTime);
    }

    [Fact]
    public void UpdateLevelStatus_OutOfMoves_SetsDefeat()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 5;
        state.MoveCount = 5;
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 100 // Can't complete
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.UpdateLevelStatus(ref state, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(LevelStatus.Defeat, state.LevelStatus);
    }

    [Fact]
    public void UpdateLevelStatus_Defeat_EmitsLevelCompletedEvent()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 5;
        state.MoveCount = 5;
        state.Score = 500;
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 100
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.UpdateLevelStatus(ref state, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<LevelCompletedEvent>(events.GetEvents()[0]);
        Assert.False(evt.IsVictory);
        Assert.Equal(500, evt.FinalScore);
        Assert.Equal(5, evt.MovesUsed);
    }

    [Fact]
    public void UpdateLevelStatus_AlreadyEnded_DoesNothing()
    {
        // Arrange
        var state = CreateState();
        state.LevelStatus = LevelStatus.Victory;
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        state.LevelStatus = LevelStatus.Victory; // Override back to Victory
        var events = new BufferedEventCollector();

        // Act
        _system.UpdateLevelStatus(ref state, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Empty(events.GetEvents()); // No event emitted
        Assert.Equal(LevelStatus.Victory, state.LevelStatus); // Unchanged
    }

    [Fact]
    public void UpdateLevelStatus_InProgress_NoChange()
    {
        // Arrange
        var state = CreateState();
        state.MoveLimit = 10;
        state.MoveCount = 3; // Still has moves
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 5 // Not yet complete
        });
        _system.Initialize(ref state, config);
        var events = new BufferedEventCollector();

        // Act
        _system.UpdateLevelStatus(ref state, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Empty(events.GetEvents());
        Assert.Equal(LevelStatus.InProgress, state.LevelStatus);
    }

    [Fact]
    public void UpdateLevelStatus_DisabledEvents_NoEventEmitted()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 1
        });
        _system.Initialize(ref state, config);
        var progressEvents = NullEventCollector.Instance;
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, progressEvents);

        // Act
        _system.UpdateLevelStatus(ref state, tick: 2, simTime: 0.2f, NullEventCollector.Instance);

        // Assert
        Assert.Equal(LevelStatus.Victory, state.LevelStatus); // Status still updated
    }

    #endregion

    #region Multiple Objectives Tests

    [Fact]
    public void MultipleObjectives_TrackIndependently()
    {
        // Arrange
        var state = CreateState();
        var config = CreateConfig(
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 3 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = 2 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Cover, ElementType = (int)CoverType.Cage, TargetCount = 4 }
        );
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 2, simTime: 0.2f, events);
        _system.OnTileDestroyed(ref state, ElementType.Item3, tick: 3, simTime: 0.3f, events);
        _system.OnCoverDestroyed(ref state, CoverType.Cage, tick: 4, simTime: 0.4f, events);

        // Assert
        Assert.Equal(2, state.ObjectiveProgress[0].CurrentCount); // Red tiles
        Assert.Equal(1, state.ObjectiveProgress[1].CurrentCount); // Blue tiles
        Assert.Equal(1, state.ObjectiveProgress[2].CurrentCount); // Cages
    }

    [Fact]
    public void MultipleObjectives_SameTypeAndLayer_OnlyMatchingUpdated()
    {
        // Arrange - Two different tile type objectives
        var state = CreateState();
        var config = CreateConfig(
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 5 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = 5 }
        );
        _system.Initialize(ref state, config);
        var events = NullEventCollector.Instance;

        // Act
        _system.OnTileDestroyed(ref state, ElementType.Item1, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(1, state.ObjectiveProgress[0].CurrentCount); // Red updated
        Assert.Equal(0, state.ObjectiveProgress[1].CurrentCount); // Blue not updated
    }

    #endregion
}

