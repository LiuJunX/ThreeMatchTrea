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

    [Fact]
    public void Bush_FiveStageProgression_AllStagesVisible()
    {
        // Build command sequence: spawn at stage 5, damage 4 times (5→4→3→2→1), then destroy
        float t = 0f;
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, Stage = 5,
                StartTime = t, Duration = 0f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, NewStage = 4,
                StartTime = t + 0.1f, Duration = 0.35f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, NewStage = 3,
                StartTime = t + 0.5f, Duration = 0.35f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, NewStage = 2,
                StartTime = t + 0.9f, Duration = 0.35f
            },
            new DamageObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, NewStage = 1,
                StartTime = t + 1.3f, Duration = 0.35f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush,
                StartTime = t + 1.7f, Duration = 0.4f
            },
            new RemoveObstacleCommand
            {
                GridPos = Pos11,
                StartTime = t + 2.1f, Duration = 0f
            }
        });

        // Stage 5 after spawn
        _player.Tick(0.05f);
        var obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(5, obs.CurrentStage);

        // Stage 4 during first damage (before second starts at 0.5)
        _player.Tick(0.2f); // total 0.25
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(4, obs.CurrentStage);
        Assert.True(obs.DamageProgress > 0f && obs.DamageProgress < 1f);

        // Stage 3 during second damage (before third starts at 0.9)
        _player.Tick(0.4f); // total 0.65
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(3, obs.CurrentStage);

        // Stage 2 during third damage (before fourth starts at 1.3)
        _player.Tick(0.4f); // total 1.05
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(2, obs.CurrentStage);

        // Stage 1 during fourth damage (before destroy starts at 1.7)
        _player.Tick(0.4f); // total 1.45
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.Equal(1, obs.CurrentStage);

        // Destroying
        _player.Tick(0.4f); // total 1.85
        obs = _visualState.GetObstacle(Pos11)!;
        Assert.True(obs.IsDestroying);
        Assert.True(obs.DeathProgress > 0f);

        // Removed
        _player.Tick(0.4f); // total 2.25
        Assert.Null(_visualState.GetObstacle(Pos11));
    }

    [Fact]
    public void Bush_DestroyThenGrassSpawn_GrassAppearsInVisualState()
    {
        var grassPos = new Position(1, 2);

        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush, Stage = 1,
                StartTime = 0f, Duration = 0f
            },
            new DestroyObstacleCommand
            {
                GridPos = Pos11, ObstacleType = ObstacleType.Bush,
                StartTime = 0.1f, Duration = 0.4f
            },
            new SpawnGroundCommand
            {
                GridPos = grassPos, GroundType = GroundType.Grass, Health = 1,
                StartTime = 0.1f, Duration = 0.3f
            },
            new RemoveObstacleCommand
            {
                GridPos = Pos11,
                StartTime = 0.5f, Duration = 0f
            }
        });

        // Before commands fire
        _player.Tick(0.05f);
        Assert.Null(_visualState.GetGround(grassPos));

        // After destroy + grass spawn starts
        _player.Tick(0.1f); // total 0.15
        var grass = _visualState.GetGround(grassPos);
        Assert.NotNull(grass);
        Assert.Equal(GroundType.Grass, grass!.Type);

        // Bush still destroying (0.4s duration)
        var obs = _visualState.GetObstacle(Pos11);
        Assert.NotNull(obs);
        Assert.True(obs!.IsDestroying);

        // After Bush removed, Grass remains
        _player.Tick(0.4f); // total 0.55
        Assert.Null(_visualState.GetObstacle(Pos11));
        Assert.NotNull(_visualState.GetGround(grassPos));
    }

    [Fact]
    public void SpawnObstacle_ColorBox_StatePreserved()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnObstacleCommand
            {
                GridPos = Pos00,
                ObstacleType = ObstacleType.ColorBox,
                Stage = 3,
                State = (byte)ElementType.Item3,
                StartTime = 0f,
                Duration = 0f
            }
        });

        _player.Tick(0.01f);

        var visual = _visualState.GetObstacle(Pos00);
        Assert.NotNull(visual);
        Assert.Equal(ObstacleType.ColorBox, visual!.Type);
        Assert.Equal(3, visual.CurrentStage);
        Assert.Equal((byte)ElementType.Item3, visual.State);
    }
}
