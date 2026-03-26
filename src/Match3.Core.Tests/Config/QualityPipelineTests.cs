using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Xunit;

namespace Match3.Core.Tests.Config;

public class QualityPipelineTests
{
    #region Unit Tests (with mock analysis)

    [Fact]
    public async Task ProcessLevel_ReturnsCompleteResult()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.80f, deadlockRate: 0.01f);

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42);

        Assert.NotNull(result);
        Assert.NotNull(result.GeneratorResult);
        Assert.NotNull(result.AnalysisResult);
        Assert.True(result.AttemptsUsed >= 1);
        Assert.NotNull(result.AdjustmentLog);
    }

    [Fact]
    public async Task ProcessLevel_PassesWhenWinRateInRange()
    {
        // Tutorial phase: MinWinRate=0.85, MaxWinRate=0.98
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.90f, deadlockRate: 0.01f);

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42);

        Assert.True(result.Passed);
        Assert.Equal(1, result.AttemptsUsed);
    }

    [Fact]
    public async Task ProcessLevel_RetriesWhenTooHard()
    {
        // WinRate 0.5 is below Tutorial's MinWinRate (0.85)
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.50f, deadlockRate: 0.01f);
        var config = new QualityPipelineConfig { MaxAttempts = 3, SimulationCount = 50 };

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42, config);

        // Should use all attempts since mock always returns same win rate
        Assert.Equal(3, result.AttemptsUsed);
        Assert.False(result.Passed);
        Assert.Contains("too hard", result.AdjustmentLog);
    }

    [Fact]
    public async Task ProcessLevel_RetriesWhenTooEasy()
    {
        // WinRate 1.0 is above Tutorial's MaxWinRate (0.98)
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 1.0f, deadlockRate: 0.0f);
        var config = new QualityPipelineConfig { MaxAttempts = 3, SimulationCount = 100 };

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42, config);

        Assert.Equal(3, result.AttemptsUsed);
        Assert.False(result.Passed);
        // Adjustment may say "too easy" or "re-seed" depending on generated MoveLimit
        Assert.Contains("Attempt", result.AdjustmentLog);
    }

    [Fact]
    public async Task ProcessLevel_RetriesOnHighDeadlock()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.90f, deadlockRate: 0.10f);
        var config = new QualityPipelineConfig { MaxAttempts = 3, MaxDeadlockRate = 0.05f, SimulationCount = 50 };

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42, config);

        Assert.Equal(3, result.AttemptsUsed);
        Assert.Contains("deadlock", result.AdjustmentLog);
    }

    [Fact]
    public async Task ProcessLevel_CachesAnalysisResult()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.90f, deadlockRate: 0.01f);

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42);

        Assert.NotNull(result.GeneratorResult.Config.AnalysisCache);
        Assert.Equal(0.90f, result.GeneratorResult.Config.AnalysisCache!.WinRate);
    }

    [Fact]
    public async Task ProcessRange_ReturnsAllResults()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.90f, deadlockRate: 0.01f);
        var config = new QualityPipelineConfig { SimulationCount = 50 };

        var batch = await pipeline.ProcessRangeAsync(blueprint, 1, 5, 42, config);

        Assert.Equal(5, batch.Results.Count);
        Assert.True(batch.PassedCount > 0);
        Assert.True(batch.AverageWinRate > 0);
    }

    [Fact]
    public async Task ProcessLevel_SupportsCancellation()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.50f, deadlockRate: 0.01f);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.ProcessLevelAsync(blueprint, 1, 42, ct: cts.Token));
    }

    [Fact]
    public async Task ProcessLevel_ReportsProgress()
    {
        var (pipeline, blueprint) = CreatePipelineWithMock(winRate: 0.90f, deadlockRate: 0.01f);
        var statuses = new System.Collections.Generic.List<string>();
        var progress = new Progress<QualityProgress>(p => statuses.Add(p.Status));

        await pipeline.ProcessLevelAsync(blueprint, 1, 42, progress: progress);

        // Allow async Progress<T> callbacks to flush
        await Task.Delay(50);

        Assert.Contains("Generating", statuses);
        Assert.Contains("Analyzing", statuses);
    }

    #endregion

    #region Integration Tests (real analysis)

    [Fact]
    [Trait("Category", "Slow")]
    public async Task ProcessLevel_RealAnalysis_ProducesReasonableResult()
    {
        var generator = new LevelGenerator();
        var analysisService = new RandomAnalysisService();
        var pipeline = new QualityPipeline(generator, analysisService);
        var blueprint = LoadRealBlueprint();
        var config = new QualityPipelineConfig { SimulationCount = 100, MaxAttempts = 3 };

        var result = await pipeline.ProcessLevelAsync(blueprint, 1, 42, config);

        Assert.NotNull(result.AnalysisResult);
        Assert.True(result.AnalysisResult.WinRate > 0, "Win rate should be > 0 for tutorial level");
        Assert.True(result.AnalysisResult.TotalSimulations == 100);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task ProcessRange_RealAnalysis_5Levels()
    {
        var generator = new LevelGenerator();
        var analysisService = new RandomAnalysisService();
        var pipeline = new QualityPipeline(generator, analysisService);
        var blueprint = LoadRealBlueprint();
        var config = new QualityPipelineConfig { SimulationCount = 100, MaxAttempts = 3 };

        var batch = await pipeline.ProcessRangeAsync(blueprint, 1, 5, 42, config);

        Assert.Equal(5, batch.Results.Count);
        Assert.True(batch.AverageWinRate > 0);

        // Tutorial levels should be relatively easy
        foreach (var r in batch.Results)
        {
            Assert.True(r.AnalysisResult.WinRate > 0.3f,
                $"Level {r.GeneratorResult.LevelNumber} win rate {r.AnalysisResult.WinRate:P1} too low for tutorial");
        }
    }

    #endregion

    #region Helpers

    private static (QualityPipeline, ProgressionBlueprint) CreatePipelineWithMock(
        float winRate, float deadlockRate)
    {
        var generator = new LevelGenerator();
        var mockService = new MockAnalysisService(winRate, deadlockRate);
        var pipeline = new QualityPipeline(generator, mockService);
        var blueprint = LoadRealBlueprint();
        return (pipeline, blueprint);
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

    /// <summary>
    /// Mock analysis service that returns fixed results for fast testing.
    /// </summary>
    private sealed class MockAnalysisService : ILevelAnalysisService
    {
        private readonly float _winRate;
        private readonly float _deadlockRate;

        public MockAnalysisService(float winRate, float deadlockRate)
        {
            _winRate = winRate;
            _deadlockRate = deadlockRate;
        }

        public Task<LevelAnalysisResult> AnalyzeAsync(
            LevelData levelData, AnalysisConfig? config = null,
            IProgress<SimulationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MakeResult(config?.SimulationCount ?? 100));
        }

        public Task<LevelAnalysisResult> AnalyzeAsync(
            LevelConfig levelConfig, AnalysisConfig? config = null,
            IProgress<SimulationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MakeResult(config?.SimulationCount ?? 100));
        }

        private LevelAnalysisResult MakeResult(int simCount)
        {
            int wins = (int)(simCount * _winRate);
            int deadlocks = (int)(simCount * _deadlockRate);
            return new LevelAnalysisResult
            {
                TotalSimulations = simCount,
                WinCount = wins,
                DeadlockCount = deadlocks,
                OutOfMovesCount = simCount - wins - deadlocks,
                AverageMovesUsed = 15,
                AverageScore = 1000
            };
        }
    }

    #endregion
}
