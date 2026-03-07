using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Presentation;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for destroy tile animation in Player.
/// Verifies scale and alpha interpolation during DestroyTileCommand playback.
/// </summary>
public class PlayerDestroyAnimationTests
{
    private readonly Player _player;
    private readonly VisualState _visualState;

    public PlayerDestroyAnimationTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    private void SetupTileAndDestroy(int tileId, float destroyDuration)
    {
        // Spawn a tile first
        var spawnCmd = new SpawnTileCommand
        {
            TileId = tileId,
            Type = ElementType.Item1,
            Bomb = BombType.None,
            GridPos = new Match3.Core.Models.Grid.Position(0, 0),
            SpawnPos = new Vector2(0, 0),
            StartTime = 0f,
            Duration = 0f,
            Priority = 0
        };

        var destroyCmd = new DestroyTileCommand
        {
            TileId = tileId,
            Position = new Vector2(0, 0),
            Reason = DestroyReason.Match,
            StartTime = 0.1f,
            Duration = destroyDuration,
            Priority = 0
        };

        _player.Load(new RenderCommand[] { spawnCmd, destroyCmd });
    }

    [Fact]
    public void DestroyAnimation_AtStart_ScaleIsOne()
    {
        SetupTileAndDestroy(1, 0.2f);

        // Tick past spawn, to the start of destroy
        _player.Tick(0.1f);

        var tile = _visualState.Tiles[1];
        Assert.Equal(1f, tile.Scale.X, 0.01f);
        Assert.Equal(1f, tile.Scale.Y, 0.01f);
    }

    [Fact]
    public void DestroyAnimation_AtMidpoint_ScaleIsHalf()
    {
        SetupTileAndDestroy(1, 0.2f);

        // Tick to midpoint of destroy (0.1 + 0.1 = 0.2)
        _player.Tick(0.2f);

        var tile = _visualState.Tiles[1];
        // At t=0.5: scale = 1 - 0.5 = 0.5
        Assert.Equal(0.5f, tile.Scale.X, 0.05f);
        Assert.Equal(0.5f, tile.Scale.Y, 0.05f);
    }

    [Fact]
    public void DestroyAnimation_AtEnd_TileIsInvisible()
    {
        SetupTileAndDestroy(1, 0.2f);

        // Tick past destroy end (0.1 + 0.2 = 0.3)
        _player.Tick(0.35f);

        var tile = _visualState.Tiles[1];
        Assert.False(tile.IsVisible);
    }

    [Fact]
    public void DestroyAnimation_AtEnd_AlphaIsZero()
    {
        SetupTileAndDestroy(1, 0.2f);

        // Tick to just before end for alpha check
        _player.Tick(0.29f);

        var tile = _visualState.Tiles[1];
        // Near end: alpha should be very low
        Assert.True(tile.Alpha < 0.1f, $"Alpha should be near 0 at end, was {tile.Alpha}");
    }

    [Fact]
    public void DestroyAnimation_ScaleReachesZero_NotPartial()
    {
        SetupTileAndDestroy(1, 0.2f);

        // Tick to 95% of destroy
        _player.Tick(0.1f + 0.19f);

        var tile = _visualState.Tiles[1];
        // At t=0.95: scale = 1 - 0.95 = 0.05
        Assert.True(tile.Scale.X < 0.1f, $"Scale should be near 0 at 95%, was {tile.Scale.X}");
    }

    [Fact]
    public void DestroyAnimation_AlphaDecreasesMonotonically()
    {
        SetupTileAndDestroy(1, 1.0f);

        // Tick past spawn
        _player.Tick(0.1f);
        float prevAlpha = _visualState.Tiles[1].Alpha;

        // Check alpha decreases over time
        for (int i = 1; i <= 9; i++)
        {
            _player.Tick(0.1f);
            if (!_visualState.Tiles.ContainsKey(1)) break;
            var tile = _visualState.Tiles[1];
            Assert.True(tile.Alpha <= prevAlpha,
                $"Alpha should decrease: was {prevAlpha}, now {tile.Alpha} at step {i}");
            prevAlpha = tile.Alpha;
        }
    }

    [Fact]
    public void DestroyAnimation_ScaleDecreasesMonotonically()
    {
        SetupTileAndDestroy(1, 1.0f);

        // Tick past spawn
        _player.Tick(0.1f);
        float prevScale = _visualState.Tiles[1].Scale.X;

        // Check scale decreases over time
        for (int i = 1; i <= 9; i++)
        {
            _player.Tick(0.1f);
            if (!_visualState.Tiles.ContainsKey(1)) break;
            var tile = _visualState.Tiles[1];
            Assert.True(tile.Scale.X <= prevScale,
                $"Scale should decrease: was {prevScale}, now {tile.Scale.X} at step {i}");
            prevScale = tile.Scale.X;
        }
    }
}
