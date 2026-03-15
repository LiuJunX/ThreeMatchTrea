using Match3.Core.Commands;
using Match3.Core.DependencyInjection;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestHelpers;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Replay;

/// <summary>
/// Verifies that ReplayController produces identical results to the original game.
/// These tests are designed to catch divergence between recording and playback,
/// specifically the random stream position issue where the Refill domain random
/// is consumed during board initialization but not restored during replay.
/// </summary>
[Trait("Category", "Slow")]
public class ReplayDivergenceTests
{
    private const int MaxMoves = 8;

    // ── Test 1: End-to-end replay via ReplayController ──────────────

    /// <summary>
    /// Hypothesis: ReplayController.Seek(1.0) should produce the same final state
    /// as the original game.
    ///
    /// Expected to FAIL if the Refill random stream starts at a different position
    /// in replay vs gameplay, causing different tiles to spawn after matches.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    [InlineData(9999)]
    public void ReplayController_SeekToEnd_MatchesOriginalGame(int seed)
    {
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        // Phase 1: Play a game with ForHumanPlay config (matching real gameplay)
        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            EnableEventCollection = false
            // SimulationConfig defaults to ForHumanPlay()
        };
        using var session = factory.CreateGameSession(gameConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed);

        PlayMoves(engine, recorder, MaxMoves);

        var finalState = engine.State;
        var recording = recorder.Complete(
            engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        // Phase 2: Replay via ReplayController
        using var replayCtrl = new ReplayController(recording, factory);
        replayCtrl.Seek(1.0f);

        // Phase 3: Compare final states
        Assert.NotNull(replayCtrl.Engine);
        var replayState = replayCtrl.Engine!.State;

        SimulationTestHelper.AssertStateEqual(finalState, replayState,
            "ReplayController vs Original");
    }

    // ── Test 2: Direct command replay (bypass ReplayController) ─────

    /// <summary>
    /// Control test: replay by directly executing commands on a fresh engine
    /// created the SAME way as gameplay (via CreateGameSession).
    /// This should always pass — both sides use CreateGameSession which
    /// consumes the Refill random during board init identically.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    public void DirectReplay_WithSameSessionFactory_MatchesOriginalGame(int seed)
    {
        var factory = new GameServiceBuilder().UseDefaultServices().Build();
        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            EnableEventCollection = false
        };

        // Play original game
        using var session = factory.CreateGameSession(gameConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed);

        PlayMoves(engine, recorder, MaxMoves);

        var finalState = engine.State;
        var recording = recorder.Complete(
            engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        // Replay on a fresh session with same seed (same init path)
        using var replaySession = factory.CreateGameSession(gameConfig);
        var replayEngine = replaySession.Engine;

        foreach (var cmd in recording.Commands)
        {
            if (replayEngine.State.LevelStatus != LevelStatus.InProgress) break;
            cmd.Execute(replayEngine);
            replayEngine.RunUntilStable();
        }

        SimulationTestHelper.AssertStateEqual(finalState, replayEngine.State,
            "Direct replay vs Original");
    }

    // ── Test 3: Same init path produces identical first move ────────

    /// <summary>
    /// Verifies that recreating a session with the same seed (the fix)
    /// produces identical results after the first move.
    /// Both engines go through CreateGameSession → same Refill stream position.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(1337)]
    public void SameInitPath_ProducesIdenticalFirstMove(int seed)
    {
        var factory = new GameServiceBuilder().UseDefaultServices().Build();
        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            EnableEventCollection = false
        };

        // Both engines created via CreateGameSession (same init path)
        using var sessionA = factory.CreateGameSession(gameConfig);
        using var sessionB = factory.CreateGameSession(gameConfig);

        // Apply the same move to both
        var state = sessionA.Engine.State;
        var moves = ValidMoveDetector.FindAllValidMoves(in state, sessionA.Engine.MatchFinder);
        try
        {
            if (moves.Count == 0) return;

            var move = moves[0];
            sessionA.Engine.ApplyMove(move.From, move.To);
            sessionB.Engine.ApplyMove(move.From, move.To);
        }
        finally
        {
            Pools.Release(moves);
        }

        sessionA.Engine.RunUntilStable();
        sessionB.Engine.RunUntilStable();

        SimulationTestHelper.AssertStateEqual(
            sessionA.Engine.State, sessionB.Engine.State,
            "After first move: session A vs session B");
    }

    // ── Test 4: Serialization round-trip doesn't lose state ─────────

    /// <summary>
    /// Verifies that serializing and deserializing a recording doesn't
    /// introduce additional divergence beyond what already exists.
    /// </summary>
    [Theory]
    [InlineData(42)]
    public void Serialization_DoesNotAddDivergence(int seed)
    {
        var factory = new GameServiceBuilder().UseDefaultServices().Build();
        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            EnableEventCollection = false
        };
        using var session = factory.CreateGameSession(gameConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed);

        PlayMoves(engine, recorder, 5);

        var recording = recorder.Complete(
            engine.CurrentTick, engine.State.Score, engine.State.MoveCount);
        recorder.Dispose();

        // Replay from original recording
        using var ctrl1 = new ReplayController(recording, factory);
        ctrl1.Seek(1.0f);

        // Replay from serialized+deserialized recording
        var json = GameRecordingSerializer.ToJson(recording);
        var deserialized = GameRecordingSerializer.FromJson(json);
        Assert.NotNull(deserialized);

        using var ctrl2 = new ReplayController(deserialized!, factory);
        ctrl2.Seek(1.0f);

        // Both replays should produce identical state
        // (even if both diverge from the original game)
        SimulationTestHelper.AssertStateEqual(
            ctrl1.Engine!.State, ctrl2.Engine!.State,
            "Replay from original vs deserialized recording");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void PlayMoves(SimulationEngine engine, GameRecorder recorder, int maxMoves)
    {
        for (int moveNum = 0; moveNum < maxMoves; moveNum++)
        {
            if (engine.State.LevelStatus != LevelStatus.InProgress) break;

            var state = engine.State;
            var moves = ValidMoveDetector.FindAllValidMoves(in state, engine.MatchFinder);
            try
            {
                if (moves.Count == 0) break;
                var move = moves[moveNum % moves.Count];

                var cmd = new SwapCommand
                {
                    IssuedAtTick = engine.CurrentTick,
                    From = move.From,
                    To = move.To
                };

                cmd.Execute(engine);
                recorder.RecordCommand(cmd);
            }
            finally
            {
                Pools.Release(moves);
            }

            engine.RunUntilStable();
        }
    }
}
