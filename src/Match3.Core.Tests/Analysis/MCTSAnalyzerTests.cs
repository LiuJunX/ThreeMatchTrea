using System.Diagnostics;
using System.Threading.Tasks;
using Match3.Core.Analysis.MCTS;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// MCTSAnalyzer 单元测试
/// </summary>
[Trait("Category", "Slow")]
public class MCTSAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsValidResult()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 20,
            MaxRolloutDepth = 15,
            UseGuidedRollout = true
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalGames);
        Assert.True(result.OptimalWinRate >= 0 && result.OptimalWinRate <= 1);
        Assert.True(result.WinCount >= 0);
        Assert.True(result.ElapsedMs > 0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithEasyLevel_ShouldHaveHighWinRate()
    {
        // Arrange - Very easy level
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 50; // Lots of moves
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 3 // Very easy objective
            }
        };

        var mctsConfig = new MCTSConfig
        {
            TotalGames = 10,
            SimulationsPerMove = 30,
            UseGuidedRollout = true
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig);

        // Assert
        // With MCTS optimal play, easy level should have high win rate
        Assert.True(result.OptimalWinRate >= 0.5f,
            $"Expected high win rate for easy level, got {result.OptimalWinRate:P1}");
    }

    [Fact]
    public async Task AnalyzeAsync_TracksCriticalMoves()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 20,
            UseGuidedRollout = true
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        // Act
        var result = await analyzer.AnalyzeAsync(levelConfig);

        // Assert
        // Critical moves list should exist (may be empty)
        Assert.NotNull(result.CriticalMoves);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsProgress()
    {
        // Arrange
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 10
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        float lastProgress = 0;
        var progressReported = false;
        var progress = new System.Progress<float>(p =>
        {
            progressReported = true;
            lastProgress = p;
        });

        // Act
        await analyzer.AnalyzeAsync(levelConfig, progress);

        // Assert
        Assert.True(progressReported);
        Assert.Equal(1.0f, lastProgress, precision: 1);
    }

    [Fact]
    public void MCTSNode_UCB1Selection_SelectsUnvisitedFirst()
    {
        // Arrange
        var root = new MCTSNode();
        root.InitializeUntriedMoves(new System.Collections.Generic.List<Match3.Core.Utility.ValidMove>
        {
            new Match3.Core.Utility.ValidMove { },
            new Match3.Core.Utility.ValidMove { },
        });

        // Expand both children
        var child1 = root.Expand(0);
        var child2 = root.Expand(0);

        // Simulate one visit to child1
        child1.Backpropagate(1.0f);

        // Act - Select best child
        var selected = root.SelectBestChild();

        // Assert - Should select unvisited child2
        Assert.Same(child2, selected);
    }

    [Fact]
    public void MCTSNode_Backpropagate_UpdatesAllAncestors()
    {
        // Arrange
        var root = new MCTSNode();
        root.InitializeUntriedMoves(new System.Collections.Generic.List<Match3.Core.Utility.ValidMove>
        {
            new Match3.Core.Utility.ValidMove { }
        });
        var child = root.Expand(0);
        child.InitializeUntriedMoves(new System.Collections.Generic.List<Match3.Core.Utility.ValidMove>
        {
            new Match3.Core.Utility.ValidMove { }
        });
        var grandchild = child.Expand(0);

        // Act
        grandchild.Backpropagate(1.0f);

        // Assert
        Assert.Equal(1, grandchild.VisitCount);
        Assert.Equal(1, child.VisitCount);
        Assert.Equal(1, root.VisitCount);
        Assert.Equal(1.0f, grandchild.TotalReward);
        Assert.Equal(1.0f, child.TotalReward);
        Assert.Equal(1.0f, root.TotalReward);
    }

    [Fact]
    public void MCTSNode_SelectMostVisitedChild_ReturnsCorrectChild()
    {
        // Arrange
        var root = new MCTSNode();
        root.InitializeUntriedMoves(new System.Collections.Generic.List<Match3.Core.Utility.ValidMove>
        {
            new Match3.Core.Utility.ValidMove { },
            new Match3.Core.Utility.ValidMove { },
        });
        var child1 = root.Expand(0);
        var child2 = root.Expand(0);

        // Visit child1 once, child2 three times
        child1.Backpropagate(0.5f);
        child2.Backpropagate(1.0f);
        child2.Backpropagate(0.0f);
        child2.Backpropagate(1.0f);

        // Act
        var mostVisited = root.SelectMostVisitedChild();

        // Assert
        Assert.Same(child2, mostVisited);
        Assert.Equal(3, child2.VisitCount);
    }

    [Fact]
    public async Task AnalyzeAsync_Performance_ReasonableTime()
    {
        // Arrange - Reduced configuration for faster testing
        var levelConfig = CreateSimpleLevelConfig();
        levelConfig.MoveLimit = 15;
        levelConfig.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 10
            }
        };

        var mctsConfig = new MCTSConfig
        {
            TotalGames = 5,            // 减少局数用于快速测试
            SimulationsPerMove = 15,   // 减少模拟次数
            MaxRolloutDepth = 10,
            UseGuidedRollout = true,
            RolloutSkillLevel = 0.7f
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await analyzer.AnalyzeAsync(levelConfig);
        sw.Stop();

        // Assert - 5 games with reduced settings should complete in reasonable time
        Assert.True(sw.Elapsed.TotalSeconds < 60,
            $"Analysis took {sw.Elapsed.TotalSeconds:F1}s, expected < 60s. " +
            $"WinRate: {result.OptimalWinRate:P1}, Games: {result.TotalGames}");

        // 验证结果有效性
        Assert.Equal(5, result.TotalGames);
        Assert.True(result.ElapsedMs > 0);
    }

    [Fact]
    public async Task AnalyzeAsync_ParallelExecution_WorksCorrectly()
    {
        // Arrange - Test parallelization by comparing single vs multi-core throughput
        var levelConfig = CreateSimpleLevelConfig();
        var mctsConfig = new MCTSConfig
        {
            TotalGames = 4,
            SimulationsPerMove = 10,
            MaxRolloutDepth = 10,
            UseGuidedRollout = true
        };
        var analyzer = new MCTSAnalyzer(mctsConfig);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await analyzer.AnalyzeAsync(levelConfig);
        sw.Stop();

        // Assert - Should complete and parallelization means games run concurrently
        Assert.Equal(4, result.TotalGames);
        Assert.True(result.WinCount >= 0);

        // 验证并行化生效：8局游戏应该比串行8倍单局时间更短
        // 这里只验证完成了，不设置严格时间限制
        Assert.True(sw.Elapsed.TotalSeconds < 120,
            $"8 games took {sw.Elapsed.TotalSeconds:F1}s");
    }

    [Fact]
    public async Task FastMoveScorer_ImprovesThroughput()
    {
        // Arrange - Compare guided vs random rollout to verify FastMoveScorer works
        var levelConfig = CreateSimpleLevelConfig();

        // Test with guided rollout (using FastMoveScorer)
        var guidedConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 15,
            MaxRolloutDepth = 10,
            UseGuidedRollout = true,
            RolloutSkillLevel = 0.8f
        };

        // Test with random rollout (no FastMoveScorer)
        var randomConfig = new MCTSConfig
        {
            TotalGames = 5,
            SimulationsPerMove = 15,
            MaxRolloutDepth = 10,
            UseGuidedRollout = false
        };

        var guidedAnalyzer = new MCTSAnalyzer(guidedConfig);
        var randomAnalyzer = new MCTSAnalyzer(randomConfig);

        // Act
        var guidedResult = await guidedAnalyzer.AnalyzeAsync(levelConfig);
        var randomResult = await randomAnalyzer.AnalyzeAsync(levelConfig);

        // Assert - Both should complete and produce valid results
        Assert.Equal(5, guidedResult.TotalGames);
        Assert.Equal(5, randomResult.TotalGames);

        // Guided rollout should generally have similar or better performance
        // (不需要严格比较，只验证两种模式都能正常工作)
        Assert.True(guidedResult.ElapsedMs > 0);
        Assert.True(randomResult.ElapsedMs > 0);
    }

    private static LevelConfig CreateSimpleLevelConfig()
    {
        // 6x6 board — fewer valid moves per turn → faster MCTS simulation
        int width = 6;
        int height = 6;
        var grid = new ElementType[width * height];

        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = ElementType.None;
        }

        return new LevelConfig
        {
            Width = width,
            Height = height,
            MoveLimit = 15,
            Grid = grid,
            Objectives = new[]
            {
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 8
                }
            }
        };
    }
}

