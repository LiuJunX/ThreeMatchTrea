using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Core.Tests.Utility;

/// <summary>
/// ValidMoveDetector unit tests.
///
/// Covers:
/// - HasValidMoves with available moves (early exit)
/// - HasValidMoves with no moves (deadlock / checkerboard)
/// - FindAllValidMoves completeness and correctness
/// - Empty board handling (all None tiles)
/// </summary>
public class ValidMoveDetectorTests
{
    #region Helpers

    private static IMatchFinder CreateMatchFinder()
    {
        var bombGenerator = new BombGenerator();
        return new ClassicMatchFinder(bombGenerator);
    }

    /// <summary>
    /// Creates a board with a single valid horizontal move.
    /// Layout (6x6, relevant row 0):
    ///   R R B R Y Y
    /// Swapping (2,0)&lt;-&gt;(3,0) creates R R R B ... = horizontal match.
    /// Remaining cells filled with alternating Yellow/Purple to avoid accidental matches.
    /// </summary>
    private static GameState CreateBoardWithOneHorizontalMove()
    {
        return new GameStateBuilder()
            .WithSize(6, 6)
            .WithCheckerboard(ElementType.Item4, ElementType.Item5)
            .WithCustomization(state =>
            {
                state.SetTile(0, 0, new Tile(0, ElementType.Item1, 0, 0));
                state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
                state.SetTile(2, 0, new Tile(2, ElementType.Item3, 2, 0));
                state.SetTile(3, 0, new Tile(3, ElementType.Item1, 3, 0));
            })
            .Build();
    }

    /// <summary>
    /// Creates a board with a single valid vertical move.
    /// Layout (6x6, relevant column 0):
    ///   row 0: R ...
    ///   row 1: R ...
    ///   row 2: B ...
    ///   row 3: R ...
    /// Swapping (0,2)&lt;-&gt;(0,3) creates R R R B ... = vertical match.
    /// </summary>
    private static GameState CreateBoardWithOneVerticalMove()
    {
        return new GameStateBuilder()
            .WithSize(6, 6)
            .WithCheckerboard(ElementType.Item4, ElementType.Item5)
            .WithCustomization(state =>
            {
                state.SetTile(0, 0, new Tile(0, ElementType.Item1, 0, 0));
                state.SetTile(0, 1, new Tile(6, ElementType.Item1, 0, 1));
                state.SetTile(0, 2, new Tile(12, ElementType.Item3, 0, 2));
                state.SetTile(0, 3, new Tile(18, ElementType.Item1, 0, 3));
            })
            .Build();
    }

    /// <summary>
    /// Creates a deadlock board: 3-color rotation pattern where no adjacent
    /// swap produces a 3-in-a-row.
    ///   R G B R G B
    ///   G B R G B R
    ///   B R G B R G
    ///   ...
    /// </summary>
    private static GameState CreateDeadlockBoard(int width = 6, int height = 6)
    {
        ElementType[] pattern = { ElementType.Item1, ElementType.Item2, ElementType.Item3 };

        return new GameStateBuilder()
            .WithSize(width, height)
            .WithTiles((x, y) =>
            {
                var type = pattern[(x + y) % 3];
                return new Tile(y * width + x, type, x, y);
            })
            .Build();
    }

    #endregion

    #region HasValidMoves - Moves Available

    [Fact]
    public void HasValidMoves_BoardWithHorizontalMove_ReturnsTrue()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Act
        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        // Assert
        Assert.True(result, "Board with a valid horizontal swap should have valid moves");
    }

    [Fact]
    public void HasValidMoves_BoardWithVerticalMove_ReturnsTrue()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneVerticalMove();

        // Act
        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        // Assert
        Assert.True(result, "Board with a valid vertical swap should have valid moves");
    }

    #endregion

    #region HasValidMoves - No Moves (Deadlock)

    [Fact]
    public void HasValidMoves_DeadlockBoard_ReturnsFalse()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateDeadlockBoard();

        // Act
        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        // Assert
        Assert.False(result, "3-color rotation pattern should be a deadlock");
    }

    [Fact]
    public void HasValidMoves_ThreeColorRotationSmallBoard_ReturnsFalse()
    {
        // 3-color rotation is a known deadlock pattern
        var matchFinder = CreateMatchFinder();
        var state = CreateDeadlockBoard(4, 4);

        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        Assert.False(result, "3-color rotation pattern on 4x4 should be a deadlock");
    }

    #endregion

    #region HasValidMoves - Empty Board

    [Fact]
    public void HasValidMoves_AllNoneTiles_ReturnsFalse()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = GameStateBuilder.CreateEmptyState(6, 6);

        // Act
        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        // Assert
        Assert.False(result, "An empty board (all None tiles) has no valid moves");
    }

    #endregion

    #region HasValidMoves - Edge Cases

    [Fact]
    public void HasValidMoves_1x1Board_ReturnsFalse()
    {
        // A 1x1 board has no neighbors at all
        var matchFinder = CreateMatchFinder();
        var state = new GameStateBuilder()
            .WithSize(1, 1)
            .WithAllTiles(ElementType.Item1)
            .Build();

        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        Assert.False(result);
    }

    [Fact]
    public void HasValidMoves_FallingTilesBlocked_ReturnsFalse()
    {
        // Arrange: board that would have moves, but all tiles are falling
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Mark all tiles as falling
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                tile.IsFalling = true;
                state.SetTile(x, y, tile);
            }
        }

        // Act
        bool result = ValidMoveDetector.HasValidMoves(in state, matchFinder);

        // Assert
        Assert.False(result, "All tiles falling should block all swaps");
    }

    #endregion

    #region FindAllValidMoves - Completeness

    [Fact]
    public void FindAllValidMoves_BoardWithMoves_ReturnsNonEmpty()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            // Assert
            Assert.NotNull(moves);
            Assert.NotEmpty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_DeadlockBoard_ReturnsEmpty()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateDeadlockBoard();

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            Assert.NotNull(moves);
            Assert.Empty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_EmptyBoard_ReturnsEmpty()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = GameStateBuilder.CreateEmptyState(6, 6);

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            Assert.NotNull(moves);
            Assert.Empty(moves);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_ContainsExpectedSwap()
    {
        // Arrange: R R B R pattern at row 0 => swap (2,0)<->(3,0) is valid
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            // The swap (2,0)<->(3,0) should be in the results
            bool containsExpectedMove = moves.Any(m =>
                (m.From.Equals(new Position(2, 0)) && m.To.Equals(new Position(3, 0))) ||
                (m.From.Equals(new Position(3, 0)) && m.To.Equals(new Position(2, 0))));

            Assert.True(containsExpectedMove,
                "Expected swap (2,0)<->(3,0) should be in valid moves");
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_AllMovesAreAdjacentSwaps()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            // Assert: every returned move should be between adjacent positions
            foreach (var move in moves)
            {
                Assert.True(
                    GridUtility.AreAdjacent(move.From, move.To),
                    $"Move {move.From} -> {move.To} should be adjacent");
            }
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_NoDuplicates()
    {
        // Arrange: a board with multiple moves
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            // Check no duplicate (From,To) pairs
            var seen = new HashSet<(int, int, int, int)>();
            foreach (var move in moves)
            {
                var key = (move.From.X, move.From.Y, move.To.X, move.To.Y);
                Assert.True(seen.Add(key),
                    $"Duplicate move found: ({move.From}) -> ({move.To})");
            }
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    #endregion

    #region FindAllValidMoves - Consistency with HasValidMoves

    [Fact]
    public void FindAllValidMoves_ConsistentWithHasValidMoves_WhenMovesExist()
    {
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        bool hasValid = ValidMoveDetector.HasValidMoves(in state, matchFinder);
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            Assert.Equal(hasValid, moves.Count > 0);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    [Fact]
    public void FindAllValidMoves_ConsistentWithHasValidMoves_WhenDeadlock()
    {
        var matchFinder = CreateMatchFinder();
        var state = CreateDeadlockBoard();

        bool hasValid = ValidMoveDetector.HasValidMoves(in state, matchFinder);
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            Assert.Equal(hasValid, moves.Count > 0);
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    #endregion

    #region FindAllValidMoves - Does Not Mutate State

    [Fact]
    public void FindAllValidMoves_DoesNotModifyOriginalState()
    {
        // Arrange
        var matchFinder = CreateMatchFinder();
        var state = CreateBoardWithOneHorizontalMove();

        // Snapshot all tile types before
        var before = new ElementType[state.Width * state.Height];
        for (int i = 0; i < before.Length; i++)
        {
            before[i] = state.Grid[i].Type;
        }

        // Act
        var moves = ValidMoveDetector.FindAllValidMoves(in state, matchFinder);

        try
        {
            // Assert: grid unchanged
            for (int i = 0; i < before.Length; i++)
            {
                Assert.Equal(before[i], state.Grid[i].Type);
            }
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    #endregion
}

