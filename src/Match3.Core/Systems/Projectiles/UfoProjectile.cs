using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Projectiles;

/// <summary>
/// Timer-based UFO projectile that flies to a target position.
/// Duration = overhead + distance / speed (matching ChoreographyConfig).
/// Checks target validity each tick and retargets if target is empty,
/// unless within the lock-in window before impact.
/// </summary>
public sealed class UfoProjectile : Projectile
{
    private readonly float _speed;
    private float _totalDuration;
    private float _elapsedTime;
    private float _phaseStartTime; // Reset on retarget to fix progress calculation
    private Vector2 _startPos;

    /// <summary>
    /// Creates a new timer-based UFO projectile.
    /// </summary>
    public UfoProjectile(
        int id,
        Position origin,
        Position target,
        float overhead = UfoConstants.LaunchOverhead,
        float speed = UfoConstants.FlightSpeed)
    {
        Id = id;
        OriginPosition = origin;
        TargetGridPosition = target;
        Position = new Vector2(origin.X, origin.Y);
        _startPos = Position;
        Velocity = Vector2.Zero;
        Type = ProjectileType.Ufo;

        _speed = speed;
        _phaseStartTime = 0f;

        var targetVec = new Vector2(target.X, target.Y);
        float distance = Vector2.Distance(_startPos, targetVec);
        _totalDuration = overhead + (distance > 0 ? distance / _speed : 0.01f);
    }

    /// <inheritdoc />
    public override bool Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector events)
    {
        if (!IsActive) return false;

        _elapsedTime += deltaTime;

        // Check target validity — retarget if target cell is empty,
        // but only if we're outside the lock-in window
        float remainingTime = _totalDuration - _elapsedTime;
        if (remainingTime > UfoConstants.LockInTime && TargetGridPosition.HasValue)
        {
            var tp = TargetGridPosition.Value;
            if (tp.X >= 0 && tp.X < state.Width && tp.Y >= 0 && tp.Y < state.Height)
            {
                var tile = state.GetTile(tp.X, tp.Y);
                if (tile.Type == ElementType.None)
                {
                    TryRetarget(ref state, tick, simTime, events);
                }
            }
        }

        // Timer-based arrival
        if (_elapsedTime >= _totalDuration)
        {
            if (TargetGridPosition.HasValue)
            {
                Position = new Vector2(TargetGridPosition.Value.X, TargetGridPosition.Value.Y);
            }
            Velocity = Vector2.Zero;
            return true; // Arrived
        }

        // Interpolate position relative to phase start (avoids position jump on retarget)
        if (TargetGridPosition.HasValue)
        {
            float phaseDuration = _totalDuration - _phaseStartTime;
            float phaseElapsed = _elapsedTime - _phaseStartTime;
            float progress = phaseDuration > 0 ? Math.Clamp(phaseElapsed / phaseDuration, 0f, 1f) : 0f;
            var targetVec = new Vector2(TargetGridPosition.Value.X, TargetGridPosition.Value.Y);
            Position = Vector2.Lerp(_startPos, targetVec, progress);
        }

        return false;
    }

    /// <inheritdoc />
    public override bool TryRetarget(
        ref GameState state,
        int tick,
        float simTime,
        IEventCollector events)
    {
        var newTarget = FindBestTarget(ref state);
        if (!newTarget.HasValue)
        {
            return false;
        }

        var oldTarget = TargetGridPosition ?? new Position(-1, -1);
        TargetGridPosition = newTarget;

        // Estimate current position based on phase-relative progress
        float phaseDuration = _totalDuration - _phaseStartTime;
        float phaseElapsed = _elapsedTime - _phaseStartTime;
        float progress = phaseDuration > 0 ? Math.Clamp(phaseElapsed / phaseDuration, 0f, 1f) : 0f;
        var oldTargetVec = new Vector2(oldTarget.X, oldTarget.Y);
        var currentPos = Vector2.Lerp(_startPos, oldTargetVec, progress);

        // Reset phase: start from current position toward new target
        _startPos = currentPos;
        _phaseStartTime = _elapsedTime;
        Position = currentPos;

        // Recalculate total duration: current time + remaining flight
        var newTargetVec = new Vector2(newTarget.Value.X, newTarget.Value.Y);
        float newDistance = Vector2.Distance(currentPos, newTargetVec);
        float remainingTime = newDistance > 0 ? newDistance / _speed : 0.01f;
        _totalDuration = _elapsedTime + remainingTime;

        if (events.IsEnabled)
        {
            events.Emit(new ProjectileRetargetedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                ProjectileId = Id,
                OldTarget = oldTarget,
                NewTarget = newTarget.Value,
                Reason = RetargetReason.OriginalTargetDestroyed
            });
        }

        return true;
    }

    /// <inheritdoc />
    public override HashSet<Position> ApplyEffect(ref GameState state)
    {
        var affected = Pools.ObtainHashSet<Position>();

        if (TargetGridPosition.HasValue)
        {
            affected.Add(TargetGridPosition.Value);
        }

        return affected;
    }

    private Position? FindBestTarget(ref GameState state)
    {
        var candidates = Pools.ObtainList<Position>();

        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    if (x == OriginPosition.X && y == OriginPosition.Y)
                        continue;

                    var tile = state.GetTile(x, y);
                    if (tile.Type != ElementType.None)
                    {
                        candidates.Add(new Position(x, y));
                    }
                }
            }

            if (candidates.Count == 0)
                return null;

            int idx = state.Random.Next(0, candidates.Count);
            return candidates[idx];
        }
        finally
        {
            Pools.Release(candidates);
        }
    }
}
