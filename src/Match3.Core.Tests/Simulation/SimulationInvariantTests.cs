using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestHelpers;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Property-based invariant tests: run random games across diverse level configs
/// and assert system-level invariants at every step.
/// On failure the seed is visible in the test name for reproduction.
/// </summary>
[Trait("Category", "Slow")]
public class SimulationInvariantTests
{
    private const int MaxMoves = 25;

    public static IEnumerable<object[]> SeedAndConfigData()
    {
        int[] seeds = { 42, 1337, 9999, 2024, 7777 };
        foreach (var seed in seeds)
            foreach (var preset in SimulationTestHelper.AllPresetNames)
                yield return new object[] { seed, preset };
    }

    [Theory]
    [MemberData(nameof(SeedAndConfigData))]
    public void RandomGame_MaintainsInvariants(int seed, string presetName)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        using var session = SimulationTestHelper.CreateSession(seed, levelConfig, tileTypesCount);
        var engine = session.Engine;

        var tracker = new InvariantTracker(engine.State);
        string Ctx(string inv) => $"[seed={seed}, preset={presetName}] {inv}";

        // Initial settling (SettleCompletely handles the engine's multi-phase gravity:
        // match clearing happens after physics in the tick pipeline, so RunUntilStable alone
        // may exit before gravity fills newly-cleared gaps)
        SimulationTestHelper.SettleCompletely(engine);
        CheckPerTickInvariants(engine.State, tracker, Ctx, "init");
        CheckStableInvariants(engine.State, engine, tracker, Ctx, "init");

        // Play moves and verify invariants
        for (int moveNum = 0; moveNum < MaxMoves; moveNum++)
        {
            if (engine.State.LevelStatus != LevelStatus.InProgress) break;

            // Apply a move
            if (!SimulationTestHelper.TryApplyRandomMove(engine, moveNum))
                break;

            // Run until settled, checking per-tick invariants along the way
            int safety = 0;
            while (!engine.IsStable() && safety < 2000)
            {
                engine.Tick();
                CheckPerTickInvariants(engine.State, tracker, Ctx, $"move {moveNum}");
                safety++;
            }

            // Full settle (multi-phase: gravity → match → gravity → ...)
            SimulationTestHelper.SettleCompletely(engine);
            CheckPerTickInvariants(engine.State, tracker, Ctx, $"move {moveNum} settled");
            CheckStableInvariants(engine.State, engine, tracker, Ctx, $"move {moveNum} settled");
        }
    }

    // ── Per-tick invariants (cheap, always valid) ────────────────────

    private static void CheckPerTickInvariants(
        GameState state, InvariantTracker tracker, Func<string, string> ctx, string phase)
    {
        // 1. Board size never changes
        Assert.True(state.Width == tracker.InitialWidth && state.Height == tracker.InitialHeight,
            ctx($"Board size changed ({phase}): {state.Width}x{state.Height}"));

        // 2. No duplicate tile IDs
        AssertNoDuplicateIds(state, ctx($"Duplicate IDs ({phase})"));

        // 3. NextTileId monotonically increases
        Assert.True(state.NextTileId >= tracker.PrevNextTileId,
            ctx($"NextTileId decreased ({phase}): {state.NextTileId} < {tracker.PrevNextTileId}"));
        tracker.PrevNextTileId = state.NextTileId;

        // 4. Score never decreases
        Assert.True(state.Score >= tracker.PrevScore,
            ctx($"Score decreased ({phase}): {state.Score} < {tracker.PrevScore}"));
        tracker.PrevScore = state.Score;

        // 5. Objective progress never decreases
        for (int i = 0; i < 4; i++)
        {
            int cur = state.ObjectiveProgress[i].CurrentCount;
            Assert.True(cur >= tracker.PrevObjectiveCounts[i],
                ctx($"Objective[{i}] decreased ({phase}): {cur} < {tracker.PrevObjectiveCounts[i]}"));
            tracker.PrevObjectiveCounts[i] = cur;
        }

        // 6. MoveCount never decreases
        Assert.True(state.MoveCount >= tracker.PrevMoveCount,
            ctx($"MoveCount decreased ({phase}): {state.MoveCount} < {tracker.PrevMoveCount}"));
        tracker.PrevMoveCount = state.MoveCount;

        // 7. No tile on Void cells
        AssertNoTileOnVoid(state, ctx($"Tile on Void ({phase})"));

        // 8. Cell structure never changes
        int size = state.Width * state.Height;
        for (int i = 0; i < size; i++)
        {
            Assert.True(state.Cells[i] == tracker.InitialCells[i],
                ctx($"CellKind changed ({phase}), index {i}: " +
                    $"{tracker.InitialCells[i]} → {state.Cells[i]}"));
        }

        // 9. LevelStatus only advances
        AssertLevelStatusIrreversible(state.LevelStatus, ref tracker.PrevLevelStatus,
            ctx($"LevelStatus reverted ({phase})"));
    }

    // ── Stable-state invariants (checked after full settling) ────────

    private static void CheckStableInvariants(
        GameState state, Match3.Core.Simulation.SimulationEngine engine,
        InvariantTracker tracker, Func<string, string> ctx, string phase)
    {
        // 10. No floating tiles (scan bottom→top: gap below tile means floating)
        AssertNoFloatingTiles(state, ctx($"Floating tile ({phase})"));

        // 11. No unprocessed 3-match
        var stableState = engine.State;
        Assert.False(engine.MatchFinder.HasMatches(in stableState),
            ctx($"Unprocessed match ({phase})"));

        // 12. No Falling flag on any tile
        AssertNoFallingFlags(state, ctx($"Falling flag ({phase})"));

        // 13. No residual cell locks
        AssertNoCellLocks(state, ctx($"Residual lock ({phase})"));

        // 14. Cover health monotonically decreases
        int coverHealth = InvariantTracker.ComputeTotalCoverHealth(state);
        Assert.True(coverHealth <= tracker.PrevTotalCoverHealth,
            ctx($"Cover health increased ({phase}): {coverHealth} > {tracker.PrevTotalCoverHealth}"));
        tracker.PrevTotalCoverHealth = coverHealth;

        // 15. Ground health monotonically decreases
        int groundHealth = InvariantTracker.ComputeTotalGroundHealth(state);
        Assert.True(groundHealth <= tracker.PrevTotalGroundHealth,
            ctx($"Ground health increased ({phase}): {groundHealth} > {tracker.PrevTotalGroundHealth}"));
        tracker.PrevTotalGroundHealth = groundHealth;
    }

    // ── Assertion helpers ────────────────────────────────────────────

    private static void AssertNoDuplicateIds(GameState state, string context)
    {
        var seen = new HashSet<int>();
        int size = state.Width * state.Height;
        for (int i = 0; i < size; i++)
        {
            if (state.Grid[i].Type == ElementType.None) continue;
            if (!seen.Add(state.Grid[i].Id))
                Assert.Fail($"{context}: duplicate ID {state.Grid[i].Id} at index {i}");
        }
    }

    private static void AssertNoTileOnVoid(GameState state, string context)
    {
        int size = state.Width * state.Height;
        for (int i = 0; i < size; i++)
        {
            if (state.Cells[i] == CellKind.Void && state.Grid[i].Type != ElementType.None)
                Assert.Fail($"{context}: non-None tile ({state.Grid[i].Type}) on Void cell at index {i}");
        }
    }

    private static void AssertLevelStatusIrreversible(
        LevelStatus current, ref LevelStatus prev, string context)
    {
        if (prev != LevelStatus.InProgress && current != prev)
            Assert.Fail($"{context}: was {prev}, now {current}");
        prev = current;
    }

    private static void AssertNoFloatingTiles(GameState state, string context)
    {
        // y increases downward (y=0 = top/spawner, y=Height-1 = bottom).
        // Scan bottom→top: once we see an empty playable cell,
        // any MOVABLE tile further up (lower y) is floating.
        // Immovable tiles (blocked by cover/lock) act as floors.
        for (int x = 0; x < state.Width; x++)
        {
            bool gapBelow = false;
            for (int y = state.Height - 1; y >= 0; y--)
            {
                int idx = y * state.Width + x;
                if (state.Cells[idx] == CellKind.Void) continue;

                if (state.Grid[idx].Type == ElementType.None && !state.HasObstacle(x, y))
                {
                    gapBelow = true;
                }
                else if (state.HasObstacle(x, y))
                {
                    // Obstacle occupies the cell — tiles above rest on it, not floating
                    gapBelow = false;
                }
                else if (!state.CanMove(x, y))
                {
                    // Immovable tile (cover/lock) acts as floor — resets gap tracking
                    gapBelow = false;
                }
                else if (gapBelow)
                {
                    Assert.Fail($"{context}: tile at ({x},{y}) floats above empty cell in column {x}");
                }
                else
                {
                    gapBelow = false;
                }
            }
        }
    }

    private static void AssertNoFallingFlags(GameState state, string context)
    {
        int size = state.Width * state.Height;
        for (int i = 0; i < size; i++)
        {
            if (state.Grid[i].IsFalling)
            {
                int x = i % state.Width, y = i / state.Width;
                var tile = state.Grid[i];
                Assert.Fail($"{context}: tile at index {i} ({x},{y}) still has Falling flag. " +
                    $"Type={tile.Type}, Pos=({tile.Position.X:F2},{tile.Position.Y:F2}), " +
                    $"Vel=({tile.Velocity.X:F2},{tile.Velocity.Y:F2})");
            }
        }
    }

    private static void AssertNoCellLocks(GameState state, string context)
    {
        int size = state.Width * state.Height;
        for (int i = 0; i < size; i++)
        {
            if (state.CellLocks[i] != 0)
                Assert.Fail($"{context}: cell lock 0x{state.CellLocks[i]:X} at index {i}");
        }
    }
}
