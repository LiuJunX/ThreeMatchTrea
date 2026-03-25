using Match3.Core.Analysis;
using Match3.Core.Config;

namespace Match3.LevelCli;

static class GenerateCommand
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
        ulong seed = (ulong)Environment.TickCount64;
        string outputDir = "config/levels";
        int simCount = 200;
        int maxAttempts = 5;

        for (int i = 1; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--start" when int.TryParse(args[i + 1], out var s):
                    startLevel = s; break;
                case "--end" when int.TryParse(args[i + 1], out var e):
                    endLevel = e; break;
                case "--seed" when ulong.TryParse(args[i + 1], out var sd):
                    seed = sd; break;
                case "-o":
                    outputDir = args[i + 1]; break;
                case "--sims" when int.TryParse(args[i + 1], out var sc):
                    simCount = sc; break;
                case "--attempts" when int.TryParse(args[i + 1], out var a):
                    maxAttempts = a; break;
            }
        }

        if (!File.Exists(blueprintPath))
        {
            Console.WriteLine($"Blueprint not found: {blueprintPath}");
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

        // Setup pipeline
        var generator = new LevelGenerator();
        var analysisService = new LevelAnalysisService();
        var pipeline = new QualityPipeline(generator, analysisService);
        var pipelineConfig = new QualityPipelineConfig
        {
            SimulationCount = simCount,
            MaxAttempts = maxAttempts
        };

        // Ensure output directory
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        int total = endLevel - startLevel + 1;
        Console.WriteLine($"=== Generate levels {startLevel}-{endLevel} ({total} levels) ===");
        Console.WriteLine($"Blueprint: {Path.GetFileName(blueprintPath)}");
        Console.WriteLine($"Output: {outputDir}");
        Console.WriteLine($"Seed: {seed} | Sims: {simCount} | MaxAttempts: {maxAttempts}");
        Console.WriteLine();

        var progress = new Progress<QualityProgress>(p =>
        {
            Console.Write($"\r  Level {p.CurrentLevel}/{endLevel} attempt {p.CurrentAttempt} [{p.Status}]    ");
        });

        var batch = await pipeline.ProcessRangeAsync(
            blueprint, startLevel, endLevel, seed, pipelineConfig, progress);

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
            // Append pipeline results
            designDoc += $"\n## 分析结果\n\n";
            designDoc += $"| 指标 | 值 |\n|------|-----|\n";
            designDoc += $"| 胜率 | {result.AnalysisResult.WinRate:P1} |\n";
            designDoc += $"| 死锁率 | {result.AnalysisResult.DeadlockRate:P1} |\n";
            designDoc += $"| 平均步数 | {result.AnalysisResult.AverageMovesUsed:F1} / {result.GeneratorResult.Config.MoveLimit} |\n";
            designDoc += $"| 难度评级 | {result.AnalysisResult.DifficultyRating} |\n";
            designDoc += $"| 通过 | {(result.Passed ? "Yes" : "No")} |\n";
            designDoc += $"| 尝试次数 | {result.AttemptsUsed} |\n";
            if (!string.IsNullOrEmpty(result.AdjustmentLog))
            {
                designDoc += $"\n### 调参日志\n\n```\n{result.AdjustmentLog}```\n";
            }
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

        // List results per level
        foreach (var result in batch.Results)
        {
            var mark = result.Passed ? "OK" : "!!";
            Console.WriteLine($"  [{mark}] Level {result.GeneratorResult.LevelNumber:D3}: " +
                $"win={result.AnalysisResult.WinRate:P1} " +
                $"deadlock={result.AnalysisResult.DeadlockRate:P1} " +
                $"moves={result.GeneratorResult.Config.MoveLimit} " +
                $"attempts={result.AttemptsUsed}");
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: levelcli generate <blueprint.json> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --start N       Start level number (default: 1)");
        Console.WriteLine("  --end N         End level number (default: 10)");
        Console.WriteLine("  --seed N        Random seed (default: system tick)");
        Console.WriteLine("  -o <dir>        Output directory (default: config/levels)");
        Console.WriteLine("  --sims N        Simulations per analysis (default: 200)");
        Console.WriteLine("  --attempts N    Max retry attempts per level (default: 5)");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  levelcli generate config/progression_blueprint.json --start 1 --end 50 -o config/levels");
    }
}
