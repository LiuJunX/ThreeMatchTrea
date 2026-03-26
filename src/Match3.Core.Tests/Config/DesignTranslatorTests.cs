using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class DesignTranslatorTests
{
    private static LevelDesignContext MakeContext(int levelNumber = 15)
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "EarlyGame",
                    StartLevel = 11,
                    EndLevel = 30,
                    MinBoardWidth = 7, MaxBoardWidth = 9,
                    MinBoardHeight = 7, MaxBoardHeight = 9,
                    Shapes = new[] { "rectangle", "cross" },
                    MinColors = 4, MaxColors = 5,
                    Covers = new[]
                    {
                        new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 12 }
                    },
                    Grounds = new[]
                    {
                        new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 }
                    },
                    MaxObjectives = 2,
                    ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile, ObjectiveTargetLayer.Ground, ObjectiveTargetLayer.Cover },
                    MinDifficulty = 0.1f, MaxDifficulty = 0.35f,
                    MinWinRate = 0.75f, MaxWinRate = 0.92f,
                    MinMoves = 18, MaxMoves = 28
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, levelNumber);
        var phase = blueprint.Phases[0];

        return new LevelDesignContext
        {
            Blueprint = blueprint,
            LevelNumber = levelNumber,
            Pool = pool,
            Phase = phase
        };
    }

    private static LevelDesign MakeSimpleDesign()
    {
        return new LevelDesign
        {
            Width = 8,
            Height = 8,
            Shape = "rectangle",
            ColorCount = 4,
            Rhythm = RhythmCategory.Normal,
            Difficulty = 0.2f,
            MoveLimit = 22,
            Covers = new List<ElementPlacement>
            {
                new ElementPlacement
                {
                    ElementType = "Cage",
                    Count = 6,
                    Stage = 1,
                    Strategy = "border"
                }
            },
            Grounds = new List<ElementPlacement>
            {
                new ElementPlacement
                {
                    ElementType = "Ice",
                    Count = 9,
                    Stage = 1,
                    Strategy = "center"
                }
            },
            Objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    TargetLayer = "Cover",
                    ElementType = "Cage",
                    TargetCount = 6
                },
                new DesignObjective
                {
                    TargetLayer = "Ground",
                    ElementType = "Ice",
                    TargetCount = 9
                }
            },
            DesignIntent = "教学关：首次引入 Cage"
        };
    }

    #region Valid Translation

    [Fact]
    public void Translate_ValidDesign_ProducesValidConfig()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        Assert.NotNull(result.Config);
        Assert.Equal(8, result.Config.Width);
        Assert.Equal(8, result.Config.Height);
        Assert.Equal(22, result.Config.MoveLimit);
        Assert.Equal("EarlyGame", result.PhaseName);
        Assert.Equal(15, result.LevelNumber);
    }

    [Fact]
    public void Translate_PlacesCagesNotOnSpawners()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        // Verify no covers on spawner cells
        for (int i = 0; i < result.Config.Cells.Length; i++)
        {
            if (result.Config.Cells[i] == CellKind.Spawner)
                Assert.Equal(CoverType.None, result.Config.Covers[i]);
        }
    }

    [Fact]
    public void Translate_PlacesCovers()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        int cageCount = result.Config.Covers.Count(c => c == CoverType.Cage);
        Assert.True(cageCount > 0, "Should have placed some cages");
        Assert.True(cageCount <= 6, "Should not exceed requested count");
    }

    [Fact]
    public void Translate_PlacesGrounds()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        int iceCount = result.Config.Grounds.Count(g => g == GroundType.Ice);
        Assert.True(iceCount > 0, "Should have placed some ice");
    }

    [Fact]
    public void Translate_GroundsNotOnObstacles()
    {
        var ctx = MakeContext();
        // Add obstacles to the design
        var design = MakeSimpleDesign();
        design.Obstacles.Add(new ElementPlacement
        {
            ElementType = "Box",
            Count = 4,
            Stage = 1,
            Strategy = "center"
        });

        // Need a context with Box allowed
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "Test", StartLevel = 1, EndLevel = 50,
                    Obstacles = new[] { new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 2, MaxCount = 10 } },
                    Covers = new[] { new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 12 } },
                    Grounds = new[] { new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 } },
                    MinMoves = 15, MaxMoves = 30
                }
            }
        };
        var ctxWithObs = new LevelDesignContext
        {
            Blueprint = blueprint,
            LevelNumber = 5,
            Pool = EffectivePool.Build(blueprint, 5),
            Phase = blueprint.Phases[0]
        };

        var result = DesignTranslator.Translate(design, ctxWithObs, 42);

        // No ground should be placed on a cell that has an obstacle
        for (int i = 0; i < result.Config.Obstacles.Length; i++)
        {
            if (result.Config.Obstacles[i] != ObstacleType.None)
                Assert.Equal(GroundType.None, result.Config.Grounds[i]);
        }
    }

    #endregion

    #region Objectives

    [Fact]
    public void Translate_PreservesObjectives()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        // First objective should be Cover/Cage
        var obj0 = result.Config.Objectives[0];
        Assert.Equal(ObjectiveTargetLayer.Cover, obj0.TargetLayer);
        Assert.Equal((int)CoverType.Cage, obj0.ElementType);
        Assert.Equal(6, obj0.TargetCount);

        // Second objective should be Ground/Ice
        var obj1 = result.Config.Objectives[1];
        Assert.Equal(ObjectiveTargetLayer.Ground, obj1.TargetLayer);
        Assert.Equal((int)GroundType.Ice, obj1.ElementType);
        Assert.Equal(9, obj1.TargetCount);
    }

    #endregion

    #region Clamping

    [Fact]
    public void Translate_ClampsMovesToPoolRange()
    {
        var ctx = MakeContext(); // MinMoves=18, MaxMoves=28
        var design = MakeSimpleDesign();
        design.MoveLimit = 50; // Exceeds max

        var result = DesignTranslator.Translate(design, ctx, 42);

        Assert.Equal(28, result.Config.MoveLimit);
    }

    [Fact]
    public void Translate_ClampsCountToAllowance()
    {
        var ctx = MakeContext(); // Cage maxCount = 12
        var design = MakeSimpleDesign();
        design.Covers[0].Count = 20; // Exceeds allowance

        var result = DesignTranslator.Translate(design, ctx, 42);

        int cageCount = result.Config.Covers.Count(c => c == CoverType.Cage);
        Assert.True(cageCount <= 12);
    }

    #endregion

    #region Unknown Types

    [Fact]
    public void Translate_UnknownObstacleType_Skipped()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();
        design.Obstacles.Add(new ElementPlacement
        {
            ElementType = "NonExistentObstacle",
            Count = 5,
            Strategy = "center"
        });

        // Should not throw
        var result = DesignTranslator.Translate(design, ctx, 42);
        Assert.NotNull(result.Config);
        Assert.Contains("WARNING", result.DesignNotes);
    }

    #endregion

    #region Design Metadata

    [Fact]
    public void Translate_IncludesDesignIntent()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();

        var result = DesignTranslator.Translate(design, ctx, 42);

        Assert.Contains("教学关", result.DesignNotes);
    }

    [Fact]
    public void Translate_SetsRhythmAndDifficulty()
    {
        var ctx = MakeContext();
        var design = MakeSimpleDesign();
        design.Rhythm = RhythmCategory.Hard;
        design.Difficulty = 0.3f;

        var result = DesignTranslator.Translate(design, ctx, 42);

        Assert.Equal(RhythmCategory.Hard, result.Rhythm);
        Assert.Equal(0.3f, result.Difficulty);
    }

    #endregion
}
