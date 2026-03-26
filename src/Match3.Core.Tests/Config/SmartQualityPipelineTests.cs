using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class SmartQualityPipelineTests
{
    #region Mock Designer

    private sealed class MockLevelDesigner : ILevelDesigner
    {
        private readonly LevelDesign _design;
        private readonly LevelDesign? _revisedDesign;
        public int DesignCallCount { get; private set; }
        public int ReviseCallCount { get; private set; }

        public MockLevelDesigner(LevelDesign design, LevelDesign? revisedDesign = null)
        {
            _design = design;
            _revisedDesign = revisedDesign;
        }

        public Task<LevelDesign> DesignAsync(LevelDesignContext context, CancellationToken ct)
        {
            DesignCallCount++;
            return Task.FromResult(_design);
        }

        public Task<LevelDesign> ReviseAsync(
            LevelDesign previousDesign, LevelAnalysisResult analysisResult,
            LevelDesignContext context, CancellationToken ct)
        {
            ReviseCallCount++;
            return Task.FromResult(_revisedDesign ?? previousDesign);
        }
    }

    #endregion

    #region Mock Analysis Service

    private sealed class MockAnalysisService : ILevelAnalysisService
    {
        private readonly float _winRate;
        private readonly float _deadlockRate;
        public int CallCount { get; private set; }

        public MockAnalysisService(float winRate, float deadlockRate = 0f)
        {
            _winRate = winRate;
            _deadlockRate = deadlockRate;
        }

        public Task<LevelAnalysisResult> AnalyzeAsync(
            LevelData levelData, AnalysisConfig? config = null,
            IProgress<SimulationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<LevelAnalysisResult> AnalyzeAsync(
            LevelConfig levelConfig, AnalysisConfig? config = null,
            IProgress<SimulationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            int total = config?.SimulationCount ?? 200;
            int wins = (int)(total * _winRate);
            int deadlocks = (int)(total * _deadlockRate);
            return Task.FromResult(new LevelAnalysisResult
            {
                TotalSimulations = total,
                WinCount = wins,
                DeadlockCount = deadlocks,
                OutOfMovesCount = total - wins - deadlocks,
                AverageMovesUsed = 15f
            });
        }
    }

    #endregion

    #region Helpers

    private static LevelDesignContext MakeContext()
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
                    MinWinRate = 0.75f,
                    MaxWinRate = 0.92f,
                    MinMoves = 18,
                    MaxMoves = 28,
                    Covers = new[]
                    {
                        new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 12 }
                    },
                    Grounds = new[]
                    {
                        new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 }
                    }
                }
            }
        };

        return new LevelDesignContext
        {
            Blueprint = blueprint,
            LevelNumber = 15,
            Pool = EffectivePool.Build(blueprint, 15),
            Phase = blueprint.Phases[0]
        };
    }

    private static LevelDesign MakeDesign(int moveLimit = 22)
    {
        return new LevelDesign
        {
            Width = 8,
            Height = 8,
            Shape = "rectangle",
            ColorCount = 4,
            Rhythm = RhythmCategory.Normal,
            Difficulty = 0.2f,
            MoveLimit = moveLimit,
            Covers = new List<ElementPlacement>
            {
                new ElementPlacement
                {
                    ElementType = "Cage",
                    Count = 4,
                    Stage = 1,
                    Strategy = "border"
                }
            },
            Grounds = new List<ElementPlacement>
            {
                new ElementPlacement
                {
                    ElementType = "Ice",
                    Count = 6,
                    Stage = 1,
                    Strategy = "center"
                }
            },
            Objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    TargetLayer = "Tile",
                    ElementType = "1",
                    TargetCount = 15
                }
            },
            DesignIntent = "Test level"
        };
    }

    #endregion

    [Fact]
    public async Task Pipeline_PassesOnFirstAttempt_WhenInRange()
    {
        var design = MakeDesign();
        var designer = new MockLevelDesigner(design);
        var analysis = new MockAnalysisService(winRate: 0.85f); // Within 0.75-0.92
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig { MaxAttempts = 3, ScreeningSimCount = 100 };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        Assert.True(result.Passed);
        Assert.Equal(1, result.AttemptsUsed);
        Assert.Equal(1, designer.DesignCallCount);
        Assert.Equal(0, designer.ReviseCallCount);
    }

    [Fact]
    public async Task Pipeline_RevisesOnFailure()
    {
        var design = MakeDesign();
        var revisedDesign = MakeDesign(moveLimit: 26); // More generous
        var designer = new MockLevelDesigner(design, revisedDesign);
        var analysis = new MockAnalysisService(winRate: 0.50f); // Below min 0.75
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig { MaxAttempts = 3, ScreeningSimCount = 100 };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        // Should have called ReviseAsync since it failed
        Assert.Equal(1, designer.DesignCallCount);
        Assert.True(designer.ReviseCallCount > 0, "Should have called ReviseAsync at least once");
    }

    [Fact]
    public async Task Pipeline_ReturnsBestAfterMaxAttempts()
    {
        var design = MakeDesign();
        var designer = new MockLevelDesigner(design);
        var analysis = new MockAnalysisService(winRate: 0.50f); // Always fails
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig { MaxAttempts = 3, ScreeningSimCount = 100 };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        Assert.False(result.Passed);
        Assert.Equal(3, result.AttemptsUsed);
        Assert.Contains("Max attempts", result.Log);
    }

    [Fact]
    public async Task Pipeline_FailsOnHighDeadlock()
    {
        var design = MakeDesign();
        var designer = new MockLevelDesigner(design);
        var analysis = new MockAnalysisService(winRate: 0.85f, deadlockRate: 0.10f);
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig
        {
            MaxAttempts = 2,
            ScreeningSimCount = 100,
            MaxDeadlockRate = 0.05f
        };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task Pipeline_ResultContainsDesign()
    {
        var design = MakeDesign();
        var designer = new MockLevelDesigner(design);
        var analysis = new MockAnalysisService(winRate: 0.85f);
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig { MaxAttempts = 1, ScreeningSimCount = 100 };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        Assert.NotNull(result.FinalDesign);
        Assert.NotNull(result.GeneratorResult);
        Assert.NotNull(result.BestAnalysis);
        Assert.Equal("rectangle", result.FinalDesign.Shape);
    }

    [Fact]
    public async Task Pipeline_ReportsTooEasy()
    {
        var design = MakeDesign();
        var designer = new MockLevelDesigner(design);
        var analysis = new MockAnalysisService(winRate: 0.98f); // Above max 0.92
        var pipeline = new SmartQualityPipeline(designer, analysis);
        var config = new SmartQualityPipelineConfig { MaxAttempts = 2, ScreeningSimCount = 100 };

        var result = await pipeline.ProcessLevelAsync(MakeContext(), config);

        Assert.False(result.Passed);
        Assert.True(designer.ReviseCallCount > 0);
    }
}
