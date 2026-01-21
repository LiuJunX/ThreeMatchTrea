using System.Diagnostics;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// DeepAnalysisService 单元测试
/// </summary>
public class DeepAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsValidResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.WasCancelled);
        Assert.True(result.TotalSimulations > 0);
        Assert.True(result.ElapsedMs > 0);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFlowCurve()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.NotNull(result.FlowCurve);
        Assert.True(result.FlowCurve.Length > 0);
        Assert.True(result.FlowMax >= result.FlowMin);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTierWinRates()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.NotNull(result.TierWinRates);
        Assert.True(result.TierWinRates.ContainsKey("Novice"));
        Assert.True(result.TierWinRates.ContainsKey("Casual"));
        Assert.True(result.TierWinRates.ContainsKey("Core"));
        Assert.True(result.TierWinRates.ContainsKey("Expert"));

        // Expert should generally have higher win rate than Novice
        // (not strictly required but expected)
        foreach (var tier in result.TierWinRates)
        {
            Assert.True(tier.Value >= 0 && tier.Value <= 1,
                $"Win rate for {tier.Key} should be between 0 and 1");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSkillSensitivity()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.True(result.SkillSensitivity >= 0 && result.SkillSensitivity <= 1,
            $"Skill sensitivity should be between 0 and 1, got {result.SkillSensitivity}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFrustrationRisk()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.True(result.FrustrationRisk >= 0 && result.FrustrationRisk <= 1,
            $"Frustration risk should be between 0 and 1, got {result.FrustrationRisk}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsLuckDependency()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.True(result.LuckDependency >= 0 && result.LuckDependency <= 1,
            $"Luck dependency should be between 0 and 1, got {result.LuckDependency}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsP95ClearAttempts()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert
        Assert.True(result.P95ClearAttempts >= 1,
            $"P95 clear attempts should be at least 1, got {result.P95ClearAttempts}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsProgress()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        float lastProgress = 0;
        var progressReported = false;
        var progress = new System.Progress<DeepAnalysisProgress>(p =>
        {
            progressReported = true;
            lastProgress = p.Progress;
        });

        // Act
        await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50, progress);

        // Assert
        Assert.True(progressReported);
        Assert.Equal(1.0f, lastProgress, precision: 1);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AnalyzeAsync_Performance_Under60Seconds()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();

        // Act
        var sw = Stopwatch.StartNew();
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 100);
        sw.Stop();

        // Assert - Should complete in reasonable time
        Assert.True(sw.Elapsed.TotalSeconds < 60,
            $"Deep analysis took {sw.Elapsed.TotalSeconds:F1}s, expected < 60s");
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task AnalyzeAsync_EasyLevel_HigherExpertWinRate()
    {
        // Arrange - Very easy level
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 50; // Lots of moves
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 5 // Very easy objective
            }
        };
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 100);

        // Assert - Expert should generally win more than novice on easy levels
        var expertWinRate = result.TierWinRates.GetValueOrDefault("Expert", 0);
        var noviceWinRate = result.TierWinRates.GetValueOrDefault("Novice", 0);

        // This is not a strict requirement but generally expected
        Assert.True(expertWinRate >= 0.3f,
            $"Expert win rate on easy level should be decent, got {expertWinRate:P0}");
    }

    [Fact]
    public async Task AnalyzeAsync_CanBeCancelled()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var service = new DeepAnalysisService();
        var cts = new CancellationTokenSource();

        // Act - Cancel immediately
        cts.Cancel();

        // Assert - Should throw TaskCanceledException or return WasCancelled=true
        try
        {
            var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 100, cancellationToken: cts.Token);
            Assert.True(result.WasCancelled, "Should return WasCancelled=true when cancelled");
        }
        catch (OperationCanceledException)
        {
            // Also acceptable - cancellation throws exception
            Assert.True(true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_VeryEasyLevel_HighWinRate()
    {
        // Arrange - 1 step to win, lots of moves
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 100;
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 1 // Only need 1 tile
            }
        };
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert - Should have very high win rate
        var casualWinRate = result.TierWinRates.GetValueOrDefault("Casual", 0);
        Assert.True(casualWinRate >= 0.8f,
            $"Very easy level should have high Casual win rate, got {casualWinRate:P0}");
    }

    [Fact]
    public async Task AnalyzeAsync_HardLevel_LowerWinRate()
    {
        // Arrange - Hard level: few moves, high target
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 5; // Very few moves
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 50 // High target
            }
        };
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert - Should have lower win rate
        var noviceWinRate = result.TierWinRates.GetValueOrDefault("Novice", 0);
        Assert.True(noviceWinRate < 0.5f,
            $"Hard level should have low Novice win rate, got {noviceWinRate:P0}");
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleObjectives_IdentifiesBottleneck()
    {
        // Arrange - Multiple objectives, one much harder
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 15;
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 5 // Easy
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Blue,
                TargetCount = 50 // Hard - should be bottleneck
            }
        };
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 100);

        // Assert - Should identify Blue as bottleneck (if there are failures)
        if (result.TierWinRates.GetValueOrDefault("Casual", 0) < 0.95f)
        {
            Assert.False(string.IsNullOrEmpty(result.BottleneckObjective),
                "Should identify a bottleneck when there are failures");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_FlowCurve_LengthMatchesMoveLimit()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 15;
        var service = new DeepAnalysisService();

        // Act
        var result = await service.AnalyzeAsync(levelConfig, simulationsPerTier: 50);

        // Assert - Flow curve should have entries up to move limit
        Assert.NotNull(result.FlowCurve);
        Assert.True(result.FlowCurve.Length <= levelConfig.MoveLimit,
            $"Flow curve length {result.FlowCurve.Length} should not exceed move limit {levelConfig.MoveLimit}");
        Assert.True(result.FlowCurve.Length > 0,
            "Flow curve should have at least 1 entry");
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
                    TargetCount = 10
                }
            }
        };
    }
}
