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
/// UFO projectile that flies to a target position.
/// Has three phases: Takeoff, Flight, and Impact.
/// </summary>
public sealed class UfoProjectile : Projectile
{
    /// <summary>
    /// Takeoff duration in seconds.
    /// </summary>
    public const float TakeoffDuration = 0.3f;

    /// <summary>
    /// Flight speed in units per second.
    /// </summary>
    public const float FlightSpeed = 12f;

    /// <summary>
    /// Arc height during takeoff.
    /// </summary>
    public const float ArcHeight = 1.5f;

    /// <summary>
    /// Arrival threshold distance.
    /// </summary>
    public const float ArrivalThreshold = 0.2f;

    private UfoPhase _phase = UfoPhase.Takeoff;
    private float _phaseTime;
    private Vector2 _takeoffStartPos;

    /// <summary>
    /// Current phase of the UFO flight.
    /// </summary>
    public UfoPhase Phase => _phase;

    /// <summary>
    /// Targeting mode for this UFO.
    /// </summary>
    public UfoTargetingMode TargetingMode { get; }

    /// <summary>
    /// Creates a new UFO projectile.
    /// </summary>
    public UfoProjectile(
        long id,
        Position origin,
        Position target,
        UfoTargetingMode targetingMode = UfoTargetingMode.FixedCell)
    {
        Id = id;
        OriginPosition = origin;
        TargetGridPosition = target;
        Position = new Vector2(origin.X, origin.Y);
        _takeoffStartPos = Position;
        Velocity = Vector2.Zero;
        Type = ProjectileType.Ufo;
        TargetingMode = targetingMode;
    }

    /// <inheritdoc />
    public override bool Update(
        ref GameState state,
        float deltaTime,
        long tick,
        float simTime,
        IEventCollector events)
    {
        if (!IsActive) return false;

        _phaseTime += deltaTime;

        return _phase switch
        {
            UfoPhase.Takeoff => UpdateTakeoff(deltaTime, tick, simTime, events),
            UfoPhase.Flight => UpdateFlight(ref state, deltaTime, tick, simTime, events),
            _ => true
        };
    }

    private bool UpdateTakeoff(float deltaTime, long tick, float simTime, IEventCollector events)
    {
        // Vertical rise during takeoff
        float t = Math.Min(_phaseTime / TakeoffDuration, 1f);

        // Ease out curve for smooth deceleration
        float easedT = 1f - (1f - t) * (1f - t);
        float height = easedT * ArcHeight;

        var prevPos = Position;
        Position = new Vector2(_takeoffStartPos.X, _takeoffStartPos.Y - height);
        Velocity = new Vector2(0, -ArcHeight / TakeoffDuration);

        // Emit movement event
        if (events.IsEnabled)
        {
            events.Emit(new ProjectileMovedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                ProjectileId = Id,
                FromPosition = prevPos,
                ToPosition = Position,
                Velocity = Velocity
            });
        }

        // Transition to flight phase
        if (t >= 1f)
        {
            _phase = UfoPhase.Flight;
            _phaseTime = 0f;
        }

        return false; // Not arrived yet
    }

    private bool UpdateFlight(
        ref GameState state,
        float deltaTime,
        long tick,
        float simTime,
        IEventCollector events)
    {
        if (!TargetGridPosition.HasValue)
        {
            // No target, fizzle out
            Deactivate();
            return true;
        }

        // Dynamic targeting: re-evaluate target each tick
        if (TargetingMode == UfoTargetingMode.Dynamic)
        {
            var newTarget = FindBestTarget(ref state);
            if (newTarget.HasValue && newTarget.Value != TargetGridPosition.Value)
            {
                var oldTarget = TargetGridPosition.Value;
                TargetGridPosition = newTarget;

                if (events.IsEnabled)
                {
                    events.Emit(new ProjectileRetargetedEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        ProjectileId = Id,
                        OldTarget = oldTarget,
                        NewTarget = newTarget.Value,
                        Reason = RetargetReason.BetterTargetFound
                    });
                }
            }
        }

        // Check if target cell is still valid (not empty)
        if (TargetingMode == UfoTargetingMode.FixedCell)
        {
            var targetX = TargetGridPosition.Value.X;
            var targetY = TargetGridPosition.Value.Y;

            // Bounds check before accessing tile
            if (targetX < 0 || targetX >= state.Width || targetY < 0 || targetY >= state.Height)
            {
                // Target is out of bounds, try to retarget
                if (!TryRetarget(ref state, tick, simTime, events))
                {
                    // Can't retarget, deactivate
                    Deactivate();
                    return true;
                }
            }
            else
            {
                var targetTile = state.GetTile(targetX, targetY);
                if (targetTile.Type == TileType.None)
                {
                    // Target was destroyed, try to retarget
                    if (!TryRetarget(ref state, tick, simTime, events))
                    {
                        // Can't retarget, continue to original position anyway
                    }
                }
            }
        }

        // Calculate movement
        var targetPos = new Vector2(TargetGridPosition.Value.X, TargetGridPosition.Value.Y);
        var direction = targetPos - Position;
        var distance = direction.Length();

        if (distance > 0.001f)
        {
            direction = Vector2.Normalize(direction);
        }

        var prevPos = Position;
        var moveDistance = FlightSpeed * deltaTime;

        if (moveDistance >= distance)
        {
            // Arrived at target
            Position = targetPos;
            Velocity = Vector2.Zero;
        }
        else
        {
            Position += direction * moveDistance;
            Velocity = direction * FlightSpeed;
        }

        // Emit movement event
        if (events.IsEnabled)
        {
            events.Emit(new ProjectileMovedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                ProjectileId = Id,
                FromPosition = prevPos,
                ToPosition = Position,
                Velocity = Velocity
            });
        }

        // Check arrival
        return HasReachedTarget(ArrivalThreshold);
    }

    /// <inheritdoc />
    public override bool TryRetarget(
        ref GameState state,
        long tick,
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
            // Find all non-empty tiles not at origin
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    if (x == OriginPosition.X && y == OriginPosition.Y)
                        continue;

                    var tile = state.GetTile(x, y);
                    if (tile.Type != TileType.None)
                    {
                        candidates.Add(new Position(x, y));
                    }
                }
            }

            if (candidates.Count == 0)
                return null;

            // Select random target
            int idx = state.Random.Next(0, candidates.Count);
            return candidates[idx];
        }
        finally
        {
            Pools.Release(candidates);
        }
    }
}

/// <summary>
/// Phase of UFO flight.
/// </summary>
public enum UfoPhase
{
    /// <summary>Initial takeoff (vertical rise).</summary>
    Takeoff,

    /// <summary>Flying towards target.</summary>
    Flight,

    /// <summary>Impact/explosion.</summary>
    Impact
}

/// <summary>
/// Targeting mode for UFO projectiles.
/// </summary>
public enum UfoTargetingMode
{
    /// <summary>Target a fixed grid cell. If cell becomes empty, may retarget.</summary>
    FixedCell,

    /// <summary>Dynamically re-evaluate best target each tick.</summary>
    Dynamic,

    /// <summary>Track a specific tile by ID.</summary>
    TrackTile
}
