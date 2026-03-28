using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Swap;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Swap;

public class SwapOperationsTests
{
    private class StubMatchFinder : IMatchFinder
    {
        public bool AlwaysMatch { get; set; } = false;
        public List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null) => new();
        public bool HasMatches(in GameState state) => AlwaysMatch;
        public bool HasMatchAt(in GameState state, Position p) => AlwaysMatch;
    }

    private class TestSwapContext : ISwapContext
    {
        public bool SyncPositionOnSwap { get; set; } = true;
        public float AnimationDuration { get; set; } = 0.15f;
        public int RevertEventCount { get; private set; } = 0;

        public bool IsSwapAnimationComplete(in GameState state, Position a, Position b, float animationTime)
        {
            return animationTime >= AnimationDuration;
        }

        public void EmitRevertEvent(in GameState state, Position from, Position to, int tick, float simTime, IEventCollector events)
        {
            RevertEventCount++;
        }
    }

    private GameState CreateTestState()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4 };

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = y * 5 + x;
                var type = types[(x + y * 2) % types.Length];
                state.SetTile(x, y, new Tile(idx + 1, type, x, y));
            }
        }

        return state;
    }

    #region SwapTiles Tests

    [Fact]
    public void SwapTiles_SwapsGridData()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var tileABefore = state.GetTile(0, 0);
        var tileBBefore = state.GetTile(1, 0);

        ops.SwapTiles(ref state, new Position(0, 0), new Position(1, 0));

        var tileAAfter = state.GetTile(0, 0);
        var tileBAfter = state.GetTile(1, 0);

        Assert.Equal(tileBBefore.Type, tileAAfter.Type);
        Assert.Equal(tileABefore.Type, tileBAfter.Type);
    }

    [Fact]
    public void SwapTiles_SyncsPosition_WhenContextRequires()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext { SyncPositionOnSwap = true };
        var ops = new SwapOperations(matchFinder, context);

        ops.SwapTiles(ref state, new Position(0, 0), new Position(1, 0));

        var tileA = state.GetTile(0, 0);
        var tileB = state.GetTile(1, 0);

        Assert.Equal(0, tileA.Position.X);
        Assert.Equal(0, tileA.Position.Y);
        Assert.Equal(1, tileB.Position.X);
        Assert.Equal(0, tileB.Position.Y);
    }

    [Fact]
    public void SwapTiles_DoesNotSyncPosition_WhenContextDoesNotRequire()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext { SyncPositionOnSwap = false };
        var ops = new SwapOperations(matchFinder, context);

        // Set initial positions that differ from grid
        var tileA = state.GetTile(0, 0);
        tileA.Position = new System.Numerics.Vector2(0.5f, 0.5f);
        state.SetTile(0, 0, tileA);

        ops.SwapTiles(ref state, new Position(0, 0), new Position(1, 0));

        // After swap, the tile at (0,0) should keep its original position
        var swappedTile = state.GetTile(0, 0);
        // Position is preserved from original tile (was at 1,0)
        Assert.Equal(1, swappedTile.Position.X);
    }

    #endregion

    #region HasMatch Tests

    [Fact]
    public void HasMatch_DelegatesToMatchFinder()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var result = ops.HasMatch(in state, new Position(0, 0));

        Assert.True(result);
    }

    [Fact]
    public void HasMatch_ReturnsFalse_WhenNoMatch()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder { AlwaysMatch = false };
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var result = ops.HasMatch(in state, new Position(0, 0));

        Assert.False(result);
    }

    #endregion

    #region ValidatePendingMove Tests

    [Fact]
    public void ValidatePendingMove_ReturnsTrueImmediately_WhenNoPending()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var pending = PendingMoveState.None;

        var result = ops.ValidatePendingMove(ref state, ref pending, 0.016f, 0, 0f, NullEventCollector.Instance);

        Assert.True(result);
    }

    [Fact]
    public void ValidatePendingMove_ReturnsFalse_WhenAnimationNotComplete()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        var pending = new PendingMoveState
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            HadMatch = false,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        var result = ops.ValidatePendingMove(ref state, ref pending, 0.05f, 0, 0f, NullEventCollector.Instance);

        Assert.False(result);
        Assert.True(pending.NeedsValidation);
        Assert.Equal(0.05f, pending.AnimationTime, 0.001f);
    }

    [Fact]
    public void ValidatePendingMove_EmitsRevertEvent_WhenNoMatch_WithoutSwappingData()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder { AlwaysMatch = false };
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        // Data is NOT pre-swapped (new design: non-matching moves don't touch grid data)
        var originalTileA = state.GetTile(0, 0).Type;
        var originalTileB = state.GetTile(1, 0).Type;

        var pending = new PendingMoveState
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            HadMatch = false,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        ops.ValidatePendingMove(ref state, ref pending, 0.2f, 0, 0f, NullEventCollector.Instance);

        // Data unchanged — tiles stay at original positions
        Assert.Equal(originalTileA, state.GetTile(0, 0).Type);
        Assert.Equal(originalTileB, state.GetTile(1, 0).Type);
        // Revert event emitted for visual layer
        Assert.Equal(1, context.RevertEventCount);
    }

    [Fact]
    public void ValidatePendingMove_DoesNotRevert_WhenHadMatch()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        // Do initial swap
        var swappedTileA = state.GetTile(1, 0).Type;
        ops.SwapTiles(ref state, new Position(0, 0), new Position(1, 0));

        var pending = new PendingMoveState
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            HadMatch = true, // Match was found
            NeedsValidation = true,
            AnimationTime = 0f
        };

        // Run validation
        ops.ValidatePendingMove(ref state, ref pending, 0.2f, 0, 0f, NullEventCollector.Instance);

        // Swap should NOT be reverted
        Assert.Equal(swappedTileA, state.GetTile(0, 0).Type);
        Assert.Equal(0, context.RevertEventCount);
    }

    [Fact]
    public void ValidatePendingMove_AccumulatesAnimationTime()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        var pending = new PendingMoveState
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            HadMatch = false,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        // First tick - not enough time
        ops.ValidatePendingMove(ref state, ref pending, 0.05f, 0, 0f, NullEventCollector.Instance);
        Assert.Equal(0.05f, pending.AnimationTime, 0.001f);
        Assert.True(pending.NeedsValidation);

        // Second tick - still not enough
        ops.ValidatePendingMove(ref state, ref pending, 0.05f, 0, 0f, NullEventCollector.Instance);
        Assert.Equal(0.10f, pending.AnimationTime, 0.001f);
        Assert.True(pending.NeedsValidation);

        // Third tick - now complete
        ops.ValidatePendingMove(ref state, ref pending, 0.05f, 0, 0f, NullEventCollector.Instance);
        Assert.False(pending.NeedsValidation);
    }

    #endregion

    #region Empty Cell Swap Tests

    private GameState CreateStateWithEmptyCell()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = y * 5 + x;
                state.SetTile(x, y, new Tile(idx + 1, ElementType.Item1, x, y));
            }
        }

        // Make (2,2) a bare empty cell
        state.SetTile(2, 2, default);
        return state;
    }

    [Fact]
    public void SwapTiles_WorksWithEmptyCell_Vertical()
    {
        var state = CreateStateWithEmptyCell();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var tileBefore = state.GetTile(2, 1);
        Assert.NotEqual(ElementType.None, tileBefore.Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);

        ops.SwapTiles(ref state, new Position(2, 1), new Position(2, 2));

        Assert.Equal(tileBefore.Type, state.GetTile(2, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);
    }

    [Fact]
    public void SwapTiles_WorksWithEmptyCell_Horizontal()
    {
        var state = CreateStateWithEmptyCell();
        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var tileBefore = state.GetTile(1, 2);
        ops.SwapTiles(ref state, new Position(1, 2), new Position(2, 2));

        Assert.Equal(tileBefore.Type, state.GetTile(2, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 2).Type);
    }

    [Fact]
    public void SwapTiles_WorksWithEmptyCell_AtBoardEdge()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, ElementType.Item1, x, y));

        // Empty cell at corner (0,0)
        state.SetTile(0, 0, default);

        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var ops = new SwapOperations(matchFinder, context);

        var tileBefore = state.GetTile(1, 0);
        ops.SwapTiles(ref state, new Position(1, 0), new Position(0, 0));

        Assert.Equal(tileBefore.Type, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 0).Type);
    }

    [Fact]
    public void ValidatePendingMove_EmitsRevertEvent_EmptySwapNoMatch_DataUnchanged()
    {
        var state = CreateStateWithEmptyCell();
        var matchFinder = new StubMatchFinder { AlwaysMatch = false };
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        // Data NOT pre-swapped (non-matching moves don't touch grid data)
        var tileType = state.GetTile(2, 1).Type;

        var pending = new PendingMoveState
        {
            From = new Position(2, 1),
            To = new Position(2, 2),
            HadMatch = false,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        ops.ValidatePendingMove(ref state, ref pending, 0.2f, 0, 0f, NullEventCollector.Instance);

        // Data unchanged — tile stays at (2,1), empty at (2,2)
        Assert.Equal(tileType, state.GetTile(2, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
        Assert.Equal(1, context.RevertEventCount);
    }

    [Fact]
    public void ValidatePendingMove_KeepsEmptySwap_WhenHadMatch()
    {
        var state = CreateStateWithEmptyCell();
        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        var tileType = state.GetTile(2, 1).Type;
        ops.SwapTiles(ref state, new Position(2, 1), new Position(2, 2));

        var pending = new PendingMoveState
        {
            From = new Position(2, 1),
            To = new Position(2, 2),
            HadMatch = true,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        ops.ValidatePendingMove(ref state, ref pending, 0.2f, 0, 0f, NullEventCollector.Instance);

        // Tile stays at (2,2), empty at (2,1)
        Assert.Equal(tileType, state.GetTile(2, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);
        Assert.Equal(0, context.RevertEventCount);
    }

    #endregion

    #region ApplyMove Empty Cell Integration Tests

    private class StubPowerUpHandler : IPowerUpHandler
    {
        public void ProcessBombSwap(ref GameState state, Position p1, Position p2, out int points) => points = 0;
        public void ProcessBombSwap(ref GameState state, Position p1, Position p2, int tick, float simTime, IEventCollector events, out int points) => points = 0;
        public void ActivateBomb(ref GameState state, Position p) { }
        public void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events, bool isChainReaction = false) { }
        public void ActivateChainBomb(ref GameState state, Position p, Tile bombTile, int tick, float simTime, IEventCollector events) { }
    }

    [Fact]
    public void ApplyMove_AcceptsSwapWithBareEmptyCell()
    {
        var state = CreateStateWithEmptyCell(); // (2,2) is empty
        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        bool result = handler.ApplyMove(
            new Position(2, 1), new Position(2, 2),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.True(result);
        Assert.True(pending.NeedsValidation);
        // Tile moved to (2,2), empty at (2,1)
        Assert.NotEqual(ElementType.None, state.GetTile(2, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 1).Type);
    }

    [Fact]
    public void ApplyMove_RejectsSwapWithObstacleEmptyCell()
    {
        var state = CreateStateWithEmptyCell();
        state.SetObstacle(2, 2, new Obstacle { Type = ObstacleType.Box, Stage = 1 });

        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        bool result = handler.ApplyMove(
            new Position(2, 1), new Position(2, 2),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.False(result);
    }

    [Fact]
    public void ApplyMove_AcceptsEmptySwap_AtBoardEdge()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, ElementType.Item1, x, y));
        state.SetTile(0, 0, default); // corner empty

        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        // Horizontal: (1,0) → (0,0) empty corner
        bool result = handler.ApplyMove(
            new Position(1, 0), new Position(0, 0),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.True(result);
    }

    [Fact]
    public void ApplyMove_RejectsBothEmpty()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        // All tiles default to None, cells are Slot

        var matchFinder = new StubMatchFinder();
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        bool result = handler.ApplyMove(
            new Position(0, 0), new Position(1, 0),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.False(result);
    }

    [Fact]
    public void ApplyMove_RejectsFromEmpty_ToTile()
    {
        // FROM must have a tile — starting from empty cell is not allowed
        var state = CreateStateWithEmptyCell(); // (2,2) is empty
        var matchFinder = new StubMatchFinder { AlwaysMatch = true };
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        // Swap FROM empty (2,2) TO tile (2,1) — should be rejected
        bool result = handler.ApplyMove(
            new Position(2, 2), new Position(2, 1),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.False(result);
    }

    [Fact]
    public void ApplyMove_NoDataChange_WhenNoMatch()
    {
        var state = CreateStateWithEmptyCell(); // (2,2) is empty
        var matchFinder = new StubMatchFinder { AlwaysMatch = false };
        var context = new TestSwapContext();
        var swapOps = new SwapOperations(matchFinder, context);
        var handler = new SimulationInputHandler(swapOps, new StubPowerUpHandler());

        var tileType = state.GetTile(2, 1).Type;
        var pending = PendingMoveState.None;
        var lastFrom = Position.Invalid;
        var lastTo = Position.Invalid;

        // Swap attempt: tile (2,1) → empty (2,2), no match
        bool result = handler.ApplyMove(
            new Position(2, 1), new Position(2, 2),
            ref state, ref pending, ref lastFrom, ref lastTo,
            0, 0f, NullEventCollector.Instance);

        Assert.True(result); // Swap accepted (will revert visually)
        // Grid data unchanged — tile still at (2,1), empty still at (2,2)
        Assert.Equal(tileType, state.GetTile(2, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);
        Assert.False(pending.HadMatch);
    }

    #endregion

    #region IsEmptySwapTarget Tests

    [Fact]
    public void IsEmptySwapTarget_ReturnsTrueForBareSlot()
    {
        var state = new GameState(3, 3, 4, new StubRandom());
        // Default: all Slot, no tiles, no obstacles, no covers
        Assert.True(state.IsEmptySwapTarget(1, 1));
    }

    [Fact]
    public void IsEmptySwapTarget_ReturnsFalseForVoid()
    {
        var state = new GameState(3, 3, 4, new StubRandom());
        state.SetCell(1, 1, CellKind.Void);
        Assert.False(state.IsEmptySwapTarget(1, 1));
    }

    [Fact]
    public void IsEmptySwapTarget_ReturnsFalseWhenTileExists()
    {
        var state = new GameState(3, 3, 4, new StubRandom());
        state.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
        Assert.False(state.IsEmptySwapTarget(1, 1));
    }

    [Fact]
    public void IsEmptySwapTarget_ReturnsFalseWhenObstacleExists()
    {
        var state = new GameState(3, 3, 4, new StubRandom());
        state.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Box, Stage = 1 });
        Assert.False(state.IsEmptySwapTarget(1, 1));
    }

    [Fact]
    public void IsEmptySwapTarget_ReturnsFalseWhenCoverExists()
    {
        var state = new GameState(3, 3, 4, new StubRandom());
        state.SetCover(1, 1, new Cover(CoverType.Cage));
        Assert.False(state.IsEmptySwapTarget(1, 1));
    }

    #endregion
}

