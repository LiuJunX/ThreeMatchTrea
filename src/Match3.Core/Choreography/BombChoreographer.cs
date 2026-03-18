using System;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles bomb creation, activation, and combo events.
/// Color bomb specific logic is delegated to <see cref="ColorBombEffectsChoreographer"/>.
/// </summary>
internal sealed class BombChoreographer
{
    private readonly ChoreographerContext _ctx;
    private readonly ColorBombEffectsChoreographer _colorBombEffects;

    /// <summary>
    /// Initializes a new instance with the shared choreographer context.
    /// </summary>
    internal BombChoreographer(ChoreographerContext ctx, ColorBombEffectsChoreographer colorBombEffects)
    {
        _ctx = ctx;
        _colorBombEffects = colorBombEffects;
    }

    /// <summary>
    /// Emit merge-hold, spawn, scale, and pop-in commands for a newly created bomb.
    /// </summary>
    internal void Visit(BombCreatedEvent evt)
    {
        float baseStart = _ctx.GetStartTime(evt);
        float mergeEndTime = baseStart + _ctx.Config.MergeDuration;
        var position = new Vector2(evt.Position.X, evt.Position.Y);

        // Hold bomb-origin tile in place during merge (sets IsBeingAnimated=true,
        // preventing SyncFallingTilesFromGameState from removing it).
        _ctx.Commands.Add(new MoveTileCommand
        {
            TileId = evt.TileId, From = position, To = position,
            StartTime = baseStart, Duration = _ctx.Config.MergeDuration,
            Easing = EasingType.Linear
        });

        // Spawn new bomb tile at scale=0 so SyncFallingTilesFromGameState
        // doesn't create it at full size (tile exists in GameState after ProcessMatches).
        _ctx.Commands.Add(new SpawnTileCommand
        {
            TileId = evt.NewTileId, Type = evt.BombType,
            GridPos = evt.Position, SpawnPos = position,
            StartTime = baseStart, Duration = 0, Priority = 1
        });

        // Keep scale at zero during merge animation
        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = evt.NewTileId, FromScale = Vector2.Zero, ToScale = Vector2.Zero,
            StartTime = baseStart, Duration = _ctx.Config.MergeDuration,
            Easing = EasingType.Linear, Priority = 2
        });

        // Remove old tile after merge completes
        _ctx.Commands.Add(new RemoveTileCommand
        {
            TileId = evt.TileId, StartTime = mergeEndTime, Duration = 0, Priority = 5
        });

        // Bomb pop-in scale animation (0 -> 1)
        _ctx.Commands.Add(new ScaleTileCommand
        {
            TileId = evt.NewTileId, FromScale = Vector2.Zero, ToScale = Vector2.One,
            StartTime = mergeEndTime, Duration = _ctx.Config.BombPopDuration,
            Easing = EasingType.OutBack, Priority = 7
        });

        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_created", Position = position,
            StartTime = mergeEndTime, Duration = 0.3f
        });
    }

    /// <summary>
    /// Emit activation effects for a bomb, dispatching to type-specific handlers.
    /// </summary>
    internal void Visit(BombActivatedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var origin = new Vector2(evt.Position.X, evt.Position.Y);

        // Common flash at origin (skip for UFO -- it flies away, flash looks wrong)
        if (evt.BombType != ElementType.Ufo)
        {
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "bomb_flash", Position = origin,
                StartTime = startTime, Duration = 0.15f
            });
        }

        switch (evt.BombType)
        {
            case ElementType.HorizontalRocket:
            case ElementType.VerticalRocket:
                EmitRocketEffects(evt, startTime, origin);
                break;
            case ElementType.ColorBomb:
                _colorBombEffects.EmitColorBombPerformance(
                    evt.TileId, evt.Position, evt.AffectedPositions, startTime, origin);
                break;
            case ElementType.Ufo:
                // UFO effects emitted from ProjectileLaunchedEvent via ProjectileChoreographer
                break;
            default:
                // Square5x5, etc. -- generic shockwave + explosion
                _ctx.Commands.Add(new ShowEffectCommand
                {
                    EffectType = "bomb_shockwave", Position = origin,
                    StartTime = startTime, Duration = 0.3f
                });
                _ctx.Commands.Add(new ShowEffectCommand
                {
                    EffectType = "bomb_explosion", Position = origin,
                    StartTime = startTime, Duration = 0.25f
                });
                break;
        }
    }

    /// <summary>
    /// Emit directional trail + head effects for Horizontal/Vertical rockets.
    /// </summary>
    private void EmitRocketEffects(BombActivatedEvent evt, float startTime, Vector2 origin)
    {
        bool isHorizontal = evt.BombType == ElementType.HorizontalRocket;
        string trailType = isHorizontal ? "rocket_trail_h" : "rocket_trail_v";
        const float trailAccel = 0.8f; // matches logical layer acceleration

        int minExtent = int.MaxValue, maxExtent = int.MinValue;

        // Pre-compute cumulative delays with acceleration
        int maxDist = 0;
        int originAxis = isHorizontal ? evt.Position.X : evt.Position.Y;
        foreach (var pos in evt.AffectedPositions)
        {
            int d = Math.Abs((isHorizontal ? pos.X : pos.Y) - originAxis);
            if (d > maxDist) maxDist = d;
        }

        float[] cumulativeDelay = new float[maxDist + 1];
        cumulativeDelay[0] = 0f;
        float interval = _ctx.Config.RocketTrailInterval;
        for (int i = 1; i <= maxDist; i++)
        {
            cumulativeDelay[i] = cumulativeDelay[i - 1] + interval;
            interval *= trailAccel;
        }

        // Emit staggered trail effects along the rocket path
        foreach (var pos in evt.AffectedPositions)
        {
            int axis = isHorizontal ? pos.X : pos.Y;
            int dist = Math.Abs(axis - originAxis);
            if (axis < minExtent) minExtent = axis;
            if (axis > maxExtent) maxExtent = axis;
            if (dist == 0) continue; // Skip origin cell (already has flash)

            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = trailType, Position = new Vector2(pos.X, pos.Y),
                StartTime = startTime + cumulativeDelay[dist], Duration = 0.15f
            });
        }

        // Head effects at the two endpoints
        if (minExtent != int.MaxValue)
        {
            int crossAxis = isHorizontal ? evt.Position.Y : evt.Position.X;
            int minDist = Math.Abs(minExtent - originAxis);
            int maxDistEnd = Math.Abs(maxExtent - originAxis);
            var minPos = isHorizontal ? new Vector2(minExtent, crossAxis) : new Vector2(crossAxis, minExtent);
            var maxPos = isHorizontal ? new Vector2(maxExtent, crossAxis) : new Vector2(crossAxis, maxExtent);

            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "rocket_head", Position = minPos,
                StartTime = startTime + cumulativeDelay[minDist], Duration = 0.2f
            });
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "rocket_head", Position = maxPos,
                StartTime = startTime + cumulativeDelay[maxDistEnd], Duration = 0.2f
            });
        }
    }

    /// <summary>
    /// Emit effects for bomb combos including double color bomb, color+bomb, and generic combos.
    /// </summary>
    internal void Visit(BombComboEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        // Double color bomb gets the full 4-phase treatment
        if (evt.BombTypeA == ElementType.ColorBomb && evt.BombTypeB == ElementType.ColorBomb)
        {
            _colorBombEffects.EmitDoubleColorBombEffects(evt, startTime, posA, posB);
            return;
        }

        // Color bomb + other combo: session-based flow or instant performance
        if (evt.BombTypeA == ElementType.ColorBomb || evt.BombTypeB == ElementType.ColorBomb)
        {
            bool aIsColor = evt.BombTypeA == ElementType.ColorBomb;
            var otherType = aIsColor ? evt.BombTypeB : evt.BombTypeA;

            if (otherType.IsBomb())
            {
                // ColorBomb + bomb combo -> session handles animation, just remove other tile
                int otherTileId = aIsColor ? evt.TileIdB : evt.TileIdA;
                _ctx.Commands.Add(new RemoveTileCommand
                {
                    TileId = otherTileId, StartTime = startTime + 0.1f,
                    Duration = 0, Priority = 10
                });
                return;
            }

            // ColorBomb + normal tile: instant path (same performance as tap)
            var colorPos = aIsColor ? evt.PositionA : evt.PositionB;
            var colorOrigin = aIsColor ? posA : posB;
            int colorTileId = aIsColor ? evt.TileIdA : evt.TileIdB;
            _colorBombEffects.EmitColorBombPerformance(
                colorTileId, colorPos, evt.AffectedPositions, startTime, colorOrigin);
            return;
        }

        // Other combos: generic effect at both positions
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_combo", Position = posA,
            StartTime = startTime, Duration = 0.5f
        });
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_combo", Position = posB,
            StartTime = startTime, Duration = 0.5f
        });
    }
}
