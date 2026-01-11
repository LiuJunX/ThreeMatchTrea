using System.Collections.Generic;
using System.Linq;
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
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests
{
    public class MockGameLogger : IGameLogger
    {
        public void LogInfo(string message) { }
        public void LogInfo<T>(string template, T arg1) { }
        public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) { }
        public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) { }

        public void LogWarning(string message) { }
        public void LogWarning<T>(string template, T arg1) { }

        public void LogError(string message, System.Exception? ex = null) { }
    }

    public class MockGameViewLocal : IGameView
    {
        public bool? SwapSuccess { get; private set; }
        public List<List<Position>> AllMatches { get; } = new();
        public List<List<TileMove>> AllGravity { get; } = new();
        public List<List<TileMove>> AllRefill { get; } = new();

        public void Reset()
        {
            SwapSuccess = null;
            AllMatches.Clear();
            AllGravity.Clear();
            AllRefill.Clear();
        }

        public void RenderBoard(TileType[,] board) { }
        
        public void ShowSwap(Position a, Position b, bool success) 
        {
            SwapSuccess = success;
        }
        
        public void ShowMatches(IReadOnlyCollection<Position> matched) 
        {
            AllMatches.Add(new List<Position>(matched));
        }

        public void ShowMatches(HashSet<Position> positions)
        {
            AllMatches.Add(new List<Position>(positions));
        }
        
        public void ShowGravity(IEnumerable<TileMove> moves) 
        {
            AllGravity.Add(new List<TileMove>(moves));
        }
        public void ShowRefill(IEnumerable<TileMove> newTiles) 
        {
            AllRefill.Add(new List<TileMove>(newTiles));
        }
    }

    public class Match3EngineTests
    {
        [Fact]
        public void TryMakeRandomMove_FindsValidMove_AndStartsSwap()
        {
            // Arrange
            int w = 4;
            int h = 4;
            var config = new Match3Config(w, h, 4);
            var seedManager = new SeedManager(12345);
            var rng = seedManager.GetRandom(RandomDomain.Main);
            
            var view = new MockGameViewLocal();
            var logger = new MockGameLogger();
            var inputSystem = new StandardInputSystem();
            var matchFinder = new ClassicMatchFinder();
            var scoreSystem = new StandardScoreSystem();
            var tileGen = new StandardTileGenerator(rng);
            var gravity = new StandardGravitySystem(tileGen);
            var processor = new StandardMatchProcessor(scoreSystem);
            var powerUp = new PowerUpHandler(scoreSystem);

            var engine = new Match3Engine(
                config, rng, view, logger, inputSystem,
                matchFinder, processor, gravity, powerUp, scoreSystem, tileGen
            );

            // Manually set tiles to ensure a specific move exists
            // R R B R
            engine.DebugSetTile(new Position(0, 0), TileType.Red);
            engine.DebugSetTile(new Position(1, 0), TileType.Red);
            engine.DebugSetTile(new Position(2, 0), TileType.Blue);
            engine.DebugSetTile(new Position(3, 0), TileType.Red);
            
            // Fill rest with noise
            for (int y = 1; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    engine.DebugSetTile(new Position(x, y), (TileType)((x + y) % 3 + 1));
                }
            }

            // Act
            bool result = engine.TryMakeRandomMove();

            // Assert
            Assert.True(result, "Should find a valid move");
            Assert.False(engine.IsIdle, "Engine should not be idle after starting a move");
            
            // Verify view was notified of swap
            Assert.True(view.SwapSuccess.HasValue, "Swap should be initiated");
        }

        [Fact]
        public void TryMakeRandomMove_NoMoves_ReturnsFalse()
        {
             // Arrange
            int w = 3;
            int h = 3;
            var config = new Match3Config(w, h, 4);
            var seedManager = new SeedManager(12345);
            var rng = seedManager.GetRandom(RandomDomain.Main);
            
            var view = new MockGameViewLocal();
            var logger = new MockGameLogger();
            var inputSystem = new StandardInputSystem();
            var matchFinder = new ClassicMatchFinder();
            var scoreSystem = new StandardScoreSystem();
            var tileGen = new StandardTileGenerator(rng);
            var gravity = new StandardGravitySystem(tileGen);
            var processor = new StandardMatchProcessor(scoreSystem);
            var powerUp = new PowerUpHandler(scoreSystem);

            var engine = new Match3Engine(
                config, rng, view, logger, inputSystem,
                matchFinder, processor, gravity, powerUp, scoreSystem, tileGen
            );

            // Set checkerboard pattern (no matches possible)
            // R G B
            // G B R
            // B R G
            // (Latin square pattern with 3 colors is resistant to single swaps)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // (x + y) % 3
                    // 0 -> Red, 1 -> Green, 2 -> Blue
                    int val = (x + y) % 3;
                    var type = val == 0 ? TileType.Red : (val == 1 ? TileType.Green : TileType.Blue);
                    engine.DebugSetTile(new Position(x, y), type);
                }
            }

            // Act
            bool result = engine.TryMakeRandomMove();

            // Assert
            Assert.False(result, "Should not find any moves");
            Assert.True(engine.IsIdle, "Engine should remain idle");
        }
    }
}
