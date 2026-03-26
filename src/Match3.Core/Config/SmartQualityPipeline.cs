using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;

namespace Match3.Core.Config;

/// <summary>
/// Configuration for the smart quality pipeline.
/// </summary>
public sealed class SmartQualityPipelineConfig
{
    /// <summary>Simulations for screening stage (Random, fast).</summary>
    public int ScreeningSimCount { get; set; } = 200;

    /// <summary>Simulations for validation stage (PlayerSim, accurate).</summary>
    public int ValidationSimCount { get; set; } = 500;

    /// <summary>Simulations per tier for deep analysis.</summary>
    public int DeepAnalysisSimsPerTier { get; set; } = 250;

    /// <summary>Max design+analysis iterations per level.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Maximum acceptable deadlock rate.</summary>
    public float MaxDeadlockRate { get; set; } = 0.05f;

    /// <summary>Base seed for deterministic generation.</summary>
    public ulong BaseSeed { get; set; } = 42;
}

/// <summary>
/// Result of smart pipeline for a single level.
/// </summary>
public sealed class SmartQualityResult
{
    /// <summary>Generated level config and metadata.</summary>
    public LevelGeneratorResult GeneratorResult { get; set; } = null!;

    /// <summary>Screening analysis result (Random, fast).</summary>
    public LevelAnalysisResult ScreeningResult { get; set; } = null!;

    /// <summary>Validation analysis result (PlayerSim, accurate). Null if screening never passed.</summary>
    public LevelAnalysisResult? ValidationResult { get; set; }

    /// <summary>Deep analysis result (7 advanced metrics). Null if validation never passed.</summary>
    public DeepAnalysisResult? DeepResult { get; set; }

    /// <summary>The best available analysis — validation if done, otherwise screening.</summary>
    public LevelAnalysisResult BestAnalysis => ValidationResult ?? ScreeningResult;

    /// <summary>The final design used for the best result.</summary>
    public LevelDesign FinalDesign { get; set; } = null!;

    /// <summary>Whether the level passed all quality criteria.</summary>
    public bool Passed { get; set; }

    /// <summary>Number of design+analysis iterations used.</summary>
    public int AttemptsUsed { get; set; }

    /// <summary>Pipeline log with per-attempt details.</summary>
    public string Log { get; set; } = "";
}

/// <summary>
/// Orchestrates Design → Translate → Analyze → Extract Insights → Revise loop.
/// Uses ILevelDesigner for semantic design decisions instead of random generation.
/// Accumulates design insights across iterations and levels.
/// </summary>
public sealed class SmartQualityPipeline
{
    private readonly ILevelDesigner _designer;
    private readonly ILevelAnalysisService _screeningService;
    private readonly ILevelAnalysisService? _validationService;
    private readonly DeepAnalysisService? _deepAnalysisService;
    private readonly IInsightExtractor? _insightExtractor;
    private readonly IInsightStore? _insightStore;

    /// <summary>
    /// Create a pipeline with three analysis stages.
    /// </summary>
    /// <param name="designer">Level designer (LLM or manual).</param>
    /// <param name="screeningService">Stage 1: fast screening (RandomAnalysisService).</param>
    /// <param name="validationService">Stage 2: player-sim validation (PlayerSimAnalysisService). Optional.</param>
    /// <param name="deepAnalysisService">Stage 3: deep analysis (7 metrics). Optional.</param>
    /// <param name="insightExtractor">Insight extraction (LLM). Optional.</param>
    /// <param name="insightStore">Insight storage. Optional.</param>
    public SmartQualityPipeline(
        ILevelDesigner designer,
        ILevelAnalysisService screeningService,
        ILevelAnalysisService? validationService = null,
        DeepAnalysisService? deepAnalysisService = null,
        IInsightExtractor? insightExtractor = null,
        IInsightStore? insightStore = null)
    {
        _designer = designer;
        _screeningService = screeningService;
        _validationService = validationService;
        _deepAnalysisService = deepAnalysisService;
        _insightExtractor = insightExtractor;
        _insightStore = insightStore;
    }

    /// <summary>
    /// Process a single level through the smart pipeline.
    /// </summary>
    public async Task<SmartQualityResult> ProcessLevelAsync(
        LevelDesignContext context,
        SmartQualityPipelineConfig config,
        IProgress<QualityProgress>? progress = null,
        CancellationToken ct = default)
    {
        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = config.ScreeningSimCount,
            UseParallel = true
        };

        var log = new StringBuilder();
        SmartQualityResult? bestResult = null;
        float targetMidWinRate = (context.Phase.MinWinRate + context.Phase.MaxWinRate) / 2f;

        // 0. Load accumulated insights for this level
        if (_insightStore != null && context.Insights.Count == 0)
        {
            context.Insights = _insightStore.LoadRelevant(context.Pool);
            if (context.Insights.Count > 0)
                log.AppendLine($"  Loaded {context.Insights.Count} insights (foundation + element + combination)");
        }

        // 1. Initial design
        progress?.Report(new QualityProgress
        {
            CurrentLevel = context.LevelNumber,
            TotalLevels = 1,
            CurrentAttempt = 1,
            Status = "Designing"
        });

        var design = await _designer.DesignAsync(context, ct);
        log.AppendLine($"  Initial design: {design.Shape} {design.Width}x{design.Height}, " +
            $"moves={design.MoveLimit}, obstacles={design.Obstacles.Count}, " +
            $"covers={design.Covers.Count}, grounds={design.Grounds.Count}");

        for (int attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // 2. Translate design → LevelConfig
            ulong seed = config.BaseSeed ^ ((ulong)context.LevelNumber * 7919) ^ (ulong)attempt;

            progress?.Report(new QualityProgress
            {
                CurrentLevel = context.LevelNumber,
                TotalLevels = 1,
                CurrentAttempt = attempt + 1,
                Status = "Translating"
            });

            var genResult = DesignTranslator.Translate(design, context, seed);

            // 3. Analyze
            progress?.Report(new QualityProgress
            {
                CurrentLevel = context.LevelNumber,
                TotalLevels = 1,
                CurrentAttempt = attempt + 1,
                Status = "Analyzing"
            });

            LevelAnalysisResult analysisResult;
            try
            {
                analysisResult = await _screeningService.AnalyzeAsync(
                    genResult.Config, analysisConfig, null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.AppendLine($"  Attempt {attempt + 1}: simulation error ({ex.InnerException?.Message ?? ex.Message}), retrying");
                continue;
            }

            // 4. Cache analysis result
            genResult.Config.AnalysisCache = new LevelAnalysisCacheData
            {
                WinRate = analysisResult.WinRate,
                DeadlockRate = analysisResult.DeadlockRate,
                AverageMovesUsed = analysisResult.AverageMovesUsed,
                Difficulty = analysisResult.DifficultyRating.ToString(),
                SimulationCount = analysisResult.TotalSimulations,
                AnalyzedAt = DateTime.UtcNow
            };

            var candidate = new SmartQualityResult
            {
                GeneratorResult = genResult,
                ScreeningResult = analysisResult,
                FinalDesign = design,
                AttemptsUsed = attempt + 1
            };

            log.AppendLine($"  Attempt {attempt + 1}: winRate={analysisResult.WinRate:P1}, " +
                $"deadlock={analysisResult.DeadlockRate:P1}, " +
                $"avgMoves={analysisResult.AverageMovesUsed:F1}/{genResult.Config.MoveLimit}");

            // 4b. Extract and store insights
            if (_insightExtractor != null && _insightStore != null)
            {
                try
                {
                    var newInsights = await _insightExtractor.ExtractAsync(
                        design, analysisResult, context, ct);
                    foreach (var insight in newInsights)
                    {
                        insight.Source = $"Level {context.LevelNumber}, attempt {attempt + 1}";
                        insight.Metrics["winRate"] = $"{analysisResult.WinRate:P1}";
                        insight.Metrics["deadlockRate"] = $"{analysisResult.DeadlockRate:P1}";
                        _insightStore.Save(insight);
                        context.Insights.Add(insight); // Available for next revision
                    }
                    if (newInsights.Count > 0)
                        log.AppendLine($"  Extracted {newInsights.Count} insight(s)");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.AppendLine($"  Insight extraction failed: {ex.Message}");
                }
            }

            // 5. Track best result
            if (bestResult == null ||
                Math.Abs(analysisResult.WinRate - targetMidWinRate) <
                Math.Abs(bestResult.BestAnalysis.WinRate - targetMidWinRate))
            {
                bestResult = candidate;
            }

            // 6. Check pass criteria
            bool winRateOk = analysisResult.WinRate >= context.Phase.MinWinRate &&
                             analysisResult.WinRate <= context.Phase.MaxWinRate;
            bool deadlockOk = analysisResult.DeadlockRate <= config.MaxDeadlockRate;

            if (winRateOk && deadlockOk)
            {
                log.AppendLine($"  Screening passed on attempt {attempt + 1}");

                // Stage 2: Validation (PlayerSim)
                if (_validationService != null)
                {
                    progress?.Report(new QualityProgress
                    {
                        CurrentLevel = context.LevelNumber,
                        TotalLevels = 1,
                        CurrentAttempt = attempt + 1,
                        Status = "Validating"
                    });

                    var validationConfig = new AnalysisConfig
                    {
                        SimulationCount = config.ValidationSimCount,
                        UseParallel = true
                    };
                    var validationResult = await _validationService.AnalyzeAsync(
                        genResult.Config, validationConfig, null, ct);
                    candidate.ValidationResult = validationResult;

                    bool valWinRateOk = validationResult.WinRate >= context.Phase.MinWinRate &&
                                       validationResult.WinRate <= context.Phase.MaxWinRate;
                    bool valDeadlockOk = validationResult.DeadlockRate <= config.MaxDeadlockRate;

                    log.AppendLine($"  Validation: winRate={validationResult.WinRate:P1}, " +
                        $"deadlock={validationResult.DeadlockRate:P1}");

                    if (!valWinRateOk || !valDeadlockOk)
                    {
                        log.AppendLine($"  Validation failed — revising with PlayerSim feedback");
                        // Use validation result for revision (more accurate feedback)
                        if (attempt < config.MaxAttempts - 1)
                        {
                            design = await _designer.ReviseAsync(design, validationResult, context, ct);
                            log.AppendLine($"  Revised design: moves={design.MoveLimit}");
                        }
                        continue;
                    }
                }

                // Stage 3: Deep Analysis (final report)
                if (_deepAnalysisService != null)
                {
                    progress?.Report(new QualityProgress
                    {
                        CurrentLevel = context.LevelNumber,
                        TotalLevels = 1,
                        CurrentAttempt = attempt + 1,
                        Status = "DeepAnalysis"
                    });

                    var deepResult = await _deepAnalysisService.AnalyzeAsync(
                        genResult.Config, config.DeepAnalysisSimsPerTier, null, ct);
                    candidate.DeepResult = deepResult;

                    log.AppendLine($"  Deep Analysis: skillSensitivity={deepResult.SkillSensitivity:F2}, " +
                        $"frustration={deepResult.FrustrationRisk:P1}, " +
                        $"luck={deepResult.LuckDependency:P1}, " +
                        $"P95={deepResult.P95ClearAttempts}");
                    foreach (var (tier, wr) in deepResult.TierWinRates)
                        log.AppendLine($"    {tier}: {wr:P1}");
                }

                candidate.Passed = true;
                candidate.Log = log.ToString();

                progress?.Report(new QualityProgress
                {
                    CurrentLevel = context.LevelNumber,
                    TotalLevels = 1,
                    CurrentAttempt = attempt + 1,
                    Status = "Passed"
                });

                return candidate;
            }

            // 7. Revise design if more attempts remain
            if (attempt < config.MaxAttempts - 1)
            {
                progress?.Report(new QualityProgress
                {
                    CurrentLevel = context.LevelNumber,
                    TotalLevels = 1,
                    CurrentAttempt = attempt + 1,
                    Status = "Revising"
                });

                design = await _designer.ReviseAsync(design, analysisResult, context, ct);
                log.AppendLine($"  Revised design: moves={design.MoveLimit}, " +
                    $"obstacles={design.Obstacles.Count}, covers={design.Covers.Count}");
            }
        }

        // Max attempts exhausted — return best
        if (bestResult == null)
        {
            // All attempts crashed (simulation errors) — return empty result
            log.AppendLine("  All attempts failed with errors");
            var fallbackResult = DesignTranslator.Translate(design, context,
                config.BaseSeed ^ (ulong)context.LevelNumber);
            bestResult = new SmartQualityResult
            {
                GeneratorResult = fallbackResult,
                ScreeningResult = new LevelAnalysisResult { TotalSimulations = 0 },
                FinalDesign = design,
            };
        }

        log.AppendLine($"  Max attempts reached, using best: winRate={bestResult.BestAnalysis.WinRate:P1}");
        bestResult.AttemptsUsed = config.MaxAttempts;
        bestResult.Log = log.ToString();

        progress?.Report(new QualityProgress
        {
            CurrentLevel = context.LevelNumber,
            TotalLevels = 1,
            CurrentAttempt = config.MaxAttempts,
            Status = "BestEffort"
        });

        return bestResult;
    }

    /// <summary>
    /// Process a range of levels sequentially.
    /// </summary>
    public async Task<SmartQualityBatchResult> ProcessRangeAsync(
        ProgressionBlueprint blueprint,
        int startLevel, int endLevel,
        SmartQualityPipelineConfig config,
        Func<int, Dictionary<string, string>> knowledgeDocLoader,
        IProgress<QualityProgress>? progress = null,
        CancellationToken ct = default)
    {
        int total = endLevel - startLevel + 1;
        var results = new List<SmartQualityResult>(total);
        float winRateSum = 0;
        int passed = 0;

        for (int level = startLevel; level <= endLevel; level++)
        {
            ct.ThrowIfCancellationRequested();

            var pool = EffectivePool.Build(blueprint, level);
            var phase = BlueprintValidator.FindPhase(blueprint, level)
                ?? throw new ArgumentOutOfRangeException(nameof(level));

            var context = new LevelDesignContext
            {
                Blueprint = blueprint,
                LevelNumber = level,
                Pool = pool,
                Phase = phase,
                KnowledgeDocs = knowledgeDocLoader(level)
            };

            var result = await ProcessLevelAsync(context, config, progress, ct);
            results.Add(result);
            winRateSum += result.BestAnalysis.WinRate;
            if (result.Passed) passed++;
        }

        return new SmartQualityBatchResult
        {
            Results = results,
            PassedCount = passed,
            FailedCount = total - passed,
            AverageWinRate = total > 0 ? winRateSum / total : 0
        };
    }
}

/// <summary>
/// Batch result for smart pipeline.
/// </summary>
public sealed class SmartQualityBatchResult
{
    public List<SmartQualityResult> Results { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public float AverageWinRate { get; set; }
}
