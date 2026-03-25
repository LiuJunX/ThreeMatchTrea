using System.IO;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class EffectivePoolTests
{
    #region Single Phase

    [Fact]
    public void Build_SinglePhase_PoolEqualsPhase()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "Tutorial", StartLevel = 1, EndLevel = 10,
                    MinBoardWidth = 7, MaxBoardWidth = 8,
                    MinColors = 4, MaxColors = 5,
                    MinDifficulty = 0.0f, MaxDifficulty = 0.2f,
                    MinMoves = 15, MaxMoves = 25,
                    MaxObjectives = 1,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 1, MaxCount = 5 }
                    }
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, 5);

        Assert.Equal(7, pool.MinBoardWidth);
        Assert.Equal(8, pool.MaxBoardWidth);
        Assert.Equal(4, pool.MinColors);
        Assert.Equal(0.2f, pool.MaxDifficulty);
        Assert.Equal(1, pool.MaxObjectives);
        Assert.Single(pool.Obstacles);
        Assert.Equal(1, pool.Obstacles[ObstacleType.Box].MaxStage);
    }

    #endregion

    #region Multi-Phase Accumulation

    [Fact]
    public void Build_MultiPhase_AccumulatesObstacles()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "A", StartLevel = 1, EndLevel = 10,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 1, MaxCount = 5 }
                    }
                },
                new PhaseConfig
                {
                    Name = "B", StartLevel = 11, EndLevel = 20,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Bush, MaxStage = 2, MaxCount = 6 }
                    }
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, 15);

        // Phase B has both Box (from A) and Bush (from B)
        Assert.Equal(2, pool.Obstacles.Count);
        Assert.True(pool.Obstacles.ContainsKey(ObstacleType.Box));
        Assert.True(pool.Obstacles.ContainsKey(ObstacleType.Bush));
    }

    [Fact]
    public void Build_LaterPhaseOverridesMaxStage()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "A", StartLevel = 1, EndLevel = 10,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 2, MaxCount = 8 }
                    }
                },
                new PhaseConfig
                {
                    Name = "B", StartLevel = 11, EndLevel = 20,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 4, MaxCount = 15 }
                    }
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, 15);

        Assert.Single(pool.Obstacles);
        Assert.Equal(4, pool.Obstacles[ObstacleType.Box].MaxStage);
        Assert.Equal(15, pool.Obstacles[ObstacleType.Box].MaxCount);
    }

    [Fact]
    public void Build_BoardParamsFromCurrentPhaseOnly()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "A", StartLevel = 1, EndLevel = 10,
                    MinBoardWidth = 7, MaxBoardWidth = 8, MinColors = 4, MaxColors = 5
                },
                new PhaseConfig
                {
                    Name = "B", StartLevel = 11, EndLevel = 20,
                    MinBoardWidth = 8, MaxBoardWidth = 10, MinColors = 5, MaxColors = 6
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, 15);

        // Board params come from phase B, not accumulated
        Assert.Equal(8, pool.MinBoardWidth);
        Assert.Equal(10, pool.MaxBoardWidth);
        Assert.Equal(5, pool.MinColors);
        Assert.Equal(6, pool.MaxColors);
    }

    #endregion

    #region DoesNotAccumulateBeyondCurrentPhase

    [Fact]
    public void Build_DoesNotIncludeFuturePhaseUnlocks()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "A", StartLevel = 1, EndLevel = 10,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 1 }
                    }
                },
                new PhaseConfig
                {
                    Name = "B", StartLevel = 11, EndLevel = 20,
                    Obstacles = new[]
                    {
                        new ObstacleAllowance { Type = ObstacleType.Safe, MaxStage = 3 }
                    }
                }
            }
        };

        // Level 5 is in phase A — should NOT have Safe from phase B
        var pool = EffectivePool.Build(blueprint, 5);

        Assert.Single(pool.Obstacles);
        Assert.True(pool.Obstacles.ContainsKey(ObstacleType.Box));
        Assert.False(pool.Obstacles.ContainsKey(ObstacleType.Safe));
    }

    #endregion

    #region All Element Types

    [Fact]
    public void Build_AccumulatesAllElementTypes()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "A", StartLevel = 1, EndLevel = 10,
                    Covers = new[] { new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 10 } },
                    Grounds = new[] { new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 } }
                },
                new PhaseConfig
                {
                    Name = "B", StartLevel = 11, EndLevel = 20,
                    Covers = new[] { new CoverAllowance { Type = CoverType.Chain, MaxHealth = 2, MaxCount = 12 } },
                    Collectibles = new[] { new CollectibleAllowance { Type = ElementType.Bird, MaxCount = 5 } },
                    MovingObstacles = new[] { new MovingObstacleAllowance { Type = ElementType.RoyalEgg, MaxStage = 1, MaxCount = 4 } }
                }
            }
        };

        var pool = EffectivePool.Build(blueprint, 15);

        Assert.Equal(2, pool.Covers.Count);  // Cage + Chain
        Assert.Single(pool.Grounds);         // Ice
        Assert.Single(pool.Collectibles);    // Bird
        Assert.Single(pool.MovingObstacles); // RoyalEgg
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Build_LevelOutOfRange_Throws()
    {
        var blueprint = new ProgressionBlueprint
        {
            Phases = new[] { new PhaseConfig { Name = "A", StartLevel = 1, EndLevel = 10 } }
        };

        Assert.Throws<System.ArgumentOutOfRangeException>(() => EffectivePool.Build(blueprint, 20));
    }

    #endregion

    #region Real Blueprint

    [Fact]
    public void Build_RealBlueprint_TutorialHasNoElements()
    {
        var blueprint = LoadRealBlueprint();

        var pool = EffectivePool.Build(blueprint, 1);

        Assert.Empty(pool.Obstacles);
        Assert.Empty(pool.Covers);
        Assert.Empty(pool.Grounds);
        Assert.Empty(pool.Collectibles);
        Assert.Empty(pool.MovingObstacles);
    }

    [Fact]
    public void Build_RealBlueprint_ExpertHasAllElements()
    {
        var blueprint = LoadRealBlueprint();

        var pool = EffectivePool.Build(blueprint, 200);

        // Expert phase should have accumulated all obstacle types
        Assert.True(pool.Obstacles.Count >= 10, $"Expected >= 10 obstacles, got {pool.Obstacles.Count}");
        Assert.True(pool.Covers.Count >= 4, $"Expected >= 4 covers, got {pool.Covers.Count}");
        Assert.True(pool.Grounds.Count >= 3, $"Expected >= 3 grounds, got {pool.Grounds.Count}");
        Assert.True(pool.Collectibles.Count >= 4, $"Expected >= 4 collectibles, got {pool.Collectibles.Count}");
        Assert.True(pool.MovingObstacles.Count >= 4, $"Expected >= 4 moving obstacles, got {pool.MovingObstacles.Count}");
    }

    [Fact]
    public void Build_RealBlueprint_ElementCountGrowsAcrossPhases()
    {
        var blueprint = LoadRealBlueprint();
        int prevTotal = 0;

        foreach (var phase in blueprint.Phases)
        {
            var pool = EffectivePool.Build(blueprint, phase.StartLevel);
            int total = pool.Obstacles.Count + pool.Covers.Count + pool.Grounds.Count
                      + pool.Collectibles.Count + pool.MovingObstacles.Count;
            Assert.True(total >= prevTotal,
                $"Phase '{phase.Name}' total elements ({total}) should >= previous ({prevTotal})");
            prevTotal = total;
        }
    }

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
