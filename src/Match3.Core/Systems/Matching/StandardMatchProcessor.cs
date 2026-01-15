using System.Collections.Generic;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

public class StandardMatchProcessor : IMatchProcessor
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombEffectRegistry _bombRegistry;

    public StandardMatchProcessor(IScoreSystem scoreSystem, BombEffectRegistry bombRegistry)
    {
        _scoreSystem = scoreSystem;
        _bombRegistry = bombRegistry;
    }

    public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
    {
        int points = 0;
        
        var tilesToClear = Pools.ObtainHashSet<Position>();
        var protectedTiles = Pools.ObtainHashSet<Position>();
        var queue = Pools.ObtainQueue<Position>();
        var cleared = Pools.ObtainHashSet<Position>();
        var explosionRange = Pools.ObtainHashSet<Position>();

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
                    if (_bombRegistry.TryGetEffect(t.Bomb, out var effect))
                    {
                        explosionRange.Clear();
                        effect!.Apply(in state, p, explosionRange);
                        
                        foreach (var exP in explosionRange)
                        {
                            if (!cleared.Contains(exP))
                                queue.Enqueue(exP);
                        }
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
            Pools.Release(explosionRange);
        }
        
        return points;
    }
}

