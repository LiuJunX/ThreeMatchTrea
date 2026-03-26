using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Random;

namespace Match3.Core.Config;

/// <summary>
/// Translates semantic placement strategies into concrete grid indices.
/// Each strategy is a pure function operating on the cell layout.
/// </summary>
public static class PlacementResolver
{
    /// <summary>
    /// Resolve placement positions for a given strategy.
    /// </summary>
    /// <param name="strategy">Strategy name (border, center, cluster, etc.).</param>
    /// <param name="cells">Cell layout array.</param>
    /// <param name="width">Board width.</param>
    /// <param name="height">Board height.</param>
    /// <param name="count">Number of positions needed.</param>
    /// <param name="region">Optional region filter (center, top_half, etc.).</param>
    /// <param name="occupied">Already-occupied indices to avoid.</param>
    /// <param name="rng">Random source for tie-breaking.</param>
    /// <returns>List of grid indices. May be fewer than count if not enough positions available.</returns>
    public static List<int> ResolvePositions(
        string strategy,
        CellKind[] cells,
        int width, int height,
        int count,
        string? region,
        HashSet<int> occupied,
        IRandom rng)
    {
        // Collect candidate positions: Slot cells not already occupied
        var candidates = CollectCandidates(cells, width, height, region, occupied);

        if (candidates.Count == 0 || count <= 0)
            return new List<int>();

        var result = strategy?.ToLowerInvariant() switch
        {
            "border" => SelectBorder(candidates, cells, width, height, count, rng),
            "center" => SelectCenter(candidates, width, height, count),
            "cluster" => SelectCluster(candidates, width, height, count, rng),
            "scattered" => SelectScattered(candidates, width, height, count, rng),
            "column_aligned" => SelectColumnAligned(candidates, width, height, count, rng),
            "row_aligned" => SelectRowAligned(candidates, width, height, count, rng),
            "near_spawner" => SelectNearSpawner(candidates, cells, width, height, count),
            "away_from_spawner" => SelectAwayFromSpawner(candidates, cells, width, height, count),
            "geometric" => SelectGeometric(candidates, width, height, count, rng),
            "layered" => SelectCenter(candidates, width, height, count), // layered uses center ordering; HP is handled by translator
            _ => SelectScattered(candidates, width, height, count, rng) // default fallback
        };

        return result;
    }

    // ── Candidate Collection ──

    private static List<int> CollectCandidates(
        CellKind[] cells, int width, int height,
        string? region, HashSet<int> occupied)
    {
        var list = new List<int>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != CellKind.Slot) continue;
            if (occupied.Contains(i)) continue;
            if (region != null && !InRegion(i, width, height, region)) continue;
            list.Add(i);
        }
        return list;
    }

    private static bool InRegion(int idx, int width, int height, string region)
    {
        int x = idx % width;
        int y = idx / width;
        float midX = (width - 1) / 2f;
        float midY = (height - 1) / 2f;

        return region.ToLowerInvariant() switch
        {
            "center" => Math.Abs(x - midX) <= width / 3f && Math.Abs(y - midY) <= height / 3f,
            "top_half" => y < height / 2,
            "bottom_half" => y >= height / 2,
            "left_half" => x < width / 2,
            "right_half" => x >= width / 2,
            _ => true
        };
    }

    // ── Strategy Implementations ──

    /// <summary>
    /// Border: select positions adjacent to Void cells or board edges.
    /// </summary>
    private static List<int> SelectBorder(
        List<int> candidates, CellKind[] cells,
        int width, int height, int count, IRandom rng)
    {
        var borderCandidates = new List<int>();
        foreach (int idx in candidates)
        {
            if (IsOnBorder(idx, cells, width, height))
                borderCandidates.Add(idx);
        }

        // Fall back to all candidates if not enough border positions
        if (borderCandidates.Count < count)
        {
            Shuffle(borderCandidates, rng);
            var remaining = new List<int>();
            var borderSet = new HashSet<int>(borderCandidates);
            foreach (int idx in candidates)
                if (!borderSet.Contains(idx))
                    remaining.Add(idx);
            Shuffle(remaining, rng);
            borderCandidates.AddRange(remaining);
        }
        else
        {
            Shuffle(borderCandidates, rng);
        }

        return Take(borderCandidates, count);
    }

    private static bool IsOnBorder(int idx, CellKind[] cells, int width, int height)
    {
        int x = idx % width;
        int y = idx / width;

        // Board edge
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
            return true;

        // Adjacent to Void
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int d = 0; d < 4; d++)
        {
            int nx = x + dx[d];
            int ny = y + dy[d];
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
            {
                if (cells[ny * width + nx] == CellKind.Void)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Center: select positions closest to the board center.
    /// </summary>
    private static List<int> SelectCenter(
        List<int> candidates, int width, int height, int count)
    {
        float cx = (width - 1) / 2f;
        float cy = (height - 1) / 2f;

        candidates.Sort((a, b) =>
        {
            float da = ManhattanFromCenter(a, width, cx, cy);
            float db = ManhattanFromCenter(b, width, cx, cy);
            return da.CompareTo(db);
        });

        return Take(candidates, count);
    }

    private static float ManhattanFromCenter(int idx, int width, float cx, float cy)
    {
        int x = idx % width;
        int y = idx / width;
        return Math.Abs(x - cx) + Math.Abs(y - cy);
    }

    /// <summary>
    /// Cluster: BFS-expand from a random seed to form a connected group.
    /// </summary>
    private static List<int> SelectCluster(
        List<int> candidates, int width, int height, int count, IRandom rng)
    {
        if (candidates.Count == 0) return new List<int>();

        var candidateSet = new HashSet<int>(candidates);
        int seedIdx = rng.Next(0, candidates.Count);
        int seed = candidates[seedIdx];

        var result = new List<int> { seed };
        var visited = new HashSet<int> { seed };
        var frontier = new Queue<int>();
        frontier.Enqueue(seed);

        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        while (result.Count < count && frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int cx = current % width;
            int cy = current / width;

            // Collect neighbors
            var neighbors = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d];
                int ny = cy + dy[d];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                int ni = ny * width + nx;
                if (visited.Contains(ni)) continue;
                if (!candidateSet.Contains(ni)) continue;
                neighbors.Add(ni);
            }

            // Shuffle neighbors for randomness
            Shuffle(neighbors, rng);

            foreach (int ni in neighbors)
            {
                if (result.Count >= count) break;
                if (visited.Add(ni))
                {
                    result.Add(ni);
                    frontier.Enqueue(ni);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Scattered: divide board into zones and pick one per zone.
    /// </summary>
    private static List<int> SelectScattered(
        List<int> candidates, int width, int height, int count, IRandom rng)
    {
        if (candidates.Count <= count)
        {
            Shuffle(candidates, rng);
            return Take(candidates, count);
        }

        // Determine zone grid size
        int zonesPerSide = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(count)));
        float zoneW = width / (float)zonesPerSide;
        float zoneH = height / (float)zonesPerSide;

        // Group candidates by zone
        var zones = new Dictionary<int, List<int>>();
        foreach (int idx in candidates)
        {
            int x = idx % width;
            int y = idx / width;
            int zx = Math.Min((int)(x / zoneW), zonesPerSide - 1);
            int zy = Math.Min((int)(y / zoneH), zonesPerSide - 1);
            int zoneId = zy * zonesPerSide + zx;
            if (!zones.ContainsKey(zoneId))
                zones[zoneId] = new List<int>();
            zones[zoneId].Add(idx);
        }

        // Pick one from each zone (round-robin if needed)
        var result = new List<int>();
        var zoneKeys = new List<int>(zones.Keys);
        Shuffle(zoneKeys, rng);

        int round = 0;
        while (result.Count < count)
        {
            bool picked = false;
            foreach (int zk in zoneKeys)
            {
                if (result.Count >= count) break;
                var z = zones[zk];
                if (round < z.Count)
                {
                    // Pick random from remaining in this zone
                    if (round == 0) Shuffle(z, rng);
                    result.Add(z[round]);
                    picked = true;
                }
            }
            if (!picked) break;
            round++;
        }

        return result;
    }

    /// <summary>
    /// Column-aligned: pick 1-2 random columns and fill vertically.
    /// </summary>
    private static List<int> SelectColumnAligned(
        List<int> candidates, int width, int height, int count, IRandom rng)
    {
        // Group by column
        var columns = new Dictionary<int, List<int>>();
        foreach (int idx in candidates)
        {
            int x = idx % width;
            if (!columns.ContainsKey(x))
                columns[x] = new List<int>();
            columns[x].Add(idx);
        }

        var colKeys = new List<int>(columns.Keys);
        Shuffle(colKeys, rng);

        var result = new List<int>();
        foreach (int col in colKeys)
        {
            if (result.Count >= count) break;
            var colCandidates = columns[col];
            Shuffle(colCandidates, rng);
            foreach (int idx in colCandidates)
            {
                if (result.Count >= count) break;
                result.Add(idx);
            }
        }

        return result;
    }

    /// <summary>
    /// Row-aligned: pick 1-2 random rows and fill horizontally.
    /// </summary>
    private static List<int> SelectRowAligned(
        List<int> candidates, int width, int height, int count, IRandom rng)
    {
        // Group by row
        var rows = new Dictionary<int, List<int>>();
        foreach (int idx in candidates)
        {
            int y = idx / width;
            if (!rows.ContainsKey(y))
                rows[y] = new List<int>();
            rows[y].Add(idx);
        }

        var rowKeys = new List<int>(rows.Keys);
        Shuffle(rowKeys, rng);

        var result = new List<int>();
        foreach (int row in rowKeys)
        {
            if (result.Count >= count) break;
            var rowCandidates = rows[row];
            Shuffle(rowCandidates, rng);
            foreach (int idx in rowCandidates)
            {
                if (result.Count >= count) break;
                result.Add(idx);
            }
        }

        return result;
    }

    /// <summary>
    /// Near spawner: select positions closest to spawner cells.
    /// </summary>
    private static List<int> SelectNearSpawner(
        List<int> candidates, CellKind[] cells,
        int width, int height, int count)
    {
        var spawners = CollectCellsOfKind(cells, CellKind.Spawner, width);

        candidates.Sort((a, b) =>
        {
            int da = MinDistToAny(a, spawners, width);
            int db = MinDistToAny(b, spawners, width);
            return da.CompareTo(db);
        });

        return Take(candidates, count);
    }

    /// <summary>
    /// Away from spawner: select positions farthest from all spawner cells.
    /// </summary>
    private static List<int> SelectAwayFromSpawner(
        List<int> candidates, CellKind[] cells,
        int width, int height, int count)
    {
        var spawners = CollectCellsOfKind(cells, CellKind.Spawner, width);

        candidates.Sort((a, b) =>
        {
            int da = MinDistToAny(a, spawners, width);
            int db = MinDistToAny(b, spawners, width);
            return db.CompareTo(da); // descending
        });

        return Take(candidates, count);
    }

    /// <summary>
    /// Geometric: place in a recognizable pattern (cross or diamond) centered on the board.
    /// </summary>
    private static List<int> SelectGeometric(
        List<int> candidates, int width, int height, int count, IRandom rng)
    {
        float cx = (width - 1) / 2f;
        float cy = (height - 1) / 2f;
        var candidateSet = new HashSet<int>(candidates);

        // Try diamond pattern first, then cross, then fall back to center
        var pattern = GenerateDiamondPattern(cx, cy, width, height, count, candidateSet);
        if (pattern.Count >= count)
            return Take(pattern, count);

        pattern = GenerateCrossPattern(cx, cy, width, height, count, candidateSet);
        if (pattern.Count >= count)
            return Take(pattern, count);

        // Fallback to center
        return SelectCenter(candidates, width, height, count);
    }

    private static List<int> GenerateDiamondPattern(
        float cx, float cy, int width, int height, int count, HashSet<int> candidateSet)
    {
        var result = new List<int>();
        // Expand diamond radius until we have enough
        for (int radius = 1; radius <= Math.Max(width, height) && result.Count < count; radius++)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dist = Math.Abs(x - cx) + Math.Abs(y - cy);
                    if (Math.Abs(dist - radius) < 0.6f)
                    {
                        int idx = y * width + x;
                        if (candidateSet.Contains(idx) && !result.Contains(idx))
                            result.Add(idx);
                    }
                }
        }
        return result;
    }

    private static List<int> GenerateCrossPattern(
        float cx, float cy, int width, int height, int count, HashSet<int> candidateSet)
    {
        var result = new List<int>();
        int icx = (int)Math.Round(cx);
        int icy = (int)Math.Round(cy);

        // Horizontal arm
        for (int x = 0; x < width; x++)
        {
            int idx = icy * width + x;
            if (candidateSet.Contains(idx))
                result.Add(idx);
        }
        // Vertical arm
        for (int y = 0; y < height; y++)
        {
            int idx = y * width + icx;
            if (candidateSet.Contains(idx) && !result.Contains(idx))
                result.Add(idx);
        }

        return result;
    }

    // ── Helpers ──

    private static List<int> CollectCellsOfKind(CellKind[] cells, CellKind kind, int width)
    {
        var result = new List<int>();
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] == kind) result.Add(i);
        return result;
    }

    private static int MinDistToAny(int idx, List<int> targets, int width)
    {
        if (targets.Count == 0) return int.MaxValue;
        int ax = idx % width;
        int ay = idx / width;
        int min = int.MaxValue;
        foreach (int t in targets)
        {
            int tx = t % width;
            int ty = t / width;
            int dist = Math.Abs(ax - tx) + Math.Abs(ay - ty);
            if (dist < min) min = dist;
        }
        return min;
    }

    private static List<int> Take(List<int> list, int count)
    {
        int n = Math.Min(count, list.Count);
        return list.GetRange(0, n);
    }

    private static void Shuffle(List<int> list, IRandom rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
