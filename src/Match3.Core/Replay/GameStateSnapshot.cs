using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;

namespace Match3.Core.Replay;

/// <summary>
/// Immutable snapshot of the game state at a specific point in time.
/// Used for saving/loading and replay initialization.
/// </summary>
public sealed record GameStateSnapshot
{
    /// <summary>Board width.</summary>
    public int Width { get; init; }

    /// <summary>Board height.</summary>
    public int Height { get; init; }

    /// <summary>Number of tile types.</summary>
    public int TileTypesCount { get; init; }

    /// <summary>Flattened tile type array (row-major order).</summary>
    public ElementType[] TileTypes { get; init; } = System.Array.Empty<ElementType>();

    /// <summary>Flattened cover layer array.</summary>
    public Cover[] CoverLayers { get; init; } = System.Array.Empty<Cover>();

    /// <summary>Flattened ground layer array.</summary>
    public Ground[] GroundLayers { get; init; } = System.Array.Empty<Ground>();

    /// <summary>Per-cell cell kind (topology).</summary>
    public CellKind[] Cells { get; init; } = System.Array.Empty<CellKind>();

    /// <summary>Next tile ID to assign.</summary>
    public int NextTileId { get; init; }

    /// <summary>Current score.</summary>
    public int Score { get; init; }

    /// <summary>Total moves made.</summary>
    public int MoveCount { get; init; }

    /// <summary>
    /// Creates a snapshot from a GameState.
    /// </summary>
    public static GameStateSnapshot FromState(in GameState state)
    {
        int size = state.Width * state.Height;
        var tileTypes = new ElementType[size];
        var coverLayers = new Cover[size];
        var groundLayers = new Ground[size];

        var cells = new CellKind[size];

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                int index = y * state.Width + x;
                var tile = state.GetTile(x, y);
                tileTypes[index] = tile.Type;
                coverLayers[index] = state.GetCover(x, y);
                groundLayers[index] = state.GetGround(x, y);
                cells[index] = state.Cells[index];
            }
        }

        return new GameStateSnapshot
        {
            Width = state.Width,
            Height = state.Height,
            TileTypesCount = state.TileTypesCount,
            TileTypes = tileTypes,
            CoverLayers = coverLayers,
            GroundLayers = groundLayers,
            Cells = cells,
            NextTileId = state.NextTileId,
            Score = state.Score,
            MoveCount = state.MoveCount
        };
    }

    /// <summary>
    /// Restores a GameState from this snapshot.
    /// </summary>
    /// <param name="random">Random generator for the state.</param>
    public GameState ToState(Match3.Random.IRandom random)
    {
        var state = new GameState(Width, Height, TileTypesCount, random)
        {
            NextTileId = NextTileId,
            Score = Score,
            MoveCount = MoveCount
        };

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                var tile = new Tile(
                    state.NextTileId++,
                    TileTypes[index],
                    x, y
                );
                state.SetTile(x, y, tile);
                state.SetCover(x, y, CoverLayers[index]);
                state.SetGround(x, y, GroundLayers[index]);
                if (index < Cells.Length)
                    state.Cells[index] = Cells[index];
            }
        }

        // Reset NextTileId to saved value (we incremented during tile creation)
        state.NextTileId = NextTileId;

        return state;
    }
}
