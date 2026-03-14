using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
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
    private readonly ICellEliminator _cellEliminator;
    private readonly BombEffectRegistry _bombRegistry;

    /// <summary>
    /// Backward-compatible constructor — creates a <see cref="CellEliminator"/> internally.
    /// </summary>
    public StandardMatchProcessor(
        IScoreSystem scoreSystem,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        BombEffectRegistry bombRegistry)
        : this(scoreSystem, new CellEliminator(coverSystem, groundSystem), bombRegistry)
    {
    }

    public StandardMatchProcessor(
        IScoreSystem scoreSystem,
        ICellEliminator cellEliminator,
        BombEffectRegistry bombRegistry)
    {
        _scoreSystem = scoreSystem;
        _cellEliminator = cellEliminator;
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

                if (g.SpawnBombType != ElementType.None && g.BombOrigin.HasValue)
                {
                    var p = g.BombOrigin.Value;
                    tilesToClear.Remove(p);
                    protectedTiles.Add(p);

                    state.SetTile(p.X, p.Y, new Tile(state.NextTileId++, g.SpawnBombType, p.X, p.Y));
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
                if (t.Type == ElementType.None) { cleared.Add(p); continue; }

                // Unified elimination (captures tile type before clearing)
                var result = _cellEliminator.Eliminate(ref state, p, DestroyReason.Match, tick, simTime, events);
                cleared.Add(p);

                // Bomb chain reaction — only if the bomb was actually eliminated
                // (cover-protected or indestructible bombs must not trigger chain)
                if (result == EliminateResult.Eliminated && t.Type.IsBomb())
                {
                    if (_bombRegistry.TryGetEffect(t.Type, out var effect))
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
