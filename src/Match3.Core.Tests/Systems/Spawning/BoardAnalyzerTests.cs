using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// BoardAnalyzer 单元测试
/// 测试棋盘状态分析功能
/// </summary>
public class BoardAnalyzerTests
{
    private class StubRandom : IRandom
    {
        public int Next(int min, int max) => min;
    }

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
            }
        }
        return state;
    }

    #region GetColorDistribution Tests

    [Fact]
    public void GetColorDistribution_EmptyBoard_AllZeros()
    {
        var state = CreateState(3, 3);
        Span<int> counts = stackalloc int[6];

        BoardAnalyzer.GetColorDistribution(ref state, counts);

        for (int i = 0; i < 6; i++)
            Assert.Equal(0, counts[i]);
    }

    [Fact]
    public void GetColorDistribution_MixedBoard_CountsCorrectly()
    {
        var state = CreateState(3, 3);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));
        state.SetTile(0, 1, new Tile(4, ElementType.Item2, 0, 1));

        Span<int> counts = stackalloc int[6];
        BoardAnalyzer.GetColorDistribution(ref state, counts);

        Assert.Equal(2, counts[0]); // Red
        Assert.Equal(1, counts[1]); // Green
        Assert.Equal(1, counts[2]); // Blue
    }

    #endregion

    #region SimulateDropTarget Tests

    [Fact]
    public void SimulateDropTarget_EmptyColumn_ReturnsZero()
    {
        var state = CreateState(3, 3);

        int target = BoardAnalyzer.SimulateDropTarget(ref state, 0);

        Assert.Equal(0, target);
    }

    [Fact]
    public void SimulateDropTarget_PartiallyFilled_ReturnsFirstEmpty()
    {
        var state = CreateState(3, 3);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));

        int target = BoardAnalyzer.SimulateDropTarget(ref state, 0);

        Assert.Equal(2, target);
    }

    [Fact]
    public void SimulateDropTarget_FullColumn_ReturnsLastRow()
    {
        var state = CreateState(3, 3);
        for (int y = 0; y < 3; y++)
            state.SetTile(0, y, new Tile(y + 1, ElementType.Item1, 0, y));

        int target = BoardAnalyzer.SimulateDropTarget(ref state, 0);

        Assert.Equal(2, target); // Height - 1
    }

    #endregion

    #region WouldCreateMatch Tests

    [Fact]
    public void WouldCreateMatch_HorizontalThree_ReturnsTrue()
    {
        var state = CreateState(5, 5);
        state.SetTile(0, 2, new Tile(1, ElementType.Item1, 0, 2));
        state.SetTile(1, 2, new Tile(2, ElementType.Item1, 1, 2));

        bool result = BoardAnalyzer.WouldCreateMatch(ref state, 2, 2, ElementType.Item1);

        Assert.True(result);
    }

    [Fact]
    public void WouldCreateMatch_VerticalThree_ReturnsTrue()
    {
        var state = CreateState(5, 5);
        state.SetTile(2, 0, new Tile(1, ElementType.Item3, 2, 0));
        state.SetTile(2, 1, new Tile(2, ElementType.Item3, 2, 1));

        bool result = BoardAnalyzer.WouldCreateMatch(ref state, 2, 2, ElementType.Item3);

        Assert.True(result);
    }

    [Fact]
    public void WouldCreateMatch_OnlyTwo_ReturnsFalse()
    {
        var state = CreateState(5, 5);
        state.SetTile(0, 2, new Tile(1, ElementType.Item1, 0, 2));

        bool result = BoardAnalyzer.WouldCreateMatch(ref state, 1, 2, ElementType.Item1);

        Assert.False(result);
    }

    [Fact]
    public void WouldCreateMatch_MiddleOfRun_ReturnsTrue()
    {
        var state = CreateState(5, 5);
        state.SetTile(0, 2, new Tile(1, ElementType.Item2, 0, 2));
        state.SetTile(2, 2, new Tile(2, ElementType.Item2, 2, 2));

        bool result = BoardAnalyzer.WouldCreateMatch(ref state, 1, 2, ElementType.Item2);

        Assert.True(result);
    }

    #endregion

    #region FindMatchingColors Tests

    [Fact]
    public void FindMatchingColors_WithPotentialMatch_SetsTrue()
    {
        var state = CreateState(5, 5);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        Span<bool> wouldMatch = stackalloc bool[6];
        BoardAnalyzer.FindMatchingColors(ref state, 2, wouldMatch);

        Assert.True(wouldMatch[0]); // Red would match
    }

    [Fact]
    public void FindMatchingColors_NoPotentialMatch_AllFalse()
    {
        var state = CreateState(5, 5);
        // All different colors, no matches possible
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));

        Span<bool> wouldMatch = stackalloc bool[6];
        BoardAnalyzer.FindMatchingColors(ref state, 3, wouldMatch);

        for (int i = 0; i < 6; i++)
            Assert.False(wouldMatch[i]);
    }

    #endregion

    #region FindRarestColor / FindMostCommonColor Tests

    [Fact]
    public void FindRarestColor_ReturnsLeastCommon()
    {
        var state = CreateState(3, 3);
        // 3 Red, 2 Blue, 1 Green
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(0, 1, new Tile(4, ElementType.Item3, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item3, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item2, 2, 1));

        var rarest = BoardAnalyzer.FindRarestColor(ref state, 6);

        // Yellow, Purple, Orange have 0 count, so one of them should be rarest
        Assert.True(rarest == ElementType.Item4 || rarest == ElementType.Item5 || rarest == ElementType.Item6);
    }

    [Fact]
    public void FindMostCommonColor_ReturnsMostCommon()
    {
        var state = CreateState(3, 3);
        // 3 Red, 2 Blue, 1 Green
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(0, 1, new Tile(4, ElementType.Item3, 0, 1));
        state.SetTile(1, 1, new Tile(5, ElementType.Item3, 1, 1));
        state.SetTile(2, 1, new Tile(6, ElementType.Item2, 2, 1));

        var mostCommon = BoardAnalyzer.FindMostCommonColor(ref state, 6);

        Assert.Equal(ElementType.Item1, mostCommon);
    }

    #endregion

    #region CalculateMatchPotential Tests

    [Fact]
    public void CalculateMatchPotential_EmptyBoard_ReturnsZero()
    {
        var state = CreateState(3, 3);

        int potential = BoardAnalyzer.CalculateMatchPotential(ref state);

        Assert.Equal(0, potential);
    }

    [Fact]
    public void CalculateMatchPotential_AdjacentPairs_CountsThem()
    {
        var state = CreateState(3, 3);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0)); // Horizontal pair

        int potential = BoardAnalyzer.CalculateMatchPotential(ref state);

        Assert.Equal(1, potential);
    }

    #endregion
}

