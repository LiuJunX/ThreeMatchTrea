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
/// Verifies that merge movement, bomb creation hold, and bomb pop-in timing
/// work correctly when a match produces a bomb.
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
                Type = ElementType.Item1,
                Positions = new[] { new Position(1, 2), new Position(2, 2), new Position(3, 2), new Position(4, 2) },
                Shape = MatchShape.Line4Horizontal,
                TileCount = 4,
                SimulationTime = 0f
            },
            // 2. Tiles destroyed with MergeTarget (non-bomb-origin tiles)
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(1, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 30, GridPosition = new Position(3, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 40, GridPosition = new Position(4, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
                MergeTarget = new Position(2, 2),
                SimulationTime = 0f
            },
            // 3. Bomb created at (2,2) — old tile ID 20, new tile ID 200
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = ElementType.HorizontalRocket,
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
                Type = ElementType.Item3,
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
                Type = ElementType.Item1, Reason = ElimSource.Match,
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
        Assert.True(moveCmd.Duration > 0f && moveCmd.Duration <= _choreographer.Config.MergeDuration,
            $"Merge duration should be positive and <= MergeDuration: {moveCmd.Duration}");
    }

    [Fact]
    public void Merge_TileDestroyedWithMergeTarget_GeneratesRemoveAfterMerge()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var removeCmd = commands.OfType<RemoveTileCommand>().First();
        Assert.Equal(10, removeCmd.TileId);
        // Remove should happen after merge completes
        Assert.Equal(_choreographer.Config.MergeDuration, removeCmd.StartTime, 0.001f);
    }


    #endregion

    #region Gravity Hold Commands


    [Fact]
    public void Merge_GravityTileInUnaffectedColumn_NoHoldCommand()
    {
        // Merge in column 3, gravity in column 5 — no hold needed
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
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
                BombType = ElementType.HorizontalRocket,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Hold command: old tile stays in place during merge
        var holdCmd = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 20 && c.From == c.To);
        Assert.Equal(new Vector2(2, 2), holdCmd.From);
        Assert.Equal(_choreographer.Config.MergeDuration, holdCmd.Duration, 0.001f);
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
                BombType = ElementType.HorizontalRocket,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var removeCmd = commands.OfType<RemoveTileCommand>().First(c => c.TileId == 20);
        Assert.Equal(_choreographer.Config.MergeDuration, removeCmd.StartTime, 0.001f);
    }

    [Fact]
    public void BombCreated_SpawnsBombTileImmediatelyAtScaleZero()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 20, NewTileId = 200,
                Position = new Position(2, 2),
                BombType = ElementType.HorizontalRocket,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Spawn happens immediately (not at mergeEnd) to prevent SyncFallingTiles
        // from creating the tile at full scale during the merge animation
        var spawnCmd = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 200);
        Assert.Equal(0f, spawnCmd.StartTime, 0.001f);
        Assert.Equal(ElementType.HorizontalRocket, spawnCmd.Type);

        // Scale should be zero during merge, then pop-in at mergeEnd
        var scaleCommands = commands.OfType<ScaleTileCommand>()
            .Where(c => c.TileId == 200).OrderBy(c => c.StartTime).ToList();
        Assert.True(scaleCommands.Count >= 2);
        // First: hold at zero during merge
        Assert.Equal(Vector2.Zero, scaleCommands[0].FromScale);
        Assert.Equal(Vector2.Zero, scaleCommands[0].ToScale);
        // Second: pop-in from zero to one
        Assert.Equal(Vector2.Zero, scaleCommands[1].FromScale);
        Assert.Equal(Vector2.One, scaleCommands[1].ToScale);
        Assert.Equal(_choreographer.Config.MergeDuration, scaleCommands[1].StartTime, 0.001f);
    }


    #endregion

    #region Spawn Delay


    [Fact]
    public void Merge_SpawnInUnaffectedColumn_NotDelayed()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(3, 2),
                Type = ElementType.Item1, Reason = ElimSource.Match,
                MergeTarget = new Position(1, 2),
                SimulationTime = 0f
            },
            new TileSpawnedEvent
            {
                TileId = 50, GridPosition = new Position(5, 0),
                Type = ElementType.Item3,                SpawnPosition = new Vector2(5, -1),
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
        float mergeDuration = _choreographer.Config.MergeDuration;

        // --- Merge movement commands ---
        // Tiles 10, 30, 40 should move to BombOrigin (2,2)
        foreach (int tileId in new[] { 10, 30, 40 })
        {
            var mergeMove = commands.OfType<MoveTileCommand>()
                .First(c => c.TileId == tileId && c.From != c.To);
            Assert.Equal(new Vector2(2, 2), mergeMove.To);
            Assert.True(mergeMove.Duration > 0f && mergeMove.Duration <= mergeDuration,
                $"Tile {tileId} merge duration should be positive and <= MergeDuration: {mergeMove.Duration}");
            Assert.Equal(0f, mergeMove.StartTime, 0.001f);
        }

        // --- Bomb origin hold ---
        var bombHold = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 20 && c.From == c.To);
        Assert.Equal(new Vector2(2, 2), bombHold.From);
        Assert.Equal(mergeDuration, bombHold.Duration, 0.001f);

        // --- Bomb spawn (immediate at scale 0, before merge ends) ---
        var bombSpawn = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 200);
        Assert.Equal(0f, bombSpawn.StartTime, 0.001f);
        Assert.Equal(ElementType.HorizontalRocket, bombSpawn.Type);

        // --- Gravity uses simulation time directly (no cascade delay) ---
        // Tiles 31 and 11 move at simulation time 0 → startTime = 0
        var gravityMove31 = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 31 && c.From != c.To);
        Assert.Equal(0f, gravityMove31.StartTime, 0.001f);

        var gravityMove11 = commands.OfType<MoveTileCommand>()
            .First(c => c.TileId == 11 && c.From != c.To);
        Assert.Equal(0f, gravityMove11.StartTime, 0.001f);

        // --- Spawn uses simulation time directly (no cascade delay) ---
        var refillSpawn = commands.OfType<SpawnTileCommand>().First(c => c.TileId == 50);
        Assert.Equal(0f, refillSpawn.StartTime, 0.001f);
    }

    #endregion
}

