using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

public class ChoreographerTests
{
    private readonly Choreographer _choreographer = new();

    [Fact]
    public void Choreograph_TileDestroy_GeneratesDestroyAndEffect()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.Match,
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is DestroyTileCommand);
        Assert.Contains(commands, c => c is ShowEffectCommand);
        Assert.Contains(commands, c => c is RemoveTileCommand);
    }

    [Fact]
    public void Choreograph_TileMove_GeneratesMoveCommand()
    {
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var moveCmd = Assert.Single(commands.OfType<MoveTileCommand>());
        Assert.Equal(1, moveCmd.TileId);
        Assert.Equal(new Vector2(3, 3), moveCmd.From);
        Assert.Equal(new Vector2(3, 4), moveCmd.To);
        Assert.Equal(_choreographer.Config.MoveDuration, moveCmd.Duration);
    }

    [Fact]
    public void Choreograph_TileSpawn_GeneratesSpawnCommand()
    {
        var events = new GameEvent[]
        {
            new TileSpawnedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 0),
                Type = ElementType.Item1,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // SpawnTileCommand creates the tile, physics handles the falling animation
        Assert.Contains(commands, c => c is SpawnTileCommand);
        // No MoveTileCommand - physics system controls falling via SyncFallingTilesFromGameState
        Assert.DoesNotContain(commands, c => c is MoveTileCommand);
    }

    [Fact]
    public void Choreograph_TilesSwapped_GeneratesSwapCommand()
    {
        var events = new GameEvent[]
        {
            new TilesSwappedEvent
            {
                TileAId = 1,
                TileBId = 2,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                IsRevert = false,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var swapCmd = Assert.Single(commands.OfType<SwapTilesCommand>());
        Assert.Equal(1, swapCmd.TileAId);
        Assert.Equal(2, swapCmd.TileBId);
        Assert.Equal(new Vector2(3, 4), swapCmd.PosA);
        Assert.Equal(new Vector2(4, 4), swapCmd.PosB);
    }

    [Fact]
    public void Choreograph_MatchDetected_GeneratesHighlight()
    {
        var events = new GameEvent[]
        {
            new MatchDetectedEvent
            {
                Type = ElementType.Item1,
                Positions = new[] { new Position(0, 0), new Position(1, 0), new Position(2, 0) },
                TileCount = 3,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var highlightCmd = Assert.Single(commands.OfType<ShowMatchHighlightCommand>());
        Assert.Equal(3, highlightCmd.Positions.Length);
    }

    [Fact]
    public void Choreograph_BombCreated_GeneratesHoldSpawnAndEffect()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 1,
                NewTileId = 10,
                Position = new Position(3, 4),
                BombType = ElementType.HorizontalRocket,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Hold old tile in place during merge
        var holdCmd = commands.OfType<MoveTileCommand>().First();
        Assert.Equal(1, holdCmd.TileId);
        Assert.Equal(holdCmd.From, holdCmd.To); // from == to = hold in place

        // Remove old tile after merge
        Assert.Contains(commands, c => c is RemoveTileCommand { TileId: 1 });

        // Spawn new bomb tile after merge
        var spawnCmd = commands.OfType<SpawnTileCommand>().First();
        Assert.Equal(10, spawnCmd.TileId);
        Assert.Equal(ElementType.HorizontalRocket, spawnCmd.Type);

        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_created" });
    }

    [Fact]
    public void Choreograph_ProjectileLaunched_NonUfo_GeneratesSpawnCommand()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 100,
                Type = ProjectileType.ColorBombBeam,
                Origin = new Vector2(3, 4),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = Assert.Single(commands.OfType<SpawnProjectileCommand>());
        Assert.Equal(100, spawnCmd.ProjectileId);
        Assert.Equal(new Vector2(3, 4), spawnCmd.Origin);
    }

    [Fact]
    public void Choreograph_UfoProjectileLaunched_GeneratesUfoLaunchCommand()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 100,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(3, 4),
                TargetPosition = new Position(6, 7),
                SourceTileId = 42,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var ufoCmd = Assert.Single(commands.OfType<UfoLaunchCommand>());
        Assert.Equal(42, ufoCmd.TileId);
        Assert.Equal(new Vector2(3, 4), ufoCmd.Origin);
        // Target includes overshoot (0.3 along flight dir) + screen-up offset (-0.40 Y)
        Assert.NotEqual(new Vector2(6, 7), ufoCmd.Target); // Raw grid pos is adjusted
        Assert.True(ufoCmd.Target.X > 6); // Overshoot pushes past raw target
        Assert.True(ufoCmd.Target.Y < 7); // Screen-up nudge reduces Y
        Assert.Equal(UfoConstants.LaunchStayFraction, ufoCmd.StayFraction);
        // Diverge control points should be set (origin-to-target distance > DivergeMinDistance)
        Assert.NotNull(ufoCmd.DivergeControl);
        Assert.NotNull(ufoCmd.ApproachControl);
    }

    [Fact]
    public void Choreograph_UfoShortDistance_NoDivergeControlPoints()
    {
        // Origin (3,4) to target (4,4) — distance ~1, below DivergeMinDistance(2)
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 200,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(3, 4),
                TargetPosition = new Position(4, 4),
                SourceTileId = 50,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var ufoCmd = Assert.Single(commands.OfType<UfoLaunchCommand>());
        Assert.Equal(50, ufoCmd.TileId);
        Assert.Null(ufoCmd.DivergeControl);
        Assert.Null(ufoCmd.ApproachControl);
    }

    [Fact]
    public void Choreograph_ProjectileImpact_GeneratesImpactAndRemove()
    {
        var events = new GameEvent[]
        {
            new ProjectileImpactEvent
            {
                ProjectileId = 100,
                ImpactPosition = new Position(5, 6),
                SimulationTime = 1f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is ImpactProjectileCommand { ProjectileId: 100 });
        Assert.Contains(commands, c => c is RemoveProjectileCommand { ProjectileId: 100 });
    }

    [Fact]
    public void Choreograph_WithBaseTime_OffsetsAllCommands()
    {
        const float baseTime = 2.0f;
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events, baseTime);

        var moveCmd = Assert.Single(commands.OfType<MoveTileCommand>());
        Assert.True(moveCmd.StartTime >= baseTime);
    }

    [Fact]
    public void Choreograph_EmptyEvents_ReturnsEmptyList()
    {
        var commands = _choreographer.Choreograph(Array.Empty<GameEvent>());

        Assert.Empty(commands);
    }

    #region Cascade Timing Tests


    [Fact]
    public void Choreograph_SpawnUsesSimulationTime_PhysicsHandlesCascade()
    {
        // Spawn commands now execute at their simulation time
        // Cascade timing is handled by physics system, not Choreographer
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 0),
                Type = ElementType.Item1,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 0),
                Type = ElementType.Item3,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First();

        // Spawn uses simulation time directly — no cascade delay prediction
        Assert.Equal(0f, spawnCmd.StartTime, 0.001f);
    }

    [Fact]
    public void Choreograph_MultipleTilesFalling_CascadeCorrectly()
    {
        // Three tiles falling in same column: from row 2,1,0 to row 5,4,3
        // Y increases downward, so row 5 is the bottommost position
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 2),
                ToPosition = new Vector2(3, 5),  // Bottommost destination
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(3, 1),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 3,
                FromPosition = new Vector2(3, 0),
                ToPosition = new Vector2(3, 3),  // Topmost destination
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Filter out hold commands (From == To) to get actual moves
        var moveCmds = commands.OfType<MoveTileCommand>().Where(c => c.From != c.To).OrderByDescending(c => c.To.Y).ToList();
        Assert.Equal(3, moveCmds.Count);

        // Tiles moving to bottommost positions (higher Y) should start first
        // Tile moving to row 5 (bottommost) should start first
        // Tile moving to row 4 should cascade after tile at row 5 clears space
        // Tile moving to row 3 should cascade after tile at row 4 clears space
        Assert.True(moveCmds[0].StartTime <= moveCmds[1].StartTime,
            $"Tile to row 5 ({moveCmds[0].StartTime}) should start <= tile to row 4 ({moveCmds[1].StartTime})");
        Assert.True(moveCmds[1].StartTime <= moveCmds[2].StartTime,
            $"Tile to row 4 ({moveCmds[1].StartTime}) should start <= tile to row 3 ({moveCmds[2].StartTime})");
    }

    [Fact]
    public void Choreograph_DifferentColumns_NoCascadeInterference()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(2, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(3, 3), // Different column
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var destroyCmd = commands.OfType<DestroyTileCommand>().First();
        var moveCmd = commands.OfType<MoveTileCommand>().First();

        // Move in column 3 should NOT wait for destroy in column 2
        Assert.Equal(0f, moveCmd.StartTime);
    }

    [Fact]
    public void Choreograph_DestroyAndSpawnSamePosition_SpawnUsesSimulationTime()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 4),
                Type = ElementType.Item3,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0.1f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First();

        // Spawn uses simulation time directly — relative offset from first event.
        // simTime=0.1, minSimTime=0 → startTime = 0.1
        Assert.Equal(0.1f, spawnCmd.StartTime, 0.001f);
    }

    #endregion

    #region Cover and Ground Events

    [Fact]
    public void Choreograph_CoverDestroyed_GeneratesCommandAndEffect()
    {
        var events = new GameEvent[]
        {
            new CoverDestroyedEvent
            {
                GridPosition = new Position(3, 4),
                Type = CoverType.Cage,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is DestroyCoverCommand);
        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "cover_destroyed" });
    }

    [Fact]
    public void Choreograph_GroundDestroyed_GeneratesCommandAndEffect()
    {
        var events = new GameEvent[]
        {
            new GroundDestroyedEvent
            {
                GridPosition = new Position(3, 4),
                Type = GroundType.Ice,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is DestroyGroundCommand);
        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "ground_destroyed" });
    }

    #endregion

    #region Bomb Events

    [Fact]
    public void Choreograph_BombActivated_Square_GeneratesGenericExplosion()
    {
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = ElementType.Square5x5,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_flash" });
        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_explosion" });
    }

    [Fact]
    public void Choreograph_BombActivated_Horizontal_GeneratesRocketEffects()
    {
        var affected = new[]
        {
            new Position(1, 4), new Position(2, 4), new Position(3, 4),
            new Position(4, 4), new Position(5, 4)
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = ElementType.HorizontalRocket,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var effects = commands.OfType<ShowEffectCommand>().ToList();

        Assert.Contains(effects, c => c.EffectType == "bomb_flash");
        Assert.Contains(effects, c => c.EffectType == "rocket_trail_h");
        Assert.Contains(effects, c => c.EffectType == "rocket_head");
        // Should NOT contain generic explosion
        Assert.DoesNotContain(effects, c => c.EffectType == "bomb_explosion");
    }

    [Fact]
    public void Choreograph_BombActivated_Horizontal_TrailsAccelerate()
    {
        // 5 cells: origin at 3, affected at 1,2,3,4,5 → distances 2,1,0,1,2
        var affected = new[]
        {
            new Position(1, 4), new Position(2, 4), new Position(3, 4),
            new Position(4, 4), new Position(5, 4)
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = ElementType.HorizontalRocket,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var trails = commands.OfType<ShowEffectCommand>()
            .Where(c => c.EffectType == "rocket_trail_h")
            .OrderBy(c => c.StartTime)
            .ToList();

        // dist=1 trails should come before dist=2 trails
        Assert.True(trails.Count >= 2);
        float firstDelay = trails[0].StartTime;
        float secondDelay = trails[2].StartTime; // dist=2 trail

        // Gap between dist1→dist2 should be SMALLER than gap from origin→dist1 (acceleration)
        float gap1 = firstDelay;             // 0 → dist1
        float gap2 = secondDelay - firstDelay; // dist1 → dist2
        Assert.True(gap2 < gap1, $"Trail interval should decrease: gap1={gap1}, gap2={gap2}");
    }

    [Fact]
    public void Choreograph_BombActivated_Color_GeneratesColorBombEffects()
    {
        var affected = new[]
        {
            new Position(3, 4), new Position(1, 1), new Position(7, 7)
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var effects = commands.OfType<ShowEffectCommand>().ToList();

        Assert.Contains(effects, c => c.EffectType == "bomb_flash");
        Assert.Contains(effects, c => c.EffectType == "color_bomb_wave");
        Assert.Contains(effects, c => c.EffectType == "color_bomb_hit");
        Assert.DoesNotContain(effects, c => c.EffectType == "bomb_explosion");

        // Beam projectiles for non-origin targets
        var beams = commands.OfType<SpawnProjectileCommand>()
            .Where(c => c.Type == ProjectileType.ColorBombBeam).ToList();
        Assert.Equal(2, beams.Count); // (1,1) and (7,7), not (3,4) which is origin
        Assert.True(beams.All(b => b.ProjectileId < 0), "Beam IDs should be negative");
    }

    [Fact]
    public void Choreograph_ColorBomb_BeamFliesFromHoppedOriginToTarget()
    {
        var origin = new Position(2, 2);
        var affected = new[] { new Position(6, 6) };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var spawn = commands.OfType<SpawnProjectileCommand>().Single();
        var move = commands.OfType<MoveProjectileCommand>().Single();
        var impact = commands.OfType<ImpactProjectileCommand>().Single();
        var remove = commands.OfType<RemoveProjectileCommand>().Single();

        // Spawn from hopped position (origin + hop offset)
        Assert.Equal(2f, spawn.Origin.X, 0.01f);
        Assert.Equal(1.7f, spawn.Origin.Y, 0.01f); // 2 - 0.3 hop
        Assert.Equal(0f, spawn.ArcHeight);

        // Move from hopped origin to target
        Assert.Equal(spawn.ProjectileId, move.ProjectileId);
        Assert.Equal(2f, move.From.X, 0.01f);
        Assert.Equal(6f, move.To.X, 0.01f);
        Assert.True(move.Duration > 0);

        // Impact at target
        Assert.Equal(spawn.ProjectileId, impact.ProjectileId);
        Assert.Equal(6f, impact.Position.X, 0.01f);

        // Remove after impact
        Assert.Equal(spawn.ProjectileId, remove.ProjectileId);
        Assert.True(remove.StartTime >= impact.StartTime);
    }

    [Fact]
    public void Choreograph_ColorBomb_ChargeUpPerformance()
    {
        var origin = new Position(3, 3);
        var affected = new[] { new Position(5, 5) };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 42,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Charge-up: scale pulse 1→1.2
        var scales = commands.OfType<ScaleTileCommand>().Where(c => c.TileId == 42).ToList();
        Assert.True(scales.Count >= 2); // charge-up + shrink
        var chargeScale = scales.First(s => s.ToScale.X > 1f);
        Assert.Equal(1.2f, chargeScale.ToScale.X, 0.01f);

        // Charge-up: hop (MoveTileCommand with Y offset)
        var hops = commands.OfType<MoveTileCommand>().Where(c => c.TileId == 42).ToList();
        Assert.Contains(hops, h => h.To.Y < h.From.Y); // hop upward (negative Y in grid coords)

        // Spin: RotateTileCommand with InQuadratic easing
        var rotate = commands.OfType<RotateTileCommand>().Single(c => c.TileId == 42);
        Assert.Equal(0f, rotate.FromAngle);
        Assert.True(rotate.ToAngle > 360f); // multiple rotations
        Assert.Equal(EasingType.InQuadratic, rotate.Easing);

        // Shrink to zero after beams land
        var shrink = scales.First(s => s.ToScale == System.Numerics.Vector2.Zero);
        Assert.True(shrink.StartTime > chargeScale.StartTime);

        // RemoveTileCommand after shrink
        var removeTile = commands.OfType<RemoveTileCommand>().Single(c => c.TileId == 42);
        Assert.True(removeTile.StartTime >= shrink.StartTime + shrink.Duration);
    }

    [Fact]
    public void Choreograph_ColorBomb_BeamsStaggeredByRing()
    {
        var origin = new Position(4, 4);
        var affected = new[]
        {
            new Position(5, 4), // Chebyshev dist 1
            new Position(6, 4), // Chebyshev dist 2
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var spawns = commands.OfType<SpawnProjectileCommand>()
            .Where(c => c.Type == ProjectileType.ColorBombBeam)
            .OrderBy(c => c.StartTime).ToList();

        Assert.Equal(2, spawns.Count);
        // Ring 2 beam launches later than ring 1
        Assert.True(spawns[1].StartTime > spawns[0].StartTime);
    }

    [Fact]
    public void Choreograph_ColorBomb_RainbowColorCycling()
    {
        var origin = new Position(4, 4);
        var affected = new[]
        {
            new Position(5, 4),
            new Position(3, 4),
            new Position(4, 5),
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var beams = commands.OfType<SpawnProjectileCommand>()
            .Where(c => c.Type == ProjectileType.ColorBombBeam).ToList();

        Assert.Equal(3, beams.Count);
        // Colors cycle through 0, 1, 2
        var colors = beams.Select(b => b.ColorIndex).ToList();
        Assert.Contains((byte)0, colors);
        Assert.Contains((byte)1, colors);
        Assert.Contains((byte)2, colors);
    }

    [Fact]
    public void Choreograph_ColorBomb_BeamHitTimesPersistAcrossBatches()
    {
        var origin = new Position(3, 3);
        var target = new Position(5, 5);
        var affected = new[] { target };

        // Batch 1: BombActivatedEvent (populates _beamHitTimes)
        var batch1 = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(batch1);

        // Batch 2: TileDestroyedEvent (should find beam hit time from batch 1)
        var batch2 = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 99,
                GridPosition = target,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 0.016f
            }
        };
        var commands2 = _choreographer.Choreograph(batch2, baseTime: 0.016f);

        // Should have a hold MoveTileCommand (from=to) keeping tile alive
        var holds = commands2.OfType<MoveTileCommand>()
            .Where(c => c.TileId == 99 && c.From == c.To).ToList();
        Assert.NotEmpty(holds);

        // Destroy should be delayed (not at batch2's base time)
        var destroy = commands2.OfType<DestroyTileCommand>().Single();
        Assert.True(destroy.StartTime > 0.016f);
    }

    [Fact]
    public void Choreograph_ColorBomb_BeamBeforeHitEffect()
    {
        var affected = new[] { new Position(0, 0), new Position(7, 7) };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 3),
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Each target has an impact + a hit effect, both at the same start time
        foreach (var pos in affected)
        {
            var impact = commands.OfType<ImpactProjectileCommand>()
                .First(c => (int)c.Position.X == pos.X && (int)c.Position.Y == pos.Y);
            var hit = commands.OfType<ShowEffectCommand>()
                .First(c => c.EffectType == "color_bomb_hit"
                    && (int)c.Position.X == pos.X && (int)c.Position.Y == pos.Y);
            Assert.Equal(impact.StartTime, hit.StartTime, 0.001f);
        }
    }

    [Fact]
    public void Choreograph_ColorBomb_AllBeamsArriveSimultaneously()
    {
        var origin = new Position(4, 4);
        var affected = new[]
        {
            new Position(5, 4), // dist 1
            new Position(7, 4), // dist 3
        };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var impacts = commands.OfType<ImpactProjectileCommand>().OrderBy(c => c.StartTime).ToList();

        Assert.Equal(2, impacts.Count);
        // All beams arrive at the same time (simultaneous impact)
        Assert.Equal(impacts[0].StartTime, impacts[1].StartTime, 0.001f);

        // Farther target launches first
        var spawns = commands.OfType<SpawnProjectileCommand>()
            .Where(c => c.Type == ProjectileType.ColorBombBeam)
            .OrderBy(c => c.StartTime).ToList();
        var moves = commands.OfType<MoveProjectileCommand>()
            .OrderBy(c => c.StartTime).ToList();

        // First spawn should correspond to farther target (7,4)
        var firstMove = moves.First(m => m.ProjectileId == spawns[0].ProjectileId);
        var secondMove = moves.First(m => m.ProjectileId == spawns[1].ProjectileId);
        Assert.Equal(7f, firstMove.To.X, 0.01f);
        Assert.Equal(5f, secondMove.To.X, 0.01f);
    }

    [Fact]
    public void Choreograph_ColorBomb_OriginSkipped()
    {
        var origin = new Position(3, 3);
        var affected = new[] { origin, new Position(5, 5) };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var beams = commands.OfType<SpawnProjectileCommand>()
            .Where(c => c.Type == ProjectileType.ColorBombBeam).ToList();
        var hits = commands.OfType<ShowEffectCommand>()
            .Where(c => c.EffectType == "color_bomb_hit").ToList();

        // Only one target (5,5), origin (3,3) should be skipped
        Assert.Single(beams);
        Assert.Single(hits);
    }

    [Fact]
    public void Choreograph_ColorBomb_AllAtOrigin_NoBeamsOrHits()
    {
        // All affected tiles at origin distance 0
        var origin = new Position(4, 4);
        var affected = new[] { origin };
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = origin,
                BombType = ElementType.ColorBomb,
                AffectedPositions = affected,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var effects = commands.OfType<ShowEffectCommand>().ToList();

        Assert.Contains(effects, e => e.EffectType == "color_bomb_wave");
        Assert.DoesNotContain(effects, e => e.EffectType == "color_bomb_hit");
        // No beams when all targets are at origin
        Assert.DoesNotContain(commands, c =>
            c is SpawnProjectileCommand s && s.Type == ProjectileType.ColorBombBeam);
    }

    [Fact]
    public void Choreograph_BombCombo_GeneratesTwoEffects()
    {
        var events = new GameEvent[]
        {
            new BombComboEvent
            {
                BombTypeA = ElementType.HorizontalRocket,
                BombTypeB = ElementType.VerticalRocket,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var comboEffects = commands.OfType<ShowEffectCommand>()
            .Where(c => c.EffectType == "bomb_combo").ToList();
        Assert.Equal(2, comboEffects.Count);
    }

    #endregion

    #region Projectile Events

    [Fact]
    public void Choreograph_ProjectileMoved_CalculatesDurationFromVelocity()
    {
        var events = new GameEvent[]
        {
            new ProjectileMovedEvent
            {
                ProjectileId = 100,
                FromPosition = new Vector2(0, 0),
                ToPosition = new Vector2(3, 4),  // Distance = 5
                Velocity = new Vector2(3, 4),    // Speed = 5
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var moveCmd = Assert.Single(commands.OfType<MoveProjectileCommand>());
        Assert.Equal(1f, moveCmd.Duration, 0.01f);  // Distance 5 / Speed 5 = 1 second
    }

    #endregion

    #region Timing Tests

    [Fact]
    public void Choreograph_EventsWithNonZeroSimulationTime_UseRelativeOffset()
    {
        // This test verifies the fix for the bug where events with SimulationTime = engine.ElapsedTime
        // would result in commands starting at baseTime + SimulationTime, which could be far in the future.
        // The fix makes SimulationTime relative to the minimum SimulationTime in the batch.
        const float baseTime = 5.0f;
        const float eventSimTime = 5.0f; // Simulates events from an engine running for 5 seconds

        var events = new GameEvent[]
        {
            new TilesSwappedEvent
            {
                TileAId = 1,
                TileBId = 2,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                IsRevert = false,
                SimulationTime = eventSimTime // Non-zero simulation time
            }
        };

        var commands = _choreographer.Choreograph(events, baseTime);

        var swapCmd = Assert.Single(commands.OfType<SwapTilesCommand>());

        // Before fix: StartTime would be 5.0 + 5.0 = 10.0 (wrong!)
        // After fix: StartTime should be 5.0 + (5.0 - 5.0) = 5.0 (correct!)
        Assert.Equal(baseTime, swapCmd.StartTime);
    }

    [Fact]
    public void Choreograph_MultipleEventsWithDifferentSimTimes_PreservesRelativeOrder()
    {
        const float baseTime = 5.0f;

        var events = new GameEvent[]
        {
            new TilesSwappedEvent
            {
                TileAId = 1,
                TileBId = 2,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                SimulationTime = 5.0f // First event at t=5.0
            },
            new TileDestroyedEvent
            {
                TileId = 3,
                GridPosition = new Position(5, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.Match,
                SimulationTime = 5.1f // Second event at t=5.1 (0.1s later)
            }
        };

        var commands = _choreographer.Choreograph(events, baseTime);

        var swapCmd = commands.OfType<SwapTilesCommand>().First();
        var destroyCmd = commands.OfType<DestroyTileCommand>().First();

        // First event starts at baseTime
        Assert.Equal(baseTime, swapCmd.StartTime);

        // Second event starts at baseTime + 0.1 (relative offset preserved)
        Assert.Equal(baseTime + 0.1f, destroyCmd.StartTime, 0.001f);
    }

    #endregion

    #region UFO Flight Lifecycle Tests

    [Fact]
    public void Choreograph_UfoLaunchThenRetarget_EmitsRetargetCommand()
    {
        // First batch: launch
        var launchEvents = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(2, 2),
                TargetPosition = new Position(6, 6),
                SourceTileId = 10,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(launchEvents);

        // Second batch: retarget
        var retargetEvents = new GameEvent[]
        {
            new ProjectileRetargetedEvent
            {
                ProjectileId = 1,
                OldTarget = new Position(6, 6),
                NewTarget = new Position(3, 0),
                Reason = RetargetReason.OriginalTargetDestroyed,
                SimulationTime = 0.5f
            }
        };
        var commands = _choreographer.Choreograph(retargetEvents, 0.5f);

        var retargetCmd = Assert.Single(commands.OfType<UfoRetargetCommand>());
        Assert.Equal(10, retargetCmd.TileId);
        // NewTarget includes overshoot (0.3 along flight dir) + screen-up offset (-0.40 Y)
        Assert.NotEqual(new Vector2(3, 0), retargetCmd.NewTarget); // Raw grid pos is adjusted
        Assert.Equal(0, retargetCmd.Duration); // Instant command
    }

    [Fact]
    public void Choreograph_UfoLaunchThenImpact_EmitsRemoveAndEffect()
    {
        // First batch: launch
        var launchEvents = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(2, 2),
                TargetPosition = new Position(5, 5),
                SourceTileId = 10,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(launchEvents);

        // Second batch: impact
        var impactEvents = new GameEvent[]
        {
            new ProjectileImpactEvent
            {
                ProjectileId = 1,
                ImpactPosition = new Position(5, 5),
                SimulationTime = 1f
            }
        };
        var commands = _choreographer.Choreograph(impactEvents, 1f);

        // UFO impact should emit RemoveTileCommand (not ImpactProjectileCommand)
        Assert.Contains(commands, c => c is RemoveTileCommand { TileId: 10 });
        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "ufo_impact" });
        Assert.DoesNotContain(commands, c => c is ImpactProjectileCommand);
    }

    [Fact]
    public void Choreograph_UfoWithoutSourceTileId_SkipsLaunchCommand()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(2, 2),
                TargetPosition = new Position(5, 5),
                SourceTileId = null, // No tile ID
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.DoesNotContain(commands, c => c is UfoLaunchCommand);
    }

    [Fact]
    public void Choreograph_UfoLaunchDuration_MatchesOverheadPlusFlightTime()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(0, 0),
                TargetPosition = new Position(4, 0), // distance = 4
                SourceTileId = 10,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var ufoCmd = Assert.Single(commands.OfType<UfoLaunchCommand>());
        float expectedDuration = _choreographer.Config.UfoLaunchOverhead +
                                  4f / _choreographer.Config.UfoFlightSpeed;
        Assert.Equal(expectedDuration, ufoCmd.Duration, 0.001f);
    }

    [Fact]
    public void Choreograph_UfoRetargetThenImpact_FullLifecycle()
    {
        // Batch 1: launch
        _choreographer.Choreograph(new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(0, 0),
                TargetPosition = new Position(7, 7),
                SourceTileId = 10,
                SimulationTime = 0f
            }
        });

        // Batch 2: retarget
        var retargetCommands = _choreographer.Choreograph(new GameEvent[]
        {
            new ProjectileRetargetedEvent
            {
                ProjectileId = 1,
                OldTarget = new Position(7, 7),
                NewTarget = new Position(3, 0),
                Reason = RetargetReason.OriginalTargetDestroyed,
                SimulationTime = 0.5f
            }
        }, 0.5f);

        Assert.Single(retargetCommands.OfType<UfoRetargetCommand>());

        // Batch 3: impact at new target
        var impactCommands = _choreographer.Choreograph(new GameEvent[]
        {
            new ProjectileImpactEvent
            {
                ProjectileId = 1,
                ImpactPosition = new Position(3, 0),
                SimulationTime = 1.5f
            }
        }, 1.5f);

        Assert.Contains(impactCommands, c => c is RemoveTileCommand { TileId: 10 });
        Assert.Contains(impactCommands, c => c is ShowEffectCommand { EffectType: "ufo_impact" });
    }

    [Fact]
    public void Choreograph_UfoImpact_RemoveNotBeforeFlightEnd()
    {
        // Launch with known duration
        _choreographer.Choreograph(new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 1,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(0, 0),
                TargetPosition = new Position(6, 0), // distance = 6
                SourceTileId = 10,
                SimulationTime = 0f
            }
        }, baseTime: 0f);

        float expectedDuration = _choreographer.Config.UfoLaunchOverhead +
                                  6f / _choreographer.Config.UfoFlightSpeed;
        float flightEndTime = expectedDuration; // launch at 0

        // Impact arrives in a later batch whose baseTime is BEFORE flight end
        // (simulates cross-batch timing desync)
        float earlyBaseTime = flightEndTime - 0.2f;
        var commands = _choreographer.Choreograph(new GameEvent[]
        {
            new ProjectileImpactEvent
            {
                ProjectileId = 1,
                ImpactPosition = new Position(6, 0),
                SimulationTime = 1f
            }
        }, earlyBaseTime);

        var removeCmd = Assert.Single(commands.OfType<RemoveTileCommand>());
        // RemoveTileCommand must not fire before UfoLaunchCommand ends
        Assert.True(removeCmd.StartTime >= flightEndTime,
            $"RemoveTile at {removeCmd.StartTime} fires before flight end {flightEndTime}");
    }

    #endregion
}
