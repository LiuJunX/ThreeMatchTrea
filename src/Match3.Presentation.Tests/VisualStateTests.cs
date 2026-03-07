using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for VisualState tile management and sync logic.
/// </summary>
public class VisualStateTests
{
    private readonly VisualState _state = new();

    [Fact]
    public void AddTile_CreatesVisibleTile()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));

        Assert.True(_state.Tiles.ContainsKey(1));
        Assert.True(_state.Tiles[1].IsVisible);
        Assert.Equal(1f, _state.Tiles[1].Alpha);
        Assert.Equal(Vector2.One, _state.Tiles[1].Scale);
    }

    [Fact]
    public void RemoveTile_RemovesTileFromState()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _state.RemoveTile(1);

        Assert.False(_state.Tiles.ContainsKey(1));
    }

    [Fact]
    public void SetTileScale_UpdatesScale()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _state.SetTileScale(1, new Vector2(0.5f, 0.5f));

        Assert.Equal(0.5f, _state.Tiles[1].Scale.X);
        Assert.Equal(0.5f, _state.Tiles[1].Scale.Y);
    }

    [Fact]
    public void SetTileAlpha_UpdatesAlpha()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _state.SetTileAlpha(1, 0.3f);

        Assert.Equal(0.3f, _state.Tiles[1].Alpha);
    }

    [Fact]
    public void SetTileVisible_UpdatesVisibility()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _state.SetTileVisible(1, false);

        Assert.False(_state.Tiles[1].IsVisible);
    }

    [Fact]
    public void SetTilePosition_UpdatesPosition()
    {
        _state.AddTile(1, ElementType.Item1, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _state.SetTilePosition(1, new Vector2(3.5f, 4.5f));

        Assert.Equal(3.5f, _state.Tiles[1].Position.X);
        Assert.Equal(4.5f, _state.Tiles[1].Position.Y);
    }

    [Fact]
    public void AddMultipleTiles_AllPresent()
    {
        for (int i = 1; i <= 5; i++)
        {
            _state.AddTile(i, ElementType.Item1, BombType.None, new Position(i, 0), new Vector2(i, 0));
        }

        Assert.Equal(5, _state.Tiles.Count);
        for (int i = 1; i <= 5; i++)
        {
            Assert.True(_state.Tiles.ContainsKey(i));
        }
    }

    [Fact]
    public void RemoveNonexistentTile_DoesNotThrow()
    {
        // Should not throw when removing a tile that doesn't exist
        _state.RemoveTile(999);
    }
}
