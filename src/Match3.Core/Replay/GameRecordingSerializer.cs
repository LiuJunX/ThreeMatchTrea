using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Match3.Core.Commands;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Replay;

/// <summary>
/// Serializes and deserializes <see cref="GameRecording"/> to/from JSON.
/// </summary>
public static class GameRecordingSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Serializes a recording to JSON.
    /// </summary>
    public static string ToJson(GameRecording recording)
    {
        if (recording == null) throw new ArgumentNullException(nameof(recording));

        var dto = ToDto(recording);
        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>
    /// Deserializes a recording from JSON.
    /// Returns null if the JSON is invalid.
    /// </summary>
    public static GameRecording? FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<RecordingDto>(json, Options);
            return dto != null ? FromDto(dto) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RecordingDto ToDto(GameRecording recording)
    {
        var commands = new List<CommandDto>(recording.Commands.Count);
        foreach (var cmd in recording.Commands)
        {
            commands.Add(CommandToDto(cmd));
        }

        var bookmarks = new List<int>(recording.Bookmarks.Count);
        for (int i = 0; i < recording.Bookmarks.Count; i++)
            bookmarks.Add(recording.Bookmarks[i]);

        return new RecordingDto
        {
            Version = recording.Version,
            RecordedAt = recording.RecordedAt.ToString("O"),
            RandomSeed = recording.RandomSeed,
            DurationTicks = recording.DurationTicks,
            FinalScore = recording.FinalScore,
            TotalMoves = recording.TotalMoves,
            InitialState = SnapshotToDto(recording.InitialState),
            Commands = commands,
            Bookmarks = bookmarks
        };
    }

    private static GameRecording FromDto(RecordingDto dto)
    {
        var commands = new List<IGameCommand>(dto.Commands.Count);
        foreach (var cmdDto in dto.Commands)
        {
            var cmd = DtoToCommand(cmdDto);
            if (cmd != null) commands.Add(cmd);
        }

        DateTimeOffset.TryParse(dto.RecordedAt, out var recordedAt);

        return new GameRecording
        {
            Version = dto.Version,
            RecordedAt = recordedAt,
            RandomSeed = dto.RandomSeed,
            DurationTicks = dto.DurationTicks,
            FinalScore = dto.FinalScore,
            TotalMoves = dto.TotalMoves,
            InitialState = DtoToSnapshot(dto.InitialState),
            Commands = commands,
            Bookmarks = dto.Bookmarks ?? new List<int>()
        };
    }

    private static SnapshotDto SnapshotToDto(GameStateSnapshot snapshot)
    {
        int size = snapshot.Width * snapshot.Height;

        var tileTypes = new byte[size];
        for (int i = 0; i < size && i < snapshot.TileTypes.Length; i++)
            tileTypes[i] = (byte)snapshot.TileTypes[i];

        var cells = new byte[size];
        for (int i = 0; i < size && i < snapshot.Cells.Length; i++)
            cells[i] = (byte)snapshot.Cells[i];

        var covers = new List<CoverDto>(size);
        for (int i = 0; i < size; i++)
        {
            var c = i < snapshot.CoverLayers.Length ? snapshot.CoverLayers[i] : default;
            covers.Add(new CoverDto { Type = (byte)c.Type, Health = c.Health, IsDynamic = c.IsDynamic });
        }

        var grounds = new List<GroundDto>(size);
        for (int i = 0; i < size; i++)
        {
            var g = i < snapshot.GroundLayers.Length ? snapshot.GroundLayers[i] : default;
            grounds.Add(new GroundDto { Type = (byte)g.Type, Health = g.Health });
        }

        var objectives = new List<ObjectiveProgressDto>(4);
        for (int i = 0; i < 4; i++)
        {
            var op = i < snapshot.ObjectiveProgress.Length ? snapshot.ObjectiveProgress[i] : default;
            objectives.Add(new ObjectiveProgressDto
            {
                TargetLayer = (byte)op.TargetLayer,
                ElementType = op.ElementType,
                TargetCount = op.TargetCount,
                CurrentCount = op.CurrentCount
            });
        }

        return new SnapshotDto
        {
            Width = snapshot.Width,
            Height = snapshot.Height,
            TileTypesCount = snapshot.TileTypesCount,
            NextTileId = snapshot.NextTileId,
            Score = snapshot.Score,
            MoveCount = snapshot.MoveCount,
            MoveLimit = snapshot.MoveLimit,
            TargetDifficulty = snapshot.TargetDifficulty,
            LevelStatus = (byte)snapshot.LevelStatus,
            TileTypes = tileTypes,
            Cells = cells,
            Covers = covers,
            Grounds = grounds,
            ObjectiveProgress = objectives
        };
    }

    private static GameStateSnapshot DtoToSnapshot(SnapshotDto dto)
    {
        int size = dto.Width * dto.Height;

        var tileTypes = new ElementType[size];
        for (int i = 0; i < size && i < dto.TileTypes.Length; i++)
            tileTypes[i] = (ElementType)dto.TileTypes[i];

        var cells = new CellKind[size];
        for (int i = 0; i < size && i < dto.Cells.Length; i++)
            cells[i] = (CellKind)dto.Cells[i];

        var coverLayers = new Cover[size];
        for (int i = 0; i < size && i < dto.Covers.Count; i++)
        {
            var c = dto.Covers[i];
            coverLayers[i] = new Cover((CoverType)c.Type, c.Health, c.IsDynamic);
        }

        var groundLayers = new Ground[size];
        for (int i = 0; i < size && i < dto.Grounds.Count; i++)
        {
            var g = dto.Grounds[i];
            groundLayers[i] = new Ground((GroundType)g.Type, g.Health);
        }

        var objectiveProgress = new ObjectiveProgress[4];
        for (int i = 0; i < 4 && i < dto.ObjectiveProgress.Count; i++)
        {
            var op = dto.ObjectiveProgress[i];
            objectiveProgress[i] = new ObjectiveProgress
            {
                TargetLayer = (ObjectiveTargetLayer)op.TargetLayer,
                ElementType = op.ElementType,
                TargetCount = op.TargetCount,
                CurrentCount = op.CurrentCount
            };
        }

        return new GameStateSnapshot
        {
            Width = dto.Width,
            Height = dto.Height,
            TileTypesCount = dto.TileTypesCount,
            NextTileId = dto.NextTileId,
            Score = dto.Score,
            MoveCount = dto.MoveCount,
            MoveLimit = dto.MoveLimit,
            TargetDifficulty = dto.TargetDifficulty,
            LevelStatus = (LevelStatus)dto.LevelStatus,
            TileTypes = tileTypes,
            Cells = cells,
            CoverLayers = coverLayers,
            GroundLayers = groundLayers,
            ObjectiveProgress = objectiveProgress
        };
    }

    private static CommandDto CommandToDto(IGameCommand command)
    {
        return command switch
        {
            SwapCommand swap => new CommandDto
            {
                Type = "Swap",
                Tick = swap.IssuedAtTick,
                FromX = swap.From.X,
                FromY = swap.From.Y,
                ToX = swap.To.X,
                ToY = swap.To.Y
            },
            TapCommand tap => new CommandDto
            {
                Type = "Tap",
                Tick = tap.IssuedAtTick,
                FromX = tap.Position.X,
                FromY = tap.Position.Y
            },
            _ => new CommandDto
            {
                Type = "Unknown",
                Tick = command.IssuedAtTick
            }
        };
    }

    private static IGameCommand? DtoToCommand(CommandDto dto)
    {
        return dto.Type switch
        {
            "Swap" => new SwapCommand
            {
                IssuedAtTick = dto.Tick,
                From = new Position(dto.FromX, dto.FromY),
                To = new Position(dto.ToX, dto.ToY)
            },
            "Tap" => new TapCommand
            {
                IssuedAtTick = dto.Tick,
                Position = new Position(dto.FromX, dto.FromY)
            },
            _ => null
        };
    }

    #region Internal DTOs

    internal sealed class RecordingDto
    {
        public int Version { get; set; }
        public string RecordedAt { get; set; } = "";
        public int RandomSeed { get; set; }
        public int DurationTicks { get; set; }
        public int FinalScore { get; set; }
        public int TotalMoves { get; set; }
        public SnapshotDto InitialState { get; set; } = new();
        public List<CommandDto> Commands { get; set; } = new();
        public List<int> Bookmarks { get; set; } = new();
    }

    internal sealed class SnapshotDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileTypesCount { get; set; }
        public int NextTileId { get; set; }
        public int Score { get; set; }
        public int MoveCount { get; set; }
        public int MoveLimit { get; set; } = 20;
        public float TargetDifficulty { get; set; } = 0.5f;
        public byte LevelStatus { get; set; }
        public byte[] TileTypes { get; set; } = Array.Empty<byte>();
        public byte[] Cells { get; set; } = Array.Empty<byte>();
        public List<CoverDto> Covers { get; set; } = new();
        public List<GroundDto> Grounds { get; set; } = new();
        public List<ObjectiveProgressDto> ObjectiveProgress { get; set; } = new();
    }

    internal sealed class CoverDto
    {
        public byte Type { get; set; }
        public byte Health { get; set; }
        public bool IsDynamic { get; set; }
    }

    internal sealed class GroundDto
    {
        public byte Type { get; set; }
        public byte Health { get; set; }
    }

    internal sealed class ObjectiveProgressDto
    {
        public byte TargetLayer { get; set; }
        public int ElementType { get; set; }
        public int TargetCount { get; set; }
        public int CurrentCount { get; set; }
    }

    internal sealed class CommandDto
    {
        public string Type { get; set; } = "";
        public int Tick { get; set; }
        public int FromX { get; set; }
        public int FromY { get; set; }
        public int ToX { get; set; }
        public int ToY { get; set; }
    }

    #endregion
}
