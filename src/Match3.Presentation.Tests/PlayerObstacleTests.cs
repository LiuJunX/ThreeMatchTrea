using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;

namespace Match3.Presentation.Tests;

/// <summary>
/// Tests for obstacle command handling in Player.
/// Verifies spawn, damage, destroy, and remove lifecycle.
/// </summary>
public class PlayerObstacleTests
{
    private readonly Player _player;
    private readonly VisualState _visualState;

    public PlayerObstacleTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    private static readonly Position Pos00 = new(0, 0);
    private static readonly Position Pos11 = new(1, 1);

    [Fact]
    public void SpawnObstacle_CreatesVisualInState()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00,
                ObstacleType = ObstacleType.Box,
                Stage = 4,
                StartTime = 0f,
                Duration = 0f
            }
        });

        _player.Tick(0.01f);

        var obs = _visualState.GetObstacle(Pos00);
        Assert.NotNull(obs);
        Assert.Equal(ObstacleType.Box, obs.Type);
        Assert.Equal(4, obs.CurrentStage);
        Assert.True(obs.IsVisible);
    }

    [Fact]
    public void DamageObstacle_UpdatesStageAndDrivesProgress()
    {
        // Spawn + damage
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box, Stage = 4,
                StartTime = 0f, Duration = 0f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box, NewStage = 3,
                StartTime = 0.1f, Duration = 0.2f
            }
        });

        // Tick to damage start
        _player.Tick(0.1f);
        var obs = _visualState.GetObstacle(Pos00)!;
        Assert.Equal(3, obs.CurrentStage); // stage updated immediately
        Assert.Equal(0f, obs.DamageProgress, 0.01f);

        // Tick to midpoint
        _player.Tick(0.1f);
        Assert.Equal(0.5f, obs.DamageProgress, 0.05f);

        // Tick past end
        _player.Tick(0.15f);
        Assert.Equal(1f, obs.DamageProgress, 0.01f);
    }

    [Fact]
    public void DestroyObstacle_DrivesDeathProgress()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box, Stage = 1,
                StartTime = 0f, Duration = 0f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box,
                StartTime = 0.1f, Duration = 0.3f
            }
        });

        // Tick to destroy start
        _player.Tick(0.1f);
        var obs = _visualState.GetObstacle(Pos00)!;
        Assert.True(obs.IsDestroying);
        Assert.Equal(0f, obs.DeathProgress, 0.01f);

        // Midpoint
        _player.Tick(0.15f);
        Assert.Equal(0.5f, obs.DeathProgress, 0.05f);

        // End
        _player.Tick(0.2f);
        Assert.Equal(1f, obs.DeathProgress, 0.01f);
    }

    [Fact]
    public void RemoveObstacle_RemovesFromVisualState()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box, Stage = 1,
                StartTime = 0f, Duration = 0f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box,
                StartTime = 0.1f, Duration = 0.3f
            },
            new RemoveObstacleCommand
            {
                GridPos = Pos00,
                StartTime = 0.4f, Duration = 0f
            }
        });

        // Tick past everything
        _player.Tick(0.5f);

        Assert.Null(_visualState.GetObstacle(Pos00));
        Assert.Empty(_visualState.Obstacles);
    }

    [Fact]
    public void FullLifecycle_SpawnDamageDestroyRemove()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, Stage = 2,
                StartTime = 0f, Duration = 0f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, NewStage = 1,
                StartTime = 0.1f, Duration = 0.35f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush,
                StartTime = 0.5f, Duration = 0.4f
            },
            new RemoveObstacleCommand
            {
                GridPos = Pos11,
                StartTime = 0.9f, Duration = 0f
            }
        });

        // After spawn
        _player.Tick(0.05f);
        var obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(2, obs.CurrentStage);
        Assert.False(obs.IsDestroying);

        // After damage
        _player.Tick(0.4f);
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(1, obs.CurrentStage);
        Assert.Equal(1f, obs.DamageProgress, 0.01f);

        // During destroy
        _player.Tick(0.2f);
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.True(obs.IsDestroying);
        Assert.True(obs.DeathProgress > 0f);

        // After remove
        _player.Tick(0.35f);
        Assert.Null(_visualState.GetObstacle(Pos11));
    }

    [Fact]
    public void DestroyObstacle_BlocksHasActiveAnimations()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box, Stage = 1,
                StartTime = 0f, Duration = 0f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos00, ObstacleType = ObstacleType.Box,
                StartTime = 0.1f, Duration = 0.3f
            }
        });

        _player.Tick(0.15f);
        Assert.True(_player.HasActiveAnimations);

        _player.Tick(0.3f);
        Assert.False(_player.HasActiveAnimations);
    }
}
