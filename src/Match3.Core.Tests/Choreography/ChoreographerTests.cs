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
                Type = TileType.Red,
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
        Assert.Equal(_choreographer.MoveDuration, moveCmd.Duration);
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
                Type = TileType.Red,
                Bomb = BombType.None,
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
                Type = TileType.Red,
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
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
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
        Assert.Equal(BombType.Horizontal, spawnCmd.Bomb);
        Assert.Equal(TileType.Red, spawnCmd.Type);

        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_created" });
    }

    [Fact]
    public void Choreograph_ProjectileLaunched_GeneratesSpawnCommand()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 100,
                Type = ProjectileType.Ufo,
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
    public void Choreograph_MoveAfterDestroy_WaitsForDestroyToComplete()
    {
        // Destroy at (3,4), then move to (3,4) should wait for destroy to finish
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var destroyCmd = commands.OfType<DestroyTileCommand>().First();
        // Filter out hold commands (From == To) to get the actual move
        var moveCmd = commands.OfType<MoveTileCommand>().First(c => c.From != c.To);

        // Move should start after destroy ends
        Assert.True(moveCmd.StartTime >= destroyCmd.StartTime + destroyCmd.Duration,
            $"Move start {moveCmd.StartTime} should be >= destroy end {destroyCmd.StartTime + destroyCmd.Duration}");
    }

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
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 0),
                Type = TileType.Blue,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First();

        // Spawn is delayed until after destroy animation in the same column
        Assert.Equal(_choreographer.DestroyDuration, spawnCmd.StartTime, 0.001f);
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
                Type = TileType.Red,
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
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 4),
                Type = TileType.Blue,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0.1f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First();

        // Spawn is delayed until after destroy animation in the same column.
        // Destroy at simTime=0 ends at DestroyDuration, spawn simTime=0.1 is
        // earlier than that, so spawn is clamped to DestroyDuration.
        Assert.Equal(_choreographer.DestroyDuration, spawnCmd.StartTime, 0.001f);
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
    public void Choreograph_BombActivated_GeneratesEffect()
    {
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = BombType.Horizontal,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_explosion" });
    }

    [Fact]
    public void Choreograph_BombCombo_GeneratesTwoEffects()
    {
        var events = new GameEvent[]
        {
            new BombComboEvent
            {
                BombTypeA = BombType.Horizontal,
                BombTypeB = BombType.Vertical,
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
                Type = TileType.Red,
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
}
