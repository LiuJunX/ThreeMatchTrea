using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Xunit;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for Player time reset and long-runtime precision.
/// Verifies that:
/// 1. Player resets _currentTime to 0 when all animations complete (prevents float drift).
/// 2. Animations play at correct speed even after simulated long runtime.
/// 3. Append() correctly locates commands after time reset.
/// </summary>
public class PlayerTimeResetTests
{
    private readonly VisualState _visualState;
    private readonly Player _player;

    public PlayerTimeResetTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    #region Time Reset After Idle

    [Fact]
    public void CurrentTime_ResetsToZero_WhenAllAnimationsComplete()
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
                StartTime = 0f, Duration = 0.2f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.01f);
        Assert.True(_player.CurrentTime > 0);

        // Advance past the animation end
        _player.Tick(0.25f);

        // All animations done, time should reset to 0
        Assert.Equal(0f, _player.CurrentTime);
        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void CurrentTime_DoesNotReset_WhileAnimationsActive()
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
                StartTime = 0f, Duration = 0.5f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.1f);

        // Animation still playing, time should NOT reset
        Assert.True(_player.CurrentTime > 0);
        Assert.True(_player.HasActiveAnimations);
    }

    [Fact]
    public void Append_WorksCorrectly_AfterTimeReset()
    {
        // First batch: play and complete
        var batch1 = new RenderCommand[]
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
                StartTime = 0f, Duration = 0.1f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(batch1);
        _player.Tick(0.15f); // complete batch 1 → time resets to 0

        Assert.Equal(0f, _player.CurrentTime);

        // Second batch: append new commands at time 0
        float baseTime = _player.CurrentTime; // should be 0
        var batch2 = new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(1, 0), To = new Vector2(2, 0),
                StartTime = baseTime, Duration = 0.2f,
                Easing = EasingType.Linear
            }
        };
        _player.Append(batch2);

        // Tick to start the new animation
        _player.Tick(0.1f);

        // Animation should be playing (tile moving)
        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.True(tile.IsBeingAnimated);

        // Position should be roughly halfway (t ≈ 0.5)
        Assert.True(tile.Position.X > 1.3f && tile.Position.X < 1.7f,
            $"Expected position ~1.5, got {tile.Position.X}");
    }

    #endregion

    #region Long-Runtime Precision

    [Fact]
    public void Animation_PlaysAtCorrectSpeed_AfterLongRuntime()
    {
        // Simulate 12 hours of idle by advancing time in large chunks
        var spawnCmd = new SpawnTileCommand
        {
            TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
            GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
            StartTime = 0f, Duration = 0f
        };
        _player.Load(new RenderCommand[] { spawnCmd });
        _player.Tick(0.001f); // process spawn → time resets (no animations)

        // Due to time reset, CurrentTime should be 0, not accumulated
        Assert.Equal(0f, _player.CurrentTime);

        // Simulate many idle ticks (no commands, time keeps resetting to 0)
        for (int i = 0; i < 100; i++)
        {
            _player.Tick(0.016f);
        }
        // Time should still be 0 (resets each tick since no active animations)
        Assert.Equal(0f, _player.CurrentTime);

        // Now start a merge animation
        float baseTime = _player.CurrentTime;
        const float mergeDuration = 0.3f;

        var mergeCommands = new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(3, 0),
                StartTime = baseTime, Duration = mergeDuration,
                Easing = EasingType.Linear
            }
        };
        _player.Append(mergeCommands);

        // Tick half the duration
        _player.Tick(mergeDuration / 2);

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // With linear easing, position should be halfway: (0,0) → (3,0) at t=0.5 → (1.5, 0)
        Assert.Equal(1.5f, tile.Position.X, 0.05f);
    }

    [Fact]
    public void Animation_CompletesInExpectedFrameCount_AfterLongRuntime()
    {
        // This test verifies animations don't take extra frames due to float drift
        var spawnCmd = new SpawnTileCommand
        {
            TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
            GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
            StartTime = 0f, Duration = 0f
        };
        _player.Load(new RenderCommand[] { spawnCmd });
        _player.Tick(0.001f); // process spawn

        // Many idle ticks
        for (int i = 0; i < 200; i++)
            _player.Tick(0.016f);

        // Start animation
        const float duration = 0.3f;
        const float dt = 0.016f; // ~60fps
        int expectedFrames = (int)Math.Ceiling(duration / dt); // 19

        var moveCmd = new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0), To = new Vector2(1, 0),
                StartTime = _player.CurrentTime, Duration = duration,
                Easing = EasingType.Linear
            }
        };
        _player.Append(moveCmd);

        int frameCount = 0;
        while (_player.HasActiveAnimations && frameCount < 100)
        {
            _player.Tick(dt);
            frameCount++;
        }

        Assert.False(_player.HasActiveAnimations);
        Assert.True(frameCount <= expectedFrames + 1,
            $"Animation should complete in ~{expectedFrames} frames, took {frameCount}");
    }

    [Fact]
    public void MultipleAppendCycles_TimeStaysLow()
    {
        // Simulate multiple play → idle → play cycles
        // Verify that CurrentTime doesn't accumulate across cycles
        var spawnCmd = new SpawnTileCommand
        {
            TileId = 1, Type = ElementType.Item1, Bomb = BombType.None,
            GridPos = new Position(0, 0), SpawnPos = new Vector2(0, 0),
            StartTime = 0f, Duration = 0f
        };
        _player.Load(new RenderCommand[] { spawnCmd });
        _player.Tick(0.001f);

        for (int cycle = 0; cycle < 50; cycle++)
        {
            // Idle ticks
            for (int i = 0; i < 10; i++)
                _player.Tick(0.016f);

            // Play an animation
            var moveCmd = new RenderCommand[]
            {
                new MoveTileCommand
                {
                    TileId = 1,
                    From = new Vector2(0, 0), To = new Vector2(1, 0),
                    StartTime = _player.CurrentTime, Duration = 0.1f,
                    Easing = EasingType.Linear
                }
            };
            _player.Append(moveCmd);

            // Complete it
            _player.Tick(0.15f);
        }

        // After 50 cycles, time should have been reset and stay near 0
        Assert.True(_player.CurrentTime < 1f,
            $"CurrentTime should stay low after resets, was {_player.CurrentTime}");
    }

    #endregion

    #region Commands Cleanup

    [Fact]
    public void CommandsList_ClearedOnReset()
    {
        // Verify that HasActiveAnimations returns false after completion
        // (indirectly tests that _commands is cleaned up)
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
                StartTime = 0f, Duration = 0.1f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.15f); // complete all

        Assert.False(_player.HasActiveAnimations);

        // Append new commands — should work without interference from old ones
        var newCommands = new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(1, 0), To = new Vector2(2, 0),
                StartTime = 0f, Duration = 0.1f,
                Easing = EasingType.Linear
            }
        };
        _player.Append(newCommands);
        _player.Tick(0.01f);

        Assert.True(_player.HasActiveAnimations,
            "New commands after reset should start correctly");
    }

    #endregion
}
