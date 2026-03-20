using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Editor.Validation;

namespace Match3.Editor.Tests.Validation;

public class ObjectiveConsistencyCheckerTests
{
    private readonly ObjectiveConsistencyChecker _checker = new();

    private static LevelConfig MakeLevel(int w = 8, int h = 8)
    {
        var config = new LevelConfig(w, h) { MoveLimit = 20 };
        for (int i = 0; i < config.Grid.Length; i++)
            config.Grid[i] = ElementType.Item1;
        return config;
    }

    [Fact]
    public void Ground_objective_with_no_ground_returns_error()
    {
        var config = MakeLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 4
        };

        var messages = _checker.Check(config).ToList();

        Assert.Single(messages);
        Assert.Equal(Severity.Error, messages[0].Severity);
        Assert.Contains("Ground/Ice", messages[0].Message);
        Assert.Contains("none placed", messages[0].Message);
    }

    [Fact]
    public void Ground_objective_exceeding_placed_count_returns_warning()
    {
        var config = MakeLevel();
        // Place 4 ice blocks
        config.Grounds[0] = GroundType.Ice;
        config.Grounds[1] = GroundType.Ice;
        config.Grounds[2] = GroundType.Ice;
        config.Grounds[3] = GroundType.Ice;
        config.GroundHealths[0] = 1;
        config.GroundHealths[1] = 1;
        config.GroundHealths[2] = 1;
        config.GroundHealths[3] = 1;

        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 9
        };

        var messages = _checker.Check(config).ToList();

        Assert.Single(messages);
        Assert.Equal(Severity.Warning, messages[0].Severity);
        Assert.Contains("targets 9", messages[0].Message);
        Assert.Contains("only 4", messages[0].Message);
    }

    [Fact]
    public void Ground_objective_matching_placed_count_returns_no_messages()
    {
        var config = MakeLevel();
        config.Grounds[0] = GroundType.Ice;
        config.Grounds[1] = GroundType.Ice;
        config.GroundHealths[0] = 1;
        config.GroundHealths[1] = 1;

        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 2
        };

        var messages = _checker.Check(config).ToList();
        Assert.Empty(messages);
    }

    [Fact]
    public void Cover_objective_with_no_cover_returns_error()
    {
        var config = MakeLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Cage,
            TargetCount = 5
        };

        var messages = _checker.Check(config).ToList();

        Assert.Single(messages);
        Assert.Equal(Severity.Error, messages[0].Severity);
        Assert.Contains("Cover/Cage", messages[0].Message);
    }

    [Fact]
    public void Obstacle_objective_with_no_obstacle_returns_error()
    {
        var config = MakeLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.Box,
            TargetCount = 3
        };

        var messages = _checker.Check(config).ToList();

        Assert.Single(messages);
        Assert.Equal(Severity.Error, messages[0].Severity);
        Assert.Contains("Obstacle/Box", messages[0].Message);
    }

    [Fact]
    public void Tile_color_objective_returns_no_messages()
    {
        var config = MakeLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 20
        };

        var messages = _checker.Check(config).ToList();
        Assert.Empty(messages);
    }

    [Fact]
    public void Inactive_objective_is_skipped()
    {
        var config = MakeLevel();
        // All objectives default to None — no messages expected
        var messages = _checker.Check(config).ToList();
        Assert.Empty(messages);
    }

    [Fact]
    public void Obstacle_objective_with_matching_placement_returns_no_messages()
    {
        var config = MakeLevel();
        config.Obstacles[0] = ObstacleType.Box;
        config.Obstacles[1] = ObstacleType.Box;
        config.ObstacleStages[0] = 1;
        config.ObstacleStages[1] = 1;

        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.Box,
            TargetCount = 2
        };

        var messages = _checker.Check(config).ToList();
        Assert.Empty(messages);
    }

    [Fact]
    public void Collectible_tile_objective_is_skipped()
    {
        var config = MakeLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Bird,
            TargetCount = 5
        };

        var messages = _checker.Check(config).ToList();
        Assert.Empty(messages);
    }

    [Fact]
    public void Multiple_objectives_mixed_validity()
    {
        var config = MakeLevel();
        // Valid: color tile objective
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10
        };
        // Invalid: ground objective with no ground
        config.Objectives[1] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 5
        };

        var messages = _checker.Check(config).ToList();
        Assert.Single(messages);
        Assert.Contains("Ground/Ice", messages[0].Message);
    }
}
