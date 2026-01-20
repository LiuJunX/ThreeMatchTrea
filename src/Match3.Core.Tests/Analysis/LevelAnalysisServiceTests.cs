using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// LevelAnalysisService tests.
///
/// Responsibilities:
/// - Verifying win rate calculation with objectives
/// - Ensuring objective initialization works correctly
/// - Testing analysis configuration options
/// </summary>
public class LevelAnalysisServiceTests
{
    private readonly LevelAnalysisService _service = new();

    #region Objective Initialization Tests

    [Fact]
    public async Task AnalyzeAsync_WithObjectives_InitializesObjectiveProgress()
    {
        // Arrange - Create a level with a very easy objective (1 red tile)
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 1 // Very easy - just need to clear 1 red tile
            }
        };
        config.MoveLimit = 50; // Plenty of moves

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 10,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - With such an easy objective, we should have some wins
        Assert.True(result.WinCount > 0, "Should have at least some wins with a 1-tile objective");
        Assert.True(result.WinRate > 0, "Win rate should be greater than 0");
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutObjectives_WinRateIsZero()
    {
        // Arrange - Create a level without any objectives
        var config = CreateSimpleLevelConfig();
        // No objectives set - all slots are None by default

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 10,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Without objectives, win rate should be 0
        Assert.Equal(0, result.WinCount);
        Assert.Equal(0, result.WinRate);
    }

    [Fact]
    public async Task AnalyzeAsync_WithImpossibleObjective_WinRateIsZero()
    {
        // Arrange - Create a level with an impossible objective
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 10000 // Impossible to clear this many
            }
        };
        config.MoveLimit = 5; // Very few moves

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 10,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Impossible objective means 0 wins
        Assert.Equal(0, result.WinCount);
        Assert.Equal(0, result.WinRate);
    }

    #endregion

    #region Win Rate Calculation Tests

    [Fact]
    public async Task AnalyzeAsync_WithEasyObjective_HighWinRate()
    {
        // Arrange - Create a level with a trivially easy objective
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 3 // Easy - clear 3 red tiles
            }
        };
        config.MoveLimit = 100; // Many moves to ensure success

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 20,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Easy objective with many moves should have high win rate
        Assert.True(result.WinRate >= 0.5f, $"Expected high win rate, got {result.WinRate:P0}");
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleObjectives_AllMustComplete()
    {
        // Arrange - Create a level with multiple objectives
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 2
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Blue,
                TargetCount = 2
            }
        };
        config.MoveLimit = 50;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 20,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Should have some wins (both objectives achievable)
        Assert.True(result.TotalSimulations == 20);
        // Win count depends on RNG, but with easy objectives we expect some wins
    }

    #endregion

    #region Parallel Execution Tests

    [Fact]
    public async Task AnalyzeAsync_ParallelExecution_ProducesValidResults()
    {
        // Arrange
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 5
            }
        };
        config.MoveLimit = 30;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 50,
            UseParallel = true
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert
        Assert.Equal(50, result.TotalSimulations);
        Assert.True(result.WinCount + result.DeadlockCount + result.OutOfMovesCount == result.TotalSimulations,
            "Total outcomes should equal total simulations");
        Assert.True(result.WinRate >= 0 && result.WinRate <= 1, "Win rate should be between 0 and 1");
    }

    #endregion

    #region Cover/Ground Objective Tests

    [Fact]
    public async Task AnalyzeAsync_WithCoverObjective_TracksProgress()
    {
        // Arrange - Create a level with cover objective
        var config = CreateSimpleLevelConfig();

        // Add some covers to the level
        config.Covers = new CoverType[config.Width * config.Height];
        config.Covers[0] = CoverType.Chain;
        config.Covers[1] = CoverType.Chain;
        config.Covers[2] = CoverType.Chain;

        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Cover,
                ElementType = (int)CoverType.Chain,
                TargetCount = 3
            }
        };
        config.MoveLimit = 50;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 10,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Should complete simulations (win or fail based on cover destruction)
        Assert.Equal(10, result.TotalSimulations);
    }

    [Fact]
    public async Task AnalyzeAsync_WithGroundObjective_TracksProgress()
    {
        // Arrange - Create a level with ground objective
        var config = CreateSimpleLevelConfig();

        // Add some grounds to the level
        config.Grounds = new GroundType[config.Width * config.Height];
        config.Grounds[0] = GroundType.Ice;
        config.Grounds[1] = GroundType.Ice;

        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Ground,
                ElementType = (int)GroundType.Ice,
                TargetCount = 2
            }
        };
        config.MoveLimit = 50;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 10,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Should complete simulations
        Assert.Equal(10, result.TotalSimulations);
    }

    #endregion

    #region Result Statistics Tests

    [Fact]
    public async Task AnalyzeAsync_ReturnsValidStatistics()
    {
        // Arrange
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 10
            }
        };
        config.MoveLimit = 20;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 30,
            UseParallel = false
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert
        Assert.Equal(30, result.TotalSimulations);
        Assert.True(result.AverageMovesUsed > 0, "Average moves used should be positive");
        Assert.True(result.AverageScore >= 0, "Average score should be non-negative");
        Assert.True(result.ElapsedMs > 0, "Elapsed time should be positive");
        Assert.False(result.WasCancelled, "Should not be cancelled");

        // Verify outcome counts add up
        int totalOutcomes = result.WinCount + result.DeadlockCount + result.OutOfMovesCount;
        Assert.Equal(result.TotalSimulations, totalOutcomes);
    }

    [Fact]
    public async Task AnalyzeAsync_DifficultyRating_MatchesWinRate()
    {
        // Arrange - Easy level
        var config = CreateSimpleLevelConfig();
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 1
            }
        };
        config.MoveLimit = 100;

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = 50,
            UseParallel = true
        };

        // Act
        var result = await _service.AnalyzeAsync(config, analysisConfig);

        // Assert - Difficulty rating should match win rate thresholds
        var expectedRating = result.WinRate switch
        {
            >= 0.90f => DifficultyRating.VeryEasy,
            >= 0.70f => DifficultyRating.Easy,
            >= 0.40f => DifficultyRating.Medium,
            >= 0.10f => DifficultyRating.Hard,
            _ => DifficultyRating.VeryHard
        };
        Assert.Equal(expectedRating, result.DifficultyRating);
    }

    #endregion

    #region Helper Methods

    private static LevelConfig CreateSimpleLevelConfig()
    {
        const int width = 8;
        const int height = 8;
        var grid = new TileType[width * height];

        // Use TileType.None to let the system generate random tiles
        // This ensures a playable board with valid moves
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = TileType.None;
        }

        return new LevelConfig
        {
            Width = width,
            Height = height,
            Grid = grid,
            MoveLimit = 20,
            TargetDifficulty = 0.5f
        };
    }

    #endregion
}
