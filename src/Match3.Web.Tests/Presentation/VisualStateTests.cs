using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Random;
using Xunit;

namespace Match3.Web.Tests.Presentation;

public class VisualStateTests
{
    private int _nextTileId = 1;

    #region SyncFallingTilesFromGameState Tests

    [Fact]
    public void SyncFallingTiles_UpdatesExistingTilePositions_WhenNotBeingAnimated()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state at position (2, 3)
        var tile = CreateTile(ElementType.Item1, 2, 3);
        // Simulate falling: tile's visual position is above its grid slot
        tile.Position = new Vector2(2, 1.5f);
        // SetTile after modifying (Tile is a struct)
        state.SetTile(2, 3, tile);

        // Add same tile to visual state at old position, NOT being animated
        visualState.AddTile(tile.Id, ElementType.Item1, new Position(2, 0), new Vector2(2, 0));
        // IsBeingAnimated defaults to false

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
    public void SyncFallingTiles_SkipsTile_WhenIsBeingAnimated()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state
        var tile = CreateTile(ElementType.Item1, 2, 3);
        tile.Position = new Vector2(2, 3);  // At grid position
        state.SetTile(2, 3, tile);

        // Add same tile to visual state at animation position, marked as being animated
        visualState.AddTile(tile.Id, ElementType.Item1, new Position(2, 3), new Vector2(1.5f, 3));
        var visual = visualState.GetTile(tile.Id);
        visual!.AddAnimationRef();  // Being controlled by Player animation

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Visual position should NOT be overwritten (IsBeingAnimated = true)
        Assert.Equal(1.5f, visual.Position.X, 0.001f);  // Still at animation position
        Assert.Equal(3f, visual.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_SyncsPosition_WhenNotBeingAnimated()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state with specific position
        var tile = CreateTile(ElementType.Item1, 2, 3);
        tile.Position = new Vector2(2, 3);  // At grid position
        state.SetTile(2, 3, tile);

        // Add same tile to visual state at different position, not being animated
        // (AnimationRefCount defaults to 0, so IsBeingAnimated is false)
        visualState.AddTile(tile.Id, ElementType.Item1, new Position(2, 0), new Vector2(2, 2.5f));
        var visual = visualState.GetTile(tile.Id);
        Assert.False(visual!.IsBeingAnimated);  // Verify not being animated

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Visual position should be synced to game state position
        Assert.Equal(2f, visual.Position.X, 0.001f);
        Assert.Equal(3f, visual.Position.Y, 0.001f);  // Synced to game state
    }

    [Fact]
    public void SyncFallingTiles_AddsNewTilesFromGameState()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile to game state but not to visual state (bomb tile)
        var tile = CreateTile(ElementType.HorizontalRocket, 4, 5);
        state.SetTile(4, 5, tile);

        // Visual state is empty
        Assert.Null(visualState.GetTile(tile.Id));

        // Sync
        visualState.SyncFallingTilesFromGameState(in state);

        // Tile should now exist in visual state
        var visual = visualState.GetTile(tile.Id);
        Assert.NotNull(visual);
        Assert.Equal(ElementType.HorizontalRocket, visual.TileType);
        Assert.Equal(4f, visual.Position.X, 0.001f);
        Assert.Equal(5f, visual.Position.Y, 0.001f);
    }

    [Fact]
    public void SyncFallingTiles_RemovesTilesNotInGameState()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Add tile only to visual state (not in game state)
        visualState.AddTile(999, ElementType.Item2, new Position(1, 1), new Vector2(1, 1));

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

        var tile = CreateTile(ElementType.VerticalRocket, 3, 3);
        // Modify position before SetTile (Tile is a struct)
        tile.Position = new Vector2(3, 2);
        tile.IsFalling = true;  // Mark as falling
        state.SetTile(3, 3, tile);

        // Add tile to visual state with custom scale and alpha
        visualState.AddTile(tile.Id, ElementType.VerticalRocket, new Position(3, 0), new Vector2(3, 0));
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
        var tile1 = CreateTile(ElementType.Item1, 0, 0);
        tile1.Position = new Vector2(0, 0);
        tile1.IsFalling = false;  // Not falling
        state.SetTile(0, 0, tile1);

        var tile2 = CreateTile(ElementType.Item3, 1, 1);
        tile2.Position = new Vector2(1, 0.5f); // Falling
        tile2.IsFalling = true;
        state.SetTile(1, 1, tile2);

        var tile3 = CreateTile(ElementType.Item2, 2, 2);
        tile3.Position = new Vector2(2, 1.0f); // Falling
        tile3.IsFalling = true;
        state.SetTile(2, 2, tile3);

        // Add only tile1 to visual state
        visualState.AddTile(tile1.Id, ElementType.Item1, new Position(0, 0), new Vector2(0, 0));

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
        var tile = CreateTile(ElementType.Item1, 0, 0);
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

        var tile = CreateTile(ElementType.Item1, 5, 7);
        // Visual position can be different from grid position during falling
        // Modify position before SetTile (Tile is a struct)
        tile.Position = new Vector2(5, 4.5f);
        tile.IsFalling = true;  // Mark as falling
        state.SetTile(5, 7, tile);

        visualState.AddTile(tile.Id, ElementType.Item1, new Position(5, 0), new Vector2(5, 0));

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
        visualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        visualState.AddTile(2, ElementType.Item3, new Position(1, 0), Vector2.One);

        Assert.Equal(2, visualState.Tiles.Count);

        // Sync with empty game state
        visualState.SyncFallingTilesFromGameState(in state);

        Assert.Empty(visualState.Tiles);
    }

    [Fact]
    public void SyncFallingTiles_PreservesAnimatedTile_WhenNotInGameState()
    {
        // Scenario: tile is destroyed in GameState but still playing destroy animation.
        // SyncFallingTilesFromGameState should NOT remove it prematurely.
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);
        // Game state is empty (tile already removed by engine)

        // Visual state has the tile with an active animation (e.g. destroy fade-out)
        visualState.AddTile(1, ElementType.Item1, new Position(3, 4), new Vector2(3, 4));
        var visual = visualState.GetTile(1);
        visual!.AddAnimationRef(); // Simulates DestroyTileCommand marking it as animated

        // Sync with game state that no longer has tile 1
        visualState.SyncFallingTilesFromGameState(in state);

        // Tile should still exist because it's being animated
        Assert.NotNull(visualState.GetTile(1));
    }

    [Fact]
    public void SyncFallingTiles_RemovesTile_AfterAnimationCompletes()
    {
        var visualState = new VisualState();
        var state = CreateGameState(8, 8);

        // Tile with active animation
        visualState.AddTile(1, ElementType.Item1, new Position(3, 4), new Vector2(3, 4));
        var visual = visualState.GetTile(1);
        visual!.AddAnimationRef();

        // First sync: tile preserved (animated)
        visualState.SyncFallingTilesFromGameState(in state);
        Assert.NotNull(visualState.GetTile(1));

        // Animation completes
        visual.ReleaseAnimationRef();
        Assert.False(visual.IsBeingAnimated);

        // Second sync: tile removed (no longer animated, not in game state)
        visualState.SyncFallingTilesFromGameState(in state);
        Assert.Null(visualState.GetTile(1));
    }

    #endregion

    private static GameState CreateGameState(int width, int height)
    {
        return new GameState(width, height, 6, new DefaultRandom(12345));
    }

    private Tile CreateTile(ElementType type, int x, int y)
    {
        return new Tile(_nextTileId++, type, x, y);
    }
}
