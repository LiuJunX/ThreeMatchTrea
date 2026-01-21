using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Projectiles;

/// <summary>
/// Manages all active projectiles in the game.
/// </summary>
public sealed class ProjectileSystem : IProjectileSystem
{
    private readonly List<Projectile> _activeProjectiles = new();
    private readonly List<Projectile> _projectilesToRemove = new();
    private long _nextProjectileId = 1;

    /// <inheritdoc />
    public IReadOnlyList<Projectile> ActiveProjectiles => _activeProjectiles;

    /// <inheritdoc />
    public bool HasActiveProjectiles => _activeProjectiles.Count > 0;

    /// <inheritdoc />
    public void Launch(Projectile projectile, long tick, float simTime, IEventCollector events)
    {
        _activeProjectiles.Add(projectile);

        if (events.IsEnabled)
        {
            events.Emit(new ProjectileLaunchedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                ProjectileId = projectile.Id,
                Type = projectile.Type,
                Origin = new System.Numerics.Vector2(projectile.OriginPosition.X, projectile.OriginPosition.Y),
                TargetPosition = projectile.TargetGridPosition
            });
        }
    }

    /// <inheritdoc />
    public HashSet<Position> Update(
        ref GameState state,
        float deltaTime,
        long tick,
        float simTime,
        IEventCollector events)
    {
        var affectedPositions = Pools.ObtainHashSet<Position>();
        _projectilesToRemove.Clear();

        foreach (var projectile in _activeProjectiles)
        {
            if (!projectile.IsActive)
            {
                _projectilesToRemove.Add(projectile);
                continue;
            }

            // Update projectile physics
            bool reachedTarget = projectile.Update(ref state, deltaTime, tick, simTime, events);

            if (reachedTarget)
            {
                // Apply effect and collect affected positions
                var affected = projectile.ApplyEffect(ref state);

                // Emit impact event
                if (events.IsEnabled)
                {
                    // Use Array.Empty for zero-allocation when no positions affected
                    var affectedArray = affected.Count == 0
                        ? System.Array.Empty<Position>()
                        : new Position[affected.Count];

                    if (affected.Count > 0)
                    {
                        affected.CopyTo(affectedArray);
                    }

                    events.Emit(new ProjectileImpactEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        ProjectileId = projectile.Id,
                        ImpactPosition = projectile.TargetGridPosition ?? new Position(-1, -1),
                        AffectedPositions = affectedArray
                    });
                }

                // Merge affected positions
                foreach (var pos in affected)
                {
                    affectedPositions.Add(pos);
                }

                // Return pooled set from ApplyEffect
                Pools.Release(affected);

                // Mark for removal
                projectile.Deactivate();
                _projectilesToRemove.Add(projectile);
            }
        }

        // Remove completed projectiles
        foreach (var projectile in _projectilesToRemove)
        {
            _activeProjectiles.Remove(projectile);
        }

        return affectedPositions;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _activeProjectiles.Clear();
        _projectilesToRemove.Clear();
    }

    /// <inheritdoc />
    public long GenerateProjectileId()
    {
        return _nextProjectileId++;
    }
}
