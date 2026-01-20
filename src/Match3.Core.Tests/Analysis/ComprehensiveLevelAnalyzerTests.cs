using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Analysis.MCTS;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// ComprehensiveLevelAnalyzer 单元测试
/// </summary>
public class ComprehensiveLevelAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_WithBothMethods_ReturnsComprehensiveResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 10
        };
        var analyzer = new ComprehensiveLevelAnalyzer(mctsConfig);

        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = true,
            MCTSTotalGames = 5,
            PopulationSimulationCount = 50,
            UseParallel = false
        };

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig, config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TheoreticalOptimalWinRate);
        Assert.NotNull(result.PopulationResult);
        Assert.NotNull(result.DifficultyGap);
        Assert.NotNull(result.Suggestions);
        Assert.True(result.Suggestions.Count > 0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutMCTS_SkipsMCTSAnalysis()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analyzer = new ComprehensiveLevelAnalyzer();

        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = false,
            PopulationSimulationCount = 50,
            UseParallel = false
        };

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig, config);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.TheoreticalOptimalWinRate);
        Assert.Null(result.MinMovesToWin);
        Assert.Null(result.DifficultyGap);
        Assert.NotNull(result.PopulationResult);
    }

    [Fact]
    public async Task AnalyzeAsync_GenerateSummary_ReturnsFormattedReport()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 3,
            SimulationsPerMove = 5
        };
        var analyzer = new ComprehensiveLevelAnalyzer(mctsConfig);

        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = true,
            MCTSTotalGames = 3,
            PopulationSimulationCount = 30,
            UseParallel = false
        };

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig, config);
        var summary = result.GenerateSummary();

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("关卡难度分析报告", summary);
        Assert.Contains("整体难度评级", summary);
        Assert.Contains("理论分析", summary);
        Assert.Contains("玩家群体模拟", summary);
        Assert.Contains("调整建议", summary);
    }

    [Fact]
    public async Task AnalyzeAsync_EasyLevel_RatesAsEasyOrBalanced()
    {
        // Arrange - Very easy level
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 50;
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 3
            }
        };

        var analyzer = new ComprehensiveLevelAnalyzer();
        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = false,
            PopulationSimulationCount = 100,
            UseParallel = true
        };

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig, config);

        // Assert
        Assert.True(
            result.OverallDifficultyRating == OverallDifficulty.TooEasy ||
            result.OverallDifficultyRating == OverallDifficulty.Easy ||
            result.OverallDifficultyRating == OverallDifficulty.Balanced,
            $"Expected easy level to be rated as Easy/TooEasy/Balanced, got {result.OverallDifficultyRating}");
    }

    [Fact]
    public async Task AnalyzeAsync_HardLevel_RatesAsHardOrChallenging()
    {
        // Arrange - Hard level
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 10; // Few moves
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 50 // High target
            }
        };

        var analyzer = new ComprehensiveLevelAnalyzer();
        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = false,
            PopulationSimulationCount = 100,
            UseParallel = true
        };

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig, config);

        // Assert
        Assert.True(
            result.OverallDifficultyRating == OverallDifficulty.Hard ||
            result.OverallDifficultyRating == OverallDifficulty.VeryHard ||
            result.OverallDifficultyRating == OverallDifficulty.Challenging ||
            result.OverallDifficultyRating == OverallDifficulty.PossiblyUnfair,
            $"Expected hard level to be rated as Hard/VeryHard/Challenging, got {result.OverallDifficultyRating}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsProgress()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var analyzer = new ComprehensiveLevelAnalyzer();

        var config = new ComprehensiveAnalysisConfig
        {
            RunMCTSAnalysis = false,
            PopulationSimulationCount = 50,
            UseParallel = false
        };

        var progressReports = new System.Collections.Generic.List<ComprehensiveProgress>();
        var progress = new System.Progress<ComprehensiveProgress>(p =>
        {
            progressReports.Add(p);
        });

        // Act
        await analyzer.AnalyzeAsync(levelConfig, config, progress);

        // Assert
        Assert.True(progressReports.Count > 0);
        Assert.Contains(progressReports, p => p.Phase == AnalysisPhase.PlayerPopulation);
    }

    private static LevelConfig CreateSimpleLevelConfig()
    {
        int width = 8;
        int height = 8;
        var grid = new TileType[width * height];

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
                    TargetCount = 15
                }
            }
        };
    }
}
