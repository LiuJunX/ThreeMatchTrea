using System.Linq;
using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

/// <summary>
/// Tests for merge-to-bomb animation choreography.
/// Verifies that gravity hold, spawn delay, and bomb timing work correctly
/// when a match produces a bomb.
/// </summary>
public class MergeToBombChoreographerTests
{
    private readonly Choreographer _choreographer = new();

    /// <summary>
    /// Simulates a horizontal 4-match at row 2, columns 1-4, BombOrigin at (2,2).
    /// Returns event sequence: TileDestroyed(merge) × 3 + BombCreated + TileMoved(gravity) + TileSpawned(refill).
    /// </summary>
    private GameEvent[] CreateMergeWithGravityEvents()
    {
        // Horizontal 4-match: (1,2), (2,2), (3,2), (4,2)
        // BombOrigin = (2,2), SpawnBombType = Horizontal
        // Tile at (2,2) is skipped in TileDestroyedEvents (it's the bomb origin)
        // After match: gravity tile at (3,1) falls to (3,2), spawn at (3,-1)
        return new GameEvent[]
        {
            // 1. Match detected
            new MatchDetectedEvent
            {
                Type = TileType.Red,
                Positions = new[] { new Position(1, 2), new Position(2, 2), new Position(3, 2), new Position(4, 2) },
                Shape = MatchShape.Line4Horizontal,
                TileCount = 4,
                SimulationTime = 0f
            },
            // 2. Tiles destroyed with MergeTarget (non-bomb-origin tiles)
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(1, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 30, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 40, GridPosition = new Position(4, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            // 3. Bomb created at (2,2) — old tile ID 20, new tile ID 200
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            },
            // 4. Gravity: tile at (3,1) falls to (3,2) — same column as merge tile 30
            new TileMovedEvent
            {
                TileId = 31, // different tile above the destroyed one
                FromPosition = new Vector2(3, 1),
                ToPosition = new Vector2(3, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            },
            // 5. Gravity: tile at (1,1) falls to (1,2) — same column as merge tile 10
            new TileMovedEvent
            {
                TileId = 11,
                FromPosition = new Vector2(1, 1),
                ToPosition = new Vector2(1, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            },
            // 6. Spawn: refill at top of column 3
            new TileSpawnedEvent
            {
                TileId = 50,
                GridPosition = new Position(3, 0),
                Type = TileType.Blue,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0f
            }
        };
    }

    #region Merge Movement Commands

    [Fact]
    public void Merge_TileDestroyedWithMergeTarget_GeneratesMoveToBombOrigin()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Should generate MoveTileCommand toward BombOrigin
        var moveCmd = commands.OfType<MoveTileCommand>().First(c => c.From != c.To);
        Assert.Equal(10, moveCmd.TileId);
        Assert.Equal(new Vector2(3, 2), moveCmd.From);
        Assert.Equal(new Vector2(1, 2), moveCmd.To);
        // Duration scales with distance: dist=2, maxDist=4, scale=0.5
        Assert.True(moveCmd.Duration > 0f && moveCmd.Duration <= _choreographer.MergeDuration,
            $"Merge duration should scale with distance: {moveCmd.Duration}");
    }

    [Fact]
    public void Merge_TileDestroyedWithMergeTarget_GeneratesRemoveAfterMerge()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var removeCmd = commands.OfType<RemoveTileCommand>().First();
        Assert.Equal(10, removeCmd.TileId);
        // Remove should happen after merge completes
        Assert.Equal(_choreographer.MergeDuration, removeCmd.StartTime, 0.001f);
    }

    [Fact]
    public void Merge_TileDestroyedWithMergeTarget_SetsColumnDestroyEndTime()
    {
        // Merge tile at column 3 should set _columnDestroyEndTimes[3] = MergeDuration
        // This is verified indirectly: a gravity move in the same column should be delayed
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 11, FromPosition = new Vector2(3, 1),
                ToPosition = new Vector2(3, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // The actual move (From != To) should be delayed to after merge
        var gravityMove = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 11 && c.From != c.To);
        Assert.True(gravityMove.StartTime >= _choreographer.MergeDuration,
            $"Gravity move start {gravityMove.StartTime} should be >= MergeDuration {_choreographer.MergeDuration}");
    }

    #endregion

    #region Gravity Hold Commands

    [Fact]
    public void Merge_GravityTileInMergeColumn_GetsHoldCommand()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 11, FromPosition = new Vector2(3, 1),
                ToPosition = new Vector2(3, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Should have a hold command (From == To) for tile 11
        var holdCmd = commands.OfType<MoveTileCommand>()
            .FirstOrDefault(c => c.TileId == 11 && c.From == c.To);
        Assert.NotNull(holdCmd);
        Assert.Equal(new Vector2(3, 1), holdCmd.From);
        Assert.Equal(0f, holdCmd.StartTime, 0.001f);
        // Hold duration should cover until the actual move starts
        Assert.True(holdCmd.Duration >= _choreographer.MergeDuration - 0.001f,
            $"Hold duration {holdCmd.Duration} should cover MergeDuration {_choreographer.MergeDuration}");
    }

    [Fact]
    public void Merge_GravityTileInUnaffectedColumn_NoHoldCommand()
    {
        // Merge in column 3, gravity in column 5 — no hold needed
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 50, FromPosition = new Vector2(5, 1),
                ToPosition = new Vector2(5, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // No hold command for tile 50 (column 5 not affected by merge)
        var holdCmd = commands.OfType<MoveTileCommand>()
            .FirstOrDefault(c => c.TileId == 50 && c.From == c.To);
        Assert.Null(holdCmd);
    }

    #endregion

    #region Bomb Created Timing

    [Fact]
    public void BombCreated_HoldsOldTileDuringMerge()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Hold command: old tile stays in place during merge
        var holdCmd = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 20 && c.From == c.To);
        Assert.Equal(new Vector2(2, 2), holdCmd.From);
        Assert.Equal(_choreographer.MergeDuration, holdCmd.Duration, 0.001f);
    }

    [Fact]
    public void BombCreated_RemovesOldTileAtMergeEnd()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var removeCmd = commands.OfType<RemoveTileCommand>().First(c => c.TileId == 20);
        Assert.Equal(_choreographer.MergeDuration, removeCmd.StartTime, 0.001f);
    }

    [Fact]
    public void BombCreated_SpawnsNewBombTileAtMergeEnd()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 200);
        Assert.Equal(_choreographer.MergeDuration, spawnCmd.StartTime, 0.001f);
        Assert.Equal(BombType.Horizontal, spawnCmd.Bomb);
        Assert.Equal(TileType.Red, spawnCmd.Type);
    }

    [Fact]
    public void BombCreated_SetsColumnDestroyEndTime()
    {
        // BombCreated at column 2 should delay gravity in column 2
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 21, FromPosition = new Vector2(2, 1),
                ToPosition = new Vector2(2, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Gravity in bomb column should be delayed
        var gravityMove = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 21 && c.From != c.To);
        Assert.True(gravityMove.StartTime >= _choreographer.MergeDuration,
            $"Gravity in bomb column should wait for merge: start={gravityMove.StartTime}");

        // Should have hold command
        var holdCmd = commands.OfType<MoveTileCommand>()
            .FirstOrDefault(c => c.TileId == 21 && c.From == c.To);
        Assert.NotNull(holdCmd);
    }

    #endregion

    #region Spawn Delay

    [Fact]
    public void Merge_SpawnInMergeColumn_DelayedToAfterMerge()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 50, GridPosition = new Position(3, 0),
                Type = TileType.Blue, Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 50);
        Assert.True(spawnCmd.StartTime >= _choreographer.MergeDuration,
            $"Spawn in merge column should be delayed: start={spawnCmd.StartTime}, merge={_choreographer.MergeDuration}");
    }

    [Fact]
    public void Merge_SpawnInUnaffectedColumn_NotDelayed()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 50, GridPosition = new Position(5, 0),
                Type = TileType.Blue, Bomb = BombType.None,
                SpawnPosition = new Vector2(5, -1),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 50);
        Assert.Equal(0f, spawnCmd.StartTime, 0.001f);
    }

    #endregion

    #region Full Merge Sequence Timing

    [Fact]
    public void FullMergeSequence_AllTimingsCorrect()
    {
        var events = CreateMergeWithGravityEvents();
        var commands = _choreographer.Choreograph(events);
        float mergeDuration = _choreographer.MergeDuration;

        // --- Merge movement commands ---
        // Tiles 10, 30, 40 should move to BombOrigin (2,2)
        // Duration scales with distance: closer tiles get shorter duration
        foreach (int tileId in new[] { 10, 30, 40 })
        {
            var mergeMove = commands.OfType<MoveTileCommand>()
                .First(c => c.TileId == tileId && c.From != c.To);
            Assert.Equal(new Vector2(2, 2), mergeMove.To);
            Assert.True(mergeMove.Duration > 0f && mergeMove.Duration <= mergeDuration,
                $"Tile {tileId} merge duration should scale with distance: {mergeMove.Duration}");
            Assert.Equal(0f, mergeMove.StartTime, 0.001f);
        }

        // --- Bomb origin hold ---
        var bombHold = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 20 && c.From == c.To);
        Assert.Equal(new Vector2(2, 2), bombHold.From);
        Assert.Equal(mergeDuration, bombHold.Duration, 0.001f);

        // --- Bomb spawn ---
        var bombSpawn = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 200);
        Assert.Equal(mergeDuration, bombSpawn.StartTime, 0.001f);
        Assert.Equal(BombType.Horizontal, bombSpawn.Bomb);

        // --- Gravity hold ---
        // Tile 31 (column 3) should be held during merge
        var gravityHold31 = commands.OfType<MoveTileCommand>()
            .FirstOrDefault(c => c.TileId == 31 && c.From == c.To);
        Assert.NotNull(gravityHold31);
        Assert.Equal(new Vector2(3, 1), gravityHold31.From);

        // Tile 11 (column 1) should be held during merge
        var gravityHold11 = commands.OfType<MoveTileCommand>()
            .FirstOrDefault(c => c.TileId == 11 && c.From == c.To);
        Assert.NotNull(gravityHold11);
        Assert.Equal(new Vector2(1, 1), gravityHold11.From);

        // --- Gravity move starts after merge ---
        var gravityMove31 = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 31 && c.From != c.To);
        Assert.True(gravityMove31.StartTime >= mergeDuration,
            $"Gravity tile 31 should start after merge: {gravityMove31.StartTime}");

        var gravityMove11 = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 11 && c.From != c.To);
        Assert.True(gravityMove11.StartTime >= mergeDuration,
            $"Gravity tile 11 should start after merge: {gravityMove11.StartTime}");

        // --- Spawn delayed ---
        var refillSpawn = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 50);
        Assert.True(refillSpawn.StartTime >= mergeDuration,
            $"Refill spawn should be delayed: {refillSpawn.StartTime}");
    }

    #endregion

    #region LastBatchHadMerge

    [Fact]
    public void LastBatchHadMerge_TrueWhenBombCreatedEventPresent()
    {
        var events = CreateMergeWithGravityEvents();
        _choreographer.Choreograph(events);

        Assert.True(_choreographer.LastBatchHadMerge,
            "LastBatchHadMerge should be true when batch contains BombCreatedEvent");
    }

    [Fact]
    public void LastBatchHadMerge_FalseForNormalMatch()
    {
        // Normal 3-match: no BombCreatedEvent
        var events = new GameEvent[]
        {
            new MatchDetectedEvent
            {
                Type = TileType.Red,
                Positions = new[] { new Position(0, 2), new Position(1, 2), new Position(2, 2) },
                Shape = MatchShape.Simple3,
                TileCount = 3,
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 1, GridPosition = new Position(0, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 2, GridPosition = new Position(1, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 3, GridPosition = new Position(2, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };

        _choreographer.Choreograph(events);

        Assert.False(_choreographer.LastBatchHadMerge,
            "LastBatchHadMerge should be false when no BombCreatedEvent in batch");
    }

    [Fact]
    public void LastBatchHadMerge_ResetsOnNextChoreograph()
    {
        // First batch: with bomb
        var mergeEvents = CreateMergeWithGravityEvents();
        _choreographer.Choreograph(mergeEvents);
        Assert.True(_choreographer.LastBatchHadMerge);

        // Second batch: normal match, should reset
        var normalEvents = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 99, GridPosition = new Position(0, 0),
                Type = TileType.Blue, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(normalEvents);

        Assert.False(_choreographer.LastBatchHadMerge,
            "LastBatchHadMerge should reset to false on next Choreograph call without BombCreatedEvent");
    }

    #endregion

    #region LastBatchHadMatch

    [Fact]
    public void LastBatchHadMatch_TrueForNormalMatch()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1, GridPosition = new Position(0, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };

        _choreographer.Choreograph(events);

        Assert.True(_choreographer.LastBatchHadMatch);
    }

    [Fact]
    public void LastBatchHadMatch_FalseForMergeOnly()
    {
        var events = CreateMergeWithGravityEvents();
        _choreographer.Choreograph(events);

        // Merge batch has no non-merge destroys
        Assert.False(_choreographer.LastBatchHadMatch);
    }

    [Fact]
    public void LastBatchHadMatch_ResetsOnNextChoreograph()
    {
        var matchEvents = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1, GridPosition = new Position(0, 0),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(matchEvents);
        Assert.True(_choreographer.LastBatchHadMatch);

        _choreographer.Choreograph(CreateMergeWithGravityEvents());
        Assert.False(_choreographer.LastBatchHadMatch);
    }

    #endregion

    #region CellLockEntries

    [Fact]
    public void LockEntries_MatchDestroy_EmitsDropDelayLock()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1, GridPosition = new Position(3, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };

        _choreographer.Choreograph(events);

        Assert.Single(_choreographer.LockEntries);
        var entry = _choreographer.LockEntries[0];
        Assert.Equal(new Position(3, 2), entry.Position);
        Assert.Equal(CellLockType.Receive, entry.LockType);
        Assert.Equal(_choreographer.DropDelay, entry.Duration, 0.001f);
        Assert.False(entry.IsMerge);
    }

    [Fact]
    public void LockEntries_MergeDestroy_EmitsMergeLock()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(1, 2),
                Type = TileType.Red, Reason = DestroyReason.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            }
        };

        _choreographer.Choreograph(events);

        Assert.Single(_choreographer.LockEntries);
        var entry = _choreographer.LockEntries[0];
        Assert.Equal(new Position(1, 2), entry.Position);
        Assert.True(entry.IsMerge);
        Assert.Equal(_choreographer.MergeDuration, entry.Duration, 0.001f);
    }

    [Fact]
    public void LockEntries_BombCreated_EmitsBombOriginLock()
    {
        var events = CreateMergeWithGravityEvents();
        _choreographer.Choreograph(events);

        // Should have entries for merge sources (tiles 10, 30, 40) + bomb origin (2,2)
        var bombOriginLock = _choreographer.LockEntries
            .FirstOrDefault(e => e.Position.Equals(new Position(2, 2)) && e.IsMerge);
        Assert.True(bombOriginLock.IsMerge);
        Assert.Equal(_choreographer.MergeDuration, bombOriginLock.Duration, 0.001f);
    }

    [Fact]
    public void LockEntries_ClearedOnNextChoreograph()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1, GridPosition = new Position(0, 0),
                Type = TileType.Red, Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };
        _choreographer.Choreograph(events);
        Assert.NotEmpty(_choreographer.LockEntries);

        _choreographer.Choreograph(System.Array.Empty<GameEvent>());
        Assert.Empty(_choreographer.LockEntries);
    }

    #endregion
}
