using System.IO;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class ProgressionBlueprintTests
{
    #region Parsing

    [Fact]
    public void ParseBlueprint_MinimalJson_ReturnsEmptyPhases()
    {
        var blueprint = ConfigParser.ParseProgressionBlueprint("{}");

        Assert.NotNull(blueprint);
        Assert.Empty(blueprint.Phases);
    }

    [Fact]
    public void ParseBlueprint_SinglePhase_ParsesAllFields()
    {
        const string json = """
        {
            "phases": [{
                "name": "Tutorial",
                "startLevel": 1,
                "endLevel": 10,
                "minBoardWidth": 7, "maxBoardWidth": 8,
                "minBoardHeight": 7, "maxBoardHeight": 8,
                "shapes": ["rectangle"],
                "minColors": 4, "maxColors": 5,
                "obstacles": [
                    { "type": "Box", "maxStage": 2, "maxCount": 8 }
                ],
                "covers": [
                    { "type": "Cage", "maxHealth": 1, "maxCount": 12 }
                ],
                "grounds": [
                    { "type": "Ice", "maxHealth": 2, "maxCount": 15 }
                ],
                "collectibles": [
                    { "type": "Bird", "maxCount": 5 }
                ],
                "movingObstacles": [
                    { "type": "RoyalEgg", "maxStage": 1, "maxCount": 3 }
                ],
                "maxObjectives": 2,
                "objectiveLayers": ["Tile", "Ground"],
                "minDifficulty": 0.1, "maxDifficulty": 0.3,
                "minWinRate": 0.85, "maxWinRate": 0.95,
                "minMoves": 15, "maxMoves": 25,
                "easyRatio": 0.3,
                "hardRatio": 0.1,
                "bossEveryN": 10
            }]
        }
        """;

        var blueprint = ConfigParser.ParseProgressionBlueprint(json);

        Assert.Single(blueprint.Phases);
        var phase = blueprint.Phases[0];

        Assert.Equal("Tutorial", phase.Name);
        Assert.Equal(1, phase.StartLevel);
        Assert.Equal(10, phase.EndLevel);
        Assert.Equal(7, phase.MinBoardWidth);
        Assert.Equal(8, phase.MaxBoardWidth);
        Assert.Equal(new[] { "rectangle" }, phase.Shapes);
        Assert.Equal(4, phase.MinColors);
        Assert.Equal(5, phase.MaxColors);

        // Obstacles
        Assert.Single(phase.Obstacles);
        Assert.Equal(ObstacleType.Box, phase.Obstacles[0].Type);
        Assert.Equal(2, phase.Obstacles[0].MaxStage);
        Assert.Equal(8, phase.Obstacles[0].MaxCount);

        // Covers
        Assert.Single(phase.Covers);
        Assert.Equal(CoverType.Cage, phase.Covers[0].Type);
        Assert.Equal(1, phase.Covers[0].MaxHealth);

        // Grounds
        Assert.Single(phase.Grounds);
        Assert.Equal(GroundType.Ice, phase.Grounds[0].Type);
        Assert.Equal(2, phase.Grounds[0].MaxHealth);

        // Collectibles
        Assert.Single(phase.Collectibles);
        Assert.Equal(ElementType.Bird, phase.Collectibles[0].Type);

        // Moving obstacles
        Assert.Single(phase.MovingObstacles);
        Assert.Equal(ElementType.RoyalEgg, phase.MovingObstacles[0].Type);

        // Objectives
        Assert.Equal(2, phase.MaxObjectives);
        Assert.Equal(new[] { ObjectiveTargetLayer.Tile, ObjectiveTargetLayer.Ground }, phase.ObjectiveLayers);

        // Difficulty
        Assert.Equal(0.1f, phase.MinDifficulty);
        Assert.Equal(0.3f, phase.MaxDifficulty);
        Assert.Equal(0.85f, phase.MinWinRate);
        Assert.Equal(0.95f, phase.MaxWinRate);
        Assert.Equal(15, phase.MinMoves);
        Assert.Equal(25, phase.MaxMoves);

        // Rhythm
        Assert.Equal(0.3f, phase.EasyRatio);
        Assert.Equal(0.1f, phase.HardRatio);
        Assert.Equal(10, phase.BossEveryN);
    }

    [Fact]
    public void ParseBlueprint_RealFile_ParsesAllPhases()
    {
        var projectRoot = FindProjectRoot();
        var path = Path.Combine(projectRoot, "config", "progression_blueprint.json");
        var json = File.ReadAllText(path);

        var blueprint = ConfigParser.ParseProgressionBlueprint(json);

        Assert.Equal(6, blueprint.Phases.Length);
        Assert.Equal("Tutorial", blueprint.Phases[0].Name);
        Assert.Equal("Expert", blueprint.Phases[5].Name);
        Assert.Equal(1, blueprint.Phases[0].StartLevel);
        Assert.Equal(200, blueprint.Phases[5].EndLevel);
    }

    #endregion

    #region Validation

    [Fact]
    public void Validate_RealFile_NoErrors()
    {
        var blueprint = LoadRealBlueprint();

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_OverlappingPhases_ReportsError()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig { Name = "A", StartLevel = 1, EndLevel = 10 },
                new PhaseConfig { Name = "B", StartLevel = 8, EndLevel = 20 }
            }
        };

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Contains(errors, e => e.Contains("overlaps"));
    }

    [Fact]
    public void Validate_GapBetweenPhases_ReportsError()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig { Name = "A", StartLevel = 1, EndLevel = 10 },
                new PhaseConfig { Name = "B", StartLevel = 15, EndLevel = 20 }
            }
        };

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Contains(errors, e => e.Contains("Gap"));
    }

    [Fact]
    public void Validate_InvertedRange_ReportsError()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig { Name = "Bad", StartLevel = 10, EndLevel = 5 }
            }
        };

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Contains(errors, e => e.Contains("startLevel"));
    }

    [Fact]
    public void Validate_BadColorRange_ReportsError()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig { Name = "Bad", StartLevel = 1, EndLevel = 10, MinColors = 6, MaxColors = 4 }
            }
        };

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Contains(errors, e => e.Contains("minColors > maxColors"));
    }

    [Fact]
    public void Validate_RhythmExceeds1_ReportsError()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig { Name = "Bad", StartLevel = 1, EndLevel = 10, EasyRatio = 0.6f, HardRatio = 0.5f }
            }
        };

        var errors = BlueprintValidator.Validate(blueprint);

        Assert.Contains(errors, e => e.Contains("exceeds 1.0"));
    }

    #endregion

    #region FindPhase

    [Fact]
    public void FindPhase_ReturnsCorrectPhase()
    {
        var blueprint = LoadRealBlueprint();

        var phase1 = BlueprintValidator.FindPhase(blueprint, 1);
        var phase5 = BlueprintValidator.FindPhase(blueprint, 5);
        var phaseMid = BlueprintValidator.FindPhase(blueprint, 60);
        var phaseLast = BlueprintValidator.FindPhase(blueprint, 200);

        Assert.Equal("Tutorial", phase1!.Name);
        Assert.Equal("Tutorial", phase5!.Name);
        Assert.Equal("MidGame", phaseMid!.Name);
        Assert.Equal("Expert", phaseLast!.Name);
    }

    [Fact]
    public void FindPhase_OutOfRange_ReturnsNull()
    {
        var blueprint = LoadRealBlueprint();

        Assert.Null(BlueprintValidator.FindPhase(blueprint, 0));
        Assert.Null(BlueprintValidator.FindPhase(blueprint, 201));
    }

    #endregion

    #region Progression Consistency

    [Fact]
    public void RealBlueprint_DifficultyIncreasesAcrossPhases()
    {
        var blueprint = LoadRealBlueprint();

        for (int i = 1; i < blueprint.Phases.Length; i++)
        {
            var prev = blueprint.Phases[i - 1];
            var curr = blueprint.Phases[i];
            Assert.True(curr.MaxDifficulty >= prev.MaxDifficulty,
                $"Phase '{curr.Name}' maxDifficulty ({curr.MaxDifficulty}) should >= Phase '{prev.Name}' ({prev.MaxDifficulty})");
        }
    }

    [Fact]
    public void RealBlueprint_WinRateDecreaseAcrossPhases()
    {
        var blueprint = LoadRealBlueprint();

        for (int i = 1; i < blueprint.Phases.Length; i++)
        {
            var prev = blueprint.Phases[i - 1];
            var curr = blueprint.Phases[i];
            Assert.True(curr.MaxWinRate <= prev.MaxWinRate,
                $"Phase '{curr.Name}' maxWinRate ({curr.MaxWinRate}) should <= Phase '{prev.Name}' ({prev.MaxWinRate})");
        }
    }

    [Fact]
    public void RealBlueprint_PhasesAreContinuous()
    {
        var blueprint = LoadRealBlueprint();

        Assert.Equal(1, blueprint.Phases[0].StartLevel);
        for (int i = 1; i < blueprint.Phases.Length; i++)
        {
            Assert.Equal(blueprint.Phases[i - 1].EndLevel + 1, blueprint.Phases[i].StartLevel);
        }
    }

    [Fact]
    public void RealBlueprint_ElementUnlocksGrowMonotonically()
    {
        var blueprint = LoadRealBlueprint();

        int prevObstacleCount = 0;
        for (int i = 0; i < blueprint.Phases.Length; i++)
        {
            var phase = blueprint.Phases[i];
            int obstacleCount = phase.Obstacles?.Length ?? 0;
            Assert.True(obstacleCount >= prevObstacleCount,
                $"Phase '{phase.Name}' obstacle types ({obstacleCount}) should >= previous ({prevObstacleCount})");
            prevObstacleCount = obstacleCount;
        }
    }

    #endregion

    #region Helpers

    private static ProgressionBlueprint LoadRealBlueprint()
    {
        var projectRoot = FindProjectRoot();
        var path = Path.Combine(projectRoot, "config", "progression_blueprint.json");
        var json = File.ReadAllText(path);
        return ConfigParser.ParseProgressionBlueprint(json);
    }

    private static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "CLAUDE.md")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        // Fallback: assume test runs from bin/Debug/netX
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
    }

    #endregion
}
