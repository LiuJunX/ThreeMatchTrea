using System;
using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Xunit;

namespace Match3.Core.Tests.Replay;

public class GameRecordingSerializerTests
{
    #region Helpers

    private static GameStateSnapshot CreateSnapshot(int width = 4, int height = 4)
    {
        int size = width * height;
        var tileTypes = new ElementType[size];
        var coverLayers = new Cover[size];
        var groundLayers = new Ground[size];
        var cells = new CellKind[size];

        for (int i = 0; i < size; i++)
        {
            tileTypes[i] = (ElementType)(i % 6 + 1);
            cells[i] = CellKind.Slot;
        }

        return new GameStateSnapshot
        {
            Width = width,
            Height = height,
            TileTypesCount = 6,
            TileTypes = tileTypes,
            CoverLayers = coverLayers,
            GroundLayers = groundLayers,
            Cells = cells,
            NextTileId = size + 1,
            Score = 500,
            MoveCount = 3,
            MoveLimit = 25,
            TargetDifficulty = 0.6f,
            LevelStatus = LevelStatus.InProgress,
            ObjectiveProgress = new[]
            {
                new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 10,
                    CurrentCount = 4
                },
                default, default, default
            }
        };
    }

    private static GameRecording CreateRecording(
        IReadOnlyList<IGameCommand>? commands = null,
        int durationTicks = 300)
    {
        return new GameRecording
        {
            Version = 1,
            RandomSeed = 42,
            DurationTicks = durationTicks,
            FinalScore = 1200,
            TotalMoves = 8,
            InitialState = CreateSnapshot(),
            Commands = commands ?? Array.Empty<IGameCommand>()
        };
    }

    #endregion

    #region Round-Trip: Basic Fields

    [Fact]
    public void RoundTrip_PreservesVersion()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal(1, restored!.Version);
    }

    [Fact]
    public void RoundTrip_PreservesRandomSeed()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(42, restored!.RandomSeed);
    }

    [Fact]
    public void RoundTrip_PreservesDurationTicks()
    {
        var recording = CreateRecording(durationTicks: 600);
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(600, restored!.DurationTicks);
    }

    [Fact]
    public void RoundTrip_PreservesFinalScoreAndMoves()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(1200, restored!.FinalScore);
        Assert.Equal(8, restored.TotalMoves);
    }

    #endregion

    #region Round-Trip: Snapshot

    [Fact]
    public void RoundTrip_PreservesSnapshotDimensions()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(4, restored!.InitialState.Width);
        Assert.Equal(4, restored.InitialState.Height);
        Assert.Equal(6, restored.InitialState.TileTypesCount);
    }

    [Fact]
    public void RoundTrip_PreservesSnapshotTileTypes()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(
            recording.InitialState.TileTypes.Length,
            restored!.InitialState.TileTypes.Length);

        for (int i = 0; i < recording.InitialState.TileTypes.Length; i++)
        {
            Assert.Equal(
                recording.InitialState.TileTypes[i],
                restored.InitialState.TileTypes[i]);
        }
    }

    [Fact]
    public void RoundTrip_PreservesSnapshotScoreAndMoves()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(500, restored!.InitialState.Score);
        Assert.Equal(3, restored.InitialState.MoveCount);
    }

    [Fact]
    public void RoundTrip_PreservesSnapshotMoveLimit()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(25, restored!.InitialState.MoveLimit);
    }

    [Fact]
    public void RoundTrip_PreservesSnapshotTargetDifficulty()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(0.6f, restored!.InitialState.TargetDifficulty, precision: 3);
    }

    [Fact]
    public void RoundTrip_PreservesSnapshotLevelStatus()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(LevelStatus.InProgress, restored!.InitialState.LevelStatus);
    }

    [Fact]
    public void RoundTrip_PreservesObjectiveProgress()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        var obj = restored!.InitialState.ObjectiveProgress[0];
        Assert.Equal(ObjectiveTargetLayer.Tile, obj.TargetLayer);
        Assert.Equal((int)ElementType.Item1, obj.ElementType);
        Assert.Equal(10, obj.TargetCount);
        Assert.Equal(4, obj.CurrentCount);
    }

    #endregion

    #region Round-Trip: Cover and Ground Layers

    [Fact]
    public void RoundTrip_PreservesCoverLayer()
    {
        var snapshot = CreateSnapshot();
        snapshot.CoverLayers[0] = new Cover(CoverType.Cage, 2, false);
        snapshot.CoverLayers[5] = new Cover(CoverType.Bubble, 1, true);

        var recording = new GameRecording
        {
            InitialState = snapshot,
            RandomSeed = 1,
            Commands = Array.Empty<IGameCommand>()
        };

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(CoverType.Cage, restored!.InitialState.CoverLayers[0].Type);
        Assert.Equal(2, restored.InitialState.CoverLayers[0].Health);
        Assert.False(restored.InitialState.CoverLayers[0].IsDynamic);

        Assert.Equal(CoverType.Bubble, restored.InitialState.CoverLayers[5].Type);
        Assert.True(restored.InitialState.CoverLayers[5].IsDynamic);
    }

    [Fact]
    public void RoundTrip_PreservesGroundLayer()
    {
        var snapshot = CreateSnapshot();
        snapshot.GroundLayers[3] = new Ground(GroundType.Ice, 3);

        var recording = new GameRecording
        {
            InitialState = snapshot,
            RandomSeed = 1,
            Commands = Array.Empty<IGameCommand>()
        };

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(GroundType.Ice, restored!.InitialState.GroundLayers[3].Type);
        Assert.Equal(3, restored.InitialState.GroundLayers[3].Health);
    }

    #endregion

    #region Round-Trip: Commands

    [Fact]
    public void RoundTrip_PreservesSwapCommand()
    {
        var commands = new IGameCommand[]
        {
            new SwapCommand
            {
                IssuedAtTick = 100,
                From = new Position(2, 3),
                To = new Position(3, 3)
            }
        };
        var recording = CreateRecording(commands: commands);

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Single(restored!.Commands);
        var cmd = Assert.IsType<SwapCommand>(restored.Commands[0]);
        Assert.Equal(100, cmd.IssuedAtTick);
        Assert.Equal(2, cmd.From.X);
        Assert.Equal(3, cmd.From.Y);
        Assert.Equal(3, cmd.To.X);
        Assert.Equal(3, cmd.To.Y);
    }

    [Fact]
    public void RoundTrip_PreservesTapCommand()
    {
        var commands = new IGameCommand[]
        {
            new TapCommand
            {
                IssuedAtTick = 50,
                Position = new Position(1, 2)
            }
        };
        var recording = CreateRecording(commands: commands);

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Single(restored!.Commands);
        var cmd = Assert.IsType<TapCommand>(restored.Commands[0]);
        Assert.Equal(50, cmd.IssuedAtTick);
        Assert.Equal(1, cmd.Position.X);
        Assert.Equal(2, cmd.Position.Y);
    }

    [Fact]
    public void RoundTrip_PreservesMixedCommands()
    {
        var commands = new IGameCommand[]
        {
            new SwapCommand { IssuedAtTick = 10, From = new Position(0, 0), To = new Position(1, 0) },
            new TapCommand { IssuedAtTick = 30, Position = new Position(5, 5) },
            new SwapCommand { IssuedAtTick = 60, From = new Position(3, 4), To = new Position(3, 5) },
        };
        var recording = CreateRecording(commands: commands);

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(3, restored!.Commands.Count);
        Assert.IsType<SwapCommand>(restored.Commands[0]);
        Assert.IsType<TapCommand>(restored.Commands[1]);
        Assert.IsType<SwapCommand>(restored.Commands[2]);

        Assert.Equal(10, restored.Commands[0].IssuedAtTick);
        Assert.Equal(30, restored.Commands[1].IssuedAtTick);
        Assert.Equal(60, restored.Commands[2].IssuedAtTick);
    }

    [Fact]
    public void RoundTrip_EmptyCommands()
    {
        var recording = CreateRecording(commands: Array.Empty<IGameCommand>());

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Empty(restored!.Commands);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void ToJson_NullRecording_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GameRecordingSerializer.ToJson(null!));
    }

    [Fact]
    public void FromJson_NullInput_ReturnsNull()
    {
        var result = GameRecordingSerializer.FromJson(null!);
        Assert.Null(result);
    }

    [Fact]
    public void FromJson_EmptyString_ReturnsNull()
    {
        var result = GameRecordingSerializer.FromJson("");
        Assert.Null(result);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        var result = GameRecordingSerializer.FromJson("not json at all {{{");
        Assert.Null(result);
    }

    #endregion

    #region JSON Format

    [Fact]
    public void ToJson_ProducesNonEmptyString()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.StartsWith("{", json);
    }

    [Fact]
    public void ToJson_ContainsVersionField()
    {
        var recording = CreateRecording();
        var json = GameRecordingSerializer.ToJson(recording);

        Assert.Contains("\"version\"", json);
    }

    [Fact]
    public void ToJson_ContainsCommandTypeField()
    {
        var commands = new IGameCommand[]
        {
            new SwapCommand { IssuedAtTick = 10, From = new Position(0, 0), To = new Position(1, 0) }
        };
        var recording = CreateRecording(commands: commands);
        var json = GameRecordingSerializer.ToJson(recording);

        Assert.Contains("\"type\":\"Swap\"", json);
    }

    #endregion

    #region Cells Round-Trip

    [Fact]
    public void RoundTrip_PreservesCells()
    {
        var snapshot = CreateSnapshot();
        snapshot.Cells[0] = CellKind.Void;
        snapshot.Cells[1] = CellKind.Spawner;
        snapshot.Cells[2] = CellKind.Sink;

        var recording = new GameRecording
        {
            InitialState = snapshot,
            RandomSeed = 1,
            Commands = Array.Empty<IGameCommand>()
        };

        var json = GameRecordingSerializer.ToJson(recording);
        var restored = GameRecordingSerializer.FromJson(json);

        Assert.Equal(CellKind.Void, restored!.InitialState.Cells[0]);
        Assert.Equal(CellKind.Spawner, restored.InitialState.Cells[1]);
        Assert.Equal(CellKind.Sink, restored.InitialState.Cells[2]);
    }

    #endregion
}
