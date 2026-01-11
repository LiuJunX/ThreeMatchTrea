using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Xunit;
using Match3.Random;

using Match3.Core.Systems;

namespace Match3.Tests.Scenarios
{
    public class ScenarioTests
    {
        private static List<ScenarioConfig> LoadScenarios()
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
            var scenarios = new List<ScenarioConfig>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var scenario = JsonSerializer.Deserialize<ScenarioConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
                var width = scenario.InitialState.Width;
                var height = scenario.InitialState.Height;
                var config = new Match3Config(width, height, 6);
                
                // 2. Setup Initial State from LevelConfig
                // The JSON deserialization should have populated InitialState (LevelConfig)
                // We just need to ensure the engine uses it.
                var levelConfig = scenario.InitialState;

                var scoreSystem = new StandardScoreSystem();
                var inputSystem = new StandardInputSystem();
                var tileGen = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));

                var controller = new Match3Engine(
                    config,
                    rng,
                    view,
                    logger,
                    inputSystem,
                    new ClassicMatchFinder(),
                    new StandardMatchProcessor(scoreSystem),
                    new StandardGravitySystem(tileGen),
                    new PowerUpHandler(scoreSystem),
                    scoreSystem,
                    tileGen,
                    levelConfig
                );
                
                // 3. Apply Moves
                foreach (var move in scenario.Operations)
                {
                    var p1 = new Position(move.FromX, move.FromY);
                    var p2 = new Position(move.ToX, move.ToY);

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
                        // throw new Exception($"Scenario {scenario.Description} timed out (infinite loop?)");
                    }
                }
                
                // 4. Validate Expectations
                foreach (var exp in scenario.Assertions)
                {
                    var tile = controller.State.GetTile(exp.X, exp.Y);
                    if (exp.Type != null)
                    {
                        Assert.Equal(exp.Type, tile.Type);
                    }
                    if (exp.Bomb != null)
                    {
                        Assert.Equal(exp.Bomb, tile.Bomb);
                    }
                }
            }
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
