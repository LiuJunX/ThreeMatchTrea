using System;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
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
    private readonly CoverSystem _coverSystem = new();

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        // Initialize grid with empty tiles
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.Item1, x, y));
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
    [InlineData(CoverType.Honey)]
    [InlineData(CoverType.Frost)]
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
    [InlineData(CoverType.Frost, false)]   // Frost blocks swap
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
    [InlineData(CoverType.Frost, true)]    // Frost allows match
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
    [InlineData(CoverType.Frost, false)]   // Frost blocks movement
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
    public void CanInteract_SwapLockedTile_ReturnsFalse()
    {
        var state = CreateState();
        state.Lock(new Position(3, 3), CellLockType.Swap);

        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void CanInteract_EmptyCell_ReturnsFalse()
    {
        var state = CreateState();
        state.SetTile(3, 3, default); // ElementType.None

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
    [InlineData(CoverType.Frost, 1)]
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
    [InlineData(CoverType.Frost, false)]
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
    [InlineData(CoverType.Frost, false)]
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
    [InlineData(CoverType.Frost, true)]
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
    [InlineData(CoverType.Frost, true)]
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
    [InlineData(CoverType.Honey)]
    [InlineData(CoverType.Frost)]
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

    #region Honey CoverRules Tests

    [Fact]
    public void CoverRules_Honey_BlocksMatch()
    {
        Assert.True(CoverRules.BlocksMatch(CoverType.Honey));
    }

    [Fact]
    public void CoverRules_Honey_BlocksSwap()
    {
        Assert.True(CoverRules.BlocksSwap(CoverType.Honey));
    }

    [Fact]
    public void CoverRules_Honey_BlocksMovement()
    {
        Assert.True(CoverRules.BlocksMovement(CoverType.Honey));
    }

    [Fact]
    public void CoverRules_Honey_IsNotDynamic()
    {
        Assert.False(CoverRules.IsDynamicType(CoverType.Honey));
    }

    [Fact]
    public void CoverRules_Honey_DefaultHealth_Is1()
    {
        Assert.Equal(1, CoverRules.GetDefaultHealth(CoverType.Honey));
    }

    [Fact]
    public void CoverRules_Honey_DamagedByAdjacent()
    {
        Assert.True(CoverRules.DamagedByAdjacent(CoverType.Honey));
    }

    [Theory]
    [InlineData(CoverType.None)]
    [InlineData(CoverType.Cage)]
    [InlineData(CoverType.Chain)]
    [InlineData(CoverType.Bubble)]
    public void CoverRules_NonAdjacentDamage_NotDamagedByAdjacent(CoverType type)
    {
        Assert.False(CoverRules.DamagedByAdjacent(type));
    }

    #endregion

    #region NotifyBatchElimination Tests

    [Fact]
    public void NotifyBatchElimination_HoneyAdjacentToMatch_Destroyed()
    {
        // Arrange: Honey at (3,3), tile eliminated at (3,2) — directly above
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Match)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Honey destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 3)).Type);
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(CoverType.Honey, evt.Type);
        Assert.Equal(new Position(3, 3), evt.GridPosition);
    }

    [Fact]
    public void NotifyBatchElimination_HoneyAdjacentToMultipleEliminations_DamagedOnce()
    {
        // Arrange: Honey at (3,3), eliminated tiles at (3,2) and (2,3) — both adjacent
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 2));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Match),
            new(new Position(2, 3), new Tile(2, ElementType.Item1, 2, 3), ElimSource.Match)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Honey damaged only once (dedup), still alive
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
        Assert.Equal(1, state.GetCover(new Position(3, 3)).Health);
    }

    [Fact]
    public void NotifyBatchElimination_CageNotAffectedByAdjacency()
    {
        // Arrange: Cage at (3,3), tile eliminated at (3,2)
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Cage, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Match)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Cage unchanged
        Assert.Equal(CoverType.Cage, state.GetCover(new Position(3, 3)).Type);
        Assert.Equal(1, state.GetCover(new Position(3, 3)).Health);
    }

    [Fact]
    public void NotifyBatchElimination_BombSource_DoesNotTriggerAdjacency()
    {
        // Arrange: Honey at (3,3), tile eliminated at (3,2) by Bomb
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Bomb)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Honey unchanged — Bomb source doesn't trigger adjacency
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
        Assert.Equal(1, state.GetCover(new Position(3, 3)).Health);
    }

    [Fact]
    public void NotifyBatchElimination_ColorBombSource_TriggersAdjacency()
    {
        // Arrange: Honey at (3,3), tile eliminated at (3,2) by ColorBomb
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.ColorBomb)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Honey destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 3)).Type);
    }

    [Fact]
    public void NotifyBatchElimination_HoneyNotAdjacentToElimination_Unchanged()
    {
        // Arrange: Honey at (3,3), tile eliminated at (0,0) — not adjacent
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(0, 0), new Tile(1, ElementType.Item1, 0, 0), ElimSource.Match)
        };

        // Act
        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        // Assert: Honey unchanged
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
        Assert.Equal(1, state.GetCover(new Position(3, 3)).Health);
    }

    [Fact]
    public void NotifyBatchElimination_AllFourDirections_DamageHoney()
    {
        // Arrange: Honey at (3,3), test each direction independently
        var honeyPos = new Position(3, 3);

        var directions = new[]
        {
            new Position(2, 3), // left
            new Position(4, 3), // right
            new Position(3, 2), // up
            new Position(3, 4), // down
        };

        foreach (var dir in directions)
        {
            var state = CreateState();
            state.SetCover(honeyPos, new Cover(CoverType.Honey, health: 1));
            var events = new BufferedEventCollector();

            var eliminated = new EliminatedTileInfo[]
            {
                new(dir, new Tile(1, ElementType.Item1, dir.X, dir.Y), ElimSource.Match)
            };

            _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

            Assert.Equal(CoverType.None, state.GetCover(honeyPos).Type);
        }
    }

    #endregion

    #region NotifyBatchElimination Edge Cases

    [Fact]
    public void NotifyBatchElimination_EmptySpan_NoCrash()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        _coverSystem.NotifyBatchElimination(ref state, ReadOnlySpan<EliminatedTileInfo>.Empty, 1, 0.1f, events);

        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
        Assert.Empty(events.GetEvents());
    }

    [Fact]
    public void NotifyBatchElimination_MultipleHoneysAdjacentToSameElimination_AllDamaged()
    {
        // Eliminated tile at (3,3), Honey at (2,3) and (4,3) and (3,2) and (3,4)
        var state = CreateState();
        state.SetCover(new Position(2, 3), new Cover(CoverType.Honey, health: 1));
        state.SetCover(new Position(4, 3), new Cover(CoverType.Honey, health: 1));
        state.SetCover(new Position(3, 2), new Cover(CoverType.Honey, health: 1));
        state.SetCover(new Position(3, 4), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 3), new Tile(1, ElementType.Item1, 3, 3), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 3)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(4, 3)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 2)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 4)).Type);

        var destroyEvents = events.GetEvents().OfType<CoverDestroyedEvent>().ToList();
        Assert.Equal(4, destroyEvents.Count);
    }

    [Fact]
    public void NotifyBatchElimination_HoneyAtBoardCorner_SafeWithPartialNeighbors()
    {
        // Honey at (0,0) — only 2 valid neighbors: (1,0) and (0,1)
        var state = CreateState();
        state.SetCover(new Position(0, 0), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        // Eliminated tile at (1,0) — adjacent to Honey at (0,0)
        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(1, 0), new Tile(1, ElementType.Item1, 1, 0), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 0)).Type);
    }

    [Fact]
    public void NotifyBatchElimination_HoneyAtBoardEdge_NoCrashOnOutOfBounds()
    {
        // Honey at (7,7) — only (6,7) and (7,6) are valid
        // Eliminated at (6,7) — should damage Honey without crash
        var state = CreateState();
        state.SetCover(new Position(7, 7), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(6, 7), new Tile(1, ElementType.Item1, 6, 7), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.None, state.GetCover(new Position(7, 7)).Type);
    }

    [Theory]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ChainReaction)]
    [InlineData(ElimSource.SideItem)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void NotifyBatchElimination_NonTriggeringSources_HoneyUnchanged(ElimSource source)
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), source)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
        Assert.Equal(1, state.GetCover(new Position(3, 3)).Health);
    }

    [Fact]
    public void NotifyBatchElimination_DiagonalPosition_NoAdjacencyDamage()
    {
        // Honey at (3,3), eliminated at (4,4) — diagonal, not adjacent
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(4, 4), new Tile(1, ElementType.Item1, 4, 4), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.Honey, state.GetCover(new Position(3, 3)).Type);
    }

    #endregion

    #region Honey GameState Integration Tests

    [Fact]
    public void GameState_HoneyCover_CanInteract_ReturnsFalse()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        Assert.False(state.CanInteract(3, 3));
    }

    [Fact]
    public void GameState_HoneyCover_CanMatch_ReturnsFalse()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        Assert.False(state.CanMatch(3, 3));
    }

    [Fact]
    public void GameState_HoneyCover_CanMove_ReturnsFalse()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        Assert.False(state.CanMove(3, 3));
    }

    [Fact]
    public void GameState_HoneyCover_CanMoveIgnoringLocks_ReturnsFalse()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Honey, health: 1));
        Assert.False(state.CanMoveIgnoringLocks(3, 3));
    }

    #endregion

    #region Frost CoverRules Tests

    [Fact]
    public void CoverRules_Frost_DoesNotBlockMatch()
    {
        Assert.False(CoverRules.BlocksMatch(CoverType.Frost));
    }

    [Fact]
    public void CoverRules_Frost_BlocksSwap()
    {
        Assert.True(CoverRules.BlocksSwap(CoverType.Frost));
    }

    [Fact]
    public void CoverRules_Frost_BlocksMovement()
    {
        Assert.True(CoverRules.BlocksMovement(CoverType.Frost));
    }

    [Fact]
    public void CoverRules_Frost_IsNotDynamic()
    {
        Assert.False(CoverRules.IsDynamicType(CoverType.Frost));
    }

    [Fact]
    public void CoverRules_Frost_DefaultHealth_Is1()
    {
        Assert.Equal(1, CoverRules.GetDefaultHealth(CoverType.Frost));
    }

    [Fact]
    public void CoverRules_Frost_DamagedByAdjacent()
    {
        Assert.True(CoverRules.DamagedByAdjacent(CoverType.Frost));
    }

    #endregion

    #region Frost NotifyBatchElimination Tests

    [Fact]
    public void NotifyBatchElimination_FrostAdjacentToMatch_Destroyed()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 3)).Type);
        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.Equal(CoverType.Frost, evt.Type);
    }

    [Fact]
    public void NotifyBatchElimination_FrostNotAdjacentToElimination_Unchanged()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(0, 0), new Tile(1, ElementType.Item1, 0, 0), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.Frost, state.GetCover(new Position(3, 3)).Type);
    }

    [Fact]
    public void GameState_FrostCover_CanMoveIgnoringLocks_ReturnsFalse()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        Assert.False(state.CanMoveIgnoringLocks(3, 3));
    }

    [Fact]
    public void NotifyBatchElimination_FrostAdjacentToColorBomb_Destroyed()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.ColorBomb)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 3)).Type);
    }

    [Fact]
    public void NotifyBatchElimination_FrostAllFourDirections_AllDestroyed()
    {
        var frostPos = new Position(3, 3);

        var directions = new[]
        {
            new Position(2, 3), // left
            new Position(4, 3), // right
            new Position(3, 2), // up
            new Position(3, 4), // down
        };

        foreach (var dir in directions)
        {
            var state = CreateState();
            state.SetCover(frostPos, new Cover(CoverType.Frost, health: 1));
            var events = new BufferedEventCollector();

            var eliminated = new EliminatedTileInfo[]
            {
                new(dir, new Tile(1, ElementType.Item1, dir.X, dir.Y), ElimSource.Match)
            };

            _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

            Assert.Equal(CoverType.None, state.GetCover(frostPos).Type);
        }
    }

    [Fact]
    public void NotifyBatchElimination_FrostDiagonal_NotDamaged()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(4, 4), new Tile(1, ElementType.Item1, 4, 4), ElimSource.Match)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.Frost, state.GetCover(new Position(3, 3)).Type);
    }

    [Fact]
    public void NotifyBatchElimination_BombSource_DoesNotTriggerFrostAdjacency()
    {
        var state = CreateState();
        state.SetCover(new Position(3, 3), new Cover(CoverType.Frost, health: 1));
        var events = new BufferedEventCollector();

        var eliminated = new EliminatedTileInfo[]
        {
            new(new Position(3, 2), new Tile(1, ElementType.Item1, 3, 2), ElimSource.Bomb)
        };

        _coverSystem.NotifyBatchElimination(ref state, new ReadOnlySpan<EliminatedTileInfo>(eliminated), 1, 0.1f, events);

        Assert.Equal(CoverType.Frost, state.GetCover(new Position(3, 3)).Type);
    }

    #endregion
}

