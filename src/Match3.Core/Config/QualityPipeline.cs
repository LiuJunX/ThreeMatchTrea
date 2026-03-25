using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;

namespace Match3.Core.Config;

/// <summary>
/// Configuration for the quality pipeline.
/// </summary>
public sealed class QualityPipelineConfig
{
    /// <summary>Simulations per analysis run. 200 for fast, 1000 for thorough.</summary>
    public int SimulationCount { get; set; } = 200;

    /// <summary>Max generation+analysis attempts per level before accepting best result.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Move limit adjustment step per retry (±N).</summary>
    public int MoveAdjustStep { get; set; } = 2;

    /// <summary>Maximum acceptable deadlock rate.</summary>
    public float MaxDeadlockRate { get; set; } = 0.05f;
}

/// <summary>
/// Progress report for quality pipeline operations.
/// </summary>
public readonly struct QualityProgress
{
    public int CurrentLevel { get; init; }
    public int TotalLevels { get; init; }
    public int CurrentAttempt { get; init; }
    public string Status { get; init; }
}

/// <summary>
/// Result for a single level through the quality pipeline.
/// </summary>
public sealed class QualityResult
{
    public LevelGeneratorResult GeneratorResult { get; set; } = null!;
    public LevelAnalysisResult AnalysisResult { get; set; } = null!;
    public bool Passed { get; set; }
    public int AttemptsUsed { get; set; }
    public string AdjustmentLog { get; set; } = "";
}

/// <summary>
/// Result for a batch of levels through the quality pipeline.
/// </summary>
public sealed class QualityBatchResult
{
    public List<QualityResult> Results { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public float AverageWinRate { get; set; }
}

/// <summary>
/// Orchestrates Generate → Analyze → Adjust → Accept loop.
/// </summary>
public sealed class QualityPipeline
{
    private readonly LevelGenerator _generator;
    private readonly ILevelAnalysisService _analysisService;

    public QualityPipeline(LevelGenerator generator, ILevelAnalysisService analysisService)
    {
        _generator = generator;
        _analysisService = analysisService;
    }

    /// <summary>
    /// Process a single level: generate, analyze, adjust if needed.
    /// </summary>
    public async Task<QualityResult> ProcessLevelAsync(
        ProgressionBlueprint blueprint,
        int levelNumber,
        ulong baseSeed,
        QualityPipelineConfig? config = null,
        IProgress<QualityProgress>? progress = null,
        CancellationToken ct = default)
    {
        config ??= new QualityPipelineConfig();
        var phase = BlueprintValidator.FindPhase(blueprint, levelNumber)
            ?? throw new ArgumentOutOfRangeException(nameof(levelNumber));

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = config.SimulationCount,
            UseParallel = true
        };

        var log = new StringBuilder();
        QualityResult? bestResult = null;
        float targetMidWinRate = (phase.MinWinRate + phase.MaxWinRate) / 2f;

        for (int attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // 1. Generate
            ulong seed = baseSeed ^ ((ulong)levelNumber * 7919) ^ (ulong)attempt;
            progress?.Report(new QualityProgress
            {
                CurrentLevel = levelNumber, TotalLevels = 1,
                CurrentAttempt = attempt + 1, Status = "Generating"
            });

            var genResult = _generator.Generate(blueprint, levelNumber, seed);

            // Apply move adjustment from previous attempts (carried forward via bestResult)
            if (bestResult != null && attempt > 0)
            {
                var prevAnalysis = bestResult.AnalysisResult;
                bool tooHard = prevAnalysis.WinRate < phase.MinWinRate;
                bool tooEasy = prevAnalysis.WinRate > phase.MaxWinRate;
                bool deadlockHigh = prevAnalysis.DeadlockRate > config.MaxDeadlockRate;

                if (deadlockHigh)
                {
                    log.AppendLine($"  Attempt {attempt}: deadlock {prevAnalysis.DeadlockRate:P1} > {config.MaxDeadlockRate:P1}, re-seed");
                }
                else if (tooHard && genResult.Config.MoveLimit < phase.MaxMoves)
                {
                    int newMoves = Math.Min(genResult.Config.MoveLimit + config.MoveAdjustStep, phase.MaxMoves);
                    log.AppendLine($"  Attempt {attempt}: too hard ({prevAnalysis.WinRate:P1}), moves {genResult.Config.MoveLimit}→{newMoves}");
                    genResult.Config.MoveLimit = newMoves;
                }
                else if (tooEasy && genResult.Config.MoveLimit > phase.MinMoves)
                {
                    int newMoves = Math.Max(genResult.Config.MoveLimit - config.MoveAdjustStep, phase.MinMoves);
                    log.AppendLine($"  Attempt {attempt}: too easy ({prevAnalysis.WinRate:P1}), moves {genResult.Config.MoveLimit}→{newMoves}");
                    genResult.Config.MoveLimit = newMoves;
                }
                else
                {
                    log.AppendLine($"  Attempt {attempt}: re-seed (moves at boundary)");
                }
            }

            // 2. Analyze
            progress?.Report(new QualityProgress
            {
                CurrentLevel = levelNumber, TotalLevels = 1,
                CurrentAttempt = attempt + 1, Status = "Analyzing"
            });

            LevelAnalysisResult analysisResult;
            try
            {
                analysisResult = await _analysisService.AnalyzeAsync(
                    genResult.Config, analysisConfig, null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Simulation crash (e.g., physics edge case) — treat as failed attempt, try different seed
                log.AppendLine($"  Attempt {attempt + 1}: simulation error ({ex.InnerException?.Message ?? ex.Message}), re-seed");
                continue;
            }

            // 3. Cache analysis result
            genResult.Config.AnalysisCache = new LevelAnalysisCacheData
            {
                WinRate = analysisResult.WinRate,
                DeadlockRate = analysisResult.DeadlockRate,
                AverageMovesUsed = analysisResult.AverageMovesUsed,
                Difficulty = analysisResult.DifficultyRating.ToString(),
                SimulationCount = analysisResult.TotalSimulations,
                AnalyzedAt = DateTime.UtcNow
            };

            var candidate = new QualityResult
            {
                GeneratorResult = genResult,
                AnalysisResult = analysisResult,
                AttemptsUsed = attempt + 1
            };

            // 4. Track best result (closest to target mid win rate)
            if (bestResult == null ||
                Math.Abs(analysisResult.WinRate - targetMidWinRate) <
                Math.Abs(bestResult.AnalysisResult.WinRate - targetMidWinRate))
            {
                bestResult = candidate;
            }

            // 5. Check pass criteria
            bool winRateOk = analysisResult.WinRate >= phase.MinWinRate &&
                             analysisResult.WinRate <= phase.MaxWinRate;
            bool deadlockOk = analysisResult.DeadlockRate <= config.MaxDeadlockRate;

            if (winRateOk && deadlockOk)
            {
                log.AppendLine($"  Passed on attempt {attempt + 1}: winRate={analysisResult.WinRate:P1}, deadlock={analysisResult.DeadlockRate:P1}");
                candidate.Passed = true;
                candidate.AdjustmentLog = log.ToString();

                progress?.Report(new QualityProgress
                {
                    CurrentLevel = levelNumber, TotalLevels = 1,
                    CurrentAttempt = attempt + 1, Status = "Passed"
                });
                return candidate;
            }

            log.AppendLine($"  Attempt {attempt + 1} result: winRate={analysisResult.WinRate:P1}, deadlock={analysisResult.DeadlockRate:P1}, moves={genResult.Config.MoveLimit}");
        }

        // Max attempts exhausted — return best result
        log.AppendLine($"  Max attempts reached, using best: winRate={bestResult!.AnalysisResult.WinRate:P1}");
        bestResult.AttemptsUsed = config.MaxAttempts;
        bestResult.AdjustmentLog = log.ToString();

        progress?.Report(new QualityProgress
        {
            CurrentLevel = levelNumber, TotalLevels = 1,
            CurrentAttempt = config.MaxAttempts, Status = "BestEffort"
        });
        return bestResult;
    }

    /// <summary>
    /// Process a range of levels sequentially.
    /// </summary>
    public async Task<QualityBatchResult> ProcessRangeAsync(
        ProgressionBlueprint blueprint,
        int startLevel, int endLevel,
        ulong baseSeed,
        QualityPipelineConfig? config = null,
        IProgress<QualityProgress>? progress = null,
        CancellationToken ct = default)
    {
        config ??= new QualityPipelineConfig();
        int total = endLevel - startLevel + 1;
        var results = new List<QualityResult>(total);
        float winRateSum = 0;
        int passed = 0;

        for (int level = startLevel; level <= endLevel; level++)
        {
            ct.ThrowIfCancellationRequested();

            var levelProgress = progress != null
                ? new Progress<QualityProgress>(p => progress.Report(new QualityProgress
                {
                    CurrentLevel = level,
                    TotalLevels = total,
                    CurrentAttempt = p.CurrentAttempt,
                    Status = p.Status
                }))
                : null;

            var result = await ProcessLevelAsync(
                blueprint, level, baseSeed, config, levelProgress, ct);

            results.Add(result);
            winRateSum += result.AnalysisResult.WinRate;
            if (result.Passed) passed++;
        }

        return new QualityBatchResult
        {
            Results = results,
            PassedCount = passed,
            FailedCount = total - passed,
            AverageWinRate = total > 0 ? winRateSum / total : 0
        };
    }
}
