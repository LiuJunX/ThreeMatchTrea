using Match3.Core.Commands;
using Match3.Core.Config;
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
/// Verifies that ReplayController produces identical results to the original game
/// across all level complexity levels (covers, grounds, objectives, irregular boards).
/// </summary>
[Trait("Category", "Slow")]
public class ReplayDivergenceTests
{
    private const int MaxMoves = 8;

    // ── Preset × Seed data sources ───────────────────────────────────

    public static IEnumerable<object[]> AllPresets_x_Seeds()
    {
        int[] seeds = { 42, 1337, 9999 };
        foreach (var preset in SimulationTestHelper.AllPresetNames)
            foreach (var seed in seeds)
                yield return new object[] { preset, seed };
    }

    public static IEnumerable<object[]> ComplexPresets_x_Seeds()
    {
        int[] seeds = { 42, 1337 };
        string[] presets = { "WithCover", "WithGround", "Irregular7x7", "WithObjectives" };
        foreach (var preset in presets)
            foreach (var seed in seeds)
                yield return new object[] { preset, seed };
    }

    // ── Test 1: End-to-end replay across all presets ─────────────────

    /// <summary>
    /// ReplayController.Seek(1.0) should produce the same final state
    /// as the original game, for every preset × seed combination.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPresets_x_Seeds))]
    public void ReplayController_SeekToEnd_MatchesOriginalGame(string presetName, int seed)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false
        };
        using var session = factory.CreateGameSession(gameConfig, levelConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed, tileTypesCount, levelConfig);

        PlayMoves(engine, recorder, MaxMoves);

        var finalState = engine.State;
        var recording = recorder.Complete(
            engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        using var replayCtrl = new ReplayController(recording, factory);
        replayCtrl.Seek(1.0f);

        Assert.NotNull(replayCtrl.Engine);
        SimulationTestHelper.AssertStateEqual(finalState, replayCtrl.Engine!.State,
            $"[{presetName}] ReplayController vs Original");
    }

    // ── Test 2: Direct command replay across complex presets ─────────

    /// <summary>
    /// Control test: direct command execution on a fresh engine should match.
    /// Covers complex presets to ensure covers/grounds/objectives don't interfere.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexPresets_x_Seeds))]
    public void DirectReplay_WithComplexLevel_MatchesOriginalGame(string presetName, int seed)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false
        };

        using var session = factory.CreateGameSession(gameConfig, levelConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed, tileTypesCount, levelConfig);

        PlayMoves(engine, recorder, MaxMoves);

        var finalState = engine.State;
        var recording = recorder.Complete(
            engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        using var replaySession = factory.CreateGameSession(gameConfig, levelConfig);
        var replayEngine = replaySession.Engine;

        foreach (var cmd in recording.Commands)
        {
            if (replayEngine.State.LevelStatus != LevelStatus.InProgress) break;
            cmd.Execute(replayEngine);
            replayEngine.RunUntilStable();
        }

        SimulationTestHelper.AssertStateEqual(finalState, replayEngine.State,
            $"[{presetName}] Direct replay vs Original");
    }

    // ── Test 3: Same init path consistency ───────────────────────────

    /// <summary>
    /// Verifies that two sessions with the same config produce identical
    /// results after the first move, including on complex boards.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexPresets_x_Seeds))]
    public void SameInitPath_ProducesIdenticalFirstMove(string presetName, int seed)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false
        };

        using var sessionA = factory.CreateGameSession(gameConfig, levelConfig);
        using var sessionB = factory.CreateGameSession(gameConfig, levelConfig);

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
            $"[{presetName}] After first move: session A vs session B");
    }

    // ── Test 4: Serialization round-trip across all presets ──────────

    /// <summary>
    /// Verifies that serializing and deserializing a recording doesn't
    /// introduce divergence — especially for LevelConfig with covers,
    /// grounds, and objectives.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPresets_x_Seeds))]
    public void Serialization_DoesNotAddDivergence(string presetName, int seed)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false
        };
        using var session = factory.CreateGameSession(gameConfig, levelConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed, tileTypesCount, levelConfig);

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

        SimulationTestHelper.AssertStateEqual(
            ctrl1.Engine!.State, ctrl2.Engine!.State,
            $"[{presetName}] Replay from original vs deserialized recording");
    }

    // ── Test 5: Many moves on complex levels ─────────────────────────

    /// <summary>
    /// Stress test: play more moves on complex levels to exercise deeper
    /// random stream positions and more cascading interactions.
    /// </summary>
    [Theory]
    [InlineData("WithCover", 42, 15)]
    [InlineData("WithGround", 1337, 15)]
    [InlineData("WithObjectives", 9999, 20)]
    [InlineData("Small5x5_3Colors", 42, 20)]
    public void ReplayController_ManyMoves_MatchesOriginalGame(
        string presetName, int seed, int moveCount)
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset(presetName);
        var factory = new GameServiceBuilder().UseDefaultServices().Build();

        var gameConfig = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false
        };
        using var session = factory.CreateGameSession(gameConfig, levelConfig);
        var engine = session.Engine;
        var initState = engine.State;
        var recorder = new GameRecorder(in initState, seed, tileTypesCount, levelConfig);

        PlayMoves(engine, recorder, moveCount);

        var finalState = engine.State;
        var recording = recorder.Complete(
            engine.CurrentTick, finalState.Score, finalState.MoveCount);
        recorder.Dispose();

        using var replayCtrl = new ReplayController(recording, factory);
        replayCtrl.Seek(1.0f);

        Assert.NotNull(replayCtrl.Engine);
        SimulationTestHelper.AssertStateEqual(finalState, replayCtrl.Engine!.State,
            $"[{presetName}] {moveCount} moves: ReplayController vs Original");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void PlayMoves(SimulationEngine engine, GameRecorder recorder, int maxMoves)
    {
        for (int moveNum = 0; moveNum < maxMoves; moveNum++)
        {
            if (engine.State.LevelStatus != LevelStatus.InProgress) break;

            // Try tappable bombs first (like real gameplay)
            var state = engine.State;
            if (ValidMoveDetector.HasTappableBomb(in state))
            {
                if (TryTapBomb(engine, recorder))
                {
                    engine.RunUntilStable();
                    continue;
                }
            }

            // Fall back to swap moves
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

    private static bool TryTapBomb(SimulationEngine engine, GameRecorder recorder)
    {
        var state = engine.State;
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var pos = new Position(x, y);
                var tile = state.GetTile(x, y);
                if (tile.Type.IsBomb() && state.CanInteract(pos))
                {
                    var cmd = new TapCommand
                    {
                        IssuedAtTick = engine.CurrentTick,
                        Position = pos
                    };
                    cmd.Execute(engine);
                    recorder.RecordCommand(cmd);
                    return true;
                }
            }
        }
        return false;
    }
}
