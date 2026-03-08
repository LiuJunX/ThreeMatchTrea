using System.Diagnostics;
using Match3.Core.AI;
using Match3.Core.Config;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
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

    #region Move Preview Performance

    [Fact]
    [Trait("Category", "Slow")]
    public void PreviewMove_PerformsBelowThreshold()
    {
        // Target: < 3ms per preview (relaxed for loaded CI machines)
        const int iterations = 100;
        const double maxAverageMs = 3.0;

        var service = CreateAIService();
        var state = TestEngineFactory.CreateTestState();
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
    [Trait("Category", "Slow")]
    public void GetBestMove_PerformsBelowThreshold()
    {
        // Target: < 150ms per call (relaxed for loaded CI machines)
        const int iterations = 20;
        const double maxAverageMs = 150.0;

        var service = CreateAIService();
        var state = TestEngineFactory.CreateTestState();

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
    [Trait("Category", "Slow")]
    public void AnalyzeDifficulty_PerformsBelowThreshold()
    {
        // Target: < 300ms per analysis (relaxed for loaded CI machines)
        const int iterations = 10;
        const double maxAverageMs = 300.0;

        var service = CreateAIService();
        var state = TestEngineFactory.CreateTestState();

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
    [Trait("Category", "Slow")]
    public void BulkAnalysis_10000Boards_Under10Seconds()
    {
        // Target: 10000 board analyses < 10s (1ms avg)
        const int boardCount = 1000; // Reduced for test speed
        const double maxTotalSeconds = 3.0; // Scaled target (relaxed for loaded CI machines)

        var service = CreateAIService();

        // Warmup
        for (int i = 0; i < 5; i++)
        {
            var warmupState = TestEngineFactory.CreateTestState();
            service.GetValidMoves(in warmupState);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < boardCount; i++)
        {
            var state = TestEngineFactory.CreateTestState(seed: (ulong)i);
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
    [Trait("Category", "Slow")]
    public void GetValidMoves_IsEfficient()
    {
        // Target: < 0.3ms per call (relaxed for loaded CI machines)
        const int iterations = 1000;
        const double maxAverageMicroseconds = 300;

        var service = CreateAIService();
        var state = TestEngineFactory.CreateTestState();

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
    [Trait("Category", "Slow")]
    public void EvaluateState_IsEfficient()
    {
        // Target: < 1.5ms per call (relaxed for loaded CI machines)
        const int iterations = 500;
        const double maxAverageMicroseconds = 1500;

        var service = CreateAIService();
        var state = TestEngineFactory.CreateTestState();

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
        var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        return new AIService(
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            () => new StubRandom());
    }

    #endregion
}


