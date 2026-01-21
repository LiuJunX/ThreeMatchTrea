using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Match3.Core.AI;
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
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Utility;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

using Move = Match3.Core.AI.Move;

namespace Match3.Core.Tests.Performance;

/// <summary>
/// Monte Carlo simulation performance tests.
/// These tests measure the feasibility of running multiple complete game simulations
/// for level analysis purposes.
/// </summary>
[Trait("Category", "Performance")]
public class MonteCarloSimulationTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// ThreadLocal cache for simulation components to avoid per-simulation allocations.
    /// Each thread gets its own context to avoid contention.
    /// </summary>
    private static readonly ThreadLocal<SimulationContext> _simulationContext =
        new(() => new SimulationContext(), trackAllValues: false);

    /// <summary>
    /// Cached simulation components for reuse across multiple simulations.
    /// </summary>
    private sealed class SimulationContext
    {
        public readonly Match3Config Config = new();
        public readonly XorShift64 StateRandom = new();
        public readonly XorShift64 MoveRandom = new();
        public readonly BombGenerator BombGenerator = new();
        public readonly SimpleScoreSystem ScoreSystem = new();
        public readonly BombEffectRegistry BombEffects = BombEffectRegistry.CreateDefault();

        // These need config/random, created lazily
        private RealtimeGravitySystem? _physics;
        private RandomSpawnModel? _spawnModel;
        private RealtimeRefillSystem? _refill;
        private ClassicMatchFinder? _matchFinder;
        private StandardMatchProcessor? _matchProcessor;
        private PowerUpHandler? _powerUpHandler;

        private int _lastTileTypesCount = -1;

        public RealtimeGravitySystem GetPhysics()
        {
            return _physics ??= new RealtimeGravitySystem(Config, MoveRandom);
        }

        public RandomSpawnModel GetSpawnModel(int tileTypesCount)
        {
            if (_spawnModel == null)
            {
                _spawnModel = new RandomSpawnModel(tileTypesCount);
                _lastTileTypesCount = tileTypesCount;
            }
            return _spawnModel;
        }

        public RealtimeRefillSystem GetRefill(int tileTypesCount)
        {
            if (_refill == null)
            {
                _refill = new RealtimeRefillSystem(GetSpawnModel(tileTypesCount));
            }
            return _refill;
        }

        public ClassicMatchFinder GetMatchFinder()
        {
            return _matchFinder ??= new ClassicMatchFinder(BombGenerator);
        }

        public StandardMatchProcessor GetMatchProcessor()
        {
            return _matchProcessor ??= new StandardMatchProcessor(ScoreSystem, BombEffects);
        }

        public PowerUpHandler GetPowerUpHandler()
        {
            return _powerUpHandler ??= new PowerUpHandler(ScoreSystem);
        }

        /// <summary>
        /// Reset all stateful components for a new simulation.
        /// </summary>
        public void ResetForSimulation(ulong seed, int tileTypesCount)
        {
            StateRandom.SetState(seed);
            MoveRandom.SetState(seed + 1);

            // Reset spawn model counter for deterministic simulation
            _spawnModel?.Reset(tileTypesCount);
        }

    }

    public MonteCarloSimulationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Game simulation result.
    /// </summary>
    public enum GameEndReason
    {
        Win,
        OutOfMoves,
        Deadlock
    }

    /// <summary>
    /// Result of a single game simulation.
    /// </summary>
    public sealed class GameSimulationResult
    {
        public GameEndReason EndReason { get; init; }
        public int MovesUsed { get; init; }
        public long FinalScore { get; init; }
        public double ElapsedMs { get; init; }
    }

    /// <summary>
    /// Aggregated results from multiple simulations.
    /// </summary>
    public sealed class MonteCarloResult
    {
        public int TotalSimulations { get; init; }
        public int Wins { get; init; }
        public int OutOfMoves { get; init; }
        public int Deadlocks { get; init; }

        public double WinRate => TotalSimulations > 0 ? (double)Wins / TotalSimulations : 0;
        public double DeadlockRate => TotalSimulations > 0 ? (double)Deadlocks / TotalSimulations : 0;

        public double AverageMovesUsed { get; init; }
        public double AverageScore { get; init; }
        public double TotalElapsedMs { get; init; }
        public double AverageSimulationMs => TotalSimulations > 0 ? TotalElapsedMs / TotalSimulations : 0;
    }

    #region Single Game Simulation Tests

    [Fact]
    public void SingleGameSimulation_CompletesSuccessfully()
    {
        var state = CreateTestState(8, 8, moveLimit: 20);
        var result = SimulateSingleGame(state, seed: 12345);

        _output.WriteLine($"End reason: {result.EndReason}");
        _output.WriteLine($"Moves used: {result.MovesUsed}");
        _output.WriteLine($"Final score: {result.FinalScore}");
        _output.WriteLine($"Elapsed: {result.ElapsedMs:F2}ms");

        Assert.True(result.MovesUsed >= 0);
        Assert.True(result.MovesUsed <= 20 || result.EndReason == GameEndReason.Deadlock);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void SingleGameSimulation_PerformanceBaseline()
    {
        // Measure single game simulation performance
        const int iterations = 50;

        var times = new List<double>();

        for (int i = 0; i < iterations; i++)
        {
            var state = CreateTestState(8, 8, moveLimit: 20, seed: (ulong)(i * 1000));
            var result = SimulateSingleGame(state, seed: (ulong)i);
            times.Add(result.ElapsedMs);
        }

        double avgMs = times.Average();
        double minMs = times.Min();
        double maxMs = times.Max();

        _output.WriteLine($"Single game simulation over {iterations} iterations:");
        _output.WriteLine($"  Average: {avgMs:F2}ms");
        _output.WriteLine($"  Min: {minMs:F2}ms");
        _output.WriteLine($"  Max: {maxMs:F2}ms");

        // Single game should complete in under 100ms
        Assert.True(avgMs < 100, $"Average simulation time {avgMs:F2}ms exceeds 100ms threshold");
    }

    #endregion

    #region Monte Carlo Simulation Tests

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData(100)]
    [InlineData(1000)]
    public void MonteCarloSimulation_MeasurePerformance(int simulationCount)
    {
        var state = CreateTestState(8, 8, moveLimit: 20);

        var sw = Stopwatch.StartNew();
        var result = RunMonteCarloSimulation(state, simulationCount);
        sw.Stop();

        _output.WriteLine($"=== Monte Carlo Simulation: {simulationCount} runs ===");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F0}ms ({sw.Elapsed.TotalSeconds:F2}s)");
        _output.WriteLine($"Average per simulation: {result.AverageSimulationMs:F2}ms");
        _output.WriteLine($"");
        _output.WriteLine($"Results:");
        _output.WriteLine($"  Win rate: {result.WinRate:P1}");
        _output.WriteLine($"  Deadlock rate: {result.DeadlockRate:P1}");
        _output.WriteLine($"  Out of moves: {result.OutOfMoves} ({(double)result.OutOfMoves / simulationCount:P1})");
        _output.WriteLine($"  Average moves used: {result.AverageMovesUsed:F1}");
        _output.WriteLine($"  Average score: {result.AverageScore:F0}");

        // Performance assertions
        if (simulationCount == 100)
        {
            Assert.True(sw.Elapsed.TotalSeconds < 5, "100 simulations should complete in under 5 seconds");
        }
        else if (simulationCount == 1000)
        {
            Assert.True(sw.Elapsed.TotalSeconds < 60, "1000 simulations should complete in under 60 seconds");
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void MonteCarloSimulation_DifferentBoardSizes()
    {
        var sizes = new[] { (6, 6), (8, 8), (9, 9) };
        const int simCount = 100;

        foreach (var (width, height) in sizes)
        {
            var state = CreateTestState(width, height, moveLimit: 20);

            var sw = Stopwatch.StartNew();
            var result = RunMonteCarloSimulation(state, simCount);
            sw.Stop();

            _output.WriteLine($"Board {width}x{height}: {sw.Elapsed.TotalMilliseconds:F0}ms total, " +
                              $"{result.AverageSimulationMs:F2}ms/sim, " +
                              $"Win: {result.WinRate:P0}, Deadlock: {result.DeadlockRate:P0}");
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void MonteCarloSimulation_DifferentMoveLimits()
    {
        var moveLimits = new[] { 10, 20, 30, 50 };
        const int simCount = 100;

        foreach (var moveLimit in moveLimits)
        {
            var state = CreateTestState(8, 8, moveLimit: moveLimit);

            var sw = Stopwatch.StartNew();
            var result = RunMonteCarloSimulation(state, simCount);
            sw.Stop();

            _output.WriteLine($"MoveLimit {moveLimit}: {sw.Elapsed.TotalMilliseconds:F0}ms total, " +
                              $"{result.AverageSimulationMs:F2}ms/sim, " +
                              $"Win: {result.WinRate:P0}, Deadlock: {result.DeadlockRate:P0}, " +
                              $"AvgMoves: {result.AverageMovesUsed:F1}");
        }
    }

    #endregion

    #region Stress Tests

    [Fact]
    [Trait("Category", "Slow")]
    public void MonteCarloSimulation_ParallelVsSequential_Comparison()
    {
        const int simCount = 100;
        var state = CreateTestState(8, 8, moveLimit: 20);

        // Sequential
        var swSeq = Stopwatch.StartNew();
        var resultSeq = RunMonteCarloSimulation(state, simCount, parallel: false);
        swSeq.Stop();

        // Parallel
        var swPar = Stopwatch.StartNew();
        var resultPar = RunMonteCarloSimulation(state, simCount, parallel: true);
        swPar.Stop();

        double speedup = swSeq.Elapsed.TotalMilliseconds / swPar.Elapsed.TotalMilliseconds;

        _output.WriteLine($"=== Parallel vs Sequential Comparison ({simCount} simulations) ===");
        _output.WriteLine($"");
        _output.WriteLine($"Sequential: {swSeq.Elapsed.TotalMilliseconds:F0}ms ({resultSeq.AverageSimulationMs:F2}ms/sim)");
        _output.WriteLine($"Parallel:   {swPar.Elapsed.TotalMilliseconds:F0}ms ({resultPar.AverageSimulationMs:F2}ms/sim)");
        _output.WriteLine($"");
        _output.WriteLine($"Speedup: {speedup:F2}x");
        _output.WriteLine($"CPU cores: {Environment.ProcessorCount}");
        _output.WriteLine($"");
        _output.WriteLine($"Results match: Deadlocks={resultSeq.Deadlocks == resultPar.Deadlocks}");

        // Note: Speedup may be limited due to lock contention in object pools
        // This test documents the current behavior
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void MonteCarloSimulation_LightweightParallel_NoPoolContention()
    {
        // This test uses a lightweight simulation that avoids shared object pools
        // to demonstrate the true parallel potential
        const int simCount = 1000;
        var state = CreateTestState(8, 8, moveLimit: 20);

        // Sequential - lightweight
        var swSeq = Stopwatch.StartNew();
        int seqDeadlocks = 0;
        for (int i = 0; i < simCount; i++)
        {
            var result = SimulateLightweight(state, seed: (ulong)i);
            if (result.EndReason == GameEndReason.Deadlock) seqDeadlocks++;
        }
        swSeq.Stop();

        // Parallel - lightweight
        var swPar = Stopwatch.StartNew();
        int parDeadlocks = 0;
        var lockObj = new object();
        Parallel.For(0, simCount, i =>
        {
            var result = SimulateLightweight(state, seed: (ulong)i);
            if (result.EndReason == GameEndReason.Deadlock)
            {
                lock (lockObj) parDeadlocks++;
            }
        });
        swPar.Stop();

        double speedup = swSeq.Elapsed.TotalMilliseconds / swPar.Elapsed.TotalMilliseconds;

        _output.WriteLine($"=== Lightweight Parallel Test ({simCount} simulations) ===");
        _output.WriteLine($"");
        _output.WriteLine($"Sequential: {swSeq.Elapsed.TotalMilliseconds:F0}ms ({swSeq.Elapsed.TotalMilliseconds / simCount:F2}ms/sim)");
        _output.WriteLine($"Parallel:   {swPar.Elapsed.TotalMilliseconds:F0}ms ({swPar.Elapsed.TotalMilliseconds / simCount:F2}ms/sim)");
        _output.WriteLine($"");
        _output.WriteLine($"Speedup: {speedup:F2}x");
        _output.WriteLine($"CPU cores: {Environment.ProcessorCount}");
        _output.WriteLine($"Expected speedup: ~{Math.Min(Environment.ProcessorCount, 8)}x");
        _output.WriteLine($"");
        _output.WriteLine($"Deadlocks - Seq: {seqDeadlocks}, Par: {parDeadlocks}");

        // Lightweight should show real parallel speedup
        Assert.True(speedup > 2.0, $"Lightweight parallel should be at least 2x faster, but was {speedup:F2}x");
    }

    /// <summary>
    /// Lightweight simulation that avoids shared object pools.
    /// Only uses local allocations to test true parallel potential.
    /// </summary>
    private GameSimulationResult SimulateLightweight(GameState initialState, ulong seed)
    {
        var sw = Stopwatch.StartNew();

        // Clone state - this is a value type copy
        var state = initialState.Clone();
        var random = new XorShift64(seed);

        int movesUsed = 0;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;
        var endReason = GameEndReason.OutOfMoves;

        while (movesUsed < moveLimit)
        {
            // Get valid moves using local list (no pool)
            var validMoves = GetValidMovesLocal(in state);

            if (validMoves.Count == 0)
            {
                endReason = GameEndReason.Deadlock;
                break;
            }

            // Randomly select and "apply" a move (simplified - just shuffle some tiles)
            int moveIndex = random.Next(0, validMoves.Count);
            var move = validMoves[moveIndex];

            // Simplified move application - just swap tiles
            var tile1 = state.GetTile(move.From.X, move.From.Y);
            var tile2 = state.GetTile(move.To.X, move.To.Y);
            state.SetTile(move.From.X, move.From.Y, new Tile(tile1.Id, tile2.Type, move.From.X, move.From.Y));
            state.SetTile(move.To.X, move.To.Y, new Tile(tile2.Id, tile1.Type, move.To.X, move.To.Y));

            // Simulate some work (match finding, gravity, etc.)
            SimulateWork(ref state, random);

            movesUsed++;
        }

        sw.Stop();

        return new GameSimulationResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            FinalScore = 0,
            ElapsedMs = sw.Elapsed.TotalMilliseconds
        };
    }

    /// <summary>
    /// Get valid moves without using object pools.
    /// </summary>
    private static List<Move> GetValidMovesLocal(in GameState state)
    {
        var moves = new List<Move>(); // Local allocation, no pool

        // Simplified: just find any two adjacent tiles of different colors
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var t1 = state.GetTile(x, y);
                var t2 = state.GetTile(x + 1, y);
                if (t1.Type != TileType.None && t2.Type != TileType.None && t1.Type != t2.Type)
                {
                    moves.Add(new Move(new Position(x, y), new Position(x + 1, y)));
                }
            }
        }

        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var t1 = state.GetTile(x, y);
                var t2 = state.GetTile(x, y + 1);
                if (t1.Type != TileType.None && t2.Type != TileType.None && t1.Type != t2.Type)
                {
                    moves.Add(new Move(new Position(x, y), new Position(x, y + 1)));
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Simulate some computational work without using shared resources.
    /// </summary>
    private static void SimulateWork(ref GameState state, XorShift64 random)
    {
        // Do some computation to simulate match finding and gravity
        // This creates realistic CPU load without pool contention

        int matches = 0;
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 2; x++)
            {
                var t1 = state.GetTile(x, y);
                var t2 = state.GetTile(x + 1, y);
                var t3 = state.GetTile(x + 2, y);
                if (t1.Type == t2.Type && t2.Type == t3.Type && t1.Type != TileType.None)
                {
                    matches++;
                    // "Clear" matched tiles
                    state.SetTile(x, y, new Tile(t1.Id, TileType.None, x, y));
                    state.SetTile(x + 1, y, new Tile(t2.Id, TileType.None, x + 1, y));
                    state.SetTile(x + 2, y, new Tile(t3.Id, TileType.None, x + 2, y));
                }
            }
        }

        // Simulate gravity - fill empty spots
        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow, TileType.Purple };
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == TileType.None)
                {
                    state.SetTile(x, y, new Tile(tile.Id, types[random.Next(types.Length)], x, y));
                }
            }
        }
    }

    [Fact(Skip = "Long running stress test - run manually")]
    public void MonteCarloSimulation_StressTest_10000Runs()
    {
        var state = CreateTestState(8, 8, moveLimit: 20);

        var sw = Stopwatch.StartNew();
        var result = RunMonteCarloSimulation(state, 10000);
        sw.Stop();

        _output.WriteLine($"=== Stress Test: 10000 simulations ===");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"Average per simulation: {result.AverageSimulationMs:F2}ms");
        _output.WriteLine($"Win rate: {result.WinRate:P2}");
        _output.WriteLine($"Deadlock rate: {result.DeadlockRate:P2}");
    }

    #endregion

    #region Simulation Implementation

    private GameSimulationResult SimulateSingleGame(GameState initialState, ulong seed)
    {
        var sw = Stopwatch.StartNew();

        // Get thread-local cached components
        var ctx = _simulationContext.Value!;
        ctx.ResetForSimulation(seed, initialState.TileTypesCount);

        // Clone state for simulation
        var state = initialState.Clone();
        state.Random = ctx.StateRandom;

        // Get cached simulation components
        var physics = ctx.GetPhysics();
        var refill = ctx.GetRefill(state.TileTypesCount);
        var matchFinder = ctx.GetMatchFinder();
        var matchProcessor = ctx.GetMatchProcessor();
        var powerUpHandler = ctx.GetPowerUpHandler();

        using var engine = new SimulationEngine(
            state,
            SimulationConfig.ForAI(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            null,
            NullEventCollector.Instance);

        int movesUsed = 0;
        int moveLimit = initialState.MoveLimit > 0 ? initialState.MoveLimit : 20;
        var endReason = GameEndReason.OutOfMoves;

        // Run simulation until game ends
        while (movesUsed < moveLimit)
        {
            // Get all valid moves
            var validMoves = GetValidMoves(engine.State, matchFinder);

            if (validMoves.Count == 0)
            {
                // Deadlock - no valid moves available
                endReason = GameEndReason.Deadlock;
                break;
            }

            // Randomly select a move
            int moveIndex = ctx.MoveRandom.Next(0, validMoves.Count);
            var move = validMoves[moveIndex];

            // Apply the move
            engine.ApplyMove(move.From, move.To);

            // Run until stable
            engine.RunUntilStable();

            movesUsed++;

            // Check win condition (simplified: just use move limit for now)
            // In real implementation, would check objectives
        }

        sw.Stop();

        return new GameSimulationResult
        {
            EndReason = endReason,
            MovesUsed = movesUsed,
            FinalScore = engine.State.Score,
            ElapsedMs = sw.Elapsed.TotalMilliseconds
        };
    }

    private MonteCarloResult RunMonteCarloSimulation(GameState initialState, int simulationCount, bool parallel = true)
    {
        var totalSw = Stopwatch.StartNew();

        List<GameSimulationResult> results;

        if (parallel)
        {
            // Parallel execution with thread-local accumulation
            var resultsBag = new ConcurrentBag<GameSimulationResult>();

            Parallel.For(0, simulationCount, i =>
            {
                var result = SimulateSingleGame(initialState, seed: (ulong)(i * 7919 + 12345));
                resultsBag.Add(result);
            });

            results = resultsBag.ToList();
        }
        else
        {
            // Sequential execution (for comparison)
            results = new List<GameSimulationResult>(simulationCount);

            for (int i = 0; i < simulationCount; i++)
            {
                var result = SimulateSingleGame(initialState, seed: (ulong)(i * 7919 + 12345));
                results.Add(result);
            }
        }

        totalSw.Stop();

        int wins = results.Count(r => r.EndReason == GameEndReason.Win);
        int outOfMoves = results.Count(r => r.EndReason == GameEndReason.OutOfMoves);
        int deadlocks = results.Count(r => r.EndReason == GameEndReason.Deadlock);

        return new MonteCarloResult
        {
            TotalSimulations = simulationCount,
            Wins = wins,
            OutOfMoves = outOfMoves,
            Deadlocks = deadlocks,
            AverageMovesUsed = results.Average(r => r.MovesUsed),
            AverageScore = results.Average(r => r.FinalScore),
            TotalElapsedMs = totalSw.Elapsed.TotalMilliseconds
        };
    }

    private static List<Move> GetValidMoves(in GameState state, IMatchFinder matchFinder)
    {
        var moves = new List<Move>();
        var stateCopy = state;

        // Horizontal swaps
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);

                if (!GridUtility.IsSwapValid(in state, from, to))
                    continue;

                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);
                bool hasMatch = matchFinder.HasMatchAt(in stateCopy, from) ||
                                matchFinder.HasMatchAt(in stateCopy, to);
                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);

                if (hasMatch)
                {
                    moves.Add(new Move(from, to));
                }
            }
        }

        // Vertical swaps
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);

                if (!GridUtility.IsSwapValid(in state, from, to))
                    continue;

                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);
                bool hasMatch = matchFinder.HasMatchAt(in stateCopy, from) ||
                                matchFinder.HasMatchAt(in stateCopy, to);
                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);

                if (hasMatch)
                {
                    moves.Add(new Move(from, to));
                }
            }
        }

        return moves;
    }

    #endregion

    #region Helper Classes

    private sealed class XorShift64 : IRandom
    {
        private ulong _state;

        public XorShift64(ulong seed = 12345)
        {
            _state = seed == 0 ? 1 : seed;
        }

        public float NextFloat()
        {
            return (float)(NextULong() & 0xFFFFFF) / 0x1000000;
        }

        public int Next(int max)
        {
            if (max <= 0) return 0;
            return (int)(NextULong() % (ulong)max);
        }

        public int Next(int min, int max)
        {
            if (max <= min) return min;
            return min + Next(max - min);
        }

        public void SetState(ulong state)
        {
            _state = state == 0 ? 1 : state;
        }

        public ulong GetState() => _state;

        private ulong NextULong()
        {
            ulong x = _state;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _state = x;
            return x;
        }
    }

    private sealed class SimpleScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2) => 100;
    }

    private sealed class RandomSpawnModel : ISpawnModel
    {
        private int _typeCount;
        private int _counter;
        private static readonly TileType[] AllTypes =
        {
            TileType.Red, TileType.Blue, TileType.Green,
            TileType.Yellow, TileType.Purple, TileType.Orange
        };

        public RandomSpawnModel(int typeCount)
        {
            _typeCount = Math.Min(typeCount, AllTypes.Length);
        }

        /// <summary>
        /// Reset state for a new simulation.
        /// </summary>
        public void Reset(int typeCount)
        {
            _typeCount = Math.Min(typeCount, AllTypes.Length);
            _counter = 0;
        }

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            // Simple deterministic spawn based on position and counter
            int idx = (_counter++ + spawnX) % _typeCount;
            return AllTypes[idx];
        }
    }

    #endregion

    #region Test State Creation

    private GameState CreateTestState(int width, int height, int moveLimit = 20, ulong seed = 12345)
    {
        var random = new XorShift64(seed);
        var state = new GameState(width, height, 5, random);
        state.MoveLimit = moveLimit;

        var types = new[] { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow, TileType.Purple };

        // Create a board without initial matches
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                TileType type;

                // Avoid creating 3-in-a-row matches
                do
                {
                    type = types[random.Next(types.Length)];
                } while (WouldCreateMatch(state, x, y, type));

                state.SetTile(x, y, new Tile(idx + 1, type, x, y));
            }
        }

        return state;
    }

    private static bool WouldCreateMatch(in GameState state, int x, int y, TileType type)
    {
        // Check horizontal match (left 2 tiles)
        if (x >= 2)
        {
            var t1 = state.GetTile(x - 1, y);
            var t2 = state.GetTile(x - 2, y);
            if (t1.Type == type && t2.Type == type)
                return true;
        }

        // Check vertical match (bottom 2 tiles)
        if (y >= 2)
        {
            var t1 = state.GetTile(x, y - 1);
            var t2 = state.GetTile(x, y - 2);
            if (t1.Type == type && t2.Type == type)
                return true;
        }

        return false;
    }

    #endregion
}
