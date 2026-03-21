using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Random;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for cover command handling in Player.
/// Verifies DestroyCoverCommand and RemoveCoverCommand lifecycle.
/// </summary>
public class PlayerCoverTests
{
    private readonly Player _player;
    private readonly VisualState _visualState;

    public PlayerCoverTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    private static readonly Position Pos00 = new(0, 0);
    private static readonly Position Pos11 = new(1, 1);

    [Fact]
    public void VisualState_AddCover_GetCover()
    {
        _visualState.AddCover(Pos00, CoverType.Frost, 1);

        var cover = _visualState.GetCover(Pos00);
        Assert.NotNull(cover);
        Assert.Equal(CoverType.Frost, cover.Type);
        Assert.Equal(1, cover.CurrentHealth);
        Assert.True(cover.IsVisible);
        Assert.False(cover.IsDestroying);
    }

    [Fact]
    public void VisualState_RemoveCover_ReturnsNull()
    {
        _visualState.AddCover(Pos00, CoverType.Frost, 1);
        _visualState.RemoveCover(Pos00);

        Assert.Null(_visualState.GetCover(Pos00));
    }

    [Fact]
    public void VisualState_GetCover_NonExistent_ReturnsNull()
    {
        Assert.Null(_visualState.GetCover(Pos11));
    }

    [Fact]
    public void VisualState_SyncFromGameState_IncludesCovers()
    {
        var state = new GameState(3, 3, 6, new XorShift64(42));
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, ElementType.Item1, x, y));

        state.SetCover(new Position(1, 1), new Cover(CoverType.Frost, health: 1));

        _visualState.SyncFromGameState(in state);

        var cover = _visualState.GetCover(new Position(1, 1));
        Assert.NotNull(cover);
        Assert.Equal(CoverType.Frost, cover.Type);
        Assert.Equal(1, cover.CurrentHealth);
    }

    [Fact]
    public void Player_DestroyCoverCommand_Apply_SetsDestroyProgress()
    {
        _visualState.AddCover(Pos00, CoverType.Frost, 1);

        _player.Load(new RenderCommand[]
        {
            new DestroyCoverCommand
            {
                GridPos = Pos00, CoverType = CoverType.Frost,
                StartTime = 0f, Duration = 0.25f
            }
        });

        _player.Tick(0.01f); // Start the command
        var cover = _visualState.GetCover(Pos00)!;
        Assert.True(cover.IsDestroying);
        Assert.Equal(0f, cover.DestroyProgress, 0.05f);

        // Tick to midpoint
        _player.Tick(0.125f);
        Assert.True(cover.DestroyProgress > 0.4f);
        Assert.True(cover.DestroyProgress < 0.6f);
    }

    [Fact]
    public void Player_DestroyCoverCommand_Complete_SetsProgressTo1()
    {
        _visualState.AddCover(Pos00, CoverType.Frost, 1);

        _player.Load(new RenderCommand[]
        {
            new DestroyCoverCommand
            {
                GridPos = Pos00, CoverType = CoverType.Frost,
                StartTime = 0f, Duration = 0.25f
            }
        });

        _player.Tick(0.3f); // Past end
        var cover = _visualState.GetCover(Pos00)!;
        Assert.Equal(1f, cover.DestroyProgress, 0.01f);
    }

    [Fact]
    public void Player_RemoveCoverCommand_RemovesCoverFromState()
    {
        _visualState.AddCover(Pos00, CoverType.Frost, 1);

        _player.Load(new RenderCommand[]
        {
            new DestroyCoverCommand
            {
                GridPos = Pos00, CoverType = CoverType.Frost,
                StartTime = 0f, Duration = 0.25f
            },
            new RemoveCoverCommand
            {
                GridPos = Pos00,
                StartTime = 0.25f, Duration = 0f
            }
        });

        // Tick past both commands
        _player.Tick(0.3f);
        Assert.Null(_visualState.GetCover(Pos00));
    }

    [Fact]
    public void Player_DestroyCoverCommand_NoCover_NoError()
    {
        // No cover at Pos00 — should not crash
        _player.Load(new RenderCommand[]
        {
            new DestroyCoverCommand
            {
                GridPos = Pos00, CoverType = CoverType.Frost,
                StartTime = 0f, Duration = 0.25f
            }
        });

        _player.Tick(0.3f); // Should complete without error
        Assert.False(_player.HasActiveAnimations);
    }
}
