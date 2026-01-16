using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

public class ExplosionSystem : IExplosionSystem
{
    private readonly List<Explosion> _activeExplosions = new();
    private readonly List<Explosion> _explosionsToRemove = new();
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;

    // Config
    private const float WaveInterval = 0.1f; // 100ms per wave

    public ExplosionSystem()
        : this(new CoverSystem(), new GroundSystem())
    {
    }

    public ExplosionSystem(ICoverSystem coverSystem, IGroundSystem groundSystem)
    {
        _coverSystem = coverSystem;
        _groundSystem = groundSystem;
    }

    public bool HasActiveExplosions => _activeExplosions.Count > 0;

    public void CreateExplosion(ref GameState state, Position origin, int radius)
    {
        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, radius, WaveInterval);

        // Calculate affected area and suspend tiles
        int width = state.Width;
        int height = state.Height;

        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    var pos = new Position(x, y);
                    explosion.AffectedArea.Add(pos);

                    // Suspend the tile immediately to block falling
                    var tile = state.GetTile(x, y);
                    if (tile.Type != TileType.None)
                    {
                        tile.IsSuspended = true;
                        state.SetTile(x, y, tile);
                    }
                }
            }
        }

        _activeExplosions.Add(explosion);
    }

    public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets)
    {
        // 1. Calculate MaxRadius
        int maxRadius = 0;
        foreach (var pos in targets)
        {
            int dist = Math.Max(
                Math.Abs(pos.X - origin.X),
                Math.Abs(pos.Y - origin.Y)
            );
            if (dist > maxRadius) maxRadius = dist;
        }

        // 2. Initialize Explosion
        var explosion = Pools.Obtain<Explosion>();
        explosion.Initialize(origin, maxRadius, WaveInterval);

        // 3. Populate AffectedArea and Suspend
        foreach (var pos in targets)
        {
            explosion.AffectedArea.Add(pos);

            if (pos.X >= 0 && pos.X < state.Width && pos.Y >= 0 && pos.Y < state.Height)
            {
                var tile = state.GetTile(pos.X, pos.Y);
                if (tile.Type != TileType.None)
                {
                    tile.IsSuspended = true;
                    state.SetTile(pos.X, pos.Y, tile);
                }
            }
        }

        _activeExplosions.Add(explosion);
    }

    public void Update(
        ref GameState state,
        float deltaTime,
        long tick,
        float simTime,
        IEventCollector eventCollector,
        List<Position> triggeredBombs)
    {
        _explosionsToRemove.Clear();

        foreach (var explosion in _activeExplosions)
        {
            explosion.Timer += deltaTime;

            // Process as many waves as time allows (to handle lag spikes)
            while (explosion.Timer >= explosion.WaveInterval && !explosion.IsFinished)
            {
                explosion.Timer -= explosion.WaveInterval;
                ProcessWave(ref state, explosion, tick, simTime, eventCollector, triggeredBombs);
            }

            if (explosion.IsFinished)
            {
                _explosionsToRemove.Add(explosion);
            }
        }

        foreach (var ex in _explosionsToRemove)
        {
            _activeExplosions.Remove(ex);
            ex.Release();
            Pools.Release(ex);
        }
    }

    private void ProcessWave(
        ref GameState state,
        Explosion explosion,
        long tick,
        float simTime,
        IEventCollector eventCollector,
        List<Position> triggeredBombs)
    {
        int currentWave = explosion.CurrentWaveRadius;

        // Iterate through affected area and process tiles at current wave distance
        foreach (var pos in explosion.AffectedArea)
        {
            // Chebyshev distance for Square
            int dist = Math.Max(
                Math.Abs(pos.X - explosion.Origin.X),
                Math.Abs(pos.Y - explosion.Origin.Y)
            );

            if (dist == currentWave)
            {
                // Check cover layer first
                if (_coverSystem.IsTileProtected(in state, pos))
                {
                    // Damage the cover, tile is protected this round
                    _coverSystem.TryDamageCover(ref state, pos, tick, simTime, eventCollector);

                    // Clear suspended flag on the tile (cover absorbed the hit)
                    var suspendedTile = state.GetTile(pos.X, pos.Y);
                    if (suspendedTile.Type != TileType.None)
                    {
                        suspendedTile.IsSuspended = false;
                        state.SetTile(pos.X, pos.Y, suspendedTile);
                    }
                    continue;
                }

                var tile = state.GetTile(pos.X, pos.Y);

                // If tile exists
                if (tile.Type != TileType.None)
                {
                    // Check for chain reaction (Bombs)
                    // If it's a bomb and NOT the origin (which is the source of this explosion), trigger it
                    if (tile.Bomb != BombType.None && !(pos.X == explosion.Origin.X && pos.Y == explosion.Origin.Y))
                    {
                        triggeredBombs.Add(pos);
                        // Clear suspended flag but don't destroy - let triggered activation handle it
                        tile.IsSuspended = false;
                        state.SetTile(pos.X, pos.Y, tile);
                        continue;
                    }

                    // Emit event
                    if (eventCollector.IsEnabled)
                    {
                        eventCollector.Emit(new TileDestroyedEvent
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

                    // Destroy (Set to None, clears IsSuspended)
                    state.SetTile(pos.X, pos.Y, new Tile(0, TileType.None, pos.X, pos.Y));

                    // Notify ground layer
                    _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, eventCollector);
                }
                else
                {
                    // If it was somehow suspended (e.g. from a previous overlapping explosion?), clear it
                    // Creating a new Tile clears flags.
                    state.SetTile(pos.X, pos.Y, new Tile(0, TileType.None, pos.X, pos.Y));
                }
            }
        }

        explosion.CurrentWaveRadius++;
    }

    public void Reset()
    {
        foreach (var ex in _activeExplosions)
        {
            ex.Release();
            Pools.Release(ex);
        }
        _activeExplosions.Clear();
    }
}
