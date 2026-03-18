using System;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles projectile/UFO events: launch, movement, retarget, and impact.
/// </summary>
internal sealed class ProjectileChoreographer
{
    private readonly ChoreographerContext _ctx;

    /// <summary>
    /// Initializes a new instance with the shared choreographer context.
    /// </summary>
    internal ProjectileChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Emit launch commands for projectiles, with special UFO handling for
    /// diverge curves, passenger bombs, and flight tracking.
    /// </summary>
    internal void Visit(ProjectileLaunchedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        // UFO projectile: emit UfoLaunchCommand instead of SpawnProjectileCommand
        if (evt.Type == ProjectileType.Ufo)
        {
            int tileId = evt.SourceTileId ?? 0;
            if (tileId == 0) return; // No tile to animate

            // Chain-triggered UFO: spawn a fresh tile visual at the launch origin.
            // Priority = -1 ensures SpawnTile sorts before UfoLaunchCommand (same StartTime).
            // Without this, unstable sort can let UfoLaunch set AnimationRef on the old visual,
            // then SpawnTile replaces it (AnimationRefCount resets to 0) and SyncFalling removes it.
            if (evt.SpawnVisual)
            {
                _ctx.Commands.Add(new SpawnTileCommand
                {
                    TileId = tileId, Type = ElementType.Ufo,
                    GridPos = new Position((int)evt.Origin.X, (int)evt.Origin.Y),
                    SpawnPos = evt.Origin, StartTime = startTime, Duration = 0,
                    Priority = -1
                });
            }

            var targetPos = evt.TargetPosition.HasValue
                ? new Vector2(evt.TargetPosition.Value.X, evt.TargetPosition.Value.Y)
                : evt.Origin;

            float distance = Vector2.Distance(evt.Origin, targetPos);
            float flightTime = distance > 0 ? distance / _ctx.Config.UfoFlightSpeed : 0.01f;
            float totalDuration = _ctx.Config.UfoLaunchOverhead + flightTime;
            float stayFrac = UfoConstants.LaunchStayFraction;

            // Compute diverge control points for cubic Bezier (random takeoff angle)
            Vector2? divergeControl = null, approachControl = null;
            if (distance >= UfoConstants.DivergeMinDistance)
            {
                var toTarget = targetPos - evt.Origin;
                float toTargetLen = toTarget.Length();
                var toTargetNorm = toTargetLen > 1e-4f ? toTarget / toTargetLen : Vector2.UnitX;

                Vector2 divergeDir;
                if (evt.ComboDivergeAngle.HasValue)
                {
                    float rad = evt.ComboDivergeAngle.Value * MathF.PI / 180f;
                    divergeDir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
                }
                else
                {
                    // Deterministic pseudo-random diverge angle
                    float hash1 = UfoConstants.HashFloat(tileId * 7919 + (int)(evt.Origin.X * 31 + evt.Origin.Y * 97));
                    float hash2 = UfoConstants.HashFloat(tileId * 6271 + (int)(evt.Origin.X * 53 + evt.Origin.Y * 41));
                    float angle = UfoConstants.DivergeMinAngle
                                + hash1 * (UfoConstants.DivergeMaxAngle - UfoConstants.DivergeMinAngle);
                    // Random left/right, with edge constraints
                    bool goRight = hash2 >= 0.5f;
                    if (evt.Origin.Y < 1.5f) goRight = toTargetNorm.X > 0;
                    if (evt.Origin.Y > 7.5f) goRight = toTargetNorm.X <= 0;
                    if (!goRight) angle = -angle;

                    float rad = angle * MathF.PI / 180f;
                    float cos = MathF.Cos(rad), sin = MathF.Sin(rad);
                    divergeDir = new Vector2(
                        toTargetNorm.X * cos - toTargetNorm.Y * sin,
                        toTargetNorm.X * sin + toTargetNorm.Y * cos);
                }

                divergeControl = evt.Origin + divergeDir * UfoConstants.DivergeStrength;
                approachControl = targetPos - toTargetNorm * UfoConstants.ApproachStrength;
            }

            _ctx.Commands.Add(new UfoLaunchCommand
            {
                TileId = tileId, Origin = evt.Origin, Target = targetPos,
                StayFraction = stayFrac, DivergeControl = divergeControl,
                ApproachControl = approachControl,
                StartTime = startTime, Duration = totalDuration
            });

            // Passenger bomb follows the same flight path (visually merges during StayFraction)
            if (evt.PassengerTileId.HasValue)
            {
                _ctx.Commands.Add(new UfoLaunchCommand
                {
                    TileId = evt.PassengerTileId.Value,
                    Origin = evt.PassengerOrigin ?? evt.Origin, Target = targetPos,
                    StayFraction = stayFrac, DivergeControl = divergeControl,
                    ApproachControl = approachControl,
                    StartTime = startTime, Duration = totalDuration
                });
            }

            // Track active flight for retarget/impact handling
            _ctx.ActiveUfoFlights[evt.ProjectileId] = new UfoFlightInfo(
                tileId, startTime, totalDuration, evt.Origin, targetPos, stayFrac,
                divergeControl, approachControl, evt.PassengerTileId);
            return;
        }

        _ctx.Commands.Add(new SpawnProjectileCommand
        {
            ProjectileId = evt.ProjectileId, Origin = evt.Origin,
            ArcHeight = _ctx.Config.ProjectileArcHeight, Type = evt.Type,
            StartTime = startTime, Duration = _ctx.Config.ProjectileTakeoffDuration
        });
    }

    /// <summary>
    /// Emit movement commands for non-UFO projectiles during flight.
    /// </summary>
    internal void Visit(ProjectileMovedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        float distance = Vector2.Distance(evt.FromPosition, evt.ToPosition);
        float velocity = evt.Velocity.Length();

        _ctx.Commands.Add(new MoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            From = evt.FromPosition, To = evt.ToPosition,
            StartTime = startTime,
            Duration = velocity > 0 ? distance / velocity : 0.016f
        });
    }

    /// <summary>
    /// Emit retarget commands for in-flight UFOs, computing new flight segments
    /// with easing-matched position estimation.
    /// </summary>
    internal void Visit(ProjectileRetargetedEvent evt)
    {
        if (!_ctx.ActiveUfoFlights.TryGetValue(evt.ProjectileId, out var flight))
            return;

        float startTime = _ctx.GetStartTime(evt);
        var newTargetVec = new Vector2(evt.NewTarget.X, evt.NewTarget.Y);

        // Estimate current position using smoothstep + stayFraction to match Player's easing
        float elapsed = startTime - flight.LaunchTime;
        float t = flight.Duration > 0 ? Math.Clamp(elapsed / flight.Duration, 0f, 1f) : 0f;
        float stayFrac = flight.StayFraction;
        const float arriveFrac = 0.97f;
        float moveT;
        if (t <= stayFrac) moveT = 0f;
        else if (t >= arriveFrac) moveT = 1f;
        else moveT = (t - stayFrac) / (arriveFrac - stayFrac);
        // initial/diverge launches -> smoothstep; retarget segments (StayFraction==0) -> ease-out
        float eased = stayFrac > 0
            ? moveT * moveT * (3f - 2f * moveT)
            : 1f - (1f - moveT) * (1f - moveT);
        Vector2 currentPos = (flight.DivergeControl.HasValue && flight.ApproachControl.HasValue)
            ? UfoConstants.CubicBezier(eased, flight.Origin,
                flight.DivergeControl.Value, flight.ApproachControl.Value, flight.Target)
            : Vector2.Lerp(flight.Origin, flight.Target, eased);

        float newDistance = Vector2.Distance(currentPos, newTargetVec);
        float newDuration = newDistance > 0 ? newDistance / _ctx.Config.UfoFlightSpeed : 0.01f;

        _ctx.Commands.Add(new UfoRetargetCommand
        {
            TileId = flight.TileId, NewTarget = newTargetVec,
            StartTime = startTime, Duration = 0
        });

        if (flight.PassengerTileId.HasValue)
        {
            _ctx.Commands.Add(new UfoRetargetCommand
            {
                TileId = flight.PassengerTileId.Value, NewTarget = newTargetVec,
                StartTime = startTime, Duration = 0
            });
        }

        // Update tracked flight info (StayFraction=0, no diverge for retarget segments)
        _ctx.ActiveUfoFlights[evt.ProjectileId] = new UfoFlightInfo(
            flight.TileId, startTime, newDuration, currentPos, newTargetVec, 0f, null, null,
            flight.PassengerTileId);
    }

    /// <summary>
    /// Emit impact effects and removal commands, with UFO flight-end synchronization.
    /// </summary>
    internal void Visit(ProjectileImpactEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var position = new Vector2(evt.ImpactPosition.X, evt.ImpactPosition.Y);

        if (_ctx.ActiveUfoFlights.TryGetValue(evt.ProjectileId, out var flight))
        {
            _ctx.ActiveUfoFlights.Remove(evt.ProjectileId);

            // Ensure removal doesn't fire before the visual flight animation completes
            float removeTime = Math.Max(startTime, flight.LaunchTime + flight.Duration);

            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "ufo_impact", Position = position,
                StartTime = removeTime, Duration = 0.3f
            });
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = flight.TileId, StartTime = removeTime, Duration = 0, Priority = 10
            });

            if (flight.PassengerTileId.HasValue)
            {
                _ctx.Commands.Add(new RemoveTileCommand
                {
                    TileId = flight.PassengerTileId.Value,
                    StartTime = removeTime, Duration = 0, Priority = 10
                });
            }
            return;
        }

        // Non-UFO projectiles: standard impact handling
        _ctx.Commands.Add(new ImpactProjectileCommand
        {
            ProjectileId = evt.ProjectileId, Position = position,
            EffectType = "projectile_explosion",
            StartTime = startTime, Duration = 0.3f
        });
        _ctx.Commands.Add(new RemoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            StartTime = startTime + 0.3f, Duration = 0
        });
    }
}
