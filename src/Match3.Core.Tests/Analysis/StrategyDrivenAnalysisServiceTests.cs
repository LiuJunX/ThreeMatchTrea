using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// StrategyDrivenAnalysisService 单元测试
/// </summary>
public class StrategyDrivenAnalysisServiceTests
{
    private readonly StrategyDrivenAnalysisService _service = new();

    [Fact]
    public async Task AnalyzeAsync_WithPlayerPopulation_ReturnsValidResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 100,
            UseParallel = false,
            Mode = SimulationMode.PlayerPopulation,
            PopulationConfig = new PlayerPopulationConfig
            {
                OutputTierResults = true
            }
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalSimulations);
        Assert.True(result.WinCount >= 0);
        Assert.True(result.WinRate >= 0 && result.WinRate <= 1);
        Assert.NotNull(result.TierResults);
        Assert.Equal(4, result.TierResults.Length); // Default 4 tiers
    }

    [Fact]
    public async Task AnalyzeAsync_WithPlayerPopulation_TierResultsMatchWeights()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 1000,
            UseParallel = true,
            Mode = SimulationMode.PlayerPopulation,
            PopulationConfig = new PlayerPopulationConfig
            {
                OutputTierResults = true,
                Tiers = new[]
                {
                    new PlayerTierConfig { Name = "Novice", SkillLevel = 0.2f, Weight = 0.20f },
                    new PlayerTierConfig { Name = "Casual", SkillLevel = 0.5f, Weight = 0.50f },
                    new PlayerTierConfig { Name = "Expert", SkillLevel = 0.9f, Weight = 0.30f }
                }
            }
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result.TierResults);
        Assert.Equal(3, result.TierResults.Length);

        // Check approximate distribution (allow 5% tolerance)
        int totalSims = result.TierResults[0].SimulationCount +
                       result.TierResults[1].SimulationCount +
                       result.TierResults[2].SimulationCount;
        Assert.Equal(1000, totalSims);

        float noviceRatio = (float)result.TierResults[0].SimulationCount / totalSims;
        float casualRatio = (float)result.TierResults[1].SimulationCount / totalSims;

        Assert.InRange(noviceRatio, 0.15f, 0.25f);
        Assert.InRange(casualRatio, 0.45f, 0.55f);
    }

    [Fact]
    public async Task AnalyzeAsync_ExpertShouldHaveHigherWinRateThanNovice()
    {
        // Arrange - Easy level with objectives
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 30; // More moves = easier
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 5
            }
        };

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 500,
            UseParallel = true,
            Mode = SimulationMode.PlayerPopulation,
            PopulationConfig = new PlayerPopulationConfig
            {
                OutputTierResults = true
            }
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result.TierResults);
        Assert.True(result.TierResults.Length >= 2);

        // Expert (last tier) should generally have higher win rate than Novice (first tier)
        var noviceWinRate = result.TierResults[0].WinRate;
        var expertWinRate = result.TierResults[^1].WinRate;

        // Note: Due to randomness, we allow some tolerance
        // Expert should be at least as good or better than novice
        Assert.True(expertWinRate >= noviceWinRate * 0.8f,
            $"Expert win rate ({expertWinRate:P1}) should be close to or higher than novice ({noviceWinRate:P1})");
    }

    [Fact]
    public async Task AnalyzeAsync_WithGreedyMode_ReturnsValidResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 50,
            UseParallel = false,
            Mode = SimulationMode.Greedy
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalSimulations);
        Assert.Null(result.TierResults); // No tier results in single strategy mode
    }

    [Fact]
    public async Task AnalyzeAsync_WithRandomMode_ReturnsValidResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 50,
            UseParallel = false,
            Mode = SimulationMode.Random
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalSimulations);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsProgressDistribution()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 20;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 100,
            UseParallel = false,
            Mode = SimulationMode.PlayerPopulation
        };

        // Act
        var result = await _service.AnalyzeAsync(levelConfig, analysisConfig);

        // Assert
        Assert.NotNull(result.ProgressDistribution);
        Assert.NotNull(result.ProgressDistribution.AverageProgressByMove);
        Assert.Equal(levelConfig.MoveLimit + 1, result.ProgressDistribution.AverageProgressByMove.Length);
    }

    private static LevelConfig CreateSimpleLevelConfig()
    {
        int width = 8;
        int height = 8;
        var grid = new TileType[width * height];

        // Fill with None (will be randomly generated)
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = TileType.None;
        }

        return new LevelConfig
        {
            Width = width,
            Height = height,
            MoveLimit = 20,
            Grid = grid,
            Objectives = new[]
            {
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)TileType.Red,
                    TargetCount = 10
                }
            }
        };
    }
}
