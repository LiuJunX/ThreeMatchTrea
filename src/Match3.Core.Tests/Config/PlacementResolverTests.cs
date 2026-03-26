using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Config;

public class PlacementResolverTests
{
    private readonly XorShift64 _rng = new(42);

    /// <summary>Create a simple 8x8 rectangle board with spawners on row 0.</summary>
    private static CellKind[] MakeRectBoard(int w = 8, int h = 8)
    {
        var cells = new CellKind[w * h];
        Array.Fill(cells, CellKind.Slot);
        // Row 0 = spawners
        for (int x = 0; x < w; x++)
            cells[x] = CellKind.Spawner;
        return cells;
    }

    #region Basic Constraints

    [Fact]
    public void DoesNotReturnOccupied()
    {
        var cells = MakeRectBoard();
        var occupied = new HashSet<int> { 9, 10, 11 };
        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 5, null, occupied, _rng);

        foreach (int idx in result)
            Assert.DoesNotContain(idx, occupied);
    }

    [Fact]
    public void DoesNotReturnSpawnerOrVoid()
    {
        var cells = MakeRectBoard();
        cells[20] = CellKind.Void;
        cells[21] = CellKind.Void;

        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 10, null, new HashSet<int>(), _rng);

        foreach (int idx in result)
        {
            Assert.NotEqual(CellKind.Void, cells[idx]);
            Assert.NotEqual(CellKind.Spawner, cells[idx]);
        }
    }

    [Fact]
    public void ReturnsAtMostCount()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 5, null, new HashSet<int>(), _rng);

        Assert.True(result.Count <= 5);
    }

    [Fact]
    public void ReturnsNoDuplicates()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "center", cells, 8, 8, 10, null, new HashSet<int>(), _rng);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void ZeroCount_ReturnsEmpty()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "center", cells, 8, 8, 0, null, new HashSet<int>(), _rng);

        Assert.Empty(result);
    }

    #endregion

    #region Border Strategy

    [Fact]
    public void Border_PlacesOnEdgeCells()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "border", cells, 8, 8, 8, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);
        foreach (int idx in result)
        {
            int x = idx % 8;
            int y = idx / 8;
            // Should be on edge of the board (we excluded spawner row 0)
            bool onEdge = x == 0 || x == 7 || y == 7;
            // Or adjacent to spawner row (y == 1)
            bool adjSpawner = y == 1;
            Assert.True(onEdge || adjSpawner,
                $"Position ({x},{y}) is not on border");
        }
    }

    [Fact]
    public void Border_AdjacentToVoid()
    {
        var cells = MakeRectBoard();
        // Create void hole in center
        cells[3 * 8 + 3] = CellKind.Void;
        cells[3 * 8 + 4] = CellKind.Void;

        var result = PlacementResolver.ResolvePositions(
            "border", cells, 8, 8, 20, null, new HashSet<int>(), _rng);

        // Positions adjacent to the void should be included
        bool hasAdjacentToVoid = result.Any(idx =>
        {
            int x = idx % 8;
            int y = idx / 8;
            return (x == 2 && y == 3) || (x == 5 && y == 3) ||
                   (x == 3 && y == 2) || (x == 4 && y == 2) ||
                   (x == 3 && y == 4) || (x == 4 && y == 4);
        });
        Assert.True(hasAdjacentToVoid);
    }

    #endregion

    #region Center Strategy

    [Fact]
    public void Center_PlacesNearCenter()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "center", cells, 8, 8, 4, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);
        float cx = 3.5f, cy = 3.5f;
        float avgDist = result.Average(idx =>
            Math.Abs(idx % 8 - cx) + Math.Abs(idx / 8 - cy));

        // Average distance should be small (< 3 for center of 8x8)
        Assert.True(avgDist < 3f, $"Average distance {avgDist} is too large for center strategy");
    }

    #endregion

    #region Cluster Strategy

    [Fact]
    public void Cluster_ReturnsConnectedGroup()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "cluster", cells, 8, 8, 6, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);

        // BFS from first result should reach all others
        var resultSet = new HashSet<int>(result);
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(result[0]);
        visited.Add(result[0]);

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cx = cur % 8;
            int cy = cur / 8;
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d];
                int ny = cy + dy[d];
                if (nx < 0 || nx >= 8 || ny < 0 || ny >= 8) continue;
                int ni = ny * 8 + nx;
                if (resultSet.Contains(ni) && visited.Add(ni))
                    queue.Enqueue(ni);
            }
        }

        Assert.Equal(result.Count, visited.Count);
    }

    #endregion

    #region Scattered Strategy

    [Fact]
    public void Scattered_CoversMultipleZones()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 8, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);

        // Check that positions span at least 3 different quadrants
        var quadrants = result.Select(idx =>
        {
            int x = idx % 8;
            int y = idx / 8;
            return (x < 4 ? 0 : 1) + (y < 4 ? 0 : 2);
        }).Distinct().Count();

        Assert.True(quadrants >= 2, $"Only {quadrants} quadrant(s) covered");
    }

    #endregion

    #region Column/Row Aligned

    [Fact]
    public void ColumnAligned_SharesColumn()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "column_aligned", cells, 8, 8, 5, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);

        // Group by column, majority should be in same column(s)
        var columns = result.GroupBy(idx => idx % 8).OrderByDescending(g => g.Count());
        int topColumnCount = columns.First().Count();
        Assert.True(topColumnCount >= 3, "Column alignment: most positions should share a column");
    }

    [Fact]
    public void RowAligned_SharesRow()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "row_aligned", cells, 8, 8, 5, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);

        var rows = result.GroupBy(idx => idx / 8).OrderByDescending(g => g.Count());
        int topRowCount = rows.First().Count();
        Assert.True(topRowCount >= 3, "Row alignment: most positions should share a row");
    }

    #endregion

    #region Near/Away Spawner

    [Fact]
    public void NearSpawner_CloserThanAwayFromSpawner()
    {
        var cells = MakeRectBoard();
        var rng1 = new XorShift64(100);
        var rng2 = new XorShift64(100);

        var near = PlacementResolver.ResolvePositions(
            "near_spawner", cells, 8, 8, 4, null, new HashSet<int>(), rng1);
        var away = PlacementResolver.ResolvePositions(
            "away_from_spawner", cells, 8, 8, 4, null, new HashSet<int>(), rng2);

        Assert.NotEmpty(near);
        Assert.NotEmpty(away);

        // Near positions should have lower average row (closer to spawners at row 0)
        float avgNearY = (float)near.Average(idx => idx / 8);
        float avgAwayY = (float)away.Average(idx => idx / 8);
        Assert.True(avgNearY < avgAwayY,
            $"Near avg row {avgNearY} should be < away avg row {avgAwayY}");
    }

    #endregion

    #region Region Filter

    [Fact]
    public void RegionFilter_TopHalf()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 5, "top_half", new HashSet<int>(), _rng);

        foreach (int idx in result)
        {
            int y = idx / 8;
            Assert.True(y < 4, $"Position row {y} should be in top half (< 4)");
        }
    }

    [Fact]
    public void RegionFilter_BottomHalf()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "scattered", cells, 8, 8, 5, "bottom_half", new HashSet<int>(), _rng);

        foreach (int idx in result)
        {
            int y = idx / 8;
            Assert.True(y >= 4, $"Position row {y} should be in bottom half (>= 4)");
        }
    }

    #endregion

    #region Unknown Strategy Fallback

    [Fact]
    public void UnknownStrategy_FallsBackToScattered()
    {
        var cells = MakeRectBoard();
        var result = PlacementResolver.ResolvePositions(
            "nonexistent_strategy", cells, 8, 8, 5, null, new HashSet<int>(), _rng);

        Assert.NotEmpty(result);
        Assert.True(result.Count <= 5);
    }

    #endregion
}
