using Match3.Core.Config;
using Match3.Core.Diagnostics;

namespace Match3.LevelCli;

static class PrintCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: levelcli print <path>");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        var json = File.ReadAllText(path);
        var config = ConfigParser.ParseLevelConfig(json);
        var title = Path.GetFileName(path);

        Console.WriteLine(BoardFormatter.Format(config, title));
        return 0;
    }
}
