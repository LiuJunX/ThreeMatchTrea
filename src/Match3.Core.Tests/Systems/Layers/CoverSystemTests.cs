using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Layers;

/// <summary>
/// CoverSystem unit tests.
///
/// Responsibilities:
/// - Managing cover element damage and destruction
/// - Checking if tiles are protected by covers
/// - Syncing dynamic covers with tile movement
/// </summary>
public class CoverSystemTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private readonly CoverSystem _coverSystem = new();

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

    #region TryDamageCover Tests

    [Fact]
    public void TryDamageCover_NoCover_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        var events = new BufferedEventCollector();

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.False(destroyed);
        Assert.Empty(events.GetEvents());
    }

    [Fact]
    public void TryDamageCover_SingleHPCover_DestroysCover()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 1));
        var events = new BufferedEventCollector();

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.True(destroyed);
        Assert.Equal(CoverType.None, state.GetCover(pos).Type);
        Assert.Single(events.GetEvents());
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(pos, evt.GridPosition);
        Assert.Equal(CoverType.Cage, evt.Type);
    }

    [Fact]
    public void TryDamageCover_MultiHPCover_ReducesHealth()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 2));
        var events = new BufferedEventCollector();

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.False(destroyed); // Not destroyed yet
        Assert.Equal(CoverType.Cage, state.GetCover(pos).Type);
        Assert.Equal(1, state.GetCover(pos).Health);
        Assert.Empty(events.GetEvents()); // No destroy event
    }

    [Fact]
    public void TryDamageCover_MultiHPCover_DestroyedAfterMultipleHits()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(2, 2);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 2));
        var events = new BufferedEventCollector();

        // Act - First hit
        bool destroyed1 = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);
        // Act - Second hit
        bool destroyed2 = _coverSystem.TryDamageCover(ref state, pos, tick: 2, simTime: 0.2f, events);

        // Assert
        Assert.False(destroyed1);
        Assert.True(destroyed2);
        Assert.Equal(CoverType.None, state.GetCover(pos).Type);
        Assert.Single(events.GetEvents());
    }

    [Fact]
    public void TryDamageCover_InvalidPosition_ReturnsFalse()
    {
        // Arrange
        var state = CreateState(8, 8);
        var invalidPos = new Position(-1, -1);
        var events = NullEventCollector.Instance;

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, invalidPos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.False(destroyed);
    }

    [Fact]
    public void TryDamageCover_DisabledEvents_NoEventEmitted()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 1));
        var events = NullEventCollector.Instance;

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.True(destroyed);
        Assert.Equal(CoverType.None, state.GetCover(pos).Type);
        // NullEventCollector doesn't store events, so no exception means success
    }

    #endregion

    #region IsTileProtected Tests

    [Fact]
    public void IsTileProtected_NoCover_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(4, 4);

        // Act
        bool isProtected = _coverSystem.IsTileProtected(in state, pos);

        // Assert
        Assert.False(isProtected);
    }

    [Fact]
    public void IsTileProtected_WithCover_ReturnsTrue()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(4, 4);
        state.SetCover(pos, new Cover(CoverType.Chain, health: 1));

        // Act
        bool isProtected = _coverSystem.IsTileProtected(in state, pos);

        // Assert
        Assert.True(isProtected);
    }

    [Fact]
    public void IsTileProtected_DestroyedCover_ReturnsFalse()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(4, 4);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 0));

        // Act
        bool isProtected = _coverSystem.IsTileProtected(in state, pos);

        // Assert
        Assert.False(isProtected);
    }

    [Fact]
    public void IsTileProtected_InvalidPosition_ReturnsFalse()
    {
        // Arrange
        var state = CreateState(8, 8);
        var invalidPos = new Position(100, 100);

        // Act
        bool isProtected = _coverSystem.IsTileProtected(in state, invalidPos);

        // Assert
        Assert.False(isProtected);
    }

    [Theory]
    [InlineData(CoverType.Cage)]
    [InlineData(CoverType.Chain)]
    [InlineData(CoverType.Bubble)]
    public void IsTileProtected_AllCoverTypes_ReturnsTrue(CoverType coverType)
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(coverType, health: 1));

        // Act
        bool isProtected = _coverSystem.IsTileProtected(in state, pos);

        // Assert
        Assert.True(isProtected);
    }

    #endregion

    #region SyncDynamicCovers Tests

    [Fact]
    public void SyncDynamicCovers_StaticCover_DoesNotMove()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(2, 2);
        var to = new Position(2, 3);
        state.SetCover(from, new Cover(CoverType.Cage, health: 1, isDynamic: false));

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(CoverType.Cage, state.GetCover(from).Type);
        Assert.Equal(CoverType.None, state.GetCover(to).Type);
    }

    [Fact]
    public void SyncDynamicCovers_DynamicCover_MovesToNewPosition()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(2, 2);
        var to = new Position(2, 3);
        state.SetCover(from, new Cover(CoverType.Bubble, health: 1, isDynamic: true));

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(CoverType.None, state.GetCover(from).Type);
        Assert.Equal(CoverType.Bubble, state.GetCover(to).Type);
        Assert.True(state.GetCover(to).IsDynamic);
    }

    [Fact]
    public void SyncDynamicCovers_NoCover_NoChange()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(2, 2);
        var to = new Position(2, 3);
        // No cover at 'from' position

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(CoverType.None, state.GetCover(from).Type);
        Assert.Equal(CoverType.None, state.GetCover(to).Type);
    }

    [Fact]
    public void SyncDynamicCovers_InvalidFromPosition_NoChange()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(-1, -1);
        var to = new Position(2, 3);
        state.SetCover(to, new Cover(CoverType.Chain, health: 1));

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(CoverType.Chain, state.GetCover(to).Type); // Unchanged
    }

    [Fact]
    public void SyncDynamicCovers_InvalidToPosition_NoChange()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(2, 2);
        var to = new Position(-1, -1);
        state.SetCover(from, new Cover(CoverType.Bubble, health: 1, isDynamic: true));

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(CoverType.Bubble, state.GetCover(from).Type); // Unchanged
    }

    [Fact]
    public void SyncDynamicCovers_PreservesHealth()
    {
        // Arrange
        var state = CreateState();
        var from = new Position(2, 2);
        var to = new Position(2, 3);
        state.SetCover(from, new Cover(CoverType.Bubble, health: 3, isDynamic: true));

        // Act
        _coverSystem.SyncDynamicCovers(ref state, from, to);

        // Assert
        Assert.Equal(3, state.GetCover(to).Health);
    }

    #endregion

    #region GameState Cover Interaction Tests

    [Theory]
    [InlineData(CoverType.Cage, false)]    // Cage blocks swap
    [InlineData(CoverType.Chain, false)]   // Chain blocks swap
    [InlineData(CoverType.Bubble, false)]  // Bubble blocks swap
    [InlineData(CoverType.None, true)]     // No cover allows swap
    public void CanInteract_WithCoverType_ReturnsExpected(CoverType coverType, bool expectedCanInteract)
    {
        // Arrange
        var state = CreateState();
        if (coverType != CoverType.None)
        {
            state.SetCover(3, 3, new Cover(coverType, health: 1));
        }

        // Act
        bool canInteract = state.CanInteract(3, 3);

        // Assert
        Assert.Equal(expectedCanInteract, canInteract);
    }

    [Theory]
    [InlineData(CoverType.Cage, false)]    // Cage blocks match
    [InlineData(CoverType.Chain, true)]    // Chain allows match
    [InlineData(CoverType.Bubble, true)]   // Bubble allows match
    [InlineData(CoverType.None, true)]     // No cover allows match
    public void CanMatch_WithCoverType_ReturnsExpected(CoverType coverType, bool expectedCanMatch)
    {
        // Arrange
        var state = CreateState();
        if (coverType != CoverType.None)
        {
            state.SetCover(3, 3, new Cover(coverType, health: 1));
        }

        // Act
        bool canMatch = state.CanMatch(3, 3);

        // Assert
        Assert.Equal(expectedCanMatch, canMatch);
    }

    [Theory]
    [InlineData(CoverType.Cage, false)]    // Cage blocks movement
    [InlineData(CoverType.Chain, false)]   // Chain blocks movement
    [InlineData(CoverType.Bubble, true)]   // Bubble allows movement (dynamic)
    [InlineData(CoverType.None, true)]     // No cover allows movement
    public void CanMove_WithCoverType_ReturnsExpected(CoverType coverType, bool expectedCanMove)
    {
        // Arrange
        var state = CreateState();
        if (coverType != CoverType.None)
        {
            state.SetCover(3, 3, new Cover(coverType, health: 1));
        }

        // Act
        bool canMove = state.CanMove(3, 3);

        // Assert
        Assert.Equal(expectedCanMove, canMove);
    }

    #endregion

    #region CanInteract — Tile State Tests

    [Fact]
    public void CanInteract_StableTile_ReturnsTrue()
    {
        var state = CreateState();
        // CreateState fills all tiles as Red, stable (no flags)
        Assert.True(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_FallingTile_ReturnsFalse()
    {
        var state = CreateState();
        var tile = state.GetTile(3, 3);
        tile.IsFalling = true;
        state.SetTile(3, 3, tile);

        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_SuspendedTile_ReturnsFalse()
    {
        var state = CreateState();
        var tile = state.GetTile(3, 3);
        tile.IsSuspended = true;
        state.SetTile(3, 3, tile);

        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_EmptyCell_ReturnsFalse()
    {
        var state = CreateState();
        state.SetTile(3, 3, default); // TileType.None

        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_FallingTileWithCover_ReturnsFalse()
    {
        // Both cover and falling should block — cover check comes first
        var state = CreateState();
        var tile = state.GetTile(3, 3);
        tile.IsFalling = true;
        state.SetTile(3, 3, tile);
        state.SetCover(3, 3, new Cover(CoverType.Cage, health: 1));

        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_StableTileNoCover_AdjacentTileFalling_BothCheckedIndependently()
    {
        // Tile at (3,3) is stable → can interact
        // Tile at (3,4) is falling → cannot interact
        // They are checked independently (no global lock)
        var state = CreateState();
        var fallingTile = state.GetTile(3, 4);
        fallingTile.IsFalling = true;
        state.SetTile(3, 4, fallingTile);

        Assert.True(state.CanInteract(3, 3), "Stable tile should be interactable");
        Assert.False(state.CanInteract(3, 4), "Falling tile should not be interactable");
    }

    #endregion

    #region CoverRules Tests

    [Theory]
    [InlineData(CoverType.None, 0)]
    [InlineData(CoverType.Cage, 1)]
    [InlineData(CoverType.Chain, 1)]
    [InlineData(CoverType.Bubble, 1)]
    public void GetDefaultHealth_ReturnsExpectedValue(CoverType coverType, byte expectedHealth)
    {
        // Act
        byte health = CoverRules.GetDefaultHealth(coverType);

        // Assert
        Assert.Equal(expectedHealth, health);
    }

    [Theory]
    [InlineData(CoverType.None, false)]
    [InlineData(CoverType.Cage, false)]
    [InlineData(CoverType.Chain, false)]
    [InlineData(CoverType.Bubble, true)]
    public void IsDynamicType_ReturnsExpectedValue(CoverType coverType, bool expectedIsDynamic)
    {
        // Act
        bool isDynamic = CoverRules.IsDynamicType(coverType);

        // Assert
        Assert.Equal(expectedIsDynamic, isDynamic);
    }

    [Theory]
    [InlineData(CoverType.None, false)]
    [InlineData(CoverType.Cage, true)]
    [InlineData(CoverType.Chain, false)]
    [InlineData(CoverType.Bubble, false)]
    public void BlocksMatch_ReturnsExpectedValue(CoverType coverType, bool expectedBlocks)
    {
        // Act
        bool blocks = CoverRules.BlocksMatch(coverType);

        // Assert
        Assert.Equal(expectedBlocks, blocks);
    }

    [Theory]
    [InlineData(CoverType.None, false)]
    [InlineData(CoverType.Cage, true)]
    [InlineData(CoverType.Chain, true)]
    [InlineData(CoverType.Bubble, true)]
    public void BlocksSwap_ReturnsExpectedValue(CoverType coverType, bool expectedBlocks)
    {
        // Act
        bool blocks = CoverRules.BlocksSwap(coverType);

        // Assert
        Assert.Equal(expectedBlocks, blocks);
    }

    [Theory]
    [InlineData(CoverType.None, false)]
    [InlineData(CoverType.Cage, true)]
    [InlineData(CoverType.Chain, true)]
    [InlineData(CoverType.Bubble, false)]
    public void BlocksMovement_ReturnsExpectedValue(CoverType coverType, bool expectedBlocks)
    {
        // Act
        bool blocks = CoverRules.BlocksMovement(coverType);

        // Assert
        Assert.Equal(expectedBlocks, blocks);
    }

    #endregion

    #region Cover Type Specific Destruction Tests

    [Theory]
    [InlineData(CoverType.Cage)]
    [InlineData(CoverType.Chain)]
    [InlineData(CoverType.Bubble)]
    public void TryDamageCover_AllCoverTypes_EmitsCorrectEvent(CoverType coverType)
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(coverType, health: 1));
        var events = new BufferedEventCollector();

        // Act
        bool destroyed = _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: 0.1f, events);

        // Assert
        Assert.True(destroyed);
        Assert.Equal(CoverType.None, state.GetCover(pos).Type);
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(coverType, evt.Type);
        Assert.Equal(pos, evt.GridPosition);
    }

    #endregion

    #region Event Data Tests

    [Fact]
    public void TryDamageCover_EventContainsCorrectTick()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(CoverType.Cage, health: 1));
        var events = new BufferedEventCollector();
        int expectedTick = 42;

        // Act
        _coverSystem.TryDamageCover(ref state, pos, tick: expectedTick, simTime: 0.1f, events);

        // Assert
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(expectedTick, evt.Tick);
    }

    [Fact]
    public void TryDamageCover_EventContainsCorrectSimTime()
    {
        // Arrange
        var state = CreateState();
        var pos = new Position(3, 3);
        state.SetCover(pos, new Cover(CoverType.Chain, health: 1));
        var events = new BufferedEventCollector();
        float expectedSimTime = 1.5f;

        // Act
        _coverSystem.TryDamageCover(ref state, pos, tick: 1, simTime: expectedSimTime, events);

        // Assert
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(expectedSimTime, evt.SimulationTime);
    }

    #endregion
}
