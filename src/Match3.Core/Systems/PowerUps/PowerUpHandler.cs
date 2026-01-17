using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

public class PowerUpHandler : IPowerUpHandler
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombComboHandler _comboHandler;
    private readonly BombEffectRegistry _effectRegistry;
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;

    public PowerUpHandler(IScoreSystem scoreSystem)
        : this(scoreSystem, new BombComboHandler(), BombEffectRegistry.CreateDefault(),
               new CoverSystem(), new GroundSystem())
    {
    }

    public PowerUpHandler(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        BombEffectRegistry effectRegistry,
        ICoverSystem coverSystem,
        IGroundSystem groundSystem)
    {
        _scoreSystem = scoreSystem;
        _comboHandler = comboHandler;
        _effectRegistry = effectRegistry;
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
    }

    public void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points)
    {
        ProcessSpecialMove(ref state, p1, p2, 0, 0f, NullEventCollector.Instance, out points);
    }

    public void ProcessSpecialMove(
        ref GameState state,
        Position p1,
        Position p2,
        long tick,
        float simTime,
        IEventCollector events,
        out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Calculate score before modifying state (tiles might be cleared)
        points = _scoreSystem.CalculateSpecialMoveScore(t1.Type, t1.Bomb, t2.Type, t2.Bomb);

        // Use BombComboHandler to process combos
        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_comboHandler.TryApplyCombo(ref state, p1, p2, affected))
            {
                // Clear bomb attributes from combo participants to prevent double explosion
                // The combo effect already accounts for both bombs' effects
                ClearBombAttribute(ref state, p1);
                ClearBombAttribute(ref state, p2);

                ClearAffectedTiles(ref state, affected, tick, simTime, events);
                return;
            }
        }
        finally
        {
            Pools.Release(affected);
        }

        // If no special move happened, reset points
        points = 0;
    }

    public void ActivateBomb(ref GameState state, Position p)
    {
        ActivateBomb(ref state, p, 0, 0f, NullEventCollector.Instance);
    }

    public void ActivateBomb(ref GameState state, Position p, long tick, float simTime, IEventCollector events)
    {
        var t = state.GetTile(p.X, p.Y);
        if (t.Bomb == BombType.None) return;

        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            // Use BombEffectRegistry to get single bomb effect
            if (_effectRegistry.TryGetEffect(t.Bomb, out var effect))
            {
                effect!.Apply(in state, p, affected);
                ClearAffectedTiles(ref state, affected, tick, simTime, events);
            }

            // Ensure the bomb itself is cleared
            var currentT = state.GetTile(p.X, p.Y);
            if (currentT.Type != TileType.None)
            {
                if (events.IsEnabled)
                {
                    events.Emit(new TileDestroyedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = currentT.Id,
                        GridPosition = p,
                        Type = currentT.Type,
                        Bomb = currentT.Bomb,
                        Reason = DestroyReason.BombEffect
                    });
                }

                state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));

                // Notify ground layer
                _groundSystem.OnTileDestroyed(ref state, p, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(affected);
        }
    }

    /// <summary>
    /// Clears the bomb attribute from a tile to prevent double explosion during combo processing.
    /// </summary>
    private static void ClearBombAttribute(ref GameState state, Position p)
    {
        var tile = state.GetTile(p.X, p.Y);
        if (tile.Bomb != BombType.None)
        {
            // Create a new tile without the bomb attribute
            var newTile = new Tile(tile.Id, tile.Type, p.X, p.Y, BombType.None)
            {
                Position = tile.Position
            };
            state.SetTile(p.X, p.Y, newTile);
        }
    }

    /// <summary>
    /// Clears all affected tiles (supports chain explosions using queue to avoid recursion)
    /// </summary>
    private void ClearAffectedTiles(
        ref GameState state,
        HashSet<Position> affected,
        long tick,
        float simTime,
        IEventCollector events)
    {
        var queue = Pools.ObtainQueue<Position>();
        var chainEffect = Pools.ObtainHashSet<Position>();
        var processed = Pools.ObtainHashSet<Position>();
        try
        {
            // Initialize queue
            foreach (var pos in affected)
            {
                queue.Enqueue(pos);
            }

            // BFS process all tiles (including chain explosions)
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();

                if (pos.X < 0 || pos.X >= state.Width || pos.Y < 0 || pos.Y >= state.Height)
                    continue;

                if (processed.Contains(pos))
                    continue;

                processed.Add(pos);

                // Check cover layer first
                if (_coverSystem.IsTileProtected(in state, pos))
                {
                    // Damage the cover, tile is protected this round
                    _coverSystem.TryDamageCover(ref state, pos, tick, simTime, events);
                    continue;
                }

                var tile = state.GetTile(pos.X, pos.Y);

                // Already empty, skip
                if (tile.Type == TileType.None)
                    continue;

                // If it's a bomb, trigger chain explosion
                if (tile.Bomb != BombType.None && _effectRegistry.TryGetEffect(tile.Bomb, out var effect))
                {
                    chainEffect.Clear();
                    effect!.Apply(in state, pos, chainEffect);

                    // Add chain positions to queue
                    foreach (var chainPos in chainEffect)
                    {
                        if (!processed.Contains(chainPos))
                            queue.Enqueue(chainPos);
                    }
                }

                // Emit event
                if (events.IsEnabled)
                {
                    events.Emit(new TileDestroyedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileId = tile.Id,
                        GridPosition = pos,
                        Type = tile.Type,
                        Bomb = tile.Bomb,
                        Reason = DestroyReason.BombEffect
                    });
                }

                // Clear the tile
                state.SetTile(pos.X, pos.Y, new Tile(0, TileType.None, pos.X, pos.Y));

                // Notify ground layer
                _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
            }
        }
        finally
        {
            Pools.Release(processed);
            Pools.Release(chainEffect);
            Pools.Release(queue);
        }
    }
}
