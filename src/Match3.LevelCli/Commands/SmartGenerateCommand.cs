using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.LevelCli.Services;

namespace Match3.LevelCli;

static class SmartGenerateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        // Parse arguments
        string blueprintPath = args[0];
        int startLevel = 1;
        int endLevel = 10;
        string outputDir = "config/levels";
        int simCount = 200;
        int maxAttempts = 3;
        string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        string model = "claude-sonnet-4-20250514";
        string knowledgeDir = "docs/03-design/level-knowledge";
        bool dryRun = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--start" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s):
                    startLevel = s; i++; break;
                case "--end" when i + 1 < args.Length && int.TryParse(args[i + 1], out var e):
                    endLevel = e; i++; break;
                case "-o" when i + 1 < args.Length:
                    outputDir = args[++i]; break;
                case "--sims" when i + 1 < args.Length && int.TryParse(args[i + 1], out var sc):
                    simCount = sc; i++; break;
                case "--attempts" when i + 1 < args.Length && int.TryParse(args[i + 1], out var a):
                    maxAttempts = a; i++; break;
                case "--api-key" when i + 1 < args.Length:
                    apiKey = args[++i]; break;
                case "--model" when i + 1 < args.Length:
                    model = args[++i]; break;
                case "--knowledge" when i + 1 < args.Length:
                    knowledgeDir = args[++i]; break;
                case "--dry-run":
                    dryRun = true; break;
            }
        }

        if (!File.Exists(blueprintPath))
        {
            Console.WriteLine($"Blueprint not found: {blueprintPath}");
            return 2;
        }

        if (string.IsNullOrEmpty(apiKey) && !dryRun)
        {
            Console.WriteLine("Error: API key required. Set ANTHROPIC_API_KEY env var or use --api-key.");
            return 2;
        }

        if (startLevel < 1 || endLevel < startLevel)
        {
            Console.WriteLine($"Invalid range: {startLevel}-{endLevel}");
            return 1;
        }

        // Load blueprint
        var blueprintJson = File.ReadAllText(blueprintPath);
        var blueprint = ConfigParser.ParseProgressionBlueprint(blueprintJson);
        var errors = BlueprintValidator.Validate(blueprint);
        if (errors.Count > 0)
        {
            Console.WriteLine("Blueprint validation errors:");
            foreach (var err in errors)
                Console.WriteLine($"  - {err}");
            return 2;
        }

        // Setup
        var llmConfig = new LlmConfig
        {
            ApiKey = apiKey ?? "",
            Model = model
        };

        var knowledgeService = new KnowledgeDocService(knowledgeDir);
        var analysisService = new RandomAnalysisService();

        // Dry run mode: show prompt for first level and exit
        if (dryRun)
        {
            return RunDryRun(blueprint, startLevel, knowledgeService);
        }

        var designer = new LlmLevelDesigner(llmConfig);
        var pipeline = new SmartQualityPipeline(designer, analysisService);
        var pipelineConfig = new SmartQualityPipelineConfig
        {
            ScreeningSimCount = simCount,
            MaxAttempts = maxAttempts,
            BaseSeed = (ulong)Environment.TickCount64
        };

        // Ensure output directory
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        int total = endLevel - startLevel + 1;
        Console.WriteLine($"=== Smart Generate levels {startLevel}-{endLevel} ({total} levels) ===");
        Console.WriteLine($"Blueprint: {Path.GetFileName(blueprintPath)}");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Output: {outputDir}");
        Console.WriteLine($"Sims: {simCount} | MaxAttempts: {maxAttempts}");
        Console.WriteLine();

        var progress = new Progress<QualityProgress>(p =>
        {
            Console.Write($"\r  Level {p.CurrentLevel}/{endLevel} attempt {p.CurrentAttempt} [{p.Status}]    ");
        });

        var docLoader = knowledgeService.CreateLoaderForBlueprint(blueprint);
        var batch = await pipeline.ProcessRangeAsync(
            blueprint, startLevel, endLevel, pipelineConfig, docLoader, progress);

        // Clear progress line
        Console.Write("\r" + new string(' ', 60) + "\r");

        // Write output files
        int written = 0;
        foreach (var result in batch.Results)
        {
            var levelNum = result.GeneratorResult.LevelNumber;
            var levelId = $"level_{levelNum:D3}";

            // Write level JSON
            var jsonPath = Path.Combine(outputDir, $"{levelId}.json");
            var json = ConfigParser.Serialize(result.GeneratorResult.Config);
            File.WriteAllText(jsonPath, json);

            // Write design document
            var designPath = Path.Combine(outputDir, $"{levelId}.design.md");
            var designDoc = result.GeneratorResult.GenerateDesignDocument();

            // Append LLM design intent
            if (result.FinalDesign != null && !string.IsNullOrEmpty(result.FinalDesign.DesignIntent))
            {
                designDoc += $"\n## LLM 设计意图\n\n{result.FinalDesign.DesignIntent}\n";
                if (!string.IsNullOrEmpty(result.FinalDesign.MoveReasoning))
                    designDoc += $"\n**步数推理**: {result.FinalDesign.MoveReasoning}\n";
                if (result.FinalDesign.DesignNotes.Count > 0)
                {
                    designDoc += "\n**设计笔记**:\n";
                    foreach (var note in result.FinalDesign.DesignNotes)
                        designDoc += $"- {note}\n";
                }
            }

            // Append analysis results
            designDoc += $"\n## 分析结果\n\n";
            designDoc += $"| 指标 | 值 |\n|------|-----|\n";
            designDoc += $"| 胜率 | {result.BestAnalysis.WinRate:P1} |\n";
            designDoc += $"| 死锁率 | {result.BestAnalysis.DeadlockRate:P1} |\n";
            designDoc += $"| 平均步数 | {result.BestAnalysis.AverageMovesUsed:F1} / {result.GeneratorResult.Config.MoveLimit} |\n";
            designDoc += $"| 难度评级 | {result.BestAnalysis.DifficultyRating} |\n";
            designDoc += $"| 通过 | {(result.Passed ? "Yes" : "No")} |\n";
            designDoc += $"| 尝试次数 | {result.AttemptsUsed} |\n";

            if (!string.IsNullOrEmpty(result.Log))
                designDoc += $"\n### 管线日志\n\n```\n{result.Log}```\n";

            File.WriteAllText(designPath, designDoc);
            written++;
        }

        // Summary
        Console.WriteLine($"=== Done ===");
        Console.WriteLine($"  Generated: {written} levels");
        Console.WriteLine($"  Passed:    {batch.PassedCount}/{total}");
        Console.WriteLine($"  Failed:    {batch.FailedCount}/{total}");
        Console.WriteLine($"  Avg Win:   {batch.AverageWinRate:P1}");
        Console.WriteLine();

        foreach (var result in batch.Results)
        {
            var mark = result.Passed ? "OK" : "!!";
            Console.WriteLine($"  [{mark}] Level {result.GeneratorResult.LevelNumber:D3}: " +
                $"win={result.BestAnalysis.WinRate:P1} " +
                $"deadlock={result.BestAnalysis.DeadlockRate:P1} " +
                $"moves={result.GeneratorResult.Config.MoveLimit} " +
                $"attempts={result.AttemptsUsed}");
        }

        return 0;
    }

    private static int RunDryRun(
        ProgressionBlueprint blueprint, int levelNumber,
        KnowledgeDocService knowledgeService)
    {
        var pool = EffectivePool.Build(blueprint, levelNumber);
        var phase = BlueprintValidator.FindPhase(blueprint, levelNumber);
        if (phase == null)
        {
            Console.WriteLine($"Level {levelNumber} not found in blueprint");
            return 2;
        }

        var rhythm = DifficultyBudget.ClassifyRhythm(
            pool, levelNumber, new Match3.Random.XorShift64((ulong)levelNumber));
        var docs = knowledgeService.LoadForLevel(pool, rhythm, phase.MaxObjectives);

        var context = new LevelDesignContext
        {
            Blueprint = blueprint,
            LevelNumber = levelNumber,
            Pool = pool,
            Phase = phase,
            KnowledgeDocs = docs
        };

        Console.WriteLine("=== DRY RUN: Prompt for Level " + levelNumber + " ===");
        Console.WriteLine();
        Console.WriteLine("--- SYSTEM PROMPT ---");
        Console.WriteLine($"[Knowledge docs loaded: {docs.Count} files, ~{KnowledgeDocService.ConcatenateDocs(docs).Length} chars]");
        Console.WriteLine();
        Console.WriteLine("--- USER PROMPT ---");
        Console.WriteLine($"Phase: {phase.Name}");
        Console.WriteLine($"Board: {pool.MinBoardWidth}-{pool.MaxBoardWidth} × {pool.MinBoardHeight}-{pool.MaxBoardHeight}");
        Console.WriteLine($"Shapes: {string.Join(", ", pool.Shapes)}");
        Console.WriteLine($"Colors: {pool.MinColors}-{pool.MaxColors}");
        Console.WriteLine($"Moves: {pool.MinMoves}-{pool.MaxMoves}");
        Console.WriteLine($"Difficulty: {pool.MinDifficulty:F2}-{pool.MaxDifficulty:F2}");
        Console.WriteLine($"WinRate: {pool.MinWinRate:P0}-{pool.MaxWinRate:P0}");
        Console.WriteLine($"MaxObjectives: {pool.MaxObjectives}");
        Console.WriteLine($"Available obstacles: {string.Join(", ", pool.Obstacles.Keys)}");
        Console.WriteLine($"Available covers: {string.Join(", ", pool.Covers.Keys)}");
        Console.WriteLine($"Available grounds: {string.Join(", ", pool.Grounds.Keys)}");
        Console.WriteLine($"Available moving: {string.Join(", ", pool.MovingObstacles.Keys)}");

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: levelcli smart-generate <blueprint.json> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --start N          Start level number (default: 1)");
        Console.WriteLine("  --end N            End level number (default: 10)");
        Console.WriteLine("  --api-key <key>    Anthropic API key (or env ANTHROPIC_API_KEY)");
        Console.WriteLine("  --model <name>     Model name (default: claude-sonnet-4-20250514)");
        Console.WriteLine("  -o <dir>           Output directory (default: config/levels)");
        Console.WriteLine("  --sims N           Simulations per analysis (default: 200)");
        Console.WriteLine("  --attempts N       Max revision attempts per level (default: 3)");
        Console.WriteLine("  --knowledge <dir>  Knowledge docs directory");
        Console.WriteLine("  --dry-run          Show prompt info without calling API");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  levelcli smart-generate config/progression_blueprint.json --start 1 --end 50");
    }
}
