using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Physics;

/// <summary>
/// Tests for single-gap falling behavior.
/// Verifies when the top tile starts falling relative to the bottom tile's position.
///
/// Issue: When there's only 1 empty cell below a falling tile, the tile above
/// seems to wait for a full 1-cell drop instead of the documented 0.5-cell threshold.
/// </summary>
public class SingleGapFallingTests
{
    private readonly ITestOutputHelper _output;

    public SingleGapFallingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    /// <summary>
    /// Scenario: 1-cell gap
    /// D (row 0)
    /// A (row 1)
    /// _ (row 2, empty)
    ///
    /// Expected (per documentation): D starts falling when A.Position.Y >= 1.5 (0.5 cell)
    /// Observed (per user): D starts falling when A.Position.Y >= 2.0 (1 full cell)
    /// </summary>
    [Fact]
    public void SingleGap_TopTileShouldStartAtHalfCell_NotFullCell()
    {
        // Arrange: 1 column, 3 rows
        var state = new GameState(1, 3, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));   // D (top)
        state.SetTile(0, 1, new Tile(2, TileType.Blue, 0, 1));  // A (middle)
        state.SetTile(0, 2, new Tile(0, TileType.None, 0, 2));  // Empty (bottom)

        // Use slow physics to observe the exact threshold
        var config = new Match3Config
        {
            GravitySpeed = 10.0f,
            MaxFallSpeed = 5.0f,
            InitialFallSpeed = 2.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        const float dt = 0.02f; // 50Hz

        float aPositionWhenDStarted = -1f;
        bool dStartedFalling = false;

        _output.WriteLine("Frame | A.Pos.Y | A.IsFalling | D.Pos.Y | D.IsFalling | A.GridY");
        _output.WriteLine("------|---------|-------------|---------|-------------|--------");

        for (int frame = 0; frame < 200; frame++)
        {
            physics.Update(ref state, dt);

            // Find tiles by type (they may have moved grid positions)
            Tile? tileA = null;
            Tile? tileD = null;
            int aGridY = -1;

            for (int y = 0; y < 3; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == TileType.Blue)
                {
                    tileA = t;
                    aGridY = y;
                }
                if (t.Type == TileType.Red) tileD = t;
            }

            if (tileA == null || tileD == null) break;

            _output.WriteLine($"{frame,5} | {tileA.Value.Position.Y,7:F3} | {tileA.Value.IsFalling,11} | {tileD.Value.Position.Y,7:F3} | {tileD.Value.IsFalling,11} | {aGridY}");

            // Record when D starts falling
            if (!dStartedFalling && tileD.Value.IsFalling)
            {
                dStartedFalling = true;
                aPositionWhenDStarted = tileA.Value.Position.Y;
                _output.WriteLine($">>> D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");
            }

            // Stop when stable
            if (physics.IsStable(in state)) break;
        }

        Assert.True(dStartedFalling, "D should have started falling");

        _output.WriteLine($"\n=== RESULT ===");
        _output.WriteLine($"D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");

        // The key assertion: D should start when A crosses 0.5 cell (Position.Y >= 1.5)
        // If this fails with aPositionWhenDStarted >= 2.0, it confirms the bug
        Assert.True(aPositionWhenDStarted < 1.8f,
            $"D should start falling when A.Position.Y ~= 1.5 (0.5 cell), " +
            $"but D started when A.Position.Y = {aPositionWhenDStarted:F3} (~{aPositionWhenDStarted - 1:F1} cell)");
    }

    /// <summary>
    /// Scenario: 2-cell gap (for comparison)
    /// D (row 0)
    /// A (row 1)
    /// _ (row 2, empty)
    /// _ (row 3, empty)
    ///
    /// With 2+ cells, A continues falling after crossing 0.5, so D can follow.
    /// </summary>
    [Fact]
    public void DoubleGap_TopTileStartsAtHalfCell()
    {
        // Arrange: 1 column, 4 rows
        var state = new GameState(1, 4, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));   // D (top)
        state.SetTile(0, 1, new Tile(2, TileType.Blue, 0, 1));  // A (middle)
        state.SetTile(0, 2, new Tile(0, TileType.None, 0, 2));  // Empty
        state.SetTile(0, 3, new Tile(0, TileType.None, 0, 3));  // Empty (bottom)

        var config = new Match3Config
        {
            GravitySpeed = 10.0f,
            MaxFallSpeed = 5.0f,
            InitialFallSpeed = 2.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        const float dt = 0.02f;

        float aPositionWhenDStarted = -1f;
        bool dStartedFalling = false;

        _output.WriteLine("Frame | A.Pos.Y | A.IsFalling | D.Pos.Y | D.IsFalling | A.GridY");
        _output.WriteLine("------|---------|-------------|---------|-------------|--------");

        for (int frame = 0; frame < 200; frame++)
        {
            physics.Update(ref state, dt);

            Tile? tileA = null;
            Tile? tileD = null;
            int aGridY = -1;

            for (int y = 0; y < 4; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == TileType.Blue)
                {
                    tileA = t;
                    aGridY = y;
                }
                if (t.Type == TileType.Red) tileD = t;
            }

            if (tileA == null || tileD == null) break;

            _output.WriteLine($"{frame,5} | {tileA.Value.Position.Y,7:F3} | {tileA.Value.IsFalling,11} | {tileD.Value.Position.Y,7:F3} | {tileD.Value.IsFalling,11} | {aGridY}");

            if (!dStartedFalling && tileD.Value.IsFalling)
            {
                dStartedFalling = true;
                aPositionWhenDStarted = tileA.Value.Position.Y;
                _output.WriteLine($">>> D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");
            }

            if (physics.IsStable(in state)) break;
        }

        Assert.True(dStartedFalling, "D should have started falling");

        _output.WriteLine($"\n=== RESULT ===");
        _output.WriteLine($"D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");

        // With 2-cell gap, D should start around 0.5 cell threshold
        Assert.True(aPositionWhenDStarted < 1.8f,
            $"D should start falling when A.Position.Y ~= 1.5, " +
            $"but D started when A.Position.Y = {aPositionWhenDStarted:F3}");
    }

    /// <summary>
    /// Direct comparison: Run both scenarios and compare the threshold.
    /// </summary>
    [Fact]
    public void Compare_SingleGap_vs_DoubleGap_Threshold()
    {
        float singleGapThreshold = MeasureStartThreshold(gapSize: 1);
        float doubleGapThreshold = MeasureStartThreshold(gapSize: 2);

        _output.WriteLine($"Single gap (1 cell): D starts when A.Position.Y = {singleGapThreshold:F3}");
        _output.WriteLine($"Double gap (2 cells): D starts when A.Position.Y = {doubleGapThreshold:F3}");
        _output.WriteLine($"Difference: {singleGapThreshold - doubleGapThreshold:F3}");

        // Both should be around 1.5 (0.5 cell threshold)
        // If single gap is significantly higher (close to 2.0), it confirms the bug
        Assert.True(singleGapThreshold - doubleGapThreshold < 0.3f,
            $"Single gap threshold ({singleGapThreshold:F3}) should be similar to " +
            $"double gap threshold ({doubleGapThreshold:F3}), but difference is {singleGapThreshold - doubleGapThreshold:F3}");
    }

    /// <summary>
    /// User's exact scenario: 3x3 grid
    /// D E F (row 0)
    /// A B C (row 1)
    /// _ _ _ (row 2, eliminated)
    /// </summary>
    [Fact]
    public void UserScenario_3x3_SingleRowEliminated()
    {
        // Arrange: 3 columns, 3 rows
        var state = new GameState(3, 3, 6, new StubRandom());

        // Row 0: D E F
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));    // D
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));   // E
        state.SetTile(2, 0, new Tile(3, TileType.Green, 2, 0));  // F

        // Row 1: A B C
        state.SetTile(0, 1, new Tile(4, TileType.Yellow, 0, 1)); // A
        state.SetTile(1, 1, new Tile(5, TileType.Purple, 1, 1)); // B
        state.SetTile(2, 1, new Tile(6, TileType.Red, 2, 1));    // C

        // Row 2: Empty (eliminated)
        state.SetTile(0, 2, new Tile(0, TileType.None, 0, 2));
        state.SetTile(1, 2, new Tile(0, TileType.None, 1, 2));
        state.SetTile(2, 2, new Tile(0, TileType.None, 2, 2));

        var config = new Match3Config
        {
            GravitySpeed = 10.0f,
            MaxFallSpeed = 5.0f,
            InitialFallSpeed = 2.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        const float dt = 0.02f;

        float aPositionWhenDStarted = -1f;
        bool dStartedFalling = false;

        _output.WriteLine("Frame | A.Pos.Y | A.Fall | D.Pos.Y | D.Fall | A.GridY");
        _output.WriteLine("------|---------|--------|---------|--------|--------");

        for (int frame = 0; frame < 200; frame++)
        {
            physics.Update(ref state, dt);

            // Track column 0 (D and A)
            Tile? tileA = null;
            Tile? tileD = null;
            int aGridY = -1;

            for (int y = 0; y < 3; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == TileType.Yellow)
                {
                    tileA = t;
                    aGridY = y;
                }
                if (t.Type == TileType.Red && t.Id == 1) tileD = t;
            }

            if (tileA == null || tileD == null) break;

            _output.WriteLine($"{frame,5} | {tileA.Value.Position.Y,7:F3} | {tileA.Value.IsFalling,6} | {tileD.Value.Position.Y,7:F3} | {tileD.Value.IsFalling,6} | {aGridY}");

            if (!dStartedFalling && tileD.Value.IsFalling)
            {
                dStartedFalling = true;
                aPositionWhenDStarted = tileA.Value.Position.Y;
                _output.WriteLine($">>> D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");
            }

            if (physics.IsStable(in state)) break;
        }

        Assert.True(dStartedFalling, "D should have started falling");

        _output.WriteLine($"\n=== RESULT ===");
        _output.WriteLine($"D started falling when A.Position.Y = {aPositionWhenDStarted:F3}");
        _output.WriteLine($"This is approximately {aPositionWhenDStarted - 1:F2} cells from starting position");

        // Verify it's around 0.5, not 1.0
        Assert.True(aPositionWhenDStarted < 1.8f,
            $"Expected D to start at ~0.5 cell (A.Y ~= 1.5), but got {aPositionWhenDStarted:F3}");
    }

    private float MeasureStartThreshold(int gapSize)
    {
        int height = 2 + gapSize; // D, A, + empty cells
        var state = new GameState(1, height, 6, new StubRandom());

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));   // D
        state.SetTile(0, 1, new Tile(2, TileType.Blue, 0, 1));  // A

        for (int y = 2; y < height; y++)
        {
            state.SetTile(0, y, new Tile(0, TileType.None, 0, y)); // Empty
        }

        var config = new Match3Config
        {
            GravitySpeed = 10.0f,
            MaxFallSpeed = 5.0f,
            InitialFallSpeed = 2.0f
        };
        var physics = new RealtimeGravitySystem(config, new StubRandom());

        const float dt = 0.02f;

        for (int frame = 0; frame < 200; frame++)
        {
            physics.Update(ref state, dt);

            Tile? tileA = null;
            Tile? tileD = null;

            for (int y = 0; y < height; y++)
            {
                var t = state.GetTile(0, y);
                if (t.Type == TileType.Blue) tileA = t;
                if (t.Type == TileType.Red) tileD = t;
            }

            if (tileA == null || tileD == null) break;

            if (tileD.Value.IsFalling)
            {
                return tileA.Value.Position.Y;
            }
        }

        return -1f; // D never started
    }
}
