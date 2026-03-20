using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Random;

namespace Match3.LevelCli;

static class CreateCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: levelcli create <width> <height> -o <path>");
            return 1;
        }

        if (!int.TryParse(args[0], out var width) || !int.TryParse(args[1], out var height))
        {
            Console.WriteLine("Width and height must be integers.");
            return 1;
        }

        if (width < 3 || width > 20 || height < 3 || height > 20)
        {
            Console.WriteLine("Width and height must be between 3 and 20.");
            return 1;
        }

        string? outputPath = null;
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "-o")
            {
                outputPath = args[i + 1];
                break;
            }
        }

        if (outputPath == null)
        {
            Console.WriteLine("Output path required: -o <path>");
            return 1;
        }

        var config = new LevelConfig(width, height)
        {
            MoveLimit = 20,
            TargetDifficulty = 0.5f
        };

        // Fill grid with random colors (6 colors)
        var rng = new XorShift64((ulong)Environment.TickCount64);
        for (int i = 0; i < config.Grid.Length; i++)
            config.Grid[i] = (ElementType)rng.Next(1, 7);

        // Default objective: collect 10 Red
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10
        };

        var json = ConfigParser.Serialize(config);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Created: {outputPath} ({width}x{height})");

        return 0;
    }
}
