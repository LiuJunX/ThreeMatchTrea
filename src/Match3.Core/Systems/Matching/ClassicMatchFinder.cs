using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

public class ClassicMatchFinder : IMatchFinder
{
    private readonly IBombGenerator _bombGenerator;

    public ClassicMatchFinder(IBombGenerator bombGenerator)
    {
        _bombGenerator = bombGenerator;
    }

    public bool HasMatches(in GameState state)
    {
        var groups = FindMatchGroups(in state);
        bool has = groups.Count > 0;

        ReleaseGroups(groups);

        return has;
    }

    public bool HasMatchAt(in GameState state, Position p)
    {
        // Check if this position can participate in matching
        if (!state.CanMatch(p)) return false;

        var type = state.GetType(p.X, p.Y);
        if (type == TileType.None || type == TileType.Rainbow || type == TileType.Bomb) return false;

        int w = state.Width;
        int h = state.Height;
        int x = p.X;
        int y = p.Y;

        // Check horizontal
        int hCount = 1;
        // Look left
        for (int i = x - 1; i >= 0; i--)
        {
            if (state.CanMatch(i, y) && state.GetType(i, y) == type) hCount++;
            else break;
        }
        // Look right
        for (int i = x + 1; i < w; i++)
        {
            if (state.CanMatch(i, y) && state.GetType(i, y) == type) hCount++;
            else break;
        }
        if (hCount >= 3) return true;

        // Check vertical
        int vCount = 1;
        // Look up
        for (int i = y - 1; i >= 0; i--)
        {
            if (state.CanMatch(x, i) && state.GetType(x, i) == type) vCount++;
            else break;
        }
        // Look down
        for (int i = y + 1; i < h; i++)
        {
            if (state.CanMatch(x, i) && state.GetType(x, i) == type) vCount++;
            else break;
        }
        return vCount >= 3;
    }

    public List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null)
    {
        var groups = Pools.ObtainList<MatchGroup>();
        var visited = Pools.ObtainHashSet<Position>();

        try
        {
            int w = state.Width;
            int h = state.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Position(x, y);
                    if (visited.Contains(p)) continue;

                    // Skip if cover blocks matching
                    if (!state.CanMatch(p)) continue;

                    var type = state.GetType(x, y);
                    if (type == TileType.None || type == TileType.Rainbow || type == TileType.Bomb) continue;

                    var component = GetConnectedComponent(in state, p, type);
                    try
                    {
                        // Delegate to BombGenerator with random source for position selection
                        var detectedGroups = _bombGenerator.Generate(component, foci, state.Random);

                        foreach (var g in detectedGroups)
                        {
                            g.Type = type; // Ensure type is set
                            groups.Add(g);
                            foreach (var mp in g.Positions) visited.Add(mp);
                        }
                    }
                    finally
                    {
                        Pools.Release(component);
                    }
                }
            }
            return groups;
        }
        finally
        {
            Pools.Release(visited);
        }
    }

    private HashSet<Position> GetConnectedComponent(in GameState state, Position start, TileType type)
    {
        var component = Pools.ObtainHashSet<Position>();
        var queue = Pools.ObtainQueue<Position>();

        try
        {
            queue.Enqueue(start);
            component.Add(start);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                CheckNeighbor(state, curr.X + 1, curr.Y, type, component, queue);
                CheckNeighbor(state, curr.X - 1, curr.Y, type, component, queue);
                CheckNeighbor(state, curr.X, curr.Y + 1, type, component, queue);
                CheckNeighbor(state, curr.X, curr.Y - 1, type, component, queue);
            }
            return component;
        }
        catch
        {
            Pools.Release(component);
            throw;
        }
        finally
        {
            Pools.Release(queue);
        }
    }

    private void CheckNeighbor(in GameState state, int x, int y, TileType type, HashSet<Position> component, Queue<Position> queue)
    {
        if (x < 0 || x >= state.Width || y < 0 || y >= state.Height) return;

        // Check if cover blocks matching at this position
        if (!state.CanMatch(x, y)) return;

        if (state.GetType(x, y) == type)
        {
            var p = new Position(x, y);
            if (!component.Contains(p))
            {
                component.Add(p);
                queue.Enqueue(p);
            }
        }
    }

    public static void ReleaseGroups(List<MatchGroup> groups)
    {
        foreach (var g in groups)
        {
            Pools.Release(g);
        }
        Pools.Release(groups);
    }
}
