using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestHelpers;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Diagnostic test for CellLock leak: seed=1337, Standard8x8.
/// Reproduces the exact game replay from SimulationInvariantTests and traces
/// when CellLocks become non-zero and fail to clear.
/// </summary>
public class CellLockDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public CellLockDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DiagnoseCellLockLeak_Seed1337_Standard8x8()
    {
        const int TargetMove = 23;
        const int Seed = 1337;
        const string PresetName = "Standard8x8";

        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(PresetName);
        using var session = SimulationTestHelper.CreateSession(Seed, levelConfig, tileTypesCount);
        var engine = session.Engine;
        int gridSize = engine.State.Width * engine.State.Height;

        _output.WriteLine($"=== CellLock Leak Diagnostic: seed={Seed}, preset={PresetName} ===");
        _output.WriteLine($"Grid: {engine.State.Width}x{engine.State.Height} ({gridSize} cells)");
        _output.WriteLine("");

        // Initial settle
        SimulationTestHelper.SettleCompletely(engine);
        _output.WriteLine($"[init settled] IsStable={engine.IsStable()}, Tick={engine.CurrentTick}");

        uint[] prevLocks = new uint[gridSize];
        Array.Copy(engine.State.CellLocks, prevLocks, gridSize);

        for (int moveNum = 0; moveNum < TargetMove + 1; moveNum++)
        {
            var stateNow = engine.State;
            if (stateNow.LevelStatus != LevelStatus.InProgress)
            {
                _output.WriteLine($"[move {moveNum}] Level ended: {stateNow.LevelStatus}, stopping.");
                break;
            }

            // Check if there are tappable bombs BEFORE applying the move
            bool hasTappable = false;
            int tappableX = -1, tappableY = -1;
            for (int y = 0; y < stateNow.Height && !hasTappable; y++)
                for (int x = 0; x < stateNow.Width && !hasTappable; x++)
                {
                    var t = stateNow.GetTile(x, y);
                    if (t.Type.IsBomb() && stateNow.CanInteract(new Position(x, y)))
                    { hasTappable = true; tappableX = x; tappableY = y; }
                }

            if (!SimulationTestHelper.TryApplyRandomMove(engine, moveNum))
            {
                _output.WriteLine($"[move {moveNum}] No valid move found, stopping.");
                break;
            }

            if (moveNum == TargetMove)
            {
                // Log what move was applied
                if (hasTappable)
                    _output.WriteLine($"--- Move {moveNum} applied: TAP bomb at ({tappableX},{tappableY}) ---");
                else
                    _output.WriteLine($"--- Move {moveNum} applied: SWAP (determined by validator) ---");

                // Log initial state AFTER move applied (before any ticks)
                _output.WriteLine($"[move {moveNum}] After apply: Tick={engine.CurrentTick}, IsStable={engine.IsStable()}, HasTimedLocks={engine.Locks.HasTimedLocks}");
                _output.WriteLine("[move {TargetMove}] Initial non-zero CellLocks after apply:");
                var afterApplyState = engine.State;
                bool anyLocked = false;
                for (int i = 0; i < gridSize; i++)
                {
                    if (afterApplyState.CellLocks[i] != 0)
                    {
                        int x = i % afterApplyState.Width, y = i / afterApplyState.Width;
                        _output.WriteLine($"  [{i}] ({x},{y}): 0x{afterApplyState.CellLocks[i]:X}  tile={afterApplyState.Grid[i].Type}");
                        anyLocked = true;
                    }
                }
                if (!anyLocked) _output.WriteLine("  (none)");
                _output.WriteLine("");
                _output.WriteLine($"[move {TargetMove}] BEGIN detailed per-tick trace (all cells)");

                // Update prevLocks to current state
                Array.Copy(engine.State.CellLocks, prevLocks, gridSize);

                // Also track tile IDs to detect tile movements/eliminations
                uint[] prevTileIds = new uint[gridSize];
                for (int i = 0; i < gridSize; i++)
                    prevTileIds[i] = (uint)engine.State.Grid[i].Id;

                int safety = 0;
                while (!engine.IsStable() && safety < 500)
                {
                    engine.Tick();
                    safety++;

                    // Check ALL cells for lock or tile changes
                    var s = engine.State;
                    bool anyChange = false;
                    for (int i = 0; i < gridSize; i++)
                    {
                        uint curLock = s.CellLocks[i];
                        uint curTileId = (uint)s.Grid[i].Id;
                        bool lockChanged = curLock != prevLocks[i];
                        bool tileChanged = curTileId != prevTileIds[i];

                        if (lockChanged || tileChanged)
                        {
                            if (!anyChange)
                            {
                                _output.WriteLine($"  tick#{engine.CurrentTick} (safety={safety}): " +
                                    $"IsStable={engine.IsStable()}, TimedLocks={engine.Locks.HasTimedLocks}");
                                anyChange = true;
                            }
                            int x = i % s.Width, y = i / s.Width;
                            if (lockChanged)
                                _output.WriteLine($"    LOCK [{i}] ({x},{y}): 0x{prevLocks[i]:X} -> 0x{curLock:X}  " +
                                    $"R={CellLockOps.GetCount(curLock, CellLockType.Receive)}" +
                                    $" D={CellLockOps.GetCount(curLock, CellLockType.Drop)}" +
                                    $" S={CellLockOps.GetCount(curLock, CellLockType.Swap)}" +
                                    $" M={CellLockOps.GetCount(curLock, CellLockType.Matching)}" +
                                    $" I={CellLockOps.GetCount(curLock, CellLockType.Indestructible)}" +
                                    $" T={CellLockOps.GetCount(curLock, CellLockType.Targeting)}" +
                                    $"  tile={s.Grid[i].Type}(id={s.Grid[i].Id})");
                            if (tileChanged)
                                _output.WriteLine($"    TILE [{i}] ({x},{y}): id={prevTileIds[i]} -> id={curTileId}/{s.Grid[i].Type}");
                            prevLocks[i] = curLock;
                            prevTileIds[i] = curTileId;
                        }
                    }

                    // Print focus-area snapshot at key ticks around the suspected loop (ticks 245-270)
                    if (engine.CurrentTick >= 245 && engine.CurrentTick <= 270 && engine.CurrentTick % 2 == 1)
                    {
                        static string TileStr(Tile t, uint lk) =>
                            $"{t.Type}#{t.Id}(L={lk:X})";
                        _output.WriteLine($"    SNAP tick#{engine.CurrentTick}:" +
                            $" r0=[{TileStr(s.Grid[3], s.CellLocks[3])}|{TileStr(s.Grid[4], s.CellLocks[4])}|{TileStr(s.Grid[5], s.CellLocks[5])}]" +
                            $" r1=[{TileStr(s.Grid[11], s.CellLocks[11])}|{TileStr(s.Grid[12], s.CellLocks[12])}|{TileStr(s.Grid[13], s.CellLocks[13])}]" +
                            $" r2=[{TileStr(s.Grid[19], s.CellLocks[19])}|{TileStr(s.Grid[20], s.CellLocks[20])}|{TileStr(s.Grid[21], s.CellLocks[21])}]");
                    }
                }

                _output.WriteLine($"[move {TargetMove}] Per-tick loop ended: safety={safety}, IsStable={engine.IsStable()}");

                // Full settle
                SimulationTestHelper.SettleCompletely(engine);
                _output.WriteLine($"[move {TargetMove}] After SettleCompletely: IsStable={engine.IsStable()}, HasTimedLocks={engine.Locks.HasTimedLocks}");

                // Dump all non-zero locks
                _output.WriteLine("\n=== Post-settle CellLock dump (all non-zero) ===");
                var finalState = engine.State;
                bool any = false;
                for (int i = 0; i < gridSize; i++)
                {
                    uint lv = finalState.CellLocks[i];
                    if (lv == 0) continue;
                    any = true;
                    int x = i % finalState.Width, y = i / finalState.Width;
                    _output.WriteLine($"  [{i}] ({x},{y}): 0x{lv:X}" +
                        $"  R={CellLockOps.GetCount(lv, CellLockType.Receive)}" +
                        $" D={CellLockOps.GetCount(lv, CellLockType.Drop)}" +
                        $" S={CellLockOps.GetCount(lv, CellLockType.Swap)}" +
                        $" M={CellLockOps.GetCount(lv, CellLockType.Matching)}" +
                        $" I={CellLockOps.GetCount(lv, CellLockType.Indestructible)}" +
                        $" T={CellLockOps.GetCount(lv, CellLockType.Targeting)}" +
                        $"  tile={finalState.Grid[i].Type}(id={finalState.Grid[i].Id})  cell={finalState.Cells[i]}");
                }
                if (!any) _output.WriteLine("  (none)");

                _output.WriteLine($"\nengine.IsStable() = {engine.IsStable()}");
                _output.WriteLine($"HasTimedLocks = {engine.Locks.HasTimedLocks}");
                _output.WriteLine($"HasActiveSessions = (check via IsStable components)");
                return;
            }
            else
            {
                // Fast path for moves 0–22
                int safety = 0;
                Array.Copy(engine.State.CellLocks, prevLocks, gridSize);
                while (!engine.IsStable() && safety < 2000)
                {
                    engine.Tick();
                    safety++;
                    // Check all cells for brief log
                    var s = engine.State;
                    for (int i = 0; i < gridSize; i++)
                    {
                        if (s.CellLocks[i] != prevLocks[i])
                        {
                            prevLocks[i] = s.CellLocks[i];
                        }
                    }
                }
                SimulationTestHelper.SettleCompletely(engine);

                // Check if any non-zero locks remain
                var finalS = engine.State;
                bool anyNZ = false;
                for (int i = 0; i < gridSize; i++)
                    if (finalS.CellLocks[i] != 0) { anyNZ = true; break; }

                if (anyNZ)
                    _output.WriteLine($"  [move {moveNum}] WARNING: non-zero locks after settle!");
                else if (moveNum >= 20)
                    _output.WriteLine($"[move {moveNum} settled] OK, Tick={engine.CurrentTick}");

                Array.Copy(finalS.CellLocks, prevLocks, gridSize);
            }
        }

        _output.WriteLine("=== Diagnostic complete ===");
    }
}
