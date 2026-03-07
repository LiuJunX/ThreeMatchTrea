using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Xunit;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for Player.Tick fast-forward logic (commit f48c667).
/// When no active commands exist but pending commands have future StartTimes,
/// the player skips idle time gaps to avoid stalling.
/// </summary>
public class PlayerFastForwardTests
{
    private readonly VisualState _visualState;
    private readonly Player _player;

    public PlayerFastForwardTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    /// <summary>
    /// Scenario 1: Pending command has StartTime beyond current targetTime.
    /// Player should fast-forward to that StartTime and begin executing it.
    /// </summary>
    [Fact]
    public void Tick_FastForwardsToPendingCommand_WhenIdleWithGap()
    {
        // Spawn at t=0, then a move starting at t=5.0 (large gap)
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
                GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(3, 0),
                StartTime = 5.0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);

        // Tick 1: processes the spawn (duration=0, completes immediately).
        // Fast-forward check runs before command start, so it doesn't trigger yet.
        _player.Tick(0.016f);

        // Tick 2: no active commands, pending move at t=5.0 → fast-forward kicks in.
        _player.Tick(0.016f);

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.True(tile.IsBeingAnimated,
            "Move command should have started via fast-forward");
        Assert.True(_player.CurrentTime >= 5.0f,
            $"CurrentTime should have jumped to at least 5.0, was {_player.CurrentTime}");
    }

    /// <summary>
    /// Scenario 2: Pending command starts within the current tick's reach.
    /// No fast-forward needed — normal advancement suffices.
    /// </summary>
    [Fact]
    public void Tick_DoesNotFastForward_WhenNoGap()
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
                GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(1, 0),
                StartTime = 0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.016f);

        // Both commands start at t=0, so normal tick at dt=0.016 handles them.
        // CurrentTime should be exactly deltaTime, not jumped.
        Assert.Equal(0.016f, _player.CurrentTime, 0.001f);
        Assert.True(_player.HasActiveAnimations);
    }

    /// <summary>
    /// Scenario 3: Active commands are still running.
    /// Fast-forward must NOT trigger even if pending commands have future StartTimes.
    /// </summary>
    [Fact]
    public void Tick_DoesNotFastForward_WhenActiveCommandsExist()
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
                GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
                StartTime = 0f, Duration = 0f
            },
            new SpawnTileCommand
            {
                TileId = 2, Type = ElementType.Item3, Bomb = BombType.None,
                GridPos = new Position(1, 0), SpawnPos = new Vector2(1, 0),
                StartTime = 0f, Duration = 0f
            },
            // Active: move tile 1, duration 1.0s
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(0, 5),
                StartTime = 0f, Duration = 1.0f,
                Easing = EasingType.Linear
            },
            // Pending: move tile 2 at t=10.0 (far future)
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0), To = new Vector2(1, 5),
                StartTime = 10.0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.1f);

        // Tile 1's move is active — no fast-forward should occur
        Assert.Equal(0.1f, _player.CurrentTime, 0.001f);

        var tile2 = _visualState.GetTile(2);
        Assert.NotNull(tile2);
        Assert.False(tile2.IsBeingAnimated,
            "Tile 2's future command should NOT have started while tile 1 is animating");
    }

    /// <summary>
    /// Scenario 4: After fast-forward, the command executes normally
    /// and completes at the expected time.
    /// </summary>
    [Fact]
    public void FastForwardedCommand_ExecutesAndCompletes_Normally()
    {
        const float gapStart = 3.0f;
        const float moveDuration = 0.3f;

        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
                GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(4, 0),
                StartTime = gapStart, Duration = moveDuration,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);

        // Tick 1: spawn completes (duration=0)
        _player.Tick(0.016f);
        // Tick 2: no active commands → fast-forward to t=3.0, move starts
        _player.Tick(0.016f);
        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.True(tile.IsBeingAnimated);

        // Tick 2: advance halfway through the move
        _player.Tick(moveDuration / 2);
        Assert.True(tile.IsBeingAnimated);
        Assert.Equal(2.0f, tile.Position.X, 0.1f); // halfway: 0 → 4 at t=0.5 → 2.0

        // Tick 3: advance past the end
        _player.Tick(moveDuration);
        Assert.False(tile.IsBeingAnimated);
        Assert.Equal(4.0f, tile.Position.X, 0.01f); // final position
    }

    /// <summary>
    /// Multiple consecutive gaps: each gap triggers fast-forward independently.
    /// </summary>
    [Fact]
    public void Tick_FastForwards_AcrossMultipleGaps()
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
                GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
                StartTime = 0f, Duration = 0f
            },
            // Gap 1: jump to t=2.0
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(1, 0),
                StartTime = 2.0f, Duration = 0.1f,
                Easing = EasingType.Linear
            },
            // Gap 2: jump to t=5.0
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(1, 0), To = new Vector2(2, 0),
                StartTime = 5.0f, Duration = 0.1f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);

        // Tick 1: spawn completes (duration=0)
        _player.Tick(0.016f);
        // Tick 2: no active commands → fast-forward to t=2.0, first move starts
        _player.Tick(0.016f);
        Assert.True(_player.CurrentTime >= 2.0f);
        Assert.True(_visualState.GetTile(1)!.IsBeingAnimated);

        // Tick 2: complete first move (0.1s duration)
        _player.Tick(0.2f);

        // First move done. No active commands, pending at t=5.0 → fast-forward again.
        Assert.True(_player.CurrentTime >= 5.0f,
            $"Should have fast-forwarded to second gap, CurrentTime={_player.CurrentTime}");
        Assert.True(_visualState.GetTile(1)!.IsBeingAnimated,
            "Second move should have started via fast-forward");

        // Tick 3: complete second move
        _player.Tick(0.2f);
        Assert.False(_player.HasActiveAnimations);
        Assert.Equal(2.0f, _visualState.GetTile(1)!.Position.X, 0.01f);
    }
}
