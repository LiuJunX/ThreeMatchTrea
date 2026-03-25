using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class LevelGeneratorTests
{
    private readonly LevelGenerator _generator = new();
    private readonly ProgressionBlueprint _blueprint;

    public LevelGeneratorTests()
    {
        _blueprint = LoadRealBlueprint();
    }

    #region Basic Generation

    [Fact]
    public void Generate_TutorialLevel_ProducesValidConfig()
    {
        var result = _generator.Generate(_blueprint, 1, 42);
        var config = result.Config;

        Assert.True(config.Width >= 7 && config.Width <= 8);
        Assert.True(config.Height >= 7 && config.Height <= 8);
        Assert.Equal(config.Width * config.Height, config.Grid.Length);
        Assert.Equal(config.Width * config.Height, config.Cells.Length);
        Assert.Equal("Tutorial", result.PhaseName);
    }

    [Fact]
    public void Generate_TutorialLevel_NoObstaclesOrCoversOrGrounds()
    {
        var result = _generator.Generate(_blueprint, 1, 42);
        var config = result.Config;

        // Tutorial phase has no element unlocks
        Assert.All(config.Obstacles, o => Assert.Equal(ObstacleType.None, o));
        Assert.All(config.Covers, c => Assert.Equal(CoverType.None, c));
        Assert.All(config.Grounds, g => Assert.Equal(GroundType.None, g));
    }

    [Fact]
    public void Generate_MidGameLevel_HasObstacles()
    {
        // Level 60 is in MidGame phase, which has obstacles unlocked
        var result = _generator.Generate(_blueprint, 60, 42);
        var config = result.Config;

        bool hasObstacle = config.Obstacles.Any(o => o != ObstacleType.None);
        bool hasCover = config.Covers.Any(c => c != CoverType.None);

        // At least one of these should be present (difficulty > 0 with available elements)
        Assert.True(hasObstacle || hasCover,
            "MidGame level should have some obstacles or covers");
    }

    #endregion

    #region Determinism

    [Fact]
    public void Generate_SameSeed_IdenticalResults()
    {
        var result1 = _generator.Generate(_blueprint, 50, 12345);
        var result2 = _generator.Generate(_blueprint, 50, 12345);

        Assert.Equal(result1.Config.Width, result2.Config.Width);
        Assert.Equal(result1.Config.Height, result2.Config.Height);
        Assert.Equal(result1.Config.MoveLimit, result2.Config.MoveLimit);
        Assert.Equal(result1.Config.TargetDifficulty, result2.Config.TargetDifficulty);
        Assert.Equal(result1.Config.Grid, result2.Config.Grid);
        Assert.Equal(result1.Config.Obstacles, result2.Config.Obstacles);
        Assert.Equal(result1.Config.Covers, result2.Config.Covers);
        Assert.Equal(result1.Config.Grounds, result2.Config.Grounds);
    }

    [Fact]
    public void Generate_DifferentSeeds_DifferentResults()
    {
        var result1 = _generator.Generate(_blueprint, 50, 111);
        var result2 = _generator.Generate(_blueprint, 50, 222);

        // At least some field should differ
        bool differs = result1.Config.Width != result2.Config.Width
                    || result1.Config.MoveLimit != result2.Config.MoveLimit
                    || !result1.Config.Obstacles.SequenceEqual(result2.Config.Obstacles);

        Assert.True(differs, "Different seeds should produce different levels");
    }

    #endregion

    #region Array Integrity

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(200)]
    public void Generate_AllArraysSameSize(int levelNumber)
    {
        var config = _generator.Generate(_blueprint, levelNumber, 42).Config;
        int expected = config.Width * config.Height;

        Assert.Equal(expected, config.Grid.Length);
        Assert.Equal(expected, config.Cells.Length);
        Assert.Equal(expected, config.Obstacles.Length);
        Assert.Equal(expected, config.ObstacleStages.Length);
        Assert.Equal(expected, config.ObstacleStates.Length);
        Assert.Equal(expected, config.Covers.Length);
        Assert.Equal(expected, config.CoverHealths.Length);
        Assert.Equal(expected, config.Grounds.Length);
        Assert.Equal(expected, config.GroundHealths.Length);
        Assert.Equal(expected, config.TileStages.Length);
    }

    #endregion

    #region Placement Constraints

    [Theory]
    [InlineData(30)]
    [InlineData(80)]
    [InlineData(150)]
    public void Generate_VoidCellsHaveNoContent(int levelNumber)
    {
        var config = _generator.Generate(_blueprint, levelNumber, 42).Config;

        for (int i = 0; i < config.Cells.Length; i++)
        {
            if (config.Cells[i] == CellKind.Void)
            {
                Assert.Equal(ElementType.None, config.Grid[i]);
                Assert.Equal(ObstacleType.None, config.Obstacles[i]);
                Assert.Equal(CoverType.None, config.Covers[i]);
                Assert.Equal(GroundType.None, config.Grounds[i]);
            }
        }
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(180)]
    public void Generate_ObstacleCellsHaveNoTile(int levelNumber)
    {
        var config = _generator.Generate(_blueprint, levelNumber, 42).Config;

        for (int i = 0; i < config.Obstacles.Length; i++)
        {
            if (config.Obstacles[i] != ObstacleType.None)
            {
                Assert.Equal(ElementType.None, config.Grid[i]);
            }
        }
    }

    [Fact]
    public void Generate_SpawnerCellsHaveNoObstacles()
    {
        var config = _generator.Generate(_blueprint, 80, 42).Config;

        for (int i = 0; i < config.Cells.Length; i++)
        {
            if (config.Cells[i] == CellKind.Spawner)
            {
                Assert.Equal(ObstacleType.None, config.Obstacles[i]);
            }
        }
    }

    #endregion

    #region Objectives

    [Theory]
    [InlineData(5)]
    [InlineData(40)]
    [InlineData(120)]
    public void Generate_HasAtLeastOneObjective(int levelNumber)
    {
        var config = _generator.Generate(_blueprint, levelNumber, 42).Config;

        bool hasObjective = config.Objectives.Any(o => o.TargetCount > 0);
        Assert.True(hasObjective, "Every level must have at least one objective");
    }

    #endregion

    #region Moves

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void Generate_MovesInPhaseRange(int levelNumber)
    {
        var result = _generator.Generate(_blueprint, levelNumber, 42);
        var phase = BlueprintValidator.FindPhase(_blueprint, levelNumber)!;

        Assert.InRange(result.Config.MoveLimit, phase.MinMoves, phase.MaxMoves);
    }

    #endregion

    #region Design Notes

    [Fact]
    public void Generate_ProducesDesignNotes()
    {
        var result = _generator.Generate(_blueprint, 50, 42);

        Assert.False(string.IsNullOrEmpty(result.DesignNotes));
        Assert.Contains("Rhythm:", result.DesignNotes);
        Assert.Contains("Board:", result.DesignNotes);
        Assert.Contains("Objectives:", result.DesignNotes);
    }

    #endregion

    #region Batch Generation

    [Fact]
    public void Generate_AllLevels_NoExceptions()
    {
        for (int level = 1; level <= 200; level++)
        {
            var result = _generator.Generate(_blueprint, level, (ulong)(level * 7919));
            Assert.NotNull(result.Config);
            Assert.True(result.Config.MoveLimit > 0);
        }
    }

    [Fact]
    public void Generate_DifficultyTrend_IncreaseAcrossPhases()
    {
        var avgDiffByPhase = new Dictionary<string, float>();

        foreach (var phase in _blueprint.Phases)
        {
            float sum = 0;
            int count = 0;
            for (int level = phase.StartLevel; level <= Math.Min(phase.EndLevel, phase.StartLevel + 9); level++)
            {
                var result = _generator.Generate(_blueprint, level, (ulong)(level * 31));
                sum += result.Difficulty;
                count++;
            }
            avgDiffByPhase[phase.Name] = sum / count;
        }

        // Verify trend: each phase's average difficulty >= previous
        float prev = 0;
        foreach (var phase in _blueprint.Phases)
        {
            float avg = avgDiffByPhase[phase.Name];
            Assert.True(avg >= prev * 0.8f, // Allow 20% tolerance
                $"Phase '{phase.Name}' avg difficulty ({avg:F2}) should >= previous ({prev:F2})");
            prev = avg;
        }
    }

    #endregion

    #region Design Document

    [Fact]
    public void GenerateDesignDocument_TutorialLevel_ReferencesCoreDocs()
    {
        var result = _generator.Generate(_blueprint, 1, 42);
        var doc = result.GenerateDesignDocument();

        Assert.Contains("# Level 1 设计文档", doc);
        Assert.Contains("Tutorial", doc);
        Assert.Contains("core/matching.md", doc);
        Assert.Contains("core/difficulty-levers.md", doc);
        Assert.Contains("patterns/tutorial-intro.md", doc); // No elements = tutorial
    }

    [Fact]
    public void GenerateDesignDocument_MidGameLevel_ReferencesElementDocs()
    {
        var result = _generator.Generate(_blueprint, 70, 42);
        var doc = result.GenerateDesignDocument();

        Assert.Contains("# Level 70 设计文档", doc);
        Assert.Contains("## 使用的元素", doc);
        Assert.Contains("## 参考知识文档", doc);
        Assert.Contains("## 待迭代", doc);
        Assert.Contains("## 设计发现", doc);

        // Should reference element docs if elements are used
        if (result.UsedObstacles.Count > 0)
            Assert.Contains("elements/obstacle-", doc);
    }

    [Fact]
    public void GenerateDesignDocument_BossLevel_ReferencesBossPattern()
    {
        // Level 90 is a Boss (BossEveryN=10)
        var result = _generator.Generate(_blueprint, 90, 42);
        if (result.Rhythm == RhythmCategory.Boss)
        {
            var doc = result.GenerateDesignDocument();
            Assert.Contains("patterns/boss-level.md", doc);
            Assert.Contains("Boss 关", doc);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_InvalidLevel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _generator.Generate(_blueprint, 999, 42));
    }

    #endregion

    #region Helpers

    private static ProgressionBlueprint LoadRealBlueprint()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            dir = Directory.GetParent(dir)?.FullName;
        dir ??= Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
        var json = File.ReadAllText(Path.Combine(dir, "config", "progression_blueprint.json"));
        return ConfigParser.ParseProgressionBlueprint(json);
    }

    #endregion
}
