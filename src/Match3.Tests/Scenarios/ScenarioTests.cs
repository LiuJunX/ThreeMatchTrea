using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Match3.Core;
using Match3.Core.Interfaces;
using Match3.Core.Logic;
using Match3.Core.Structs;
using Xunit;
using Match3.Random;

namespace Match3.Tests.Scenarios
{
    public class ScenarioTests
    {
        public static IEnumerable<object[]> GetScenarios()
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
            if (files.Length == 0)
            {
                 throw new FileNotFoundException($"No JSON files found in: {dataDir}");
            }

            var scenarios = new List<object[]>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var scenario = JsonSerializer.Deserialize<TestScenario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (scenario != null)
                    {
                        scenarios.Add(new object[] { scenario });
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load scenario {file}: {ex.Message}");
                }
            }

            return scenarios;
        }

        [Theory]
        [MemberData(nameof(GetScenarios))]
        public void RunScenario(TestScenario scenario)
        {
            var rng = new DefaultRandom(42);
            var state = new GameState(scenario.Width, scenario.Height, 6, rng);
            
            // Systems
            var tileGen = new StandardTileGenerator(new DefaultRandom(4242));
            var finder = new ClassicMatchFinder();
            var processor = new StandardMatchProcessor();
            var gravity = new StandardGravitySystem(tileGen);
            var powerUp = new PowerUpHandler();

            // Parse layout
            for (int y = 0; y < scenario.Height; y++)
            {
                var row = scenario.Layout[y].Split(',', StringSplitOptions.TrimEntries);
                for (int x = 0; x < scenario.Width; x++)
                {
                    var type = ParseType(row[x]);
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                }
            }
            
            // Execute moves
            foreach (var move in scenario.Moves)
            {
                var p1 = ParsePos(move.From);
                var p2 = ParsePos(move.To);
                
                // Execute Logic Loop
                ApplyMove(ref state, p1, p2, finder, processor, gravity, powerUp);
            }
            
            // Verify expectations
            foreach (var exp in scenario.Expectations)
            {
                var tile = state.GetTile(exp.X, exp.Y);
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

        private void ApplyMove(
            ref GameState state, 
            Position from, 
            Position to, 
            IMatchFinder finder, 
            IMatchProcessor processor, 
            IGravitySystem gravity,
            IPowerUpHandler powerUp)
        {
            // 1. Swap
            Swap(ref state, from, to);

            // 2. Check Special
            var t1 = state.GetTile(from.X, from.Y);
            var t2 = state.GetTile(to.X, to.Y);
            bool isSpecial = (t1.Bomb != BombType.None || t1.Type == TileType.Rainbow) && 
                             (t2.Bomb != BombType.None || t2.Type == TileType.Rainbow);
            
            // Also Color Mix
            if (!isSpecial && (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow)) isSpecial = true;

            if (isSpecial)
            {
                powerUp.ProcessSpecialMove(ref state, from, to, out _);
            }
            else
            {
                if (!finder.HasMatches(in state))
                {
                    Swap(ref state, from, to); // Revert
                    return;
                }
            }

            // 3. Loop
            while (true)
            {
                var groups = finder.FindMatchGroups(in state);
                if (groups.Count == 0) break;

                processor.ProcessMatches(ref state, groups);
                gravity.ApplyGravity(ref state);
                gravity.Refill(ref state);
            }
        }

        private void Swap(ref GameState state, Position a, Position b)
        {
            var idxA = state.Index(a.X, a.Y);
            var idxB = state.Index(b.X, b.Y);
            var temp = state.Grid[idxA];
            state.Grid[idxA] = state.Grid[idxB];
            state.Grid[idxB] = temp;
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
    }
}
