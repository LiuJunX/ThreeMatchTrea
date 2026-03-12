using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Tests.TestFixtures;

/// <summary>
/// Fluent builder for creating GameState instances in tests.
/// Simplifies test setup with a readable, chainable API.
/// </summary>
public class GameStateBuilder
{
    private int _width = 8;
    private int _height = 8;
    private int _TileTypesCount = 6;
    private IRandom _random;
    private Func<int, int, Tile>? _tileFactory;
    private Action<GameState>? _customizer;

    public GameStateBuilder()
    {
        _random = new StubRandom();
    }

    /// <summary>
    /// Sets the board dimensions.
    /// </summary>
    public GameStateBuilder WithSize(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    /// <summary>
    /// Sets the number of tile types.
    /// </summary>
    public GameStateBuilder WithTileTypesCount(int count)
    {
        _TileTypesCount = count;
        return this;
    }

    /// <summary>
    /// Sets the random number generator.
    /// </summary>
    public GameStateBuilder WithRandom(IRandom random)
    {
        _random = random;
        return this;
    }

    /// <summary>
    /// Sets a custom tile factory for initializing tiles.
    /// </summary>
    public GameStateBuilder WithTiles(Func<int, int, Tile> tileFactory)
    {
        _tileFactory = tileFactory;
        return this;
    }

    /// <summary>
    /// Fills all tiles with the same type.
    /// </summary>
    public GameStateBuilder WithAllTiles(ElementType type)
    {
        return WithTiles((x, y) => new Tile(y * _width + x, type, x, y));
    }

    /// <summary>
    /// Fills all tiles with empty (None) type.
    /// </summary>
    public GameStateBuilder WithEmptyTiles()
    {
        return WithAllTiles(ElementType.None);
    }

    /// <summary>
    /// Initializes tiles in a checkerboard pattern using two tile types.
    /// Useful for testing scenarios with no possible matches.
    /// </summary>
    public GameStateBuilder WithCheckerboard(ElementType type1, ElementType type2)
    {
        return WithTiles((x, y) =>
        {
            var type = (x + y) % 2 == 0 ? type1 : type2;
            return new Tile(y * _width + x, type, x, y);
        });
    }

    /// <summary>
    /// Sets a custom action to modify the state after creation.
    /// </summary>
    public GameStateBuilder WithCustomization(Action<GameState> customizer)
    {
        _customizer = customizer;
        return this;
    }

    /// <summary>
    /// Builds the GameState with the configured options.
    /// </summary>
    public GameState Build()
    {
        var state = new GameState(_width, _height, _TileTypesCount, _random);
        state.SelectedPosition = Position.Invalid;

        // Initialize tiles
        if (_tileFactory != null)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    state.SetTile(x, y, _tileFactory(x, y));
                }
            }
        }
        else
        {
            // Default: initialize with None tiles
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    state.SetTile(x, y, new Tile(y * _width + x, ElementType.None, x, y));
                }
            }
        }

        // Apply customization
        _customizer?.Invoke(state);

        return state;
    }

    /// <summary>
    /// Creates a state configured for a horizontal match scenario.
    /// Pattern: R R B R at row 0 (swap positions 2,3 to create RRR match).
    /// </summary>
    public static GameState CreateHorizontalMatchScenario()
    {
        return new GameStateBuilder()
            .WithSize(8, 8)
            .WithEmptyTiles()
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
    /// Creates a state configured for a vertical match scenario.
    /// Pattern: R R B R in column 0 (swap positions (0,2) and (0,3) to create RRR match).
    /// </summary>
    public static GameState CreateVerticalMatchScenario()
    {
        return new GameStateBuilder()
            .WithSize(8, 8)
            .WithEmptyTiles()
            .WithCustomization(state =>
            {
                state.SetTile(0, 0, new Tile(0, ElementType.Item1, 0, 0));
                state.SetTile(0, 1, new Tile(8, ElementType.Item1, 0, 1));
                state.SetTile(0, 2, new Tile(16, ElementType.Item3, 0, 2));
                state.SetTile(0, 3, new Tile(24, ElementType.Item1, 0, 3));
            })
            .Build();
    }

    /// <summary>
    /// Creates a stable state with no possible matches.
    /// </summary>
    public static GameState CreateStableState(int width = 8, int height = 8)
    {
        return new GameStateBuilder()
            .WithSize(width, height)
            .WithCheckerboard(ElementType.Item1, ElementType.Item3)
            .Build();
    }

    /// <summary>
    /// Creates an empty state with all None tiles.
    /// </summary>
    public static GameState CreateEmptyState(int width = 8, int height = 8)
    {
        return new GameStateBuilder()
            .WithSize(width, height)
            .WithEmptyTiles()
            .Build();
    }

    #region Batch Tile Helpers

    /// <summary>
    /// Sets a single tile at (<paramref name="x"/>, <paramref name="y"/>) with an
    /// auto-generated ID taken from <c>state.NextTileId</c>.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="type">The element type to assign.</param>
    public static void SetTile(ref GameState state, int x, int y, ElementType type)
    {
        state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
    }

    /// <summary>
    /// Sets a horizontal row of tiles starting at (<paramref name="startX"/>, <paramref name="y"/>).
    /// Each tile receives an auto-generated ID from <c>state.NextTileId</c>.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="y">Row index.</param>
    /// <param name="startX">Starting column index.</param>
    /// <param name="types">Element types to place left-to-right.</param>
    public static void SetTileRow(ref GameState state, int y, int startX, params ElementType[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            int x = startX + i;
            state.SetTile(x, y, new Tile(state.NextTileId++, types[i], x, y));
        }
    }

    /// <summary>
    /// Sets a vertical column of tiles starting at (<paramref name="x"/>, <paramref name="startY"/>).
    /// Each tile receives an auto-generated ID from <c>state.NextTileId</c>.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="x">Column index.</param>
    /// <param name="startY">Starting row index.</param>
    /// <param name="types">Element types to place top-to-bottom.</param>
    public static void SetTileColumn(ref GameState state, int x, int startY, params ElementType[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            int y = startY + i;
            state.SetTile(x, y, new Tile(state.NextTileId++, types[i], x, y));
        }
    }

    /// <summary>
    /// Fills a rectangular area with the specified element type.
    /// Each tile receives an auto-generated ID from <c>state.NextTileId</c>.
    /// Tiles are filled row by row, left-to-right, top-to-bottom.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    /// <param name="x">Left column of the rectangle.</param>
    /// <param name="y">Top row of the rectangle.</param>
    /// <param name="w">Width (number of columns).</param>
    /// <param name="h">Height (number of rows).</param>
    /// <param name="type">The element type to fill.</param>
    public static void FillRect(ref GameState state, int x, int y, int w, int h, ElementType type)
    {
        for (int row = y; row < y + h; row++)
        {
            for (int col = x; col < x + w; col++)
            {
                state.SetTile(col, row, new Tile(state.NextTileId++, type, col, row));
            }
        }
    }

    #endregion
}


