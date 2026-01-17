using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Random;
using Xunit;

namespace Match3.Web.Tests.Presentation;

public class VisualStateTests
{
    private long _nextTileId = 1;

    #region SyncFallingTilesFromGameState Tests

    [Fact]
    public void SyncFallingTiles_UpdatesExistingTilePositions_WhenFalling()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state at position (2, 3)
        var tile = CreateTile(TileType.Red, 2, 3);
        // Simulate falling: tile's visual position is above its grid slot
        tile.Position = new Vector2(2, 1.5f);
        tile.IsFalling = true;  // Mark as falling
        // SetTile after modifying (Tile is a struct)
        state.SetTile(2, 3, tile);

        // Add same tile to visual state at old position
        visualState.AddTile(tile.Id, TileType.Red, BombType.None, new Position(2, 0), new Vector2(2, 0));

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Visual position should match game state's tile.Position
        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        Assert.Equal(2f, visual.Position.X, 0.001f);
        Assert.Equal(1.5f, visual.Position.Y, 0.001f);
        Assert.Equal(2, visual.GridPosition.X);
        Assert.Equal(3, visual.GridPosition.Y);
    }

    [Fact]
    public void SyncFallingTiles_DoesNotUpdatePosition_WhenNotFalling()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state (not falling - e.g., during swap animation)
        var tile = CreateTile(TileType.Red, 2, 3);
        tile.Position = new Vector2(2, 3);  // At grid position
        tile.IsFalling = false;  // Not falling (swap animation in progress)
        state.SetTile(2, 3, tile);

        // Add same tile to visual state at animation position (different from grid)
        visualState.AddTile(tile.Id, TileType.Red, BombType.None, new Position(2, 3), new Vector2(1.5f, 3));

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Visual position should NOT be overwritten (preserves animation position)
        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        Assert.Equal(1.5f, visual.Position.X, 0.001f);  // Still at animation position
        Assert.Equal(3f, visual.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_AddsNewTilesFromGameState()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state but not to visual state
        var tile = CreateTile(TileType.Blue, 4, 5, BombType.Horizontal);
        state.SetTile(4, 5, tile);

        // Visual state is empty
        Assert.Null(visualState.GetTile(tile.Id));

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Tile should now exist in visual state
        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        Assert.Equal(TileType.Blue, visual.TileType);
        Assert.Equal(BombType.Horizontal, visual.BombType);
        Assert.Equal(4f, visual.Position.X, 0.001f);
        Assert.Equal(5f, visual.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_RemovesTilesNotInGameState()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile only to visual state (not in game state)
        visualState.AddTile(999, TileType.Green, BombType.None, new Position(1, 1), new Vector2(1, 1));

        Assert.NotNull(visualState.GetTile(999));

        // Sync with empty game state
        visualState.SyncFallingTilesFromGameState(in state);

        // Tile should be removed
        Assert.Null(visualState.GetTile(999));
    }

    [Fact]
    public void SyncFallingTiles_PreservesTileProperties()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        var tile = CreateTile(TileType.Yellow, 3, 3, BombType.Vertical);
        // Modify position before SetTile (Tile is a struct)
        tile.Position = new Vector2(3, 2);
        tile.IsFalling = true;  // Mark as falling
        state.SetTile(3, 3, tile);

        // Add tile to visual state with custom scale and alpha
        visualState.AddTile(tile.Id, TileType.Yellow, BombType.Vertical, new Position(3, 0), new Vector2(3, 0));
        visualState.SetTileScale(tile.Id, new Vector2(1.5f, 1.5f));
        visualState.SetTileAlpha(tile.Id, 0.8f);

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        // Position should be updated
        Assert.Equal(3f, visual.Position.X, 0.001f);
        Assert.Equal(2f, visual.Position.Y, 0.001f);
        // Scale and alpha should be preserved (sync only updates position)
        Assert.Equal(1.5f, visual.Scale.X, 0.001f);
        Assert.Equal(0.8f, visual.Alpha, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_HandlesMultipleTiles()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add 3 tiles to game state (modify position before SetTile since Tile is a struct)
        var tile1 = CreateTile(TileType.Red, 0, 0);
        tile1.Position = new Vector2(0, 0);
        tile1.IsFalling = false;  // Not falling
        state.SetTile(0, 0, tile1);

        var tile2 = CreateTile(TileType.Blue, 1, 1);
        tile2.Position = new Vector2(1, 0.5f); // Falling
        tile2.IsFalling = true;
        state.SetTile(1, 1, tile2);

        var tile3 = CreateTile(TileType.Green, 2, 2);
        tile3.Position = new Vector2(2, 1.0f); // Falling
        tile3.IsFalling = true;
        state.SetTile(2, 2, tile3);

        // Add only tile1 to visual state
        visualState.AddTile(tile1.Id, TileType.Red, BombType.None, new Position(0, 0), new Vector2(0, 0));

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // All 3 tiles should exist (tile2 and tile3 added as new)
        Assert.NotNull(visualState.GetTile(tile1.Id));
        Assert.NotNull(visualState.GetTile(tile2.Id));
        Assert.NotNull(visualState.GetTile(tile3.Id));

        // Check positions of falling tiles
        Assert.Equal(0.5f, visualState.GetTile(tile2.Id)!.Position.Y, 0.001f);
        Assert.Equal(1.0f, visualState.GetTile(tile3.Id)!.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_IgnoresEmptySlots()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add one tile, leave rest empty
        var tile = CreateTile(TileType.Red, 0, 0);
        state.SetTile(0, 0, tile);

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Only one tile should exist
        Assert.Single(visualState.Tiles);
    }

    [Fact]
    public void SyncFallingTiles_UpdatesGridPositionCorrectly()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        var tile = CreateTile(TileType.Red, 5, 7);
        // Visual position can be different from grid position during falling
        // Modify position before SetTile (Tile is a struct)
        tile.Position = new Vector2(5, 4.5f);
        tile.IsFalling = true;  // Mark as falling
        state.SetTile(5, 7, tile);

        visualState.AddTile(tile.Id, TileType.Red, BombType.None, new Position(5, 0), new Vector2(5, 0));

        visualState.SyncFallingTilesFromGameState(in state);

        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        // Grid position should be the actual grid slot
        Assert.Equal(5, visual.GridPosition.X);
        Assert.Equal(7, visual.GridPosition.Y);
        // Visual position should match physics position
        Assert.Equal(5f, visual.Position.X, 0.001f);
        Assert.Equal(4.5f, visual.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_EmptyGameState_ClearsAllTiles()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tiles to visual state
        visualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);
        visualState.AddTile(2, TileType.Blue, BombType.None, new Position(1, 0), Vector2.One);

        Assert.Equal(2, visualState.Tiles.Count);

        // Sync with empty game state
        visualState.SyncFallingTilesFromGameState(in state);

        Assert.Empty(visualState.Tiles);
    }

    #endregion

    private static GameState CreateGameState(int width, int height)
    {
        return new GameState(width, height, 6, new DefaultRandom(12345));
    }

    private Tile CreateTile(TileType type, int x, int y, BombType bomb = BombType.None)
    {
        return new Tile(_nextTileId++, type, x, y, bomb);
    }
}
