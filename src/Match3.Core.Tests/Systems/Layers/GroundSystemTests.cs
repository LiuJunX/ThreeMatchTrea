using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Layers;

/// <summary>
/// GroundSystem unit tests.
///
/// Responsibilities:
/// - Managing ground element damage and destruction
/// - Ground elements are damaged when tiles above are destroyed
/// </summary>
public class GroundSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private readonly GroundSystem _groundSystem = new();

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        // Initialize grid with empty tiles
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, TileType.Red, x, y));
            }
        }
        return state;
    }

    #region OnTileDestroyed Tests

    [Fact]
    public void OnTileDestroyed_NoGround_NoChange()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        var events = new BufferedEventCollector();

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Empty(events.GetEvents());
    }

    [Fact]
    public void OnTileDestroyed_SingleHPGround_DestroysGround()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 1));
        var events = new BufferedEventCollector();

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(pos, evt.GridPosition);
        Assert.Equal(GroundType.Ice, evt.Type);
    }

    [Fact]
    public void OnTileDestroyed_MultiHPGround_ReducesHealth()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetGround(pos, new Ground(GroundType.Jelly, health: 3));
        var events = new BufferedEventCollector();

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.Jelly, state.GetGround(pos).Type); // Still exists
        Assert.Equal(2, state.GetGround(pos).Health);
        Assert.Empty(events.GetEvents()); // No destroy event yet
    }

    [Fact]
    public void OnTileDestroyed_MultiHPGround_DestroyedAfterMultipleHits()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetGround(pos, new Ground(GroundType.Jelly, health: 2));
        var events = new BufferedEventCollector();

        // Act - First hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);
        // Act - Second hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 2, simTime: 0.2f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(pos, evt.GridPosition);
        Assert.Equal(GroundType.Jelly, evt.Type);
    }

    [Fact]
    public void OnTileDestroyed_InvalidPosition_NoChange()
    {
        // Arrange
        var state = CreateState(8, 8);
        var invalidPos = new Position(-1, -1);
        var events = NullEventCollector.Instance;

        // Act - Should not throw
        _groundSystem.OnTileDestroyed(ref state, invalidPos, tick: 1, simTime: 0.1f, events);

        // Assert - No exception means success
    }

    [Fact]
    public void OnTileDestroyed_DisabledEvents_NoEventEmitted()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 1));
        var events = NullEventCollector.Instance;

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        // NullEventCollector doesn't store events
    }

    [Theory]
    [InlineData(GroundType.Ice)]
    [InlineData(GroundType.Jelly)]
    [InlineData(GroundType.Honey)]
    public void OnTileDestroyed_AllGroundTypes_HandledCorrectly(GroundType groundType)
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(4, 4);
        state.SetGround(pos, new Ground(groundType, health: 1));
        var events = new BufferedEventCollector();

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(groundType, evt.Type);
    }

    #endregion

    #region Event Data Tests

    [Fact]
    public void OnTileDestroyed_EventContainsCorrectTick()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 1));
        var events = new BufferedEventCollector();
        long expectedTick = 42;

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: expectedTick, simTime: 0.1f, events);

        // Assert
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(expectedTick, evt.Tick);
    }

    [Fact]
    public void OnTileDestroyed_EventContainsCorrectSimTime()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 1));
        var events = new BufferedEventCollector();
        float expectedSimTime = 1.5f;

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: expectedSimTime, events);

        // Assert
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(expectedSimTime, evt.SimulationTime);
    }

    #endregion

    #region GameState Ground Access Tests

    [Fact]
    public void HasGround_WithGround_ReturnsTrue()
    {
        // Arrange
        var state = CreateState();
        state.SetGround(3, 3, new Ground(GroundType.Ice, health: 1));

        // Act & Assert
        Assert.True(state.HasGround(3, 3));
        Assert.True(state.HasGround(new Position(3, 3)));
    }

    [Fact]
    public void HasGround_WithoutGround_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();

        // Act & Assert
        Assert.False(state.HasGround(3, 3));
        Assert.False(state.HasGround(new Position(3, 3)));
    }

    [Fact]
    public void GetGround_ReturnsCorrectGround()
    {
        // Arrange
        var state = CreateState();
        var expected = new Ground(GroundType.Jelly, health: 2);
        state.SetGround(3, 3, expected);

        // Act
        ref var ground = ref state.GetGround(3, 3);

        // Assert
        Assert.Equal(GroundType.Jelly, ground.Type);
        Assert.Equal(2, ground.Health);
    }

    [Fact]
    public void SetGround_UpdatesGround()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);

        // Act
        state.SetGround(pos, new Ground(GroundType.Honey, health: 3));

        // Assert
        Assert.Equal(GroundType.Honey, state.GetGround(pos).Type);
        Assert.Equal(3, state.GetGround(pos).Health);
    }

    #endregion

    #region Ice Specific Tests

    [Fact]
    public void Ice_HP1_DestroyedAfterOneHit()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 1));
        var events = new BufferedEventCollector();

        // Act
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(GroundType.Ice, evt.Type);
    }

    [Fact]
    public void Ice_HP2_DestroyedAfterTwoHits()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 2));
        var events = new BufferedEventCollector();

        // Act - First hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert - Still exists
        Assert.Equal(GroundType.Ice, state.GetGround(pos).Type);
        Assert.Equal(1, state.GetGround(pos).Health);
        Assert.Empty(events.GetEvents());

        // Act - Second hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 2, simTime: 0.2f, events);

        // Assert - Destroyed
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
    }

    [Fact]
    public void Ice_HP3_DestroyedAfterThreeHits()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 3));
        var events = new BufferedEventCollector();

        // Act & Assert - First hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);
        Assert.Equal(GroundType.Ice, state.GetGround(pos).Type);
        Assert.Equal(2, state.GetGround(pos).Health);

        // Act & Assert - Second hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 2, simTime: 0.2f, events);
        Assert.Equal(GroundType.Ice, state.GetGround(pos).Type);
        Assert.Equal(1, state.GetGround(pos).Health);

        // Act & Assert - Third hit
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 3, simTime: 0.3f, events);
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<GroundDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(GroundType.Ice, evt.Type);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Ice_ValidHPRange_WorksCorrectly(byte hp)
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: hp));
        var events = new BufferedEventCollector();

        // Act - Hit hp times
        for (int i = 0; i < hp; i++)
        {
            _groundSystem.OnTileDestroyed(ref state, pos, tick: i + 1, simTime: 0.1f * (i + 1), events);
        }

        // Assert - Destroyed after exactly hp hits
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents());
    }

    [Fact]
    public void Ice_DoesNotBlockTileOperations()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Ice, health: 2));

        // Assert - Tile operations should work normally
        // Ice doesn't affect matching, movement, or swapping
        var tile = state.GetTile(pos);
        Assert.NotEqual(TileType.None, tile.Type); // Tile exists above ice
        Assert.True(state.HasGround(pos)); // Ice exists
    }

    #endregion

    #region Multiple Positions Tests

    [Fact]
    public void OnTileDestroyed_MultiplePositions_IndependentHandling()
    {
        // Arrange
        var state = CreateState();
        var pos1 = new Position(2, 2);
        var pos2 = new Position(4, 4);
        state.SetGround(pos1, new Ground(GroundType.Ice, health: 1));
        state.SetGround(pos2, new Ground(GroundType.Jelly, health: 2));
        var events = new BufferedEventCollector();

        // Act - Destroy first position
        _groundSystem.OnTileDestroyed(ref state, pos1, tick: 1, simTime: 0.1f, events);

        // Assert - First destroyed, second untouched
        Assert.Equal(GroundType.None, state.GetGround(pos1).Type);
        Assert.Equal(GroundType.Jelly, state.GetGround(pos2).Type);
        Assert.Equal(2, state.GetGround(pos2).Health);
    }

    [Fact]
    public void OnTileDestroyed_SamePositionMultipleTimes_CorrectDamage()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetGround(pos, new Ground(GroundType.Jelly, health: 3));
        var events = new BufferedEventCollector();

        // Act - Three hits
        _groundSystem.OnTileDestroyed(ref state, pos, tick: 1, simTime: 0.1f, events);
        Assert.Equal(2, state.GetGround(pos).Health);

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 2, simTime: 0.2f, events);
        Assert.Equal(1, state.GetGround(pos).Health);

        _groundSystem.OnTileDestroyed(ref state, pos, tick: 3, simTime: 0.3f, events);

        // Assert
        Assert.Equal(GroundType.None, state.GetGround(pos).Type);
        Assert.Single(events.GetEvents()); // Only one destroy event
    }

    #endregion
}
