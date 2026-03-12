using Match3.Core.Commands;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Tests.TestHelpers;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Determinism and Clone equivalence tests.
/// Verifies that the simulation is fully deterministic and that Clone()
/// produces functionally independent but equivalent engines.
/// </summary>
[Trait("Category", "Slow")]
public class DeterminismTests
{
    private const int MaxMoves = 20;

    // ── Test 1: Same seed, same operations → identical state ────────

    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    [InlineData(9999)]
    public void SameSeed_SameOperations_ProduceIdenticalState(int seed)
    {
        using var sessionA = SimulationTestHelper.CreateSession(seed);
        using var sessionB = SimulationTestHelper.CreateSession(seed);
        var engineA = sessionA.Engine;
        var engineB = sessionB.Engine;

        SimulationTestHelper.AssertStateEqual(engineA.State, engineB.State, "initial");

        for (int moveNum = 0; moveNum < MaxMoves; moveNum++)
        {
            if (engineA.State.LevelStatus != LevelStatus.InProgress) break;

            var stateA = engineA.State;
            var moves = ValidMoveDetector.FindAllValidMoves(in stateA, engineA.MatchFinder);
            try
            {
                if (moves.Count == 0) break;
                var move = moves[moveNum % moves.Count];
                engineA.ApplyMove(move.From, move.To);
                engineB.ApplyMove(move.From, move.To);
            }
            finally
            {
                Pools.Release(moves);
            }

            engineA.RunUntilStable();
            engineB.RunUntilStable();

            SimulationTestHelper.AssertStateEqual(engineA.State, engineB.State,
                $"after move {moveNum}");
        }
    }

    // ── Test 2: Clone + same operations → identical final state ─────

    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    [InlineData(9999)]
    public void Clone_WithSameRng_ProduceIdenticalState(int seed)
    {
        using var session = SimulationTestHelper.CreateSession(seed);
        var engine = session.Engine;

        // Run engine for a while to build up complex state
        engine.RunUntilStable();

        var stateSnap = engine.State;
        if (stateSnap.LevelStatus != LevelStatus.InProgress) return;

        // Apply a move
        var moves = ValidMoveDetector.FindAllValidMoves(in stateSnap, engine.MatchFinder);
        Position from, to;
        try
        {
            if (moves.Count == 0) return;
            (from, to) = moves[0];
        }
        finally
        {
            Pools.Release(moves);
        }

        // Clone twice with the same RNG seed → both must produce identical results
        const ulong cloneSeed = 999_999;
        var cloneA = engine.Clone(new XorShift64(cloneSeed));
        var cloneB = engine.Clone(new XorShift64(cloneSeed));

        SimulationTestHelper.AssertStateEqual(cloneA.State, cloneB.State, "clones initial");

        cloneA.ApplyMove(from, to);
        cloneB.ApplyMove(from, to);

        cloneA.RunUntilStable();
        cloneB.RunUntilStable();

        SimulationTestHelper.AssertStateEqual(cloneA.State, cloneB.State, "clones after move");
    }

    // ── Test 3: Clone is independent from original ──────────────────

    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    public void Clone_IsIndependentFromOriginal(int seed)
    {
        using var session = SimulationTestHelper.CreateSession(seed);
        var engine = session.Engine;
        engine.RunUntilStable();

        if (engine.State.LevelStatus != LevelStatus.InProgress) return;

        // Snapshot original state before clone
        var originalStateBefore = engine.State;
        var originalScore = originalStateBefore.Score;
        var originalMoveCount = originalStateBefore.MoveCount;

        // Clone and apply a move to the clone only
        var cloned = engine.Clone(new XorShift64(42));

        var cloneState = cloned.State;
        var cloneMoves = ValidMoveDetector.FindAllValidMoves(in cloneState, cloned.MatchFinder);
        try
        {
            if (cloneMoves.Count > 0)
            {
                var move = cloneMoves[0];
                cloned.ApplyMove(move.From, move.To);
                cloned.RunUntilStable();
            }
        }
        finally
        {
            Pools.Release(cloneMoves);
        }

        // Original must be unchanged
        Assert.Equal(originalScore, engine.State.Score);
        Assert.Equal(originalMoveCount, engine.State.MoveCount);
    }

    // ── Test 4: RunUntilStable is deterministic ─────────────────────

    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    [InlineData(9999)]
    public void RunUntilStable_SameSeed_SameResult(int seed)
    {
        using var sessionA = SimulationTestHelper.CreateSession(seed);
        using var sessionB = SimulationTestHelper.CreateSession(seed);
        var engineA = sessionA.Engine;
        var engineB = sessionB.Engine;

        var stateA = engineA.State;
        var moves = ValidMoveDetector.FindAllValidMoves(in stateA, engineA.MatchFinder);
        try
        {
            if (moves.Count > 0)
            {
                var move = moves[0];
                engineA.ApplyMove(move.From, move.To);
                engineB.ApplyMove(move.From, move.To);
            }
        }
        finally
        {
            Pools.Release(moves);
        }

        var resultA = engineA.RunUntilStable();
        var resultB = engineB.RunUntilStable();

        Assert.Equal(resultA.TickCount, resultB.TickCount);
        Assert.Equal(resultA.ScoreGained, resultB.ScoreGained);
        Assert.Equal(resultA.TilesCleared, resultB.TilesCleared);
        Assert.Equal(resultA.MatchesProcessed, resultB.MatchesProcessed);
        SimulationTestHelper.AssertStateEqual(engineA.State, engineB.State, "RunUntilStable");
    }

    // ── Test 5: Record → Serialize → Deserialize → Replay → same state ──

    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    public void ReplayThroughSerialization_ProducesIdenticalState(int seed)
    {
        // Phase 1: play a game and record commands
        using var session = SimulationTestHelper.CreateSession(seed);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(initState, seed);
        var recordedMoves = new List<(Position From, Position To)>();

        for (int moveNum = 0; moveNum < MaxMoves; moveNum++)
        {
            if (engine.State.LevelStatus != LevelStatus.InProgress) break;

            var state = engine.State;
            var moves = ValidMoveDetector.FindAllValidMoves(in state, engine.MatchFinder);
            try
            {
                if (moves.Count == 0) break;
                var move = moves[moveNum % moves.Count];
                engine.ApplyMove(move.From, move.To);

                var cmd = new SwapCommand
                {
                    IssuedAtTick = engine.CurrentTick,
                    From = move.From,
                    To = move.To
                };
                recorder.RecordCommand(cmd);
                recordedMoves.Add((move.From, move.To));
            }
            finally
            {
                Pools.Release(moves);
            }

            engine.RunUntilStable();
        }

        var finalState = engine.State;
        var recording = recorder.Complete(engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        // Phase 2: serialize → deserialize
        string json = GameRecordingSerializer.ToJson(recording);
        var deserialized = GameRecordingSerializer.FromJson(json);
        Assert.NotNull(deserialized);
        Assert.Equal(recording.Commands.Count, deserialized!.Commands.Count);

        // Phase 3: replay on a fresh engine with same seed, applying same moves
        using var replaySession = SimulationTestHelper.CreateSession(seed);
        var replayEngine = replaySession.Engine;

        foreach (var cmd in deserialized.Commands)
        {
            if (replayEngine.State.LevelStatus != LevelStatus.InProgress) break;
            cmd.Execute(replayEngine);
            replayEngine.RunUntilStable();
        }

        // Phase 4: compare final states
        Assert.Equal(finalState.Score, replayEngine.State.Score);
        Assert.Equal(finalState.MoveCount, replayEngine.State.MoveCount);
        Assert.Equal(finalState.LevelStatus, replayEngine.State.LevelStatus);

        int size = finalState.Width * finalState.Height;
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(finalState.Grid[i].Type, replayEngine.State.Grid[i].Type);
        }
    }
}
