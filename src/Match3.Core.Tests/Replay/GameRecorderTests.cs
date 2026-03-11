using System;
using Match3.Core.Commands;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Replay;

public class GameRecorderTests
{
    #region Helpers

    private static GameState CreateTestState()
    {
        var random = new StubRandom();
        var state = new GameState(4, 4, 6, random)
        {
            Score = 0,
            MoveCount = 0,
            MoveLimit = 20
        };

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
            }
        }

        return state;
    }

    private sealed record StubCommand : IGameCommand
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public int IssuedAtTick { get; init; }
        public bool Execute(SimulationEngine engine) => true;
        public bool CanExecute(in GameState state) => true;
    }

    #endregion

    #region Construction

    [Fact]
    public void Constructor_CapturesInitialState()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 42);

        Assert.NotNull(recorder.InitialState);
        Assert.Equal(4, recorder.InitialState.Width);
        Assert.Equal(4, recorder.InitialState.Height);
    }

    [Fact]
    public void Constructor_CapturesSeed()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 12345);

        Assert.Equal(12345, recorder.Seed);
    }

    [Fact]
    public void Constructor_IsRecordingByDefault()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        Assert.True(recorder.IsRecording);
    }

    [Fact]
    public void Constructor_CommandCountIsZero()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        Assert.Equal(0, recorder.CommandCount);
    }

    #endregion

    #region RecordCommand

    [Fact]
    public void RecordCommand_IncrementsCount()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        recorder.RecordCommand(new StubCommand { IssuedAtTick = 10 });
        Assert.Equal(1, recorder.CommandCount);

        recorder.RecordCommand(new StubCommand { IssuedAtTick = 20 });
        Assert.Equal(2, recorder.CommandCount);
    }

    [Fact]
    public void RecordCommand_SwapCommand_Recorded()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        var swap = new SwapCommand
        {
            IssuedAtTick = 100,
            From = new Position(0, 0),
            To = new Position(1, 0)
        };
        recorder.RecordCommand(swap);

        Assert.Equal(1, recorder.CommandCount);
    }

    [Fact]
    public void RecordCommand_TapCommand_Recorded()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        var tap = new TapCommand
        {
            IssuedAtTick = 50,
            Position = new Position(2, 3)
        };
        recorder.RecordCommand(tap);

        Assert.Equal(1, recorder.CommandCount);
    }

    [Fact]
    public void RecordCommand_AfterDispose_Ignored()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);
        recorder.Dispose();

        recorder.RecordCommand(new StubCommand { IssuedAtTick = 10 });

        Assert.Equal(0, recorder.CommandCount);
    }

    [Fact]
    public void RecordCommand_AfterComplete_Ignored()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);
        recorder.RecordCommand(new StubCommand { IssuedAtTick = 10 });
        recorder.Complete(100, 500, 5);

        recorder.RecordCommand(new StubCommand { IssuedAtTick = 20 });

        // Count is from before Complete, new command is ignored
        Assert.False(recorder.IsRecording);
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_ReturnsValidRecording()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 42);
        recorder.RecordCommand(new SwapCommand
        {
            IssuedAtTick = 10,
            From = new Position(0, 0),
            To = new Position(1, 0)
        });
        recorder.RecordCommand(new TapCommand
        {
            IssuedAtTick = 50,
            Position = new Position(3, 3)
        });

        var recording = recorder.Complete(durationTicks: 600, finalScore: 1500, totalMoves: 2);

        Assert.Equal(42, recording.RandomSeed);
        Assert.Equal(600, recording.DurationTicks);
        Assert.Equal(1500, recording.FinalScore);
        Assert.Equal(2, recording.TotalMoves);
        Assert.Equal(2, recording.Commands.Count);
    }

    [Fact]
    public void Complete_PreservesInitialState()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 42);

        var recording = recorder.Complete(100, 0, 0);

        Assert.Equal(state.Width, recording.InitialState.Width);
        Assert.Equal(state.Height, recording.InitialState.Height);
        Assert.Equal(state.Score, recording.InitialState.Score);
    }

    [Fact]
    public void Complete_PreservesCommandOrder()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);
        recorder.RecordCommand(new StubCommand { IssuedAtTick = 10 });
        recorder.RecordCommand(new StubCommand { IssuedAtTick = 20 });
        recorder.RecordCommand(new StubCommand { IssuedAtTick = 30 });

        var recording = recorder.Complete(100, 0, 3);

        Assert.Equal(10, recording.Commands[0].IssuedAtTick);
        Assert.Equal(20, recording.Commands[1].IssuedAtTick);
        Assert.Equal(30, recording.Commands[2].IssuedAtTick);
    }

    [Fact]
    public void Complete_StopsRecording()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);
        recorder.Complete(100, 0, 0);

        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void Complete_EmptyRecording_HasNoCommands()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        var recording = recorder.Complete(60, 0, 0);

        Assert.Empty(recording.Commands);
        Assert.Equal(60, recording.DurationTicks);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_StopsRecording()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        recorder.Dispose();

        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var state = CreateTestState();
        var recorder = new GameRecorder(in state, seed: 0);

        recorder.Dispose();
        recorder.Dispose(); // should not throw
    }

    #endregion

    #region Snapshot Captures New Fields

    [Fact]
    public void InitialState_CapturesMoveLimit()
    {
        var state = CreateTestState();
        state.MoveLimit = 30;
        var recorder = new GameRecorder(in state, seed: 0);

        Assert.Equal(30, recorder.InitialState.MoveLimit);
    }

    [Fact]
    public void InitialState_CapturesTargetDifficulty()
    {
        var state = CreateTestState();
        state.TargetDifficulty = 0.9f;
        var recorder = new GameRecorder(in state, seed: 0);

        Assert.Equal(0.9f, recorder.InitialState.TargetDifficulty);
    }

    [Fact]
    public void InitialState_CapturesLevelStatus()
    {
        var state = CreateTestState();
        state.LevelStatus = LevelStatus.Victory;
        var recorder = new GameRecorder(in state, seed: 0);

        Assert.Equal(LevelStatus.Victory, recorder.InitialState.LevelStatus);
    }

    #endregion
}
