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
            "validate" => ValidateCommand.Run(commandArgs),
            "analyze" => await AnalyzeCommand.RunAsync(commandArgs),
            "create" => CreateCommand.Run(commandArgs),
            _ => PrintUsage()
        };
    }

    static int PrintUsage()
    {
        Console.WriteLine("Match3 Level CLI - 关卡协作工具");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  levelcli validate <path>              验证关卡合法性");
        Console.WriteLine("  levelcli analyze <path> [--count N] [--mode random|greedy|population]");
        Console.WriteLine("  levelcli create <w> <h> -o <path>     创建空白关卡模板");
        Console.WriteLine();
        return 1;
    }
}
