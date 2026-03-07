using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// Simulation tests that reproduce the color-convergence bug.
/// Runs hundreds of spawn cycles and asserts color distribution stays healthy.
/// </summary>
public class SpawnDiversitySimulationTests
{
    private readonly ITestOutputHelper _output;

    private static readonly ElementType[] Colors =
    {
        ElementType.Item1, ElementType.Item2, ElementType.Item3,
        ElementType.Item4, ElementType.Item5, ElementType.Item6
    };

    public SpawnDiversitySimulationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Simulates gameplay: each round clears a row and refills, repeated 300 times.
    /// Asserts no single color ever exceeds 45% of the board.
    /// </summary>
    [Theory]
    [InlineData(0.2f, "Help")]        // Low difficulty → Help strategy
    [InlineData(0.5f, "Balance")]      // Medium difficulty → Balance strategy
    [InlineData(0.9f, "Challenge")]    // High difficulty → Challenge strategy
    public void SpawnModel_AfterManyRounds_ColorDistributionStaysHealthy(
        float difficulty, string label)
    {
        const int width = 8;
        const int height = 8;
        const int rounds = 300;
        const float maxAllowedRatio = 0.45f; // No color should exceed 45%

        var rng = new XorShift64(42);
        var model = new RuleBasedSpawnModel(rng);
        var state = new GameState(width, height, 6, rng);

        // Fill board with balanced initial distribution
        FillBoardBalanced(ref state);

        var context = new SpawnContext
        {
            TargetDifficulty = difficulty,
            RemainingMoves = 20,
            GoalProgress = 0f,
            FailedAttempts = 0,
            InFlowState = false
        };

        float worstRatio = 0f;
        int worstRound = 0;
        ElementType worstColor = ElementType.None;

        for (int round = 0; round < rounds; round++)
        {
            // Simulate match: clear a random row
            int clearY = rng.Next(0, height);
            for (int x = 0; x < width; x++)
                state.SetTile(x, clearY, new Tile(0, ElementType.None, x, clearY));

            // Refill: spawn new tiles at the cleared positions
            // (Simplified: spawn directly at the cleared row instead of top,
            //  because we're testing color selection, not physics)
            for (int x = 0; x < width; x++)
            {
                if (state.GetTile(x, clearY).Type == ElementType.None)
                {
                    var type = model.Predict(ref state, x, in context);
                    state.SetTile(x, clearY,
                        new Tile(state.NextTileId++, type, x, clearY));
                }
            }

            // Check distribution
            int[] counts = new int[6];
            int total = 0;
            for (int i = 0; i < state.Grid.Length; i++)
            {
                int idx = BoardAnalyzer.GetColorIndex(state.Grid[i].Type);
                if (idx >= 0)
                {
                    counts[idx]++;
                    total++;
                }
            }

            if (total == 0) continue;

            for (int c = 0; c < 6; c++)
            {
                float ratio = (float)counts[c] / total;
                if (ratio > worstRatio)
                {
                    worstRatio = ratio;
                    worstRound = round;
                    worstColor = Colors[c];
                }
            }
        }

        _output.WriteLine(
            $"[{label}] Worst: {worstColor} at {worstRatio:P1} (round {worstRound})");
        PrintDistribution(ref state);

        Assert.True(worstRatio < maxAllowedRatio,
            $"[{label}] {worstColor} reached {worstRatio:P1} at round {worstRound}, " +
            $"exceeds {maxAllowedRatio:P0} threshold");
    }

    /// <summary>
    /// Specifically tests the scenario that caused the original bug:
    /// RemainingMoves stuck at 0 (past move limit) → permanent Help mode.
    /// </summary>
    [Fact]
    public void SpawnModel_PastMoveLimit_DoesNotConverge()
    {
        const int width = 8;
        const int height = 8;
        const int rounds = 300;

        var rng = new XorShift64(123);
        var model = new RuleBasedSpawnModel(rng);
        var state = new GameState(width, height, 6, rng);
        FillBoardBalanced(ref state);

        // Simulate being past the move limit (the original bug trigger)
        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 0, // Past move limit → always triggers Help
            GoalProgress = 0f,
            FailedAttempts = 0,
            InFlowState = false
        };

        for (int round = 0; round < rounds; round++)
        {
            // Clear random positions (2-4 per round)
            int clearCount = 2 + rng.Next(0, 3);
            for (int i = 0; i < clearCount; i++)
            {
                int cx = rng.Next(0, width);
                int cy = rng.Next(0, height);
                state.SetTile(cx, cy, new Tile(0, ElementType.None, cx, cy));
            }

            // Refill empty positions
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (state.GetTile(x, y).Type == ElementType.None)
                    {
                        var type = model.Predict(ref state, x, in context);
                        state.SetTile(x, y,
                            new Tile(state.NextTileId++, type, x, y));
                    }
                }
            }
        }

        // Final check: no color should dominate
        int[] counts = new int[6];
        int total = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            int idx = BoardAnalyzer.GetColorIndex(state.Grid[i].Type);
            if (idx >= 0) { counts[idx]++; total++; }
        }

        _output.WriteLine("Final distribution after 300 rounds (RemainingMoves=0):");
        PrintDistribution(ref state);

        for (int c = 0; c < 6; c++)
        {
            float ratio = (float)counts[c] / total;
            Assert.True(ratio < 0.40f,
                $"{Colors[c]} converged to {ratio:P1} after 300 rounds — " +
                $"feedback loop not suppressed");
        }
    }

    #region Helpers

    private static void FillBoardBalanced(ref GameState state)
    {
        int id = 1;
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var color = Colors[(x + y) % Colors.Length];
                state.SetTile(x, y, new Tile(id++, color, x, y));
            }
        }
        state.NextTileId = id;
    }

    private void PrintDistribution(ref GameState state)
    {
        int[] counts = new int[6];
        int total = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            int idx = BoardAnalyzer.GetColorIndex(state.Grid[i].Type);
            if (idx >= 0) { counts[idx]++; total++; }
        }
        for (int c = 0; c < 6; c++)
        {
            float pct = total > 0 ? (float)counts[c] / total * 100 : 0;
            _output.WriteLine($"  {Colors[c],-8}: {counts[c],3} ({pct:F1}%)");
        }
    }

    #endregion
}

