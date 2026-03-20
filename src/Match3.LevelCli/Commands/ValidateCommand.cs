using Match3.Core.Config;
using Match3.Editor.Validation;

namespace Match3.LevelCli;

static class ValidateCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: levelcli validate <path>");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return 2;
        }

        var json = File.ReadAllText(path);
        var config = ConfigParser.ParseLevelConfig(json);
        var validator = new LevelValidator();
        var result = validator.Validate(config);

        Console.WriteLine($"=== Validate: {Path.GetFileName(path)} ({config.Width}x{config.Height}) ===");

        if (result.IsValid)
        {
            Console.WriteLine("PASS");
        }

        foreach (var msg in result.Messages)
        {
            var prefix = msg.Severity == Severity.Error ? "ERROR" : "WARN";
            Console.WriteLine($"  [{prefix}] {msg.Message}");
        }

        if (result.IsValid && result.Messages.Count == 0)
        {
            Console.WriteLine("  No issues found.");
        }

        return result.IsValid ? 0 : 2;
    }
}
