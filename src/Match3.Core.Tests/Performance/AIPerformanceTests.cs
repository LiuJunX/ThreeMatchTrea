using System.Diagnostics;
using Match3.Core.AI;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Performance;

/// <summary>
/// Performance tests for AI service.
/// Target benchmarks:
/// - Single move preview: &lt; 1ms
/// - GetBestMove: &lt; 50ms
/// - Full difficulty analysis: &lt; 100ms
/// </summary>
[Trait("Category", "Performance")]
public class AIPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public AIPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubRandom : IRandom
    {
        private ulong _state = 12345;
        public float NextFloat() => (float)(Next(1000) / 1000.0);
        public int Next(int max) => max > 0 ? (int)(_state++ % (ulong)max) : 0;
        public int Next(int min, int max) => max > min ? min + Next(max - min) : min;
        public void SetState(ulong state) { _state = state; }
        public ulong GetState() => _state;
    }

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2) => 100;
    }

    private class StubSpawnModel : ISpawnModel
    {
        private int _counter = 0;
        private static readonly TileType[] _types = { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow, TileType.Purple };

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            return _types[(_counter++ + spawnX) % _types.Length];
        }
    }

    #region Move Preview Performance

    [Fact]
    public void PreviewMove_PerformsBelowThreshold()
    {
        // Target: < 1ms per preview
        const int iterations = 100;
        const double maxAverageMs = 1.0;

        var service = CreateAIService();
        var state = CreateTestState(8, 8);
        var moves = service.GetValidMoves(in state);

        Assert.NotEmpty(moves);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            service.PreviewMove(in state, moves[i % moves.Count]);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            service.PreviewMove(in state, moves[i % moves.Count]);
        }

        sw.Stop();

        double averageMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"PreviewMove average: {averageMs:F3}ms over {iterations} iterations");

        Assert.True(averageMs < maxAverageMs,
            $"Average preview time {averageMs:F3}ms exceeds threshold {maxAverageMs}ms");
    }

    #endregion

    #region GetBestMove Performance

    [Fact]
    public void GetBestMove_PerformsBelowThreshold()
    {
        // Target: < 50ms per call
        const int iterations = 20;
        const double maxAverageMs = 50.0;

        var service = CreateAIService();
        var state = CreateTestState(8, 8);

        // Warmup
        for (int i = 0; i < 3; i++)
        {
            service.GetBestMove(in state);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            service.GetBestMove(in state);
        }

        sw.Stop();

        double averageMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"GetBestMove average: {averageMs:F2}ms over {iterations} iterations");

        Assert.True(averageMs < maxAverageMs,
            $"Average GetBestMove time {averageMs:F2}ms exceeds threshold {maxAverageMs}ms");
    }

    #endregion

    #region Difficulty Analysis Performance

    [Fact]
    public void AnalyzeDifficulty_PerformsBelowThreshold()
    {
        // Target: < 100ms per analysis
        const int iterations = 10;
        const double maxAverageMs = 100.0;

        var service = CreateAIService();
        var state = CreateTestState(8, 8);

        // Warmup
        for (int i = 0; i < 2; i++)
        {
            service.AnalyzeDifficulty(in state);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            service.AnalyzeDifficulty(in state);
        }

        sw.Stop();

        double averageMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"AnalyzeDifficulty average: {averageMs:F2}ms over {iterations} iterations");

        Assert.True(averageMs < maxAverageMs,
            $"Average AnalyzeDifficulty time {averageMs:F2}ms exceeds threshold {maxAverageMs}ms");
    }

    #endregion

    #region Bulk Analysis Performance

    [Fact]
    public void BulkAnalysis_10000Boards_Under10Seconds()
    {
        // Target: 10000 board analyses < 10s (1ms avg)
        const int boardCount = 1000; // Reduced for test speed
        const double maxTotalSeconds = 1.0; // Scaled target

        var service = CreateAIService();

        // Warmup
        for (int i = 0; i < 5; i++)
        {
            var warmupState = CreateTestState(8, 8);
            service.GetValidMoves(in warmupState);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < boardCount; i++)
        {
            var state = CreateTestState(8, 8, seed: (ulong)i);
            var moves = service.GetValidMoves(in state);
            _ = service.EvaluateState(in state);
        }

        sw.Stop();

        double totalSeconds = sw.Elapsed.TotalSeconds;
        double avgMs = sw.Elapsed.TotalMilliseconds / boardCount;

        _output.WriteLine($"Analyzed {boardCount} boards in {totalSeconds:F2}s");
        _output.WriteLine($"Average per board: {avgMs:F3}ms");

        Assert.True(totalSeconds < maxTotalSeconds,
            $"Bulk analysis took {totalSeconds:F2}s, exceeds threshold {maxTotalSeconds}s");
    }

    #endregion

    #region GetValidMoves Performance

    [Fact]
    public void GetValidMoves_IsEfficient()
    {
        // Target: < 0.1ms per call
        const int iterations = 1000;
        const double maxAverageMicroseconds = 100;

        var service = CreateAIService();
        var state = CreateTestState(8, 8);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            service.GetValidMoves(in state);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            service.GetValidMoves(in state);
        }

        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"GetValidMoves average: {avgMicroseconds:F2}µs over {iterations} iterations");

        Assert.True(avgMicroseconds < maxAverageMicroseconds,
            $"Average GetValidMoves time {avgMicroseconds:F2}µs exceeds threshold {maxAverageMicroseconds}µs");
    }

    #endregion

    #region EvaluateState Performance

    [Fact]
    public void EvaluateState_IsEfficient()
    {
        // Target: < 0.5ms per call
        const int iterations = 500;
        const double maxAverageMicroseconds = 500;

        var service = CreateAIService();
        var state = CreateTestState(8, 8);

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            service.EvaluateState(in state);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            service.EvaluateState(in state);
        }

        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"EvaluateState average: {avgMicroseconds:F2}µs over {iterations} iterations");

        Assert.True(avgMicroseconds < maxAverageMicroseconds,
            $"Average EvaluateState time {avgMicroseconds:F2}µs exceeds threshold {maxAverageMicroseconds}µs");
    }

    #endregion

    #region Helper Methods

    private AIService CreateAIService()
    {
        var random = new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, random);
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        return new AIService(
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            () => new StubRandom());
    }

    private GameState CreateTestState(int width, int height, ulong seed = 12345)
    {
        var random = new StubRandom();
        random.SetState(seed);

        var state = new GameState(width, height, 5, random);
        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow, TileType.Purple };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                // Use seed to vary the pattern
                int typeIdx = (int)(((ulong)x + (ulong)y + seed) % (ulong)types.Length);
                state.SetTile(x, y, new Tile(idx + 1, types[typeIdx], x, y));
            }
        }

        return state;
    }

    #endregion
}
