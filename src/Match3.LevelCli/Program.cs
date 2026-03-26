namespace Match3.LevelCli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args[1..];

        return command switch
        {
            "print" => PrintCommand.Run(commandArgs),
            "validate" => ValidateCommand.Run(commandArgs),
            "analyze" => await AnalyzeCommand.RunAsync(commandArgs),
            "create" => CreateCommand.Run(commandArgs),
            "generate" => await GenerateCommand.RunAsync(commandArgs),
            "smart-generate" => await SmartGenerateCommand.RunAsync(commandArgs),
            _ => PrintUsage()
        };
    }

    static int PrintUsage()
    {
        Console.WriteLine("Match3 Level CLI - 关卡协作工具");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  levelcli print <path>                 渲染关卡为可读网格");
        Console.WriteLine("  levelcli validate <path>              验证关卡合法性");
        Console.WriteLine("  levelcli analyze <path> [--count N] [--mode random|greedy|population]");
        Console.WriteLine("  levelcli create <w> <h> -o <path>     创建空白关卡模板");
        Console.WriteLine("  levelcli generate <blueprint> [--start N] [--end N] [--seed N] [-o dir] [--sims N]");
        Console.WriteLine("                                        批量生成关卡 (规则生成+分析+调参)");
        Console.WriteLine("  levelcli smart-generate <blueprint> [--start N] [--end N] [--api-key K] [-o dir]");
        Console.WriteLine("                                        LLM 智能生成关卡 (设计+分析+修订)");
        Console.WriteLine();
        return 1;
    }
}
