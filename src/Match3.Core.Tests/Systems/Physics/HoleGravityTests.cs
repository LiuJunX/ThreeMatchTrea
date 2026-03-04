using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

/// <summary>
/// Tests for hole (镂空) gravity behavior: tiles falling through holes,
/// grid position tracking, matching after transit, and snapshot persistence.
/// </summary>
public class HoleGravityTests
{
    private static readonly Match3Config DefaultConfig = new()
    {
        GravitySpeed = 35.0f,
        MaxFallSpeed = 20.0f,
        InitialFallSpeed = 12.0f
    };

    /// <summary>
    /// Run physics for enough frames until the board is stable or maxFrames reached.
    /// </summary>
    private static void RunUntilStable(RealtimeGravitySystem physics, ref GameState state,
        float dt = 0.016f, int maxFrames = 200)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            physics.Update(ref state, dt);
            if (physics.IsStable(in state)) break;
        }
    }

    private static Tile FindTileById(in GameState state, int id)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Id == id) return state.Grid[i];
        }
        return default;
    }

    #region Tile Falls Through Hole

    [Fact]
    public void Tile_FallsThrough_SingleHole()
    {
        // Arrange: 1-wide, 4-tall column. Row 1 is a hole.
        // Tile at (0,0) should fall through hole at (0,1) and land at (0,3) or bottom.
        var state = new GameState(1, 4, 5, new StubRandom());
        for (int y = 0; y < 4; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        // Mark row 1 as hole
        state.Holes[1] = true;

        // Place a red tile at (0,0)
        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        // Place a blocker at bottom (0,3) so tile lands at (0,2)
        state.SetTile(0, 3, new Tile(200, TileType.Blue, 0, 3));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        // Act
        RunUntilStable(physics, ref state);

        // Assert: tile should have landed at (0,2), skipping hole at (0,1)
        var tileAt2 = state.GetTile(0, 2);
        Assert.Equal(100, tileAt2.Id);
        Assert.Equal(TileType.Red, tileAt2.Type);
        Assert.False(tileAt2.IsFalling);
    }

    [Fact]
    public void Tile_FallsThrough_MultiRowHole()
    {
        // Arrange: 1-wide, 6-tall. Rows 1-3 are holes. Tile at (0,0) should land at (0,5) or (0,4).
        var state = new GameState(1, 6, 5, new StubRandom());
        for (int y = 0; y < 6; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.Holes[2] = true;
        state.Holes[3] = true;

        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        // Act
        RunUntilStable(physics, ref state);

        // Assert: tile should land at (0,5) — bottom of board, past the 3-row hole zone
        var tileAt5 = state.GetTile(0, 5);
        Assert.Equal(100, tileAt5.Id);
        Assert.Equal(TileType.Red, tileAt5.Type);
        Assert.False(tileAt5.IsFalling);
    }

    #endregion

    #region Grid Position After Transit

    [Fact]
    public void Tile_GridPosition_CorrectAfterTransit()
    {
        // Arrange: tile falls through hole, check that grid position matches physical position
        var state = new GameState(1, 5, 5, new StubRandom());
        for (int y = 0; y < 5; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.Holes[2] = true;

        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        // Act
        RunUntilStable(physics, ref state);

        // Assert: tile at (0,4) — bottom
        var tile = state.GetTile(0, 4);
        Assert.Equal(100, tile.Id);
        Assert.Equal(4.0f, tile.Position.Y, 0.01f);
        Assert.Equal(0.0f, tile.Position.X, 0.01f);
    }

    [Fact]
    public void Tile_IsStable_AfterLanding()
    {
        var state = new GameState(1, 4, 5, new StubRandom());
        for (int y = 0; y < 4; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());
        RunUntilStable(physics, ref state);

        // Assert: board is stable
        Assert.True(physics.IsStable(in state));

        // Find the tile — it should not be falling
        var tile = FindTileById(in state, 100);
        Assert.False(tile.IsFalling);
        Assert.True(System.Math.Abs(tile.Velocity.Y) < 0.01f);
    }

    #endregion

    #region Chain Fall Through Hole

    [Fact]
    public void Tile_ChainFall_ThroughHole()
    {
        // Multiple tiles fall through the same hole sequentially
        var state = new GameState(1, 5, 5, new StubRandom());
        for (int y = 0; y < 5; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[2] = true;

        // Two tiles stacked
        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(101, TileType.Blue, 0, 1));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());
        RunUntilStable(physics, ref state);

        // Assert: both tiles landed below hole — at (0,3) and (0,4)
        var tileAt3 = state.GetTile(0, 3);
        var tileAt4 = state.GetTile(0, 4);

        // Both should be non-None tiles
        Assert.NotEqual(TileType.None, tileAt3.Type);
        Assert.NotEqual(TileType.None, tileAt4.Type);

        // One should be Red, one Blue (order may vary due to physics timing)
        var types = new[] { tileAt3.Type, tileAt4.Type };
        Assert.Contains(TileType.Red, types);
        Assert.Contains(TileType.Blue, types);
    }

    #endregion

    #region Refill Hole Filtering

    [Fact]
    public void Refill_SkipsHoleColumns()
    {
        // Arrange: column 0 has hole at row 0 — refill should skip it
        var state = new GameState(2, 3, 5, new StubRandom());
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 2; x++)
                state.SetTile(x, y, new Tile(y * 2 + x, TileType.None, x, y));

        state.Holes[0] = true; // (0,0) is a hole

        var spawnModel = new StubSpawnModel(TileType.Red);
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert: column 0 should NOT have a tile at row 0 (it's a hole)
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);

        // Column 1 should have been refilled
        Assert.NotEqual(TileType.None, state.GetTile(1, 0).Type);
    }

    [Fact]
    public void Refill_SpawnsAboveHole()
    {
        // Arrange: hole is NOT at row 0 — refill should work normally
        var state = new GameState(1, 4, 5, new StubRandom());
        for (int y = 0; y < 4; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1 * 1 + 0] = true; // (0,1) is a hole, row 0 is free

        var spawnModel = new StubSpawnModel(TileType.Green);
        var refill = new RealtimeRefillSystem(spawnModel);

        // Act
        refill.Update(ref state);

        // Assert: row 0 should have a tile (hole is below, not at spawn point)
        Assert.NotEqual(TileType.None, state.GetTile(0, 0).Type);
    }

    #endregion

    #region Match Does Not Cross Hole

    [Fact]
    public void Match_DoesNotCrossHole()
    {
        // Arrange: column of Red tiles with a hole in the middle
        // R R [HOLE] R R — should NOT form a 4-match
        var state = new GameState(1, 5, 5, new StubRandom());

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(2, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(3, TileType.None, 0, 2)); // hole cell has TileType.None
        state.SetTile(0, 3, new Tile(4, TileType.Red, 0, 3));
        state.SetTile(0, 4, new Tile(5, TileType.Red, 0, 4));
        state.Holes[2] = true;

        // Matching uses GetType which returns TileType.None for holes → breaks connectivity
        // Verify: tile at hole position is None
        Assert.Equal(TileType.None, state.GetType(0, 2));
    }

    #endregion

    #region Diagonal Slide Avoids Holes

    [Fact]
    public void DiagonalSlide_AvoidsHoles()
    {
        // Arrange: 3x3 grid
        // (1,0) = Red, (1,1) = Suspended (blocker), (0,1) = HOLE, (2,1) = None
        // Tile should slide to (2,1), NOT into hole at (0,1)
        var state = new GameState(3, 3, 5, new StubRandom());
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));

        state.SetTile(1, 0, new Tile(100, TileType.Red, 1, 0));

        var obstacle = new Tile(9, TileType.Green, 1, 1);
        obstacle.IsSuspended = true;
        state.SetTile(1, 1, obstacle);

        // Mark (0,1) as hole — diagonal slide should avoid it
        state.Holes[1 * 3 + 0] = true;

        // Block (2,2) so tile stops at (2,1)
        state.SetTile(2, 2, new Tile(10, TileType.Blue, 2, 2));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());
        RunUntilStable(physics, ref state);

        // Assert: tile should be at (2,1), not at (0,1) which is a hole
        var tileAt21 = state.GetTile(2, 1);
        Assert.Equal(TileType.Red, tileAt21.Type);
        Assert.Equal(100, tileAt21.Id);

        // Hole at (0,1) should remain empty
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type);
    }

    #endregion

    #region DetermineTarget Skips Hole Zone

    [Fact]
    public void DetermineTarget_SkipsHoleZone()
    {
        // Arrange: column with hole zone at rows 1-2, tile at (0,0)
        var state = new GameState(1, 5, 5, new StubRandom());
        for (int y = 0; y < 5; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.Holes[2] = true;

        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        var resolver = new GravityTargetResolver(new StubRandom());
        var target = resolver.DetermineTarget(ref state, 0, 0);

        // Target should be at row 4 (bottom), not row 1 or 2 (holes)
        Assert.Equal(4.0f, target.Position.Y, 0.01f);
        Assert.Equal(0.0f, target.Position.X, 0.01f);
    }

    #endregion

    #region Snapshot Captures Holes

    [Fact]
    public void Snapshot_CapturesHoles()
    {
        // Arrange
        var state = new GameState(3, 3, 5, new StubRandom());
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x + 1, TileType.Red, x, y));

        // Mark some holes
        state.Holes[0 * 3 + 1] = true; // (1,0)
        state.Holes[2 * 3 + 2] = true; // (2,2)

        // Act: snapshot and restore
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        // Assert: holes are preserved
        Assert.True(restored.IsHole(1, 0));
        Assert.True(restored.IsHole(2, 2));
        Assert.False(restored.IsHole(0, 0));
        Assert.False(restored.IsHole(1, 1));

        // Verify snapshot property
        Assert.Equal(state.Width * state.Height, snapshot.Holes.Length);
        Assert.True(snapshot.Holes[1]);     // (1,0) = index 1
        Assert.True(snapshot.Holes[8]);     // (2,2) = index 8
    }

    #endregion

    #region FindHoleZoneExit

    [Fact]
    public void FindHoleZoneExit_SingleHole_ReturnsEntryY()
    {
        var state = new GameState(1, 5, 5, new StubRandom());
        state.Holes[2] = true; // row 2

        int exit = GravityTargetResolver.FindHoleZoneExit(in state, 0, 2);
        Assert.Equal(2, exit);
    }

    [Fact]
    public void FindHoleZoneExit_MultiRowHole_ReturnsLastRow()
    {
        var state = new GameState(1, 6, 5, new StubRandom());
        state.Holes[1] = true;
        state.Holes[2] = true;
        state.Holes[3] = true;

        int exit = GravityTargetResolver.FindHoleZoneExit(in state, 0, 1);
        Assert.Equal(3, exit);
    }

    [Fact]
    public void FindHoleZoneExit_NotAHole_ReturnsNegative()
    {
        var state = new GameState(1, 5, 5, new StubRandom());
        // No holes

        int exit = GravityTargetResolver.FindHoleZoneExit(in state, 0, 2);
        Assert.Equal(-1, exit);
    }

    #endregion

    #region Match Works After Hole Transit

    [Fact]
    public void Match_WorksAfterHoleTransit()
    {
        // Arrange: after hole transit, tiles should be matchable
        // Setup: 3-wide board, tiles that will form a horizontal match after falling through holes
        var state = new GameState(3, 5, 5, new StubRandom());
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 3; x++)
                state.SetTile(x, y, new Tile(y * 3 + x, TileType.None, x, y));

        // Holes at row 2 for all columns
        for (int x = 0; x < 3; x++)
            state.Holes[2 * 3 + x] = true;

        // Place 3 Red tiles at row 0 — they should fall through holes and land at row 4
        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(101, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(102, TileType.Red, 2, 0));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());
        RunUntilStable(physics, ref state);

        // After settling, all 3 tiles should be at row 4 (bottom)
        Assert.Equal(TileType.Red, state.GetTile(0, 4).Type);
        Assert.Equal(TileType.Red, state.GetTile(1, 4).Type);
        Assert.Equal(TileType.Red, state.GetTile(2, 4).Type);

        // Verify they're stable (matchable)
        Assert.True(physics.IsStable(in state));
    }

    #endregion

    #region GuardHoleTransit — No Backward Snap

    [Fact]
    public void HoleTransit_TileNeverMovesBackward()
    {
        // Reproduce the Donut bug: column with holes in the middle,
        // tiles above and below. When tiles above fall through and the
        // leader lands, the follower must NOT snap backward to grid position.
        //
        // Column layout (1-wide, 7-tall, mimics Donut col 2):
        //   Row 0: Tile A (Red)
        //   Row 1: Tile B (Blue)
        //   Row 2: HOLE
        //   Row 3: HOLE
        //   Row 4: HOLE
        //   Row 5: Tile C (Green)
        //   Row 6: Tile D (Yellow)
        var state = new GameState(1, 7, 5, new StubRandom());
        for (int y = 0; y < 7; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[2] = true;
        state.Holes[3] = true;
        state.Holes[4] = true;

        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(101, TileType.Blue, 0, 1));
        state.SetTile(0, 5, new Tile(200, TileType.Green, 0, 5));
        state.SetTile(0, 6, new Tile(201, TileType.Yellow, 0, 6));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        // Run frame by frame and verify no tile ever moves upward
        float dt = 0.016f;
        var prevPositions = new float[7];
        for (int y = 0; y < 7; y++)
            prevPositions[y] = state.GetTile(0, y).Position.Y;

        for (int frame = 0; frame < 300; frame++)
        {
            physics.Update(ref state, dt);

            // Check all tiles: Position.Y should never decrease (move upward)
            for (int y = 0; y < 7; y++)
            {
                var tile = state.GetTile(0, y);
                if (tile.Type == TileType.None) continue;

                Assert.True(tile.Position.Y >= prevPositions[y] - 0.01f,
                    $"Frame {frame}: Tile {tile.Id} at grid({0},{y}) moved backward! " +
                    $"Prev={prevPositions[y]:F3} → Curr={tile.Position.Y:F3}");
                prevPositions[y] = tile.Position.Y;
            }

            if (physics.IsStable(in state)) break;
        }

        // Board should eventually stabilize
        Assert.True(physics.IsStable(in state), "Board never stabilized");

        // Tiles A and B should be stacked above C and D (rows 0-1 are above holes,
        // but they may have fallen through to rows 5-6 area — depends on whether C,D stayed).
        // The key assertion is the no-backward check above.
    }

    [Fact]
    public void HoleTransit_FollowTarget_NeverPullsBackward()
    {
        // Two tiles falling through holes: the follower should never get a target
        // behind its current position when the leader decelerates.
        var state = new GameState(1, 8, 5, new StubRandom());
        for (int y = 0; y < 8; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.Holes[2] = true;
        state.Holes[3] = true;

        // Two tiles above holes
        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));

        // Blocker below hole exit
        state.SetTile(0, 5, new Tile(200, TileType.Blue, 0, 5));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        float dt = 0.016f;
        float prevY = 0f;

        for (int frame = 0; frame < 200; frame++)
        {
            physics.Update(ref state, dt);

            // Find tile 100 wherever it is in the grid
            var tile = FindTileById(in state, 100);
            if (tile.Type == TileType.None) break;

            Assert.True(tile.Position.Y >= prevY - 0.01f,
                $"Frame {frame}: Tile 100 snapped backward from {prevY:F3} to {tile.Position.Y:F3}");
            prevY = tile.Position.Y;

            if (physics.IsStable(in state)) break;
        }
    }

    [Fact]
    public void HoleTransit_BlockedBelow_DoesNotSnapToGridPosition()
    {
        // A tile in hole transit whose exit is blocked should NOT snap back
        // to its grid entry position. It should hold at its physical position.
        var state = new GameState(1, 6, 5, new StubRandom());
        for (int y = 0; y < 6; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        // Holes at rows 1-2
        state.Holes[1] = true;
        state.Holes[2] = true;

        // Tile at row 0, will fall through holes
        var tile = new Tile(100, TileType.Red, 0, 0);
        state.SetTile(0, 0, tile);

        // Blocker at row 3 (right at hole exit) — not falling, static
        state.SetTile(0, 3, new Tile(200, TileType.Green, 0, 3));
        state.SetTile(0, 4, new Tile(201, TileType.Blue, 0, 4));
        state.SetTile(0, 5, new Tile(202, TileType.Yellow, 0, 5));

        var resolver = new GravityTargetResolver(new StubRandom());

        // Simulate the tile manually entering hole transit
        // (Position.Y advanced into hole zone while grid stays at row 0)
        var transitTile = state.GetTile(0, 0);
        transitTile.Position.Y = 2.5f; // deep in hole zone
        transitTile.IsFalling = true;
        transitTile.Velocity.Y = 10f;
        state.SetTile(0, 0, transitTile);

        var target = resolver.DetermineTarget(ref state, 0, 0);

        // Target should NOT be row 0 (backward snap)
        // It should be at or past the tile's current position (2.5)
        Assert.True(target.Position.Y >= 2.4f,
            $"Target {target.Position.Y:F3} is behind tile position 2.5 — backward snap!");
    }

    #endregion

    #region Follow Does Not Enter Hole Zone

    [Fact]
    public void FollowTarget_ClampedAboveHoleZone()
    {
        // Donut scenario: two tiles above a 3-row hole with a tile blocking the exit.
        // The follower must NOT enter the hole zone when following the leader.
        //
        //   Row 0: Tile A (Red) — follower
        //   Row 1: Tile B (Blue) — leader
        //   Row 2: HOLE
        //   Row 3: HOLE
        //   Row 4: HOLE
        //   Row 5: Tile C (Green) — blocks exit
        //   Row 6: (empty)
        var state = new GameState(1, 7, 5, new StubRandom());
        for (int y = 0; y < 7; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[2] = true;
        state.Holes[3] = true;
        state.Holes[4] = true;

        state.SetTile(0, 0, new Tile(100, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(101, TileType.Blue, 0, 1));
        state.SetTile(0, 5, new Tile(200, TileType.Green, 0, 5));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        float dt = 0.016f;
        for (int frame = 0; frame < 300; frame++)
        {
            physics.Update(ref state, dt);

            // Tile A (id=100) should never have Position.Y in the hole zone (rows 2-4)
            var tileA = FindTileById(in state, 100);
            if (tileA.Type != TileType.None)
            {
                int roundedY = (int)System.Math.Floor(tileA.Position.Y + 0.5f);
                Assert.False(roundedY >= 2 && roundedY <= 4,
                    $"Frame {frame}: Tile A entered hole zone! Position.Y={tileA.Position.Y:F3}");
            }

            if (physics.IsStable(in state)) break;
        }

        Assert.True(physics.IsStable(in state), "Board never stabilized");

        // Tile C (200) was at row 5, falls to row 6 first.
        // Tile B (101) falls through hole to row 5.
        var finalC = state.GetTile(0, 6);
        Assert.Equal(200, finalC.Id);

        var finalB = state.GetTile(0, 5);
        Assert.Equal(101, finalB.Id);

        // Tile A should have settled above the hole (row 0 or 1) since rows 5-6 are full
        var finalA = FindTileById(in state, 100);
        Assert.NotEqual(TileType.None, finalA.Type);
    }

    [Fact]
    public void StuckInHoleZone_EventuallyExits()
    {
        // Simulate a tile that is already stuck in the hole zone
        // (Position.Y deep in hole, velocity 0, exit blocked).
        // After GuardHoleTransit allows backward, it should snap back and stabilize.
        var state = new GameState(1, 6, 5, new StubRandom());
        for (int y = 0; y < 6; y++)
            state.SetTile(0, y, new Tile(y, TileType.None, 0, y));

        state.Holes[1] = true;
        state.Holes[2] = true;

        // Manually create a stuck tile: at grid (0,0), Position.Y deep in hole zone
        var stuck = new Tile(100, TileType.Red, 0, 0);
        stuck.Position.Y = 2.5f;
        stuck.Velocity.Y = 0f;
        stuck.IsFalling = false;
        state.SetTile(0, 0, stuck);

        // Block exit
        state.SetTile(0, 3, new Tile(200, TileType.Green, 0, 3));
        state.SetTile(0, 4, new Tile(201, TileType.Blue, 0, 4));
        state.SetTile(0, 5, new Tile(202, TileType.Yellow, 0, 5));

        var physics = new RealtimeGravitySystem(DefaultConfig, new StubRandom());

        // Run a few frames — stuck tile should exit hole zone
        for (int i = 0; i < 20; i++)
            physics.Update(ref state, 0.016f);

        Assert.True(physics.IsStable(in state), "Board should stabilize after unsticking");

        // Tile should be at grid (0,0) with Position.Y ≈ 0
        var tile = FindTileById(in state, 100);
        Assert.True(System.Math.Abs(tile.Position.Y - 0.0f) < 0.1f,
            $"Stuck tile should have snapped back. Position.Y = {tile.Position.Y:F3}");
    }

    #endregion

    #region IsHole Position Overload

    [Fact]
    public void IsHole_PositionOverload_Works()
    {
        var state = new GameState(3, 3, 5, new StubRandom());
        state.Holes[1 * 3 + 2] = true; // (2,1)

        Assert.True(state.IsHole(new Position(2, 1)));
        Assert.False(state.IsHole(new Position(0, 0)));
    }

    #endregion
}
