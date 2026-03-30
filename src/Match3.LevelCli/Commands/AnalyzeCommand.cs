using Match3.Core.Analysis;
using Match3.Core.Config;

namespace Match3.LevelCli;

static class AnalyzeCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: levelcli analyze <path> [--count N] [--mode random|greedy|population]");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return 2;
        }

        int simCount = 1000;
        string modeName = "random";

        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--count" && int.TryParse(args[i + 1], out var n))
                simCount = n;
            if (args[i] == "--mode")
                modeName = args[i + 1].ToLowerInvariant();
        }

        var mode = modeName switch
        {
            "greedy" => SimulationMode.Greedy,
            "population" or "pop" => SimulationMode.PlayerPopulation,
            "bomb" => SimulationMode.BombPriority,
            _ => SimulationMode.Random
        };

        var json = File.ReadAllText(path);
        var config = ConfigParser.ParseLevelConfig(json);

        Console.WriteLine($"=== Analyze: {Path.GetFileName(path)} ({config.Width}x{config.Height}) ===");
        Console.WriteLine($"Moves: {config.MoveLimit} | Mode: {modeName} | Sims: {simCount}");

        var analysisConfig = new AnalysisConfig
        {
            SimulationCount = simCount,
            UseParallel = true,
            Mode = mode
        };

        var progress = new Progress<SimulationProgress>(p =>
        {
            Console.Write($"\r  Running... {p.CompletedCount}/{p.TotalCount} ({p.Progress:P0}) | Win: {p.WinRate:P1} | Deadlock: {p.DeadlockRate:P1}");
        });

        ILevelAnalysisService service = mode == SimulationMode.Random
            ? new RandomAnalysisService()
            : new PlayerSimAnalysisService();

        var result = await service.AnalyzeAsync(config, analysisConfig, progress);

        // Clear progress line
        Console.Write("\r" + new string(' ', 80) + "\r");

        Console.WriteLine("--- Results ---");
        Console.WriteLine($"  Win Rate:      {result.WinRate:P1}");
        Console.WriteLine($"  Deadlock Rate: {result.DeadlockRate:P1}");
        Console.WriteLine($"  Out of Moves:  {(result.TotalSimulations > 0 ? (float)result.OutOfMovesCount / result.TotalSimulations : 0):P1}");
        Console.WriteLine($"  Avg Moves:     {result.AverageMovesUsed:F1} / {config.MoveLimit}");
        Console.WriteLine($"  Difficulty:    {result.DifficultyRating}");
        Console.WriteLine("--- L1 Diagnostics ---");
        Console.WriteLine($"  Loss Objective:  {result.AvgLossObjectiveCompletion:P1}  (失败时平均完成度)");
        Console.WriteLine($"  Win Remaining:   {result.AvgWinRemainingMoves:F1} +/- {result.StdDevWinRemainingMoves:F1} moves  (赢时剩余步数)");
        Console.WriteLine($"  Score/Move:      {result.AvgScorePerMove:F1}  (每步消除量)");

        // Init quality metrics (only shown when non-zero or from LevelConfig analysis)
        if (result.InitDeadlockRate > 0 || result.InitShuffleRate > 0)
        {
            Console.WriteLine("--- Init Quality ---");
            Console.WriteLine($"  Init Deadlock:   {result.InitDeadlockRate:P1}  (开局无有效步)");
            Console.WriteLine($"  Init Shuffle:    {result.InitShuffleRate:P1}  (开局即塌陷)");
        }

        // Show tier results for population mode
        if (result.TierResults != null)
        {
            Console.WriteLine("--- Player Tiers ---");
            foreach (var tier in result.TierResults)
            {
                Console.WriteLine($"  {tier.TierName,-8} Win: {tier.WinRate:P1}  Avg Moves: {tier.AverageMovesUsed:F1}  ({tier.SimulationCount} sims)");
            }
        }

        Console.WriteLine($"  Elapsed:         {result.ElapsedMs:F0}ms");

        return 0;
    }
}
