using System.Collections.Generic;
using System.Linq;
using Match3.Core.Interfaces;
using Match3.Core.Structs;

namespace Match3.Core.Logic;

public class ClassicMatchFinder : IMatchFinder
{
    public bool HasMatches(in GameState state)
    {
        return FindMatchGroups(in state).Count > 0;
    }

    public List<MatchGroup> FindMatchGroups(in GameState state, Position? focus = null)
    {
        var groups = new List<MatchGroup>();
        var visited = new HashSet<Position>();
        int w = state.Width;
        int h = state.Height;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Position(x, y);
                if (visited.Contains(p)) continue;
                
                var type = state.GetType(x, y);
                if (type == TileType.None || type == TileType.Rainbow) continue;

                var component = GetConnectedComponent(in state, p, type);
                var validMatch = AnalyzeMatch(component, focus);
                
                if (validMatch != null)
                {
                    validMatch.Type = type;
                    groups.Add(validMatch);
                    foreach(var mp in validMatch.Positions) visited.Add(mp);
                }
            }
        }
        return groups;
    }

    private HashSet<Position> GetConnectedComponent(in GameState state, Position start, TileType type)
    {
        var component = new HashSet<Position>();
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        component.Add(start);
        
        while(queue.Count > 0)
        {
            var curr = queue.Dequeue();
            CheckNeighbor(state, curr.X + 1, curr.Y, type, component, queue);
            CheckNeighbor(state, curr.X - 1, curr.Y, type, component, queue);
            CheckNeighbor(state, curr.X, curr.Y + 1, type, component, queue);
            CheckNeighbor(state, curr.X, curr.Y - 1, type, component, queue);
        }
        return component;
    }
    
    private void CheckNeighbor(in GameState state, int x, int y, TileType type, HashSet<Position> component, Queue<Position> queue)
    {
        if (x < 0 || x >= state.Width || y < 0 || y >= state.Height) return;
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

    private MatchGroup? AnalyzeMatch(HashSet<Position> component, Position? focus)
    {
        bool hasRun = false;
        var positions = new List<Position>(component);
        positions.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        // Horizontal
        var byY = positions.GroupBy(p => p.Y);
        foreach(var grp in byY)
        {
            var row = grp.OrderBy(p => p.X).ToList();
            int run = 1;
            for(int i=1; i<row.Count; i++)
            {
                if (row[i].X == row[i-1].X + 1) run++;
                else run = 1;
                if (run >= 3) hasRun = true;
            }
        }
        
        // Vertical
        var byX = positions.GroupBy(p => p.X);
        foreach(var grp in byX)
        {
            var col = grp.OrderBy(p => p.Y).ToList();
            int run = 1;
            for(int i=1; i<col.Count; i++)
            {
                if (col[i].Y == col[i-1].Y + 1) run++;
                else run = 1;
                if (run >= 3) hasRun = true;
            }
        }

        if (!hasRun) return null;

        var group = new MatchGroup();
        group.Positions = component;
        
        int count = component.Count;
        int minX = positions.Min(p => p.X);
        int maxX = positions.Max(p => p.X);
        int minY = positions.Min(p => p.Y);
        int maxY = positions.Max(p => p.Y);
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        if (count >= 5)
        {
            if (width == count || height == count)
                group.SpawnBombType = BombType.Color;
            else
                group.SpawnBombType = count == 5 ? BombType.SmallCross : BombType.Square9x9;
        }
        else if (count == 4)
        {
            if (width == 2 && height == 2) { /* Bird? */ }
            else group.SpawnBombType = (width > height) ? BombType.Vertical : BombType.Horizontal;
        }
        
        if (focus.HasValue && component.Contains(focus.Value))
            group.BombOrigin = focus.Value;
        else
            group.BombOrigin = positions[count / 2];

        return group;
    }
}
