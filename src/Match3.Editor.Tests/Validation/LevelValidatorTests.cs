using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Editor.Validation;

namespace Match3.Editor.Tests.Validation;

public class LevelValidatorTests
{
    private static LevelConfig MakeValidLevel()
    {
        var config = new LevelConfig(4, 4);
        for (int i = 0; i < config.Grid.Length; i++)
            config.Grid[i] = ElementType.Item1;
        config.MoveLimit = 10;
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 5
        };
        return config;
    }

    [Fact]
    public void Valid_level_passes()
    {
        var validator = new LevelValidator();
        var result = validator.Validate(MakeValidLevel());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Empty_board_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        for (int i = 0; i < config.Grid.Length; i++)
            config.Grid[i] = ElementType.None;

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Message.Contains("no playable cells"));
    }

    [Fact]
    public void All_holes_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        for (int i = 0; i < config.Grid.Length; i++)
        {
            config.Grid[i] = ElementType.None;
            config.Cells[i] = CellKind.Void;
        }

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void No_objectives_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        for (int i = 0; i < config.Objectives.Length; i++)
            config.Objectives[i] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Message.Contains("objective"));
    }

    [Fact]
    public void Zero_target_count_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 0
        };

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Message.Contains("target count"));
    }

    [Fact]
    public void Zero_move_limit_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        config.MoveLimit = 0;

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Message.Contains("Move limit"));
    }

    [Fact]
    public void Grid_size_mismatch_fails()
    {
        var validator = new LevelValidator();
        var config = MakeValidLevel();
        config.Width = 5; // mismatch: grid is 4*4=16, but 5*4=20

        var result = validator.Validate(config);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, m => m.Message.Contains("does not match"));
    }
}
