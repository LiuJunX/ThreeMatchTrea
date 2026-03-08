using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
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
    public void ValidatePendingMove_RevertsSwap_WhenNoMatch()
    {
        var state = CreateTestState();
        var matchFinder = new StubMatchFinder { AlwaysMatch = false };
        var context = new TestSwapContext { AnimationDuration = 0.15f };
        var ops = new SwapOperations(matchFinder, context);

        // Do initial swap
        var originalTileA = state.GetTile(0, 0).Type;
        var originalTileB = state.GetTile(1, 0).Type;
        ops.SwapTiles(ref state, new Position(0, 0), new Position(1, 0));

        // Verify swap happened
        Assert.Equal(originalTileB, state.GetTile(0, 0).Type);
        Assert.Equal(originalTileA, state.GetTile(1, 0).Type);

        var pending = new PendingMoveState
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            HadMatch = false,
            NeedsValidation = true,
            AnimationTime = 0f
        };

        // Run validation with enough time to complete animation
        ops.ValidatePendingMove(ref state, ref pending, 0.2f, 0, 0f, NullEventCollector.Instance);

        // Verify tiles are back to original positions
        Assert.Equal(originalTileA, state.GetTile(0, 0).Type);
        Assert.Equal(originalTileB, state.GetTile(1, 0).Type);
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
}

