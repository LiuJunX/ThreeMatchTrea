using System.Text.Json;
using System.Text.Json.Serialization;
using Match3.Core.Commands;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Progress;
using Match3.Core.Replay;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Round-trip serialization tests: Object → JSON → Object → JSON.
/// The second JSON must equal the first, proving no data loss.
/// </summary>
public class RoundTripTests
{
    // ── Test 1: GameRecording round-trip ─────────────────────────────

    [Fact]
    public void GameRecording_JsonRoundTrip_IsLossless()
    {
        var snapshot = new GameStateSnapshot
        {
            Width = 6,
            Height = 8,
            TileTypesCount = 5,
            TileTypes = CreateTileTypes(6, 8),
            CoverLayers = CreateCovers(6, 8),
            GroundLayers = CreateGrounds(6, 8),
            Cells = CreateCells(6, 8),
            NextTileId = 99,
            Score = 1234,
            MoveCount = 7,
            MoveLimit = 25,
            TargetDifficulty = 0.6f,
            ObjectiveProgress = new[]
            {
                new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 20,
                    CurrentCount = 8
                },
                new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)GroundType.Ice,
                    TargetCount = 10,
                    CurrentCount = 3
                },
                default,
                default
            },
            LevelStatus = LevelStatus.InProgress
        };

        var commands = new IGameCommand[]
        {
            new SwapCommand
            {
                IssuedAtTick = 10,
                From = new Position(0, 0),
                To = new Position(1, 0)
            },
            new TapCommand
            {
                IssuedAtTick = 50,
                Position = new Position(3, 4)
            },
            new SwapCommand
            {
                IssuedAtTick = 120,
                From = new Position(5, 7),
                To = new Position(5, 6)
            }
        };

        var recording = GameRecording.Create(
            snapshot, seed: 42, commands: commands,
            durationTicks: 300, finalScore: 5678, totalMoves: 12,
            bookmarks: new[] { 50, 150 });

        // Round-trip 1
        string json1 = GameRecordingSerializer.ToJson(recording);
        var deserialized = GameRecordingSerializer.FromJson(json1);
        Assert.NotNull(deserialized);

        // Round-trip 2
        string json2 = GameRecordingSerializer.ToJson(deserialized!);

        Assert.Equal(json1, json2);
    }

    // ── Test 2: LevelConfig round-trip ──────────────────────────────

    [Fact]
    public void LevelConfig_JsonRoundTrip_IsLossless()
    {
        var config = new LevelConfig(7, 9)
        {
            MoveLimit = 30,
            TargetDifficulty = 0.75f,
            Objectives =
            [
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item2,
                    TargetCount = 15
                },
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Cover,
                    ElementType = (int)CoverType.Cage,
                    TargetCount = 8
                },
                default,
                default
            ]
        };

        // Grid with mixed element types including bombs
        config.Grid = new ElementType[7 * 9];
        for (int i = 0; i < config.Grid.Length; i++)
            config.Grid[i] = (ElementType)((i % 6) + 1); // Item1-Item6

        config.Grid[0] = ElementType.HorizontalRocket;
        config.Grid[10] = ElementType.VerticalRocket;
        config.Grid[20] = ElementType.ColorBomb;
        config.Grid[30] = ElementType.Ufo;
        config.Grid[40] = ElementType.Square5x5;

        // Cells with mixed kinds
        config.Cells = new CellKind[7 * 9];
        for (int i = 0; i < config.Cells.Length; i++)
            config.Cells[i] = CellKind.Slot;
        config.Cells[0] = CellKind.Spawner;
        config.Cells[7 * 8] = CellKind.Sink;
        config.Cells[3 * 7 + 3] = CellKind.Void;

        // Covers
        config.Covers = new CoverType[7 * 9];
        config.CoverHealths = new byte[7 * 9];
        config.Covers[5] = CoverType.Cage;
        config.CoverHealths[5] = 2;
        config.Covers[15] = CoverType.Chain;
        config.CoverHealths[15] = 1;
        config.Covers[25] = CoverType.Bubble;
        config.CoverHealths[25] = 1;

        // Grounds
        config.Grounds = new GroundType[7 * 9];
        config.GroundHealths = new byte[7 * 9];
        config.Grounds[1] = GroundType.Ice;
        config.GroundHealths[1] = 3;
        config.Grounds[8] = GroundType.Ice;
        config.GroundHealths[8] = 1;

        // AnalysisCache
        config.AnalysisCache = new LevelAnalysisCacheData
        {
            WinRate = 0.65f,
            DeadlockRate = 0.02f,
            AverageMovesUsed = 18.5f,
            Difficulty = "Medium",
            SimulationCount = 1000,
            AnalyzedAtTicks = 638400000000000000L
        };

        // Round-trip 1
        string json1 = ConfigParser.Serialize(config);
        var parsed = ConfigParser.ParseLevelConfig(json1);

        // Round-trip 2
        string json2 = ConfigParser.Serialize(parsed);

        Assert.Equal(json1, json2);
    }

    // ── Test 3: PlayerProgress round-trip ────────────────────────────

    [Fact]
    public void PlayerProgress_JsonRoundTrip_IsLossless()
    {
        var progress = new PlayerProgress();
        progress.SetBestStars("level_001", 3);
        progress.SetBestStars("level_002", 2);
        progress.SetBestStars("level_010", 1);
        progress.UnlockedLevels.Add("level_001");
        progress.UnlockedLevels.Add("level_002");
        progress.UnlockedLevels.Add("level_003");
        progress.UnlockedLevels.Add("level_010");

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Round-trip 1
        string json1 = JsonSerializer.Serialize(progress, options);
        var deserialized = JsonSerializer.Deserialize<PlayerProgress>(json1, options);
        Assert.NotNull(deserialized);

        // Round-trip 2
        string json2 = JsonSerializer.Serialize(deserialized, options);

        // JSON string comparison may differ due to property ordering.
        // Compare semantically instead.
        var re1 = JsonSerializer.Deserialize<PlayerProgress>(json1, options)!;
        var re2 = JsonSerializer.Deserialize<PlayerProgress>(json2, options)!;

        Assert.Equal(re1.BestStars.Count, re2.BestStars.Count);
        foreach (var kvp in re1.BestStars)
        {
            Assert.True(re2.BestStars.ContainsKey(kvp.Key), $"Missing key: {kvp.Key}");
            Assert.Equal(kvp.Value, re2.BestStars[kvp.Key]);
        }

        Assert.True(re1.UnlockedLevels.SetEquals(re2.UnlockedLevels));
    }

    // ── Test data helpers ────────────────────────────────────────────

    private static ElementType[] CreateTileTypes(int w, int h)
    {
        var types = new ElementType[w * h];
        for (int i = 0; i < types.Length; i++)
            types[i] = (ElementType)((i % 6) + 1);
        // Sprinkle some bombs
        types[0] = ElementType.HorizontalRocket;
        types[5] = ElementType.ColorBomb;
        types[10] = ElementType.Ufo;
        return types;
    }

    private static Cover[] CreateCovers(int w, int h)
    {
        var covers = new Cover[w * h];
        covers[3] = new Cover(CoverType.Cage, 2);
        covers[7] = new Cover(CoverType.Bubble, 1, isDynamic: true);
        return covers;
    }

    private static Ground[] CreateGrounds(int w, int h)
    {
        var grounds = new Ground[w * h];
        grounds[1] = new Ground { Type = GroundType.Ice, Health = 2 };
        return grounds;
    }

    private static CellKind[] CreateCells(int w, int h)
    {
        var cells = new CellKind[w * h];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = CellKind.Slot;
        cells[0] = CellKind.Spawner;
        cells[cells.Length - 1] = CellKind.Sink;
        cells[w * 2 + 2] = CellKind.Void;
        return cells;
    }
}
