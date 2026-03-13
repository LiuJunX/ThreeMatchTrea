using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Utility;
using Xunit;

namespace Match3.Core.Tests.Utility;

/// <summary>
/// GridUtility unit tests.
///
/// Covers:
/// - IsSwapValid: adjacency not checked here, only tile validity
/// - AreAdjacent: cardinal adjacency detection
/// - GetNeighbor: directional neighbor calculation
/// - SwapTilesForCheck: temporary grid swap for match detection
/// </summary>
public class GridUtilityTests
{
    #region Helpers

    private GameState CreateFilledState(int width = 8, int height = 8)
    {
        return new GameStateBuilder()
            .WithSize(width, height)
            .WithAllTiles(ElementType.Item1)
            .Build();
    }

    #endregion

    #region IsSwapValid - Adjacent Valid Positions

    [Fact]
    public void IsSwapValid_BothPositionsHaveNormalTiles_ReturnsTrue()
    {
        // Arrange
        var state = CreateFilledState();

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSwapValid_HorizontalNeighbors_ReturnsTrue()
    {
        // Arrange
        var state = CreateFilledState();

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(3, 4), new Position(4, 4));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSwapValid_VerticalNeighbors_ReturnsTrue()
    {
        // Arrange
        var state = CreateFilledState();

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(3, 4), new Position(3, 5));

        // Assert
        Assert.True(result);
    }

    #endregion

    #region IsSwapValid - Out-of-Bounds

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(8, 0, 0, 0)]
    [InlineData(0, 8, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(0, 0, 8, 0)]
    [InlineData(0, 0, 0, 8)]
    public void IsSwapValid_OutOfBoundsPosition_ReturnsFalse(
        int fromX, int fromY, int toX, int toY)
    {
        // Arrange
        var state = CreateFilledState();

        // Act
        bool result = GridUtility.IsSwapValid(
            in state,
            new Position(fromX, fromY),
            new Position(toX, toY));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsSwapValid - None Tiles

    [Fact]
    public void IsSwapValid_FromTileIsNone_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSwapValid_ToTileIsNone_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        state.SetTile(1, 0, new Tile(1, ElementType.None, 1, 0));

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSwapValid_BothTilesAreNone_ReturnsFalse()
    {
        // Arrange
        var state = GameStateBuilder.CreateEmptyState();

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsSwapValid - Falling Tiles

    [Fact]
    public void IsSwapValid_FromTileIsFalling_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        var tile = state.GetTile(0, 0);
        tile.IsFalling = true;
        state.SetTile(0, 0, tile);

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSwapValid_ToTileIsFalling_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        var tile = state.GetTile(1, 0);
        tile.IsFalling = true;
        state.SetTile(1, 0, tile);

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsSwapValid - Cover Blocking

    [Fact]
    public void IsSwapValid_FromTileHasCageCover_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        state.SetCover(0, 0, new Cover { Type = CoverType.Cage, Health = 1 });

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSwapValid_ToTileHasCageCover_ReturnsFalse()
    {
        // Arrange
        var state = CreateFilledState();
        state.SetCover(1, 0, new Cover { Type = CoverType.Cage, Health = 1 });

        // Act
        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(1, 0));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsSwapValid - Non-Adjacent (still valid per IsSwapValid contract)

    [Fact]
    public void IsSwapValid_NonAdjacentButValidTiles_ReturnsTrue()
    {
        // IsSwapValid does NOT check adjacency -- that is AreAdjacent's job
        var state = CreateFilledState();

        bool result = GridUtility.IsSwapValid(in state, new Position(0, 0), new Position(5, 5));

        Assert.True(result);
    }

    #endregion

    #region AreAdjacent

    [Theory]
    [InlineData(0, 0, 1, 0, true)]   // right
    [InlineData(1, 0, 0, 0, true)]   // left
    [InlineData(0, 0, 0, 1, true)]   // down
    [InlineData(0, 1, 0, 0, true)]   // up
    [InlineData(0, 0, 1, 1, false)]  // diagonal
    [InlineData(0, 0, 2, 0, false)]  // two apart horizontal
    [InlineData(0, 0, 0, 2, false)]  // two apart vertical
    [InlineData(3, 3, 3, 3, false)]  // same position
    public void AreAdjacent_VariousCases_ReturnsExpected(
        int ax, int ay, int bx, int by, bool expected)
    {
        // Act
        bool result = GridUtility.AreAdjacent(new Position(ax, ay), new Position(bx, by));

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AreAdjacent_IsSymmetric()
    {
        var a = new Position(2, 3);
        var b = new Position(3, 3);

        Assert.Equal(
            GridUtility.AreAdjacent(a, b),
            GridUtility.AreAdjacent(b, a));
    }

    #endregion

    #region GetNeighbor

    [Fact]
    public void GetNeighbor_Up_DecreasesY()
    {
        var pos = new Position(3, 3);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Up);

        Assert.Equal(3, neighbor.X);
        Assert.Equal(2, neighbor.Y);
    }

    [Fact]
    public void GetNeighbor_Down_IncreasesY()
    {
        var pos = new Position(3, 3);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Down);

        Assert.Equal(3, neighbor.X);
        Assert.Equal(4, neighbor.Y);
    }

    [Fact]
    public void GetNeighbor_Left_DecreasesX()
    {
        var pos = new Position(3, 3);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Left);

        Assert.Equal(2, neighbor.X);
        Assert.Equal(3, neighbor.Y);
    }

    [Fact]
    public void GetNeighbor_Right_IncreasesX()
    {
        var pos = new Position(3, 3);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Right);

        Assert.Equal(4, neighbor.X);
        Assert.Equal(3, neighbor.Y);
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void GetNeighbor_FromOrigin_ProducesAdjacentPosition(Direction direction)
    {
        var origin = new Position(4, 4);
        var neighbor = GridUtility.GetNeighbor(origin, direction);

        Assert.True(GridUtility.AreAdjacent(origin, neighbor));
    }

    [Fact]
    public void GetNeighbor_AtTopLeftCorner_Up_ProducesNegativeY()
    {
        // The method does not clamp to boundaries
        var pos = new Position(0, 0);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Up);

        Assert.Equal(0, neighbor.X);
        Assert.Equal(-1, neighbor.Y);
    }

    [Fact]
    public void GetNeighbor_AtTopLeftCorner_Left_ProducesNegativeX()
    {
        var pos = new Position(0, 0);
        var neighbor = GridUtility.GetNeighbor(pos, Direction.Left);

        Assert.Equal(-1, neighbor.X);
        Assert.Equal(0, neighbor.Y);
    }

    #endregion

    #region SwapTilesForCheck

    [Fact]
    public void SwapTilesForCheck_SwapsGridEntries()
    {
        // Arrange
        var state = new GameStateBuilder()
            .WithSize(4, 4)
            .WithTiles((x, y) => new Tile(y * 4 + x, x == 0 && y == 0 ? ElementType.Item1 : ElementType.Item3, x, y))
            .Build();

        var posA = new Position(0, 0);
        var posB = new Position(1, 0);

        Assert.Equal(ElementType.Item1, state.GetTile(posA).Type);
        Assert.Equal(ElementType.Item3, state.GetTile(posB).Type);

        // Act
        GridUtility.SwapTilesForCheck(ref state, posA, posB);

        // Assert
        Assert.Equal(ElementType.Item3, state.GetTile(posA).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(posB).Type);
    }

    [Fact]
    public void SwapTilesForCheck_DoubleSwapRestoresOriginalState()
    {
        // Arrange
        var state = new GameStateBuilder()
            .WithSize(4, 4)
            .WithTiles((x, y) => new Tile(y * 4 + x, x == 0 && y == 0 ? ElementType.Item1 : ElementType.Item3, x, y))
            .Build();

        var posA = new Position(0, 0);
        var posB = new Position(1, 0);

        var originalA = state.GetTile(posA);
        var originalB = state.GetTile(posB);

        // Act
        GridUtility.SwapTilesForCheck(ref state, posA, posB);
        GridUtility.SwapTilesForCheck(ref state, posA, posB);

        // Assert
        Assert.Equal(originalA.Type, state.GetTile(posA).Type);
        Assert.Equal(originalA.Id, state.GetTile(posA).Id);
        Assert.Equal(originalB.Type, state.GetTile(posB).Type);
        Assert.Equal(originalB.Id, state.GetTile(posB).Id);
    }

    [Fact]
    public void SwapTilesForCheck_PreservesOtherGridEntries()
    {
        // Arrange
        var state = new GameStateBuilder()
            .WithSize(4, 4)
            .WithTiles((x, y) =>
            {
                ElementType type = (x + y) % 3 == 0 ? ElementType.Item1
                    : (x + y) % 3 == 1 ? ElementType.Item3
                    : ElementType.Item2;
                return new Tile(y * 4 + x, type, x, y);
            })
            .Build();

        // Capture all tiles except the two being swapped
        var posA = new Position(0, 0);
        var posB = new Position(1, 0);
        var unaffectedTiles = new List<(Position pos, ElementType type)>();
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                var p = new Position(x, y);
                if (!p.Equals(posA) && !p.Equals(posB))
                {
                    unaffectedTiles.Add((p, state.GetTile(p).Type));
                }
            }
        }

        // Act
        GridUtility.SwapTilesForCheck(ref state, posA, posB);

        // Assert -- all other tiles unchanged
        foreach (var (pos, expectedType) in unaffectedTiles)
        {
            Assert.Equal(expectedType, state.GetTile(pos).Type);
        }
    }

    [Fact]
    public void SwapTilesForCheck_VerticalSwap_Works()
    {
        // Arrange
        var state = new GameStateBuilder()
            .WithSize(4, 4)
            .WithTiles((x, y) => new Tile(y * 4 + x, y == 0 ? ElementType.Item1 : ElementType.Item2, x, y))
            .Build();

        var posA = new Position(2, 0);
        var posB = new Position(2, 1);

        // Act
        GridUtility.SwapTilesForCheck(ref state, posA, posB);

        // Assert
        Assert.Equal(ElementType.Item2, state.GetTile(posA).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(posB).Type);
    }

    #endregion
}

