using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Obstacles;
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
    private readonly IObstacleSystem? _obstacleSystem;
    private readonly ICoverSystem? _coverSystem;

    /// <summary>
    /// Backward-compatible constructor — creates a <see cref="CellEliminator"/> internally.
    /// </summary>
    public StandardMatchProcessor(
        IScoreSystem scoreSystem,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem,
        BombEffectRegistry bombRegistry)
        : this(scoreSystem, new CellEliminator(coverSystem, groundSystem), bombRegistry,
            obstacleSystem: null, coverSystem: coverSystem)
    {
    }

    public StandardMatchProcessor(
        IScoreSystem scoreSystem,
        ICellEliminator cellEliminator,
        BombEffectRegistry bombRegistry,
        IObstacleSystem? obstacleSystem = null,
        ICoverSystem? coverSystem = null)
    {
        _scoreSystem = scoreSystem;
        _cellEliminator = cellEliminator;
        _bombRegistry = bombRegistry;
        _obstacleSystem = obstacleSystem;
        _coverSystem = coverSystem;
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

        var protectedTiles = Pools.ObtainHashSet<Position>();
        var queue = Pools.ObtainQueue<Position>();
        var globalCleared = Pools.ObtainHashSet<Position>();
        var explosionRange = Pools.ObtainHashSet<Position>();
        var groupEliminated = Pools.ObtainList<EliminatedTileInfo>();

        try
        {
            foreach (var g in groups)
            {
                points += _scoreSystem.CalculateMatchScore(g);

                // Bomb spawn: protect BombOrigin and place the bomb tile
                if (g.SpawnBombType != ElementType.None && g.BombOrigin.HasValue)
                {
                    var bp = g.BombOrigin.Value;
                    protectedTiles.Add(bp);
                    state.SetTile(bp.X, bp.Y, new Tile(state.NextTileId++, g.SpawnBombType, bp.X, bp.Y));
                }

                // Enqueue group positions for elimination
                foreach (var p in g.Positions)
                {
                    if (!protectedTiles.Contains(p) && !globalCleared.Contains(p))
                        queue.Enqueue(p);
                }

                // Eliminate group positions + drain any bomb chain reactions
                groupEliminated.Clear();
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    if (protectedTiles.Contains(p)) continue;
                    if (globalCleared.Contains(p)) continue;

                    var t = state.GetTile(p.X, p.Y);
                    if (t.Type == ElementType.None) { globalCleared.Add(p); continue; }

                    var result = _cellEliminator.Eliminate(ref state, p, ElimSource.Match, tick, simTime, events);
                    globalCleared.Add(p);

                    // Collect eliminated tiles for obstacle/cover adjacency notification
                    if (result.Outcome == EliminateOutcome.Eliminated)
                    {
                        groupEliminated.Add(new EliminatedTileInfo(p, result.Tile, ElimSource.Match));

                        // Bomb chain reaction — only if the bomb was actually eliminated
                        // (cover-protected or indestructible bombs must not trigger chain)
                        if (result.Tile.Type.IsBomb())
                        {
                            if (_bombRegistry.TryGetEffect(result.Tile.Type, out var effect))
                            {
                                explosionRange.Clear();
                                effect!.Apply(in state, p, explosionRange);

                                foreach (var exP in explosionRange)
                                {
                                    if (!globalCleared.Contains(exP))
                                        queue.Enqueue(exP);
                                }
                            }
                        }
                    }
                }

                // Obstacle + Cover adjacency notification (per group = per dedup scope)
                if (groupEliminated.Count > 0)
                {
                    var eliminatedSpan = new ReadOnlySpan<EliminatedTileInfo>(
                        groupEliminated.ToArray());

                    if (_obstacleSystem != null)
                    {
                        _obstacleSystem.NotifyBatchElimination(
                            ref state, eliminatedSpan, tick, simTime, events);

                        _obstacleSystem.NotifyGlobalColorElimination(
                            ref state, g.Type, tick, simTime, events);
                    }

                    _coverSystem?.NotifyBatchElimination(
                        ref state, eliminatedSpan, tick, simTime, events);
                }

                // Collectible adjacency: adjacent match eliminates neighboring collectible tiles
                EliminateAdjacentCollectibles(
                    ref state, groupEliminated, globalCleared, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(protectedTiles);
            Pools.Release(queue);
            Pools.Release(globalCleared);
            Pools.Release(explosionRange);
            Pools.Release(groupEliminated);
        }

        return points;
    }

    /// <summary>
    /// After a batch elimination, check if any eliminated tile has a neighboring
    /// collectible tile (Plate, Pearl, Bird) and eliminate it via CellEliminator.
    /// Only Match sources trigger this (same rule as obstacle adjacency).
    /// </summary>
    private void EliminateAdjacentCollectibles(
        ref GameState state, List<EliminatedTileInfo> eliminated,
        HashSet<Position> globalCleared,
        int tick, float simTime, IEventCollector events)
    {
        foreach (var info in eliminated)
        {
            if (info.Source != ElimSource.Match && info.Source != ElimSource.ColorBomb)
                continue;

            TryEliminateCollectibleAt(ref state, new Position(info.Pos.X - 1, info.Pos.Y), globalCleared, tick, simTime, events);
            TryEliminateCollectibleAt(ref state, new Position(info.Pos.X + 1, info.Pos.Y), globalCleared, tick, simTime, events);
            TryEliminateCollectibleAt(ref state, new Position(info.Pos.X, info.Pos.Y - 1), globalCleared, tick, simTime, events);
            TryEliminateCollectibleAt(ref state, new Position(info.Pos.X, info.Pos.Y + 1), globalCleared, tick, simTime, events);
        }
    }

    private void TryEliminateCollectibleAt(
        ref GameState state, Position pos, HashSet<Position> globalCleared,
        int tick, float simTime, IEventCollector events)
    {
        if (!state.IsValid(pos.X, pos.Y)) return;
        if (globalCleared.Contains(pos)) return;

        var tile = state.GetTile(pos.X, pos.Y);
        if (!tile.Type.IsCollectible()) return;

        var result = _cellEliminator.Eliminate(ref state, pos, ElimSource.Match, tick, simTime, events);
        if (result.Outcome == EliminateOutcome.Eliminated)
            globalCleared.Add(pos);
    }
}
