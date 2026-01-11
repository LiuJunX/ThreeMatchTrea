using Xunit;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;
using Match3.Core.Systems.Matching;
using Match3.Core.Utility; // For IGameLogger
using Match3.Random;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Match3.Core.Tests
{
    public class TryMoveOptimizationTests
    {
        class MockView : IGameView
        {
            public void RenderBoard(TileType[,] board) { }
            public void ShowSwap(Position a, Position b, bool success) { }
            public void ShowMatches(IReadOnlyCollection<Position> matched) { }
            public void ShowGravity(IEnumerable<TileMove> moves) { }
            public void ShowRefill(IEnumerable<TileMove> newTiles) { }
        }

        class MockLogger : IGameLogger
        {
            public void LogError(string message, Exception? ex = null) { }
            public void LogInfo(string message) { }
            public void LogWarning(string message) { }
            public void LogInfo<T>(string message, T args) { }
            public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) { }
            public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) { }
            public void LogWarning<T>(string template, T arg1) { }
        }

        class MockInput : IInputSystem
        {
            public bool IsValidPosition(in GameState state, Position p) => true;
            public Position GetSwipeTarget(Position from, Direction direction) => from; // Simplified
        }

        class MockScore : IScoreSystem
        {
            public int CalculateMatchScore(MatchGroup group) => 10;
            public int Score => 0;
            public void AddScore(int points) { }
            public void Reset() { }
            public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2) => 50;
        }

        class MockGravity : IGravitySystem
        {
            public List<TileMove> ApplyGravity(ref GameState state) => new List<TileMove>();
            public List<TileMove> Refill(ref GameState state) => new List<TileMove>();
        }

        class MockPowerUp : IPowerUpHandler
        {
            public void HandlePowerUp(ref GameState state, Position p, BombType bomb) { }
            public bool TryActivate(ref GameState state, Position p) => false;
            public void ProcessSpecialMove(ref GameState state, Position a, Position b, out int score) { score = 0; }
        }

        class MockTileGen : ITileGenerator
        {
            public TileType GenerateNonMatchingTile(ref GameState state, int x, int y) => TileType.None;
            public TileType GenerateTile(ref GameState state, int x, int y) => TileType.None;
        }

        class MockRandom : IRandom
        {
            public int Next(int min, int max) => min;
            public float NextFloat() => 0f;
            public int Next() => 0;
            public int Next(int max) => 0;
        }

        private Match3Engine CreateEngine(int width, int height)
        {
            var config = new Match3Config { Width = width, Height = height, TileTypesCount = 5 };
            var rng = new MockRandom();
            var view = new MockView();
            var logger = new MockLogger();
            var input = new MockInput();
            var matchFinder = new ClassicMatchFinder();
            var scoreSystem = new MockScore();
            var matchProcessor = new StandardMatchProcessor(scoreSystem);
            var tileGen = new MockTileGen();
            var gravity = new MockGravity();
            var powerUp = new MockPowerUp();

            return new Match3Engine(
                config,
                rng,
                view,
                logger,
                input,
                matchFinder,
                matchProcessor,
                gravity,
                powerUp,
                scoreSystem,
                tileGen
            );
        }

        private bool CallTryMove(Match3Engine engine, Position a, Position b)
        {
            var method = typeof(Match3Engine).GetMethod("TryMove", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new InvalidOperationException("TryMove method not found");
            return (bool)method.Invoke(engine, new object[] { a, b });
        }

        [Fact]
        public void TryMove_ValidHorizontalMatch_ReturnsTrue()
        {
            var engine = CreateEngine(4, 4);
            // Setup board manually
            // 0 1 2 3
            // R R B R
            engine.DebugSetTile(new Position(0, 0), TileType.Red); // R
            engine.DebugSetTile(new Position(1, 0), TileType.Red); // R
            engine.DebugSetTile(new Position(2, 0), TileType.Blue); // B
            engine.DebugSetTile(new Position(3, 0), TileType.Red); // R

            // Swap (2,0) with (3,0) -> R R R B (Match!)
            bool result = CallTryMove(engine, new Position(2, 0), new Position(3, 0));
            Assert.True(result);
        }

        [Fact]
        public void TryMove_NoMatch_ReturnsFalse()
        {
            var engine = CreateEngine(4, 4);
            // R B R B
            engine.DebugSetTile(new Position(0, 0), TileType.Red);
            engine.DebugSetTile(new Position(1, 0), TileType.Blue);
            engine.DebugSetTile(new Position(2, 0), TileType.Red);
            engine.DebugSetTile(new Position(3, 0), TileType.Blue);

            bool result = CallTryMove(engine, new Position(0, 0), new Position(1, 0));
            Assert.False(result);
        }
    }
}
