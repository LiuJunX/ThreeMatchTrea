using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Projectiles;

/// <summary>
/// Payload type determining the landing effect of a UFO projectile.
/// </summary>
public enum UfoPayload
{
    /// <summary>Single tile (normal UFO).</summary>
    Default,

    /// <summary>Entire row at target (UFO + HorizontalRocket combo).</summary>
    Row,

    /// <summary>Entire column at target (UFO + VerticalRocket combo).</summary>
    Column,

    /// <summary>5×5 area at target (UFO + Square5x5 combo).</summary>
    Area5x5,
}

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
    /// Landing effect payload. Default = single tile; enhanced payloads from bomb combos.
    /// </summary>
    public UfoPayload Payload { get; set; } = UfoPayload.Default;

    /// <summary>
    /// Tile ID of the bomb being dragged behind the UFO (Rocket or Square5x5).
    /// Null for normal UFO or UFO+UFO combos.
    /// </summary>
    public int? PassengerTileId { get; set; }

    /// <summary>
    /// Grid position of the passenger bomb (for correct visual starting position).
    /// </summary>
    public Position? PassengerOrigin { get; set; }

    /// <summary>
    /// Reference to the projectile system for in-flight target coordination during retarget.
    /// Set by <see cref="ProjectileSystem.Launch"/> at launch time.
    /// </summary>
    internal IProjectileSystem? ProjectileSystem { get; set; }

    /// <summary>
    /// Absolute diverge angle (degrees) for combo-launched UFOs.
    /// When set, Choreographer uses this instead of computing a random angle.
    /// </summary>
    public float? ComboDivergeAngle { get; set; }

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

        // Check target validity — retarget if target cell has no attackable value,
        // but only if we're outside the lock-in window
        float remainingTime = _totalDuration - _elapsedTime;
        if (remainingTime > UfoConstants.LockInTime && TargetGridPosition.HasValue)
        {
            var tp = TargetGridPosition.Value;
            if (state.IsValid(tp))
            {
                // Re-evaluate the target cell using the same scoring system
                var eval = Targeting.CellEvaluator.Evaluate(
                    in state, tp.X, tp.Y,
                    Targeting.UfoTargetConfig.Default,
                    Targeting.Capacity.MaxCapacityRule.Instance);

                if (!eval.CanAttack || eval.MeaningfulHits <= 0)
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

        if (!TargetGridPosition.HasValue)
            return affected;

        var target = TargetGridPosition.Value;

        switch (Payload)
        {
            case UfoPayload.Row:
                for (int x = 0; x < state.Width; x++)
                    affected.Add(new Position(x, target.Y));
                break;

            case UfoPayload.Column:
                for (int y = 0; y < state.Height; y++)
                    affected.Add(new Position(target.X, y));
                break;

            case UfoPayload.Area5x5:
                BombComboHelpers.ApplyArea(in state, target, 2, affected);
                break;

            default:
                affected.Add(target);
                break;
        }

        return affected;
    }

    private Position? FindBestTarget(ref GameState state)
    {
        return UfoTargetSelector.SelectTarget(
            in state,
            OriginPosition,
            UfoTargetConfig.Default,
            ProjectileSystem);
    }
}
