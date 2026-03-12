using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.TestHelpers;

/// <summary>
/// Reusable assertion helpers for verifying grid state in tests.
/// </summary>
public static class GridAssertions
{
    /// <summary>
    /// Asserts the tile at (<paramref name="x"/>, <paramref name="y"/>) is cleared (ElementType.None).
    /// </summary>
    public static void AssertTileCleared(in GameState state, int x, int y)
    {
        var actual = state.GetType(x, y);
        Assert.True(actual == ElementType.None,
            $"Expected tile at ({x},{y}) to be cleared, but was {actual}");
    }

    /// <summary>
    /// Asserts the tile at (<paramref name="x"/>, <paramref name="y"/>) has the
    /// <paramref name="expected"/> element type.
    /// </summary>
    public static void AssertTileType(in GameState state, int x, int y, ElementType expected)
    {
        var actual = state.GetType(x, y);
        Assert.True(actual == expected,
            $"Expected tile at ({x},{y}) to be {expected}, but was {actual}");
    }

    /// <summary>
    /// Asserts every tile in row <paramref name="y"/> from column
    /// <paramref name="startX"/> to <paramref name="endX"/> (inclusive) is cleared.
    /// </summary>
    public static void AssertRowCleared(in GameState state, int y, int startX, int endX)
    {
        for (int x = startX; x <= endX; x++)
        {
            var actual = state.GetType(x, y);
            Assert.True(actual == ElementType.None,
                $"Expected tile at ({x},{y}) to be cleared, but was {actual}");
        }
    }

    /// <summary>
    /// Asserts every tile in column <paramref name="x"/> from row
    /// <paramref name="startY"/> to <paramref name="endY"/> (inclusive) is cleared.
    /// </summary>
    public static void AssertColumnCleared(in GameState state, int x, int startY, int endY)
    {
        for (int y = startY; y <= endY; y++)
        {
            var actual = state.GetType(x, y);
            Assert.True(actual == ElementType.None,
                $"Expected tile at ({x},{y}) to be cleared, but was {actual}");
        }
    }
}
