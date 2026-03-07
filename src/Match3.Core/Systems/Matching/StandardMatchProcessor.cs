using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

/// <summary>
/// Handles the resolution of matches: scoring, destroying tiles, creating bombs.
/// </summary>
public class StandardMatchProcessor : IMatchProcessor
{
    private readonly IScoreSystem _scoreSystem;
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly BombEffectRegistry _bombRegistry; // Was IBombRegistry, but based on search it is a class BombEffectRegistry

    public StandardMatchProcessor(
        IScoreSystem scoreSystem,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        BombEffectRegistry bombRegistry)
    {
        _scoreSystem = scoreSystem;
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
        _bombRegistry = bombRegistry;
    }

    public int ProcessMatches(ref GameState state, List<MatchGroup> groups)
    {
        return ProcessMatches(ref state, groups, 0, 0f, NullEventCollector.Instance);
    }

    public int ProcessMatches(
        ref GameState state,
        List<MatchGroup> groups,
        int tick,
        float simTime,
        IEventCollector events)
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

                    var newType = g.SpawnBombType == BombType.Color ? ElementType.Universal : g.Type;
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
                if (t.Type == ElementType.None) continue;

                // Check cover layer first
                if (_coverSystem.IsTileProtected(in state, p))
                {
                    // Damage the cover, tile is protected this round
                    _coverSystem.TryDamageCover(ref state, p, tick, simTime, events);
                    cleared.Add(p); // Mark as processed to avoid re-processing
                    continue;
                }

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

                // Emit tile destroyed event
                if (events.IsEnabled)
                {
                    events.Emit(new TileDestroyedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = t.Id,
                        GridPosition = p,
                        Type = t.Type,
                        Bomb = t.Bomb,
                        Reason = DestroyReason.Match
                    });
                }

                // Clear the tile
                state.SetTile(p.X, p.Y, new Tile(0, ElementType.None, p.X, p.Y));

                // Notify ground layer
                _groundSystem.OnTileDestroyed(ref state, p, tick, simTime, events);
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
