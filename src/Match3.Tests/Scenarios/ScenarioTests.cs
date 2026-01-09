using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Match3.Core;
using Match3.Core.Interfaces;
using Match3.Core.Logic;
using Match3.Core.Structs;
using Match3.Core.Config;
using Xunit;
using Match3.Random;

namespace Match3.Tests.Scenarios
{
    public class ScenarioTests
    {
        private static List<TestScenario> LoadScenarios()
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Scenarios", "Data");
            if (!Directory.Exists(dataDir))
            {
                var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Scenarios/Data"));
                if (Directory.Exists(projectDir))
                    dataDir = projectDir;
                else
                    throw new DirectoryNotFoundException($"Scenarios directory not found at: {dataDir}");
            }

            var files = Directory.GetFiles(dataDir, "*.json", SearchOption.AllDirectories);
            var scenarios = new List<TestScenario>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var scenario = JsonSerializer.Deserialize<TestScenario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (scenario != null)
                    {
                        scenarios.Add(scenario);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load scenario {file}: {ex.Message}");
                }
            }

            return scenarios;
        }

        [Fact]
        public void RunScenarios()
        {
            var scenarios = LoadScenarios();
            if (scenarios.Count == 0) return;

            foreach (var scenario in scenarios)
            {
                // 1. Setup Dependencies
                var seed = scenario.Seed > 0 ? scenario.Seed : 42;
                var seedManager = new SeedManager(seed);
                var rng = seedManager.GetRandom(RandomDomain.Main);
                var view = new NullView();
                var logger = new ConsoleGameLogger();
                var config = new Match3Config(scenario.Width, scenario.Height, 6);
                
                // 2. Setup Initial State from Layout
                var levelConfig = new LevelConfig(scenario.Width, scenario.Height);
                for (int y = 0; y < scenario.Height; y++)
                {
                    var row = scenario.Layout[y].Split(',', StringSplitOptions.TrimEntries);
                    for (int x = 0; x < scenario.Width; x++)
                    {
                        var index = y * scenario.Width + x;
                        levelConfig.Grid[index] = ParseType(row[x]);
                        // Note: Bomb parsing from layout string not fully supported in simple ParseType
                        // But if layout has bombs, we might need a richer parser or use the ScenarioEditor's output format.
                        // Currently ScenarioEditor exports Type codes. If Bomb is needed, we might need to update export or manual edit.
                    }
                }

                var controller = new Match3Controller(
                    config,
                    rng,
                    view,
                    new ClassicMatchFinder(),
                    new StandardMatchProcessor(),
                    new StandardGravitySystem(new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill))),
                    new PowerUpHandler(),
                    new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill)),
                    logger,
                    levelConfig
                );
                
                // 3. Apply Moves
                foreach (var move in scenario.Moves)
                {
                    var p1 = ParsePos(move.From);
                    var p2 = ParsePos(move.To);

                    // Simulate Input
                    controller.OnTap(p1);
                    controller.OnTap(p2);

                    // Pump Loop until Idle
                    // We need a fail-safe max steps to prevent infinite loops in broken tests
                    int maxSteps = 1000;
                    while (!controller.IsIdle && maxSteps-- > 0)
                    {
                        controller.Update(0.016f); // 16ms steps
                    }
                    
                    if (maxSteps <= 0)
                    {
                        throw new Exception($"Scenario {scenario.Name} timed out (infinite loop?)");
                    }
                }
                
                // 4. Validate Expectations
                foreach (var exp in scenario.Expectations)
                {
                    var tile = controller.State.GetTile(exp.X, exp.Y);
                    if (exp.Type != null)
                    {
                        var expectedType = ParseType(exp.Type); 
                        Assert.Equal(expectedType, tile.Type);
                    }
                    if (exp.Bomb != null)
                    {
                        var expectedBomb = ParseBombType(exp.Bomb);
                        Assert.Equal(expectedBomb, tile.Bomb);
                    }
                }
            }
        }

        private TileType ParseType(string code)
        {
            return code switch
            {
                "_" => TileType.None,
                "R" => TileType.Red,
                "G" => TileType.Green,
                "B" => TileType.Blue,
                "Y" => TileType.Yellow,
                "P" => TileType.Purple,
                "O" => TileType.Orange,
                "Rainbow" => TileType.Rainbow,
                _ => Enum.TryParse<TileType>(code, out var t) ? t : TileType.None
            };
        }

        private BombType ParseBombType(string code)
        {
            return Enum.TryParse<BombType>(code, true, out var b) ? b : BombType.None;
        }

        private Position ParsePos(string pos)
        {
            var parts = pos.Split(',');
            return new Position(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        private class NullView : IGameView
        {
            public void RenderBoard(TileType[,] board) { }
            public void ShowSwap(Position a, Position b, bool success) { }
            public void ShowMatches(IReadOnlyCollection<Position> matched) { }
            public void ShowGravity(IEnumerable<TileMove> moves) { }
            public void ShowRefill(IEnumerable<TileMove> moves) { }
        }
    }
}
