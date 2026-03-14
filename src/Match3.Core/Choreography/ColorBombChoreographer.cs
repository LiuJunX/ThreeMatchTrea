using System;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles color bomb session events (multi-tick): session start, beam launch,
/// batch destroy, combo transform, and combo batch activate.
/// Instant color bomb effects (tap/swap paths) are in <see cref="ColorBombEffectsChoreographer"/>.
/// </summary>
internal sealed class ColorBombChoreographer
{
    private readonly ChoreographerContext _ctx;

    /// <summary>
    /// Initializes a new instance with the shared choreographer context.
    /// </summary>
    internal ColorBombChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Emit charge-up animation (scale + hop + spin + hold) for a new color bomb session.
    /// </summary>
    internal void Visit(ColorBombSessionStartEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var origin = new Vector2(evt.Position.X, evt.Position.Y);
        var hopOffset = new Vector2(0, _ctx.Config.ColorBombHopOffset);
        var hoppedOrigin = origin + hopOffset;
        float chargeDuration = _ctx.Config.ColorBombChargeDuration;
        float chargeScale = _ctx.Config.ColorBombChargeScale;
        float holdDuration = _ctx.Config.ColorBombHoldDuration;
        float totalDuration = chargeDuration + holdDuration;

        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = evt.TileId, FromScale = Vector2.One,
            ToScale = new Vector2(chargeScale, chargeScale),
            Easing = EasingType.OutBack, StartTime = startTime, Duration = chargeDuration
        });
        _ctx.Commands.Add(new MoveTileCommand
        {
            TileId = evt.TileId, From = origin, To = hoppedOrigin,
            Easing = EasingType.OutBack, StartTime = startTime, Duration = chargeDuration
        });
        // Hold at charged position (keeps tile alive until BatchDestroy removes it)
        _ctx.Commands.Add(new MoveTileCommand
        {
            TileId = evt.TileId, From = hoppedOrigin, To = hoppedOrigin,
            Easing = EasingType.Linear,
            StartTime = startTime + chargeDuration, Duration = holdDuration
        });
        // Spin covers entire performance (charge + hold, removed by BatchDestroy)
        _ctx.Commands.Add(new RotateTileCommand
        {
            TileId = evt.TileId, FromAngle = 0f,
            ToAngle = _ctx.Config.ColorBombSpinRate * totalDuration,
            Easing = EasingType.InQuadratic, StartTime = startTime, Duration = totalDuration
        });
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_flash", Position = hoppedOrigin,
            StartTime = startTime, Duration = totalDuration
        });

        _ctx.ActiveColorBombSessions[evt.TileId] = new ColorBombSessionInfo(
            startTime, hoppedOrigin, startTime + chargeDuration);
    }

    /// <summary>
    /// Emit beam projectile commands (spawn, fly, impact, remove) for a single beam.
    /// </summary>
    internal void Visit(ColorBombBeamLaunchedEvent evt)
    {
        float launchTime = _ctx.GetStartTime(evt);
        float arrivalTime = launchTime + evt.FlightDuration;

        // Get beam origin from session info or fallback to event origin
        Vector2 beamOrigin;
        if (_ctx.ActiveColorBombSessions.TryGetValue(evt.BombTileId, out var sessionInfo))
        {
            beamOrigin = sessionInfo.HoppedOrigin;
            if (arrivalTime > sessionInfo.MaxBeamArrivalTime)
                _ctx.ActiveColorBombSessions[evt.BombTileId] =
                    sessionInfo with { MaxBeamArrivalTime = arrivalTime };
        }
        else
        {
            beamOrigin = evt.Origin;
        }

        var targetPos = new Vector2(evt.TargetPosition.X, evt.TargetPosition.Y);
        _ctx.BeamHitTimes[evt.TargetPosition] = arrivalTime;

        int beamId = _ctx.NextBeamId--;
        byte beamColor = (byte)(evt.BeamIndex % 6);

        _ctx.Commands.Add(new SpawnProjectileCommand
        {
            ProjectileId = beamId, Origin = beamOrigin, ArcHeight = 0f,
            Type = ProjectileType.ColorBombBeam, ColorIndex = beamColor,
            StartTime = launchTime, Duration = 0
        });
        _ctx.Commands.Add(new MoveProjectileCommand
        {
            ProjectileId = beamId, From = beamOrigin, To = targetPos,
            StartTime = launchTime, Duration = evt.FlightDuration
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
        // Continuous shake on target tile from beam impact until destroyed
        _ctx.Commands.Add(new ShakeTileCommand
        {
            TileId = evt.TargetTileId,
            Amplitude = _ctx.Config.ColorBombHitShakeAmplitude,
            Frequency = _ctx.Config.ColorBombHitShakeFrequency,
            StartTime = arrivalTime, Duration = _ctx.Config.ColorBombHoldDuration
        });
    }

    /// <summary>
    /// Emit batch destruction: override beam hit times, shrink + remove the color bomb tile.
    /// </summary>
    internal void Visit(ColorBombBatchDestroyEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        if (_ctx.ActiveColorBombSessions.TryGetValue(evt.BombTileId, out var sessionInfo))
        {
            // Override beamHitTimes so all targets destroy simultaneously (batch)
            foreach (var pos in evt.DestroyedPositions)
                _ctx.BeamHitTimes[pos] = sessionInfo.MaxBeamArrivalTime;

            EmitBombShrinkAndRemove(evt.BombTileId, sessionInfo.MaxBeamArrivalTime, startTime);
            _ctx.ActiveColorBombSessions.Remove(evt.BombTileId);
        }
        else
        {
            // Session info missing (edge case) -- minimal removal
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = evt.BombTileId, StartTime = startTime, Duration = 0, Priority = 10
            });
        }
    }

    /// <summary>
    /// Emit tile type update when a beam transforms a target into a bomb (combo mode).
    /// </summary>
    internal void Visit(ColorBombComboTransformEvent evt)
    {
        float hitTime = _ctx.GetStartTime(evt);
        _ctx.BeamHitTimes[evt.TargetPosition] = hitTime;

        _ctx.Commands.Add(new UpdateTileTypeCommand
        {
            TileId = evt.TargetTileId, Position = evt.TargetPosition,
            TileType = evt.NewBombType, StartTime = hitTime, Duration = 0
        });
    }

    /// <summary>
    /// Emit removal commands for color bomb and all transformed bombs in combo batch activate.
    /// </summary>
    internal void Visit(ColorBombComboBatchActivateEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        if (_ctx.ActiveColorBombSessions.TryGetValue(evt.BombTileId, out var sessionInfo))
        {
            EmitBombShrinkAndRemove(evt.BombTileId, sessionInfo.MaxBeamArrivalTime, startTime);
            _ctx.ActiveColorBombSessions.Remove(evt.BombTileId);
        }
        else
        {
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = evt.BombTileId, StartTime = startTime, Duration = 0, Priority = 10
            });
        }

        // Remove each transformed bomb tile (ConsumeBomb set them to None)
        foreach (var tileId in evt.ActivatedTileIds)
        {
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = tileId, StartTime = startTime, Duration = 0, Priority = 10
            });
        }
    }

    /// <summary>
    /// Emit shrink + remove commands for a color bomb tile after beams have landed.
    /// </summary>
    private void EmitBombShrinkAndRemove(int bombTileId, float maxHitTime, float startTime)
    {
        const float hitPause = 0.5f;
        float shrinkStart = Math.Max(maxHitTime + hitPause, startTime);
        float shrinkDuration = _ctx.Config.ColorBombShrinkDuration;

        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = bombTileId,
            FromScale = new Vector2(_ctx.Config.ColorBombChargeScale, _ctx.Config.ColorBombChargeScale),
            ToScale = Vector2.Zero, Easing = EasingType.InQuadratic,
            StartTime = shrinkStart, Duration = shrinkDuration
        });
        _ctx.Commands.Add(new RemoveTileCommand
        {
            TileId = bombTileId, StartTime = shrinkStart + shrinkDuration,
            Duration = 0, Priority = 10
        });
    }
}
