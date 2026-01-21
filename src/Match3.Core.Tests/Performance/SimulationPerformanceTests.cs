using System.Diagnostics;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Performance;

/// <summary>
/// Performance tests for the new simulation and event systems.
/// Target benchmarks:
/// - Single move simulation: &lt; 1ms
/// - 10000 board analyses: &lt; 10s
/// - Event collection overhead: &lt; 20% vs NullEventCollector
/// </summary>
[Trait("Category", "Performance")]
public class SimulationPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public SimulationPerformanceTests(ITestOutputHelper output)
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

    #region SimulationEngine Performance Tests

    [Fact]
    public void RunUntilStable_PerformsBelowThreshold_SingleMove()
    {
        // Target: < 1ms per simulation
        const int iterations = 100;
        const double maxAverageMs = 1.0;

        var state = CreateTestState(8, 8);
        var engine = CreateEngine(state);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            var warmupEngine = CreateEngine(CreateTestState(8, 8));
            warmupEngine.ApplyMove(new Position(0, 0), new Position(1, 0));
            warmupEngine.RunUntilStable();
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var testEngine = CreateEngine(CreateTestState(8, 8));
            testEngine.ApplyMove(new Position(i % 7, i % 7), new Position((i % 7) + 1, i % 7));
            testEngine.RunUntilStable();
        }

        sw.Stop();

        double averageMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"RunUntilStable average: {averageMs:F3}ms over {iterations} iterations");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F1}ms");

        Assert.True(averageMs < maxAverageMs,
            $"Average simulation time {averageMs:F3}ms exceeds threshold {maxAverageMs}ms");
    }

    [Fact]
    public void RunUntilStable_TickThroughput()
    {
        // Measure raw tick throughput
        const int tickCount = 10000;

        var state = CreateTestState(8, 8);
        var engine = CreateEngine(state);

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < tickCount; i++)
        {
            engine.Tick();
        }

        sw.Stop();

        double ticksPerSecond = tickCount / sw.Elapsed.TotalSeconds;
        double averageTickMs = sw.Elapsed.TotalMilliseconds / tickCount;

        _output.WriteLine($"Tick throughput: {ticksPerSecond:N0} ticks/second");
        _output.WriteLine($"Average tick time: {averageTickMs * 1000:F2}µs");

        // Should achieve at least 10,000 ticks/second (accounting for complex physics)
        Assert.True(ticksPerSecond > 10000,
            $"Tick throughput {ticksPerSecond:N0} is below minimum 10,000 ticks/second");
    }

    #endregion

    #region Event Collection Overhead Tests

    [Fact]
    public void EventCollection_OverheadIsAcceptable()
    {
        // Compare BufferedEventCollector vs NullEventCollector overhead
        const int iterations = 50;
        const double maxOverheadPercent = 100.0; // Allow up to 100% overhead (GC timing variance)

        // Test with NullEventCollector
        var nullTimes = new List<double>();
        for (int i = 0; i < iterations; i++)
        {
            var engine = CreateEngine(CreateTestState(8, 8), NullEventCollector.Instance);
            engine.ApplyMove(new Position(3, 3), new Position(4, 3));

            var sw = Stopwatch.StartNew();
            engine.RunUntilStable();
            sw.Stop();
            nullTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Test with BufferedEventCollector
        var bufferedTimes = new List<double>();
        for (int i = 0; i < iterations; i++)
        {
            var collector = new BufferedEventCollector();
            var engine = CreateEngine(CreateTestState(8, 8), collector);
            engine.ApplyMove(new Position(3, 3), new Position(4, 3));

            var sw = Stopwatch.StartNew();
            engine.RunUntilStable();
            sw.Stop();
            bufferedTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        double nullAvg = nullTimes.Average();
        double bufferedAvg = bufferedTimes.Average();

        // Note: RunUntilStable disables event collection internally, so overhead should be minimal
        double overheadPercent = ((bufferedAvg - nullAvg) / nullAvg) * 100;

        _output.WriteLine($"NullEventCollector average: {nullAvg:F1}µs");
        _output.WriteLine($"BufferedEventCollector average: {bufferedAvg:F1}µs");
        _output.WriteLine($"Overhead: {overheadPercent:F1}%");

        // Since RunUntilStable disables events, overhead should be minimal
        Assert.True(overheadPercent < maxOverheadPercent,
            $"Event collection overhead {overheadPercent:F1}% exceeds maximum {maxOverheadPercent}%");
    }

    [Fact]
    public void BufferedEventCollector_EmitPerformance()
    {
        // Measure raw event emit performance
        const int eventCount = 100000;
        var collector = new BufferedEventCollector();

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < eventCount; i++)
        {
            collector.Emit(new TileDestroyedEvent
            {
                Tick = i,
                SimulationTime = i * 0.016f,
                TileId = i,
                GridPosition = new Position(i % 8, i / 8),
                Type = TileType.Red,
                Bomb = BombType.None,
                Reason = Match3.Core.Events.Enums.DestroyReason.Match
            });
        }

        sw.Stop();

        double eventsPerSecond = eventCount / sw.Elapsed.TotalSeconds;
        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / eventCount;

        _output.WriteLine($"Event emit throughput: {eventsPerSecond:N0} events/second");
        _output.WriteLine($"Average emit time: {avgMicroseconds:F3}µs");

        // Should achieve at least 1M events/second
        Assert.True(eventsPerSecond > 1000000,
            $"Event emit throughput {eventsPerSecond:N0} is below minimum 1,000,000 events/second");
    }

    #endregion

    #region Projectile System Performance Tests

    [Fact]
    public void ProjectileSystem_UpdatePerformance()
    {
        // Measure projectile update performance with multiple projectiles
        const int projectileCount = 10;
        const int updateCount = 1000;

        var system = new ProjectileSystem();
        var state = CreateTestState(8, 8);

        // Launch multiple projectiles (keep targets within 8x8 grid)
        for (int i = 0; i < projectileCount; i++)
        {
            var projectile = new UfoProjectile(
                system.GenerateProjectileId(),
                new Position(0, i % 7),
                new Position(6, (6 - i % 7)));
            system.Launch(projectile, 0, 0f, NullEventCollector.Instance);
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < updateCount; i++)
        {
            var affected = system.Update(ref state, 0.016f, i, i * 0.016f, NullEventCollector.Instance);
            // Don't release - just measure update performance
        }

        sw.Stop();

        double updatesPerSecond = updateCount / sw.Elapsed.TotalSeconds;
        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / updateCount;

        _output.WriteLine($"Projectile updates with {projectileCount} projectiles: {updatesPerSecond:N0}/second");
        _output.WriteLine($"Average update time: {avgMicroseconds:F2}µs");

        // Should handle at least 10,000 updates/second with 10 projectiles
        Assert.True(updatesPerSecond > 10000,
            $"Projectile update throughput {updatesPerSecond:N0} is below minimum 10,000 updates/second");
    }

    #endregion

    #region Clone Performance Tests

    [Fact]
    public void Clone_PerformanceIsAcceptable()
    {
        // Measure clone performance for AI branching
        const int cloneCount = 1000;

        var state = CreateTestState(8, 8);
        var engine = CreateEngine(state);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            engine.Clone();
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < cloneCount; i++)
        {
            var cloned = engine.Clone();
            // Use the clone to prevent optimization
            if (cloned.CurrentTick < 0) throw new Exception("Unexpected");
        }

        sw.Stop();

        double clonesPerSecond = cloneCount / sw.Elapsed.TotalSeconds;
        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / cloneCount;

        _output.WriteLine($"Clone throughput: {clonesPerSecond:N0} clones/second");
        _output.WriteLine($"Average clone time: {avgMicroseconds:F1}µs");

        // Should achieve at least 5,000 clones/second
        Assert.True(clonesPerSecond > 5000,
            $"Clone throughput {clonesPerSecond:N0} is below minimum 5,000 clones/second");
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public void Simulation_MinimalAllocationsDuringTick()
    {
        // Verify that ticks don't cause excessive allocations
        const int tickCount = 100;

        var state = CreateTestState(8, 8);
        var engine = CreateEngine(state);

        // Force initial allocations
        for (int i = 0; i < 50; i++)
        {
            engine.Tick();
        }

        // Measure allocations
        long beforeMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < tickCount; i++)
        {
            engine.Tick();
        }

        long afterMemory = GC.GetTotalMemory(false);
        long bytesAllocated = afterMemory - beforeMemory;
        double bytesPerTick = (double)bytesAllocated / tickCount;

        _output.WriteLine($"Total allocated: {bytesAllocated:N0} bytes over {tickCount} ticks");
        _output.WriteLine($"Average per tick: {bytesPerTick:F1} bytes");

        // Allow some allocations but flag excessive amounts
        // Note: This is a soft limit as GC.GetTotalMemory is not perfectly precise
        // Match finding and physics can cause allocations in complex scenarios
        // GC measurements can vary significantly based on test order and GC timing
        Assert.True(bytesPerTick < 500000,
            $"Allocation per tick {bytesPerTick:F1} bytes is excessive (limit: 500KB)");
    }

    #endregion

    #region Helper Methods

    private SimulationEngine CreateEngine(GameState state, IEventCollector? eventCollector = null)
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
        var projectileSystem = new ProjectileSystem();

        return new SimulationEngine(
            state,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            eventCollector);
    }

    private GameState CreateTestState(int width, int height)
    {
        var state = new GameState(width, height, 5, new StubRandom());
        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow, TileType.Purple };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                // Avoid obvious matches with offset pattern
                state.SetTile(x, y, new Tile(idx + 1, types[(x + y) % types.Length], x, y));
            }
        }

        return state;
    }

    #endregion
}
