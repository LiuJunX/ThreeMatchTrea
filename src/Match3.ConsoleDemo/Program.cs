using System;
using System.Collections.Generic;
using System.IO;
using Match3.Core;
using Match3.Core.Config;

sealed class ConsoleView : IGameView
{
    public void RenderBoard(TileType[,] board)
    {
        var h = board.GetLength(1);
        var w = board.GetLength(0);
        Console.WriteLine("Board:");
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                Console.Write(TileChar(board[x, y]));
                Console.Write(' ');
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }
    public void ShowSwap(Position a, Position b, bool success)
    {
        Console.WriteLine($"Swap ({a.X},{a.Y}) <-> ({b.X},{b.Y}) {(success ? "OK" : "Rejected")}");
    }
    public void ShowMatches(IReadOnlyCollection<Position> matched)
    {
        Console.WriteLine($"Matches: {matched.Count}");
    }
    public void ShowGravity()
    {
        Console.WriteLine("Gravity");
    }
    public void ShowRefill()
    {
        Console.WriteLine("Refill");
    }
    private static char TileChar(TileType t) => t switch
    {
        TileType.Red => 'R',
        TileType.Green => 'G',
        TileType.Blue => 'B',
        TileType.Yellow => 'Y',
        TileType.Purple => 'P',
        TileType.Orange => 'O',
        _ => '.'
    };
}
sealed class Program
{
    static void Main(string[] args)
    {
        // --- Config Loading Demo ---
        Console.WriteLine("--- Initializing Game Config ---");
        var configPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../config.bin"));
        var configManager = new ConfigManager();
        try
        {
            configManager.Load(configPath);
            Console.WriteLine("Config Loaded Successfully!");
            foreach (var item in configManager.GetAllItems())
            {
                Console.WriteLine($"Loaded: {item}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
            Console.WriteLine($"Looking at: {configPath}");
        }
        Console.WriteLine("--------------------------------");

        int seed = Environment.TickCount;
        if (args.Length > 0 && int.TryParse(args[0], out var s))
        {
            seed = s;
        }
        else
        {
            Console.WriteLine("You can provide a seed as the first argument.");
        }

        Console.WriteLine($"Using Seed: {seed}");
        var rng = new DefaultRandom(seed);
        // Deprecated: var board = new GameBoard(8, 8, 6, rng);
        var view = new ConsoleView();
        // Updated to use the new Match3Controller constructor (width, height, tileCount, rng, view)
        var controller = new Match3Controller(8, 8, 6, rng, view);
        Console.WriteLine("Enter swap as: x1 y1 x2 y2, or 'q' to quit");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null) break;
            if (line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) break;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4) { Console.WriteLine("Format: x1 y1 x2 y2"); continue; }
            if (!int.TryParse(parts[0], out var x1) || !int.TryParse(parts[1], out var y1) ||
                !int.TryParse(parts[2], out var x2) || !int.TryParse(parts[3], out var y2))
            { Console.WriteLine("Invalid numbers"); continue; }
            controller.TrySwap(new Position(x1, y1), new Position(x2, y2));
        }
    }
}
