using System.Collections.Generic;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

public class StandardMatchProcessor : IMatchProcessor
{
    private readonly IScoreSystem _scoreSystem;

    public StandardMatchProcessor(IScoreSystem scoreSystem)
    {
        _scoreSystem = scoreSystem;
    }

    public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
    {
        int points = 0;
        
        var tilesToClear = Pools.ObtainHashSet<Position>();
        var protectedTiles = Pools.ObtainHashSet<Position>();
        var queue = Pools.ObtainQueue<Position>();
        var cleared = Pools.ObtainHashSet<Position>();

        try
        {
            foreach (var g in groups)
            {
                points += _scoreSystem.CalculateMatchScore(g);
                
                foreach (var p in g.Positions)
                {
                    tilesToClear.Add(p);
                }

                if (g.SpawnBombType != BombType.None && g.BombOrigin.HasValue)
                {
                    var p = g.BombOrigin.Value;
                    tilesToClear.Remove(p);
                    protectedTiles.Add(p);
                    
                    var newType = g.SpawnBombType == BombType.Color ? TileType.Rainbow : g.Type;
                    state.SetTile(p.X, p.Y, new Tile(state.NextTileId++, newType, p.X, p.Y, g.SpawnBombType));
                }
            }

            foreach (var p in tilesToClear)
            {
                queue.Enqueue(p);
            }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (protectedTiles.Contains(p)) continue;
                if (cleared.Contains(p)) continue;

                var t = state.GetTile(p.X, p.Y);
                if (t.Type == TileType.None) continue;

                cleared.Add(p);

                if (t.Bomb != BombType.None)
                {
                    var explosionRange = GetExplosionRange(in state, p.X, p.Y, t.Bomb);
                    try
                    {
                        foreach (var exP in explosionRange)
                        {
                            if (!cleared.Contains(exP))
                                queue.Enqueue(exP);
                        }
                    }
                    finally
                    {
                        Pools.Release(explosionRange);
                    }
                }
                
                state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
            }
        }
        finally
        {
            Pools.Release(tilesToClear);
            Pools.Release(protectedTiles);
            Pools.Release(queue);
            Pools.Release(cleared);
        }
        
        return points;
    }
    
    private List<Position> GetExplosionRange(in GameState state, int cx, int cy, BombType type)
    {
        switch (type)
        {
            case BombType.Horizontal:
            {
                var list = Pools.ObtainList<Position>();
                for (int x = 0; x < state.Width; x++)
                {
                    list.Add(new Position(x, cy));
                }
                return list;
            }
            case BombType.Vertical:
            {
                var list = Pools.ObtainList<Position>();
                for (int y = 0; y < state.Height; y++)
                {
                    list.Add(new Position(cx, y));
                }
                return list;
            }
            case BombType.Square3x3:
            {
                var list = Pools.ObtainList<Position>();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx >= 0 && nx < state.Width && ny >= 0 && ny < state.Height)
                        {
                            list.Add(new Position(nx, ny));
                        }
                    }
                }
                return list;
            }
            case BombType.Color:
            {
                var list = Pools.ObtainList<Position>();
                list.Add(new Position(cx, cy));
                return list;
            }
            case BombType.Ufo:
            {
                var candidates = Pools.ObtainList<Position>();
                try
                {
                    for (int y = 0; y < state.Height; y++)
                    {
                        for (int x = 0; x < state.Width; x++)
                        {
                            if (x == cx && y == cy) continue;
                            var t = state.GetTile(x, y);
                            if (t.Type != TileType.None)
                            {
                                candidates.Add(new Position(x, y));
                            }
                        }
                    }

                    if (candidates.Count == 0)
                    {
                        return Pools.ObtainList<Position>();
                    }

                    int idx = state.Random.Next(0, candidates.Count);
                    var result = Pools.ObtainList<Position>();
                    result.Add(candidates[idx]);
                    return result;
                }
                finally
                {
                    Pools.Release(candidates);
                }
            }
            default:
                return Pools.ObtainList<Position>();
        }
    }
}
