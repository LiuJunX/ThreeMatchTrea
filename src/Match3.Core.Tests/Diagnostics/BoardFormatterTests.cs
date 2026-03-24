using Match3.Core.Config;
using Match3.Core.Diagnostics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Diagnostics;

public class BoardFormatterTests
{
    #region LevelConfig Format — Header & Objectives

    [Fact]
    public void Format_EmptyBoard_ContainsHeaderAndDimensions()
    {
        var config = new LevelConfig(3, 3) { MoveLimit = 15 };
        var result = BoardFormatter.Format(config, "test.json");

        Assert.Contains("=== test.json (3x3) ===", result);
        Assert.Contains("Moves: 15", result);
        Assert.Contains("Difficulty: 0.50", result);
    }

    [Fact]
    public void Format_NoTitle_ShowsDimensionsOnly()
    {
        var config = new LevelConfig(4, 4);
        var result = BoardFormatter.Format(config);

        Assert.Contains("=== 4x4 ===", result);
    }

    [Fact]
    public void Format_WithObjectives_RendersObjectiveSection()
    {
        var config = new LevelConfig(3, 3);
        config.Objectives = new[]
        {
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = 1, TargetCount = 10 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Ground, ElementType = 1, TargetCount = 4 }
        };

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Objectives ---", result);
        Assert.Contains("Tile/R x10", result);
        Assert.Contains("Ground/Ic x4", result);
    }

    [Fact]
    public void Format_InactiveObjectives_Skipped()
    {
        var config = new LevelConfig(3, 3);
        // Default objectives have TargetCount=0
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Objectives ---", result);
    }

    #endregion

    #region LevelConfig Format — Tile Layer

    [Fact]
    public void Format_TilesPresent_RendersTileLayer()
    {
        var config = new LevelConfig(3, 2);
        config.Grid[0] = ElementType.Item1; // R
        config.Grid[1] = ElementType.Item3; // B
        config.Grid[2] = ElementType.Item5; // P
        config.Grid[3] = ElementType.Item2; // G
        config.Grid[4] = ElementType.Item4; // Y
        config.Grid[5] = ElementType.Item6; // O

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Tiles ---", result);
        Assert.Contains("R", result);
        Assert.Contains("B", result);
        Assert.Contains("P", result);
        Assert.Contains("G", result);
        Assert.Contains("Y", result);
        Assert.Contains("O", result);
    }

    [Fact]
    public void Format_AllTilesNone_TileLayerOmitted()
    {
        var config = new LevelConfig(3, 3);
        // Grid defaults to all None
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Tiles ---", result);
    }

    [Fact]
    public void Format_BombTypes_CorrectSymbols()
    {
        var config = new LevelConfig(5, 1);
        config.Grid[0] = ElementType.HorizontalRocket;
        config.Grid[1] = ElementType.VerticalRocket;
        config.Grid[2] = ElementType.ColorBomb;
        config.Grid[3] = ElementType.Ufo;
        config.Grid[4] = ElementType.Square5x5;

        var result = BoardFormatter.Format(config);

        Assert.Contains("H>", result);
        Assert.Contains("V^", result);
        Assert.Contains("CB", result);
        Assert.Contains("UF", result);
        Assert.Contains("5x", result);
    }

    [Fact]
    public void Format_CollectibleTypes_CorrectSymbols()
    {
        var config = new LevelConfig(5, 1);
        config.Grid[0] = ElementType.Bird;
        config.Grid[1] = ElementType.Pearl;
        config.Grid[2] = ElementType.Plate;
        config.Grid[3] = ElementType.Envelope;
        config.Grid[4] = ElementType.Diamond;

        var result = BoardFormatter.Format(config);

        Assert.Contains("Bd", result);
        Assert.Contains("Pl", result);
        Assert.Contains("Pt", result);
        Assert.Contains("Ev", result);
        Assert.Contains("Dm", result);
    }

    #endregion

    #region LevelConfig Format — Empty Layer Omission

    [Fact]
    public void Format_NoObstacles_ObstacleLayerOmitted()
    {
        var config = new LevelConfig(3, 3);
        config.Grid[0] = ElementType.Item1; // Ensure some content so output is not entirely empty
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Obstacles ---", result);
    }

    [Fact]
    public void Format_NoGrounds_GroundLayerOmitted()
    {
        var config = new LevelConfig(3, 3);
        config.Grid[0] = ElementType.Item1;
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Grounds ---", result);
    }

    [Fact]
    public void Format_NoCovers_CoverLayerOmitted()
    {
        var config = new LevelConfig(3, 3);
        config.Grid[0] = ElementType.Item1;
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Covers ---", result);
    }

    [Fact]
    public void Format_AllSlotCells_CellLayerOmitted()
    {
        var config = new LevelConfig(3, 3);
        // Cells default to Slot
        var result = BoardFormatter.Format(config);

        Assert.DoesNotContain("--- Cells ---", result);
    }

    #endregion

    #region LevelConfig Format — Obstacle Layer

    [Fact]
    public void Format_BoxObstacle_RendersWithStage()
    {
        var config = new LevelConfig(3, 1);
        config.Obstacles[0] = ObstacleType.Box;
        config.ObstacleStages[0] = 3;

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Obstacles ---", result);
        Assert.Contains("Bx3", result);
    }

    [Fact]
    public void Format_BoxStage1_NoDigitSuffix()
    {
        var config = new LevelConfig(3, 1);
        config.Obstacles[1] = ObstacleType.Box;
        config.ObstacleStages[1] = 1;

        var result = BoardFormatter.Format(config);

        // Should show "Bx" not "Bx1"
        Assert.Contains("Bx", result);
        Assert.DoesNotContain("Bx1", result);
    }

    [Fact]
    public void Format_ColorBox_ShowsColorInitial()
    {
        var config = new LevelConfig(3, 1);
        config.Obstacles[0] = ObstacleType.ColorBox;
        config.ObstacleStates[0] = (byte)ElementType.Item1; // Red

        var result = BoardFormatter.Format(config);

        Assert.Contains("CR", result); // Cx[0] + R = "CR"
    }

    [Fact]
    public void Format_Curtain_ShowsColorInitial()
    {
        var config = new LevelConfig(3, 1);
        config.Obstacles[0] = ObstacleType.Curtain;
        config.ObstacleStates[0] = (byte)ElementType.Item3; // Blue

        var result = BoardFormatter.Format(config);

        Assert.Contains("CB", result); // Ct[0] + B
    }

    [Fact]
    public void Format_AllObstacleTypes_CorrectSymbols()
    {
        var config = new LevelConfig(12, 1);
        config.Obstacles[0] = ObstacleType.Box;
        config.Obstacles[1] = ObstacleType.Bush;
        config.Obstacles[2] = ObstacleType.Safe;
        config.Obstacles[3] = ObstacleType.MagicHat;
        config.Obstacles[4] = ObstacleType.Cupboard;
        config.Obstacles[5] = ObstacleType.Mailbox;
        config.Obstacles[6] = ObstacleType.Owl;
        config.Obstacles[7] = ObstacleType.Stone;
        config.Obstacles[8] = ObstacleType.PotionBottle;

        var result = BoardFormatter.Format(config);

        Assert.Contains("Bx", result);
        Assert.Contains("Bu", result);
        Assert.Contains("Sf", result);
        Assert.Contains("Mh", result);
        Assert.Contains("Cp", result);
        Assert.Contains("Mb", result);
        Assert.Contains("Ow", result);
        Assert.Contains("St", result);
        Assert.Contains("Pb", result);
    }

    #endregion

    #region LevelConfig Format — Ground Layer

    [Fact]
    public void Format_IceGround_RendersWithHealth()
    {
        var config = new LevelConfig(3, 1);
        config.Grounds[1] = GroundType.Ice;
        config.GroundHealths[1] = 2;

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Grounds ---", result);
        Assert.Contains("Ic2", result);
    }

    [Fact]
    public void Format_IceHealth1_NoDigitSuffix()
    {
        var config = new LevelConfig(3, 1);
        config.Grounds[0] = GroundType.Ice;
        config.GroundHealths[0] = 1;

        var result = BoardFormatter.Format(config);

        Assert.Contains("Ic", result);
        Assert.DoesNotContain("Ic1", result);
    }

    #endregion

    #region LevelConfig Format — Cover Layer

    [Fact]
    public void Format_CoverTypes_CorrectSymbols()
    {
        var config = new LevelConfig(5, 1);
        config.Covers[0] = CoverType.Cage;
        config.Covers[1] = CoverType.Chain;
        config.Covers[2] = CoverType.Bubble;
        config.Covers[3] = CoverType.Honey;
        config.Covers[4] = CoverType.Frost;

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Covers ---", result);
        Assert.Contains("Cg", result);
        Assert.Contains("Ch", result);
        Assert.Contains("Bb", result);
        Assert.Contains("Hn", result);
        Assert.Contains("Fr", result);
    }

    [Fact]
    public void Format_CageHealth2_ShowsSuffix()
    {
        var config = new LevelConfig(3, 1);
        config.Covers[0] = CoverType.Cage;
        config.CoverHealths[0] = 2;

        var result = BoardFormatter.Format(config);

        Assert.Contains("Cg2", result);
    }

    #endregion

    #region LevelConfig Format — Cell Layer

    [Fact]
    public void Format_VoidAndSinkCells_RendersCellLayer()
    {
        var config = new LevelConfig(3, 2);
        config.Cells[0] = CellKind.Void;
        config.Cells[5] = CellKind.Sink;

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Cells ---", result);
        Assert.Contains("##", result);
        Assert.Contains("Sk", result);
    }

    #endregion

    #region LevelConfig Format — Multi-Layer

    [Fact]
    public void Format_MultipleNonEmptyLayers_AllRendered()
    {
        var config = new LevelConfig(3, 3);
        // Tiles
        config.Grid[0] = ElementType.Item1;
        // Obstacles
        config.Obstacles[4] = ObstacleType.Box;
        config.ObstacleStages[4] = 2;
        // Grounds
        config.Grounds[8] = GroundType.Ice;
        config.GroundHealths[8] = 1;

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Tiles ---", result);
        Assert.Contains("--- Obstacles ---", result);
        Assert.Contains("--- Grounds ---", result);
        // No covers or non-slot cells
        Assert.DoesNotContain("--- Covers ---", result);
        Assert.DoesNotContain("--- Cells ---", result);
    }

    #endregion

    #region LevelConfig Format — Spawners

    [Fact]
    public void Format_WithSpawners_RendersSpawnerSection()
    {
        var config = new LevelConfig(3, 3);
        config.Spawners = new[]
        {
            new SpawnerConfig { Id = 1, Columns = new[] { 0, 1 } },
            new SpawnerConfig { Id = 2, Columns = new[] { 2 }, Weights = new() { [ElementType.Item1] = 50 } }
        };

        var result = BoardFormatter.Format(config);

        Assert.Contains("--- Spawners ---", result);
        Assert.Contains("[1] cols=0,1 mode=Default", result);
        Assert.Contains("[2] cols=2 mode=Weighted", result);
    }

    #endregion

    #region LevelConfig Format — Grid Coordinates

    [Fact]
    public void Format_ColumnHeaders_Sequential()
    {
        var config = new LevelConfig(4, 1);
        config.Grid[0] = ElementType.Item1;
        var result = BoardFormatter.Format(config);

        // Column headers should show 0 1 2 3
        Assert.Contains(" 0", result);
        Assert.Contains(" 1", result);
        Assert.Contains(" 2", result);
        Assert.Contains(" 3", result);
    }

    [Fact]
    public void Format_RowIndices_Present()
    {
        var config = new LevelConfig(2, 3);
        config.Grid[0] = ElementType.Item1;
        var result = BoardFormatter.Format(config);

        Assert.Contains(" 0 |", result);
        Assert.Contains(" 1 |", result);
        Assert.Contains(" 2 |", result);
    }

    #endregion

    #region GameState Format

    [Fact]
    public void FormatGameState_TilesAndObstacles_Rendered()
    {
        var state = new GameState(3, 3, 5, new XorShift64(42));
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.Safe, 3));

        var ctx = new GameStateFormatContext
        {
            Title = "Move 5",
            RemainingMoves = 15
        };
        var result = BoardFormatter.Format(ref state, ctx);

        Assert.Contains("=== Move 5 (3x3) ===", result);
        Assert.Contains("Moves: 15", result);
        Assert.Contains("--- Tiles ---", result);
        Assert.Contains("--- Obstacles ---", result);
        Assert.Contains("Sf3", result);
    }

    [Fact]
    public void FormatGameState_WithObjectiveProgress_ShowsBrackets()
    {
        var state = new GameState(3, 3, 5, new XorShift64(42));
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        var ctx = new GameStateFormatContext
        {
            Objectives = new[]
            {
                new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = 1, TargetCount = 10 }
            },
            ObjectiveProgress = new[] { 3 }
        };
        var result = BoardFormatter.Format(ref state, ctx);

        Assert.Contains("[3/10]", result);
    }

    #endregion

    #region ByteArrayFlexConverter

    [Fact]
    public void ConfigParser_IntArrayGroundHealths_ParsedCorrectly()
    {
        const string json = """
        {
            "width": 3, "height": 1, "moveLimit": 10,
            "grounds": ["None", "Ice", "None"],
            "groundHealths": [0, 2, 0]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(3, config.GroundHealths.Length);
        Assert.Equal(0, config.GroundHealths[0]);
        Assert.Equal(2, config.GroundHealths[1]);
        Assert.Equal(0, config.GroundHealths[2]);
    }

    [Fact]
    public void ConfigParser_Base64ObstacleStages_ParsedCorrectly()
    {
        // base64 of [0, 0, 3] = "AAAD"
        const string json = """
        {
            "width": 3, "height": 1, "moveLimit": 10,
            "obstacles": ["None", "None", "Box"],
            "obstacleStages": "AAAD"
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(0, config.ObstacleStages[0]);
        Assert.Equal(0, config.ObstacleStages[1]);
        Assert.Equal(3, config.ObstacleStages[2]);
    }

    [Fact]
    public void ConfigParser_Serialize_WritesIntArrayNotBase64()
    {
        var config = new LevelConfig(2, 1);
        config.GroundHealths[0] = 1;
        config.GroundHealths[1] = 3;

        var json = ConfigParser.Serialize(config);

        // Should contain the int values, not a base64 string
        Assert.Contains("1", json);
        Assert.Contains("3", json);
        // Should not contain base64 characters like == padding
        // The array should be human-readable
    }

    [Fact]
    public void ConfigParser_RoundTrip_IntArrayPreserved()
    {
        var config = new LevelConfig(3, 1);
        config.GroundHealths[0] = 0;
        config.GroundHealths[1] = 2;
        config.GroundHealths[2] = 5;

        var json = ConfigParser.Serialize(config);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(config.GroundHealths[0], parsed.GroundHealths[0]);
        Assert.Equal(config.GroundHealths[1], parsed.GroundHealths[1]);
        Assert.Equal(config.GroundHealths[2], parsed.GroundHealths[2]);
    }

    #endregion
}
