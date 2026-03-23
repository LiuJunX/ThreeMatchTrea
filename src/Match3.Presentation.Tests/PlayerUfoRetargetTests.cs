using System.Numerics;
using Match3.Core;
using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Xunit;

namespace Match3.Presentation.Tests;

public class PlayerUfoRetargetTests
{
    private readonly VisualState _visualState;
    private readonly Player _player;

    public PlayerUfoRetargetTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    #region Retarget During StayFraction

    [Fact]
    public void RetargetDuringStayFraction_TargetUpdated_ProgressNotJumped()
    {
        // Launch UFO with StayFraction=0.15, tick to ~5% progress, then retarget
        float duration = 1.0f;
        var origin = new Vector2(1, 1);
        var oldTarget = new Vector2(7, 7);
        var newTarget = new Vector2(3, 5);

        SpawnAndLaunchUfo(tileId: 1, origin, oldTarget, stayFraction: 0.15f, duration);

        // Tick to ~5% progress (within StayFraction)
        _player.Tick(duration * 0.05f);

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // Retarget during stay phase
        var retargetCmd = new UfoRetargetCommand
        {
            TileId = 1,
            NewTarget = newTarget,
            StartTime = _player.CurrentTime,
            Duration = 0f
        };
        _player.Load(new RenderCommand[]
        {
            // Re-create the launch so it's active when retarget fires
            new UfoLaunchCommand
            {
                TileId = 1, Origin = origin, Target = oldTarget,
                StayFraction = 0.15f,
                StartTime = 0f, Duration = duration
            },
            retargetCmd
        });
        _player.Tick(duration * 0.05f); // tick to ~5%, then retarget fires

        tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // During StayFraction retarget: progress should NOT be jumped to 0.35
        Assert.NotEqual(0.35f, tile!.UfoFlightProgress);
        // Progress should be low (still in takeoff range)
        Assert.True(tile.UfoFlightProgress < 0.2f,
            $"Progress should be in takeoff range, got {tile.UfoFlightProgress}");
        // Retarget flag should NOT be set (silent target update during stay)
        Assert.False(tile.UfoRetargetFlag);
    }

    [Fact]
    public void RetargetDuringStayFraction_DurationUpdatedForNewDistance()
    {
        float duration = 1.0f;
        var origin = new Vector2(1, 1);
        var oldTarget = new Vector2(7, 7);
        var newTarget = new Vector2(2, 2); // much closer

        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Ufo,
                GridPos = new Position(1, 1), SpawnPos = origin,
                StartTime = 0f, Duration = 0f, Priority = -1
            },
            new UfoLaunchCommand
            {
                TileId = 1, Origin = origin, Target = oldTarget,
                StayFraction = 0.15f,
                StartTime = 0f, Duration = duration
            },
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = newTarget,
                StartTime = duration * 0.05f,
                Duration = 0f
            }
        };

        _player.Load(commands);
        _player.Tick(duration * 0.06f); // past retarget time

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // New distance is shorter â†?duration should differ from original
        // New duration = LaunchOverhead + distance(origin, newTarget) / FlightSpeed
        float newDistance = Vector2.Distance(origin, newTarget);
        float expectedDuration = UfoConstants.LaunchOverhead + newDistance / UfoConstants.FlightSpeed;
        Assert.Equal(expectedDuration, tile!.UfoFlightDuration, 0.01f);
        Assert.NotEqual(duration, tile.UfoFlightDuration);
    }

    #endregion

    #region Retarget During Cruise

    [Fact]
    public void RetargetDuringCruise_UsesExistingBehavior()
    {
        float duration = 1.0f;
        var origin = new Vector2(1, 1);
        var oldTarget = new Vector2(7, 7);
        var newTarget = new Vector2(3, 5);

        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Ufo,
                GridPos = new Position(1, 1), SpawnPos = origin,
                StartTime = 0f, Duration = 0f, Priority = -1
            },
            new UfoLaunchCommand
            {
                TileId = 1, Origin = origin, Target = oldTarget,
                StayFraction = 0.15f,
                StartTime = 0f, Duration = duration
            },
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = newTarget,
                StartTime = duration * 0.5f, // well past StayFraction
                Duration = 0f
            }
        };

        _player.Load(commands);
        _player.Tick(duration * 0.51f); // past retarget time

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // Cruise retarget: flag IS set, progress starts at 0.35 (may advance slightly in same frame)
        Assert.True(tile!.UfoRetargetFlag);
        Assert.True(tile.UfoFlightProgress >= 0.35f,
            $"Cruise retarget should set progress >= 0.35, got {tile.UfoFlightProgress}");
        Assert.True(tile.UfoFlightProgress < 0.5f,
            $"Progress should be near 0.35 start, got {tile.UfoFlightProgress}");
    }

    #endregion

    #region StayFraction Retarget then Cruise

    [Fact]
    public void RetargetDuringStayFraction_ThenCruise_FliesToNewTarget()
    {
        float duration = 2.0f;
        var origin = new Vector2(1, 1);
        var oldTarget = new Vector2(7, 7);
        var newTarget = new Vector2(4, 4);

        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = ElementType.Ufo,
                GridPos = new Position(1, 1), SpawnPos = origin,
                StartTime = 0f, Duration = 0f, Priority = -1
            },
            new UfoLaunchCommand
            {
                TileId = 1, Origin = origin, Target = oldTarget,
                StayFraction = 0.15f,
                StartTime = 0f, Duration = duration
            },
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = newTarget,
                StartTime = duration * 0.05f, // during stay
                Duration = 0f
            }
        };

        _player.Load(commands);

        // First: tick past retarget (fires during stay)
        _player.Tick(duration * 0.06f);

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        // Position should be at origin during stay
        Assert.Equal(origin, tile!.Position);

        // Tick well past stay fraction into cruise (e.g. 60% through updated duration)
        float updatedDuration = tile.UfoFlightDuration;
        float remainingToMiddle = updatedDuration * 0.6f - _player.CurrentTime;
        if (remainingToMiddle > 0)
            _player.Tick(remainingToMiddle);

        tile = _visualState.GetTile(1);
        Assert.NotNull(tile);

        // Position should be moving toward newTarget, not oldTarget
        // Verify by checking that position is closer to newTarget than oldTarget
        float distToNew = Vector2.Distance(tile!.Position, newTarget);
        float distToOld = Vector2.Distance(tile.Position, oldTarget);
        Assert.True(distToNew < distToOld,
            $"UFO should be heading toward new target ({newTarget}), " +
            $"but position {tile.Position} is closer to old target ({oldTarget}). " +
            $"distToNew={distToNew:F2}, distToOld={distToOld:F2}");
    }

    #endregion

    #region Helpers

    private void SpawnAndLaunchUfo(int tileId, Vector2 origin, Vector2 target,
        float stayFraction, float duration)
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = tileId, Type = ElementType.Ufo,
                GridPos = new Position((int)origin.X, (int)origin.Y),
                SpawnPos = origin,
                StartTime = 0f, Duration = 0f, Priority = -1
            },
            new UfoLaunchCommand
            {
                TileId = tileId, Origin = origin, Target = target,
                StayFraction = stayFraction,
                StartTime = 0f, Duration = duration
            }
        };

        _player.Load(commands);
        _player.Tick(0.001f); // process spawn + start launch
    }

    #endregion
}
