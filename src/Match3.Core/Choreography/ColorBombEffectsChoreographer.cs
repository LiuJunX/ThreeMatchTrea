using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Emits instant color bomb effects (tap and swap paths) and double color bomb combo.
/// Used by <see cref="BombChoreographer"/> for BombActivatedEvent/BombComboEvent color bomb types.
/// </summary>
internal sealed class ColorBombEffectsChoreographer
{
    private readonly ChoreographerContext _ctx;

    /// <summary>
    /// Initializes a new instance with the shared choreographer context.
    /// </summary>
    internal ColorBombEffectsChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Color bomb performance: charge-up (scale + hop + spin) -> beams -> shrink + remove.
    /// Shared by tap (BombActivatedEvent) and swap (BombComboEvent) paths.
    /// </summary>
    internal void EmitColorBombPerformance(int tileId, Position gridOrigin,
        IReadOnlyCollection<Position> affectedPositions, float startTime, Vector2 origin)
    {
        float chargeDuration = _ctx.Config.ColorBombChargeDuration;
        float chargeScale = _ctx.Config.ColorBombChargeScale;
        float beamLaunchTime = startTime + chargeDuration;
        var hopOffset = new Vector2(0, _ctx.Config.ColorBombHopOffset);

        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = tileId, FromScale = Vector2.One,
            ToScale = new Vector2(chargeScale, chargeScale),
            Easing = EasingType.OutBack, StartTime = startTime, Duration = chargeDuration
        });
        _ctx.Commands.Add(new MoveTileCommand
        {
            TileId = tileId, From = origin, To = origin + hopOffset,
            Easing = EasingType.OutBack, StartTime = startTime, Duration = chargeDuration
        });

        var hoppedOrigin = origin + hopOffset;
        float maxHitTime = EmitColorBombBeams(gridOrigin, affectedPositions, beamLaunchTime, hoppedOrigin);

        // Spin covers the entire performance (charge + beam flight + hit pause)
        const float hitPause = 0.5f;
        float spinEndTime = Math.Max(maxHitTime + hitPause, beamLaunchTime + 0.1f);
        float spinDuration = spinEndTime - startTime;

        _ctx.Commands.Add(new RotateTileCommand
        {
            TileId = tileId, FromAngle = 0f,
            ToAngle = _ctx.Config.ColorBombSpinRate * spinDuration,
            Easing = EasingType.InQuadratic, StartTime = startTime, Duration = spinDuration
        });
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_flash", Position = hoppedOrigin,
            StartTime = startTime, Duration = spinDuration
        });

        // Phase 3: shrink to nothing + remove
        float shrinkDuration = _ctx.Config.ColorBombShrinkDuration;
        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = tileId,
            FromScale = new Vector2(chargeScale, chargeScale), ToScale = Vector2.Zero,
            Easing = EasingType.InQuadratic, StartTime = spinEndTime, Duration = shrinkDuration
        });
        _ctx.Commands.Add(new RemoveTileCommand
        {
            TileId = tileId, StartTime = spinEndTime + shrinkDuration,
            Duration = 0, Priority = 10
        });
    }

    /// <summary>
    /// Four-phase choreography for double color bomb combo:
    /// Converge -> Fusion -> Wipe -> Aftermath.
    /// </summary>
    internal void EmitDoubleColorBombEffects(BombComboEvent evt, float startTime, Vector2 posA, Vector2 posB)
    {
        var midpoint = (posA + posB) * 0.5f;
        float t = startTime;

        float convergeDur = _ctx.Config.DoubleColorConvergeDuration;
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_converge", Position = midpoint,
            StartTime = t, Duration = convergeDur
        });
        t += convergeDur;

        float fusionDur = _ctx.Config.DoubleColorFusionDuration;
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_fusion", Position = midpoint,
            StartTime = t, Duration = fusionDur
        });
        t += fusionDur;

        // Wipe: compute max Chebyshev distance and cumulative delays
        int originX = evt.PositionB.X, originY = evt.PositionB.Y;
        int maxDist = 0;
        foreach (var pos in evt.AffectedPositions)
        {
            int d = Math.Max(Math.Abs(pos.X - originX), Math.Abs(pos.Y - originY));
            if (d > maxDist) maxDist = d;
        }

        float[] wipeCumDelay = new float[maxDist + 1];
        wipeCumDelay[0] = 0f;
        float wipeInterval = _ctx.Config.DoubleColorWipeInterval;
        for (int i = 1; i <= maxDist; i++)
        {
            wipeCumDelay[i] = wipeCumDelay[i - 1] + wipeInterval;
            wipeInterval *= _ctx.Config.DoubleColorWipeAccel;
        }

        foreach (var pos in evt.AffectedPositions)
        {
            int dist = Math.Max(Math.Abs(pos.X - originX), Math.Abs(pos.Y - originY));
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "colorx2_wipe_hit", Position = new Vector2(pos.X, pos.Y),
                StartTime = t + wipeCumDelay[dist], Duration = 0.15f
            });
        }
        t += wipeCumDelay[maxDist] + 0.15f;

        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_aftermath", Position = midpoint,
            StartTime = t, Duration = 0.3f
        });
    }

    /// <summary>
    /// Emit rainbow wave + flying beam projectiles + sparkle hits. Beams fire farthest-first
    /// with staggered launches, all arriving simultaneously. Returns the arrival time.
    /// </summary>
    private float EmitColorBombBeams(Position gridOrigin, IReadOnlyCollection<Position> affectedPositions,
        float startTime, Vector2 origin)
    {
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "color_bomb_wave", Position = origin,
            StartTime = startTime, Duration = 0.6f
        });

        if (affectedPositions.Count == 0) return startTime;

        // Build target list with distance and angle, skipping origin
        var targets = new List<(Position pos, Vector2 target, float dist, float angle)>();
        foreach (var pos in affectedPositions)
        {
            if (pos.X == gridOrigin.X && pos.Y == gridOrigin.Y) continue;
            var targetPos = new Vector2(pos.X, pos.Y);
            float dist = Vector2.Distance(origin, targetPos);
            float angle = MathF.Atan2(pos.Y - gridOrigin.Y, pos.X - gridOrigin.X);
            targets.Add((pos, targetPos, dist, angle));
        }
        if (targets.Count == 0) return startTime;

        // Sort: farthest first; same distance -> by angle (deterministic spread)
        targets.Sort((a, b) =>
        {
            int cmp = b.dist.CompareTo(a.dist);
            return cmp != 0 ? cmp : a.angle.CompareTo(b.angle);
        });

        float stagger = _ctx.Config.ColorBombBeamStagger;
        float firstLaunch = startTime + 0.05f;
        float lastLaunch = firstLaunch + (targets.Count - 1) * stagger;
        float maxDist = targets[0].dist;
        float arrivalTime = Math.Max(
            firstLaunch + maxDist / _ctx.Config.ColorBombBeamSpeed,
            lastLaunch + _ctx.Config.ColorBombMinFlightDuration);

        byte colorIndex = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            var (pos, targetPos, dist, _) = targets[i];
            float launchTime = firstLaunch + i * stagger;
            float flightDuration = arrivalTime - launchTime;
            _ctx.BeamHitTimes[pos] = arrivalTime;

            int beamId = _ctx.NextBeamId--;
            byte beamColor = colorIndex;
            colorIndex = (byte)((colorIndex + 1) % 6);

            _ctx.Commands.Add(new SpawnProjectileCommand
            {
                ProjectileId = beamId, Origin = origin, ArcHeight = 0f,
                Type = ProjectileType.ColorBombBeam, ColorIndex = beamColor,
                StartTime = launchTime, Duration = 0
            });
            _ctx.Commands.Add(new MoveProjectileCommand
            {
                ProjectileId = beamId, From = origin, To = targetPos,
                StartTime = launchTime, Duration = flightDuration
            });
            _ctx.Commands.Add(new ImpactProjectileCommand
            {
                ProjectileId = beamId, Position = targetPos,
                EffectType = "color_bomb_hit", StartTime = arrivalTime, Duration = 0.5f
            });
            _ctx.Commands.Add(new RemoveProjectileCommand
            {
                ProjectileId = beamId, StartTime = arrivalTime + 0.5f, Duration = 0
            });
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "color_bomb_hit", Position = targetPos,
                StartTime = arrivalTime, Duration = 0.5f
            });
        }

        return arrivalTime;
    }
}
