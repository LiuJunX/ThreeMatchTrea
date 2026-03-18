using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles board-level events: match highlight, shuffle animation,
/// and game state notifications (score, combo, move, deadlock, objective, level).
/// </summary>
internal sealed class BoardChoreographer
{
    private readonly ChoreographerContext _ctx;

    internal BoardChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Emit highlight command for matched tile positions.
    /// </summary>
    internal void Visit(MatchDetectedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var positions = new Position[evt.Positions.Count];
        int i = 0;
        foreach (var pos in evt.Positions)
            positions[i++] = pos;

        _ctx.Commands.Add(new ShowMatchHighlightCommand
        {
            Positions = positions, StartTime = startTime,
            Duration = _ctx.Config.MatchHighlightDuration
        });
    }

    /// <summary>
    /// Emit gather/scatter shuffle animation with per-tile staggering,
    /// type updates, and a center flash effect.
    /// </summary>
    internal void Visit(BoardShuffledEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var allTiles = evt.AllShuffledTiles;
        if (allTiles.Count == 0)
        {
            // Fallback: no AllShuffledTiles -- instant type update
            foreach (var change in evt.Changes)
            {
                _ctx.Commands.Add(new UpdateTileTypeCommand
                {
                    TileId = change.TileId, Position = change.Position,
                    TileType = change.ToType, StartTime = startTime, Duration = 0
                });
            }
            return;
        }

        // Compute board center for stagger ordering
        float centerX = 0f, centerY = 0f;
        foreach (var tile in allTiles) { centerX += tile.Position.X; centerY += tile.Position.Y; }
        centerX /= allTiles.Count;
        centerY /= allTiles.Count;

        float maxDist = 0f;
        foreach (var tile in allTiles)
        {
            float dx = tile.Position.X - centerX, dy = tile.Position.Y - centerY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > maxDist) maxDist = dist;
        }
        if (maxDist < 0.001f) maxDist = 1f;

        float gatherDuration = _ctx.Config.ShuffleGatherDuration;
        float gatherScale = _ctx.Config.ShuffleGatherScale;
        float gatherRotation = _ctx.Config.ShuffleGatherRotation;
        float midPause = _ctx.Config.ShuffleMidpointPause;
        float scatterDuration = _ctx.Config.ShuffleScatterDuration;
        float staggerMax = _ctx.Config.ShuffleStaggerMax;

        var changeMap = new Dictionary<int, TileTypeChange>(evt.Changes.Count);
        foreach (var change in evt.Changes)
            changeMap[change.TileId] = change;

        foreach (var tile in allTiles)
        {
            float dx = tile.Position.X - centerX, dy = tile.Position.Y - centerY;
            float normalizedDist = MathF.Sqrt(dx * dx + dy * dy) / maxDist;
            float stagger = normalizedDist * staggerMax;

            // Phase 1: Gather (shrink + rotate, staggered by distance)
            float gatherStart = startTime + stagger;
            _ctx.Commands.Add(new ScaleTileCommand
            {
                TileId = tile.TileId, FromScale = Vector2.One,
                ToScale = new Vector2(gatherScale, gatherScale),
                StartTime = gatherStart, Duration = gatherDuration,
                Easing = EasingType.InQuadratic
            });
            _ctx.Commands.Add(new RotateTileCommand
            {
                TileId = tile.TileId, FromAngle = 0f, ToAngle = gatherRotation,
                StartTime = gatherStart, Duration = gatherDuration,
                Easing = EasingType.InQuadratic
            });

            // Phase 2: Type swap (instant, when tiles are invisible)
            float typeSwapTime = startTime + staggerMax + gatherDuration;
            if (changeMap.TryGetValue(tile.TileId, out var change))
            {
                _ctx.Commands.Add(new UpdateTileTypeCommand
                {
                    TileId = tile.TileId, Position = change.Position,
                    TileType = change.ToType, StartTime = typeSwapTime, Duration = 0
                });
            }

            // Phase 3: Scatter (scale back up + rotate, reverse stagger)
            float scatterStart = typeSwapTime + midPause + (staggerMax - stagger);
            _ctx.Commands.Add(new ScaleTileCommand
            {
                TileId = tile.TileId,
                FromScale = new Vector2(gatherScale, gatherScale), ToScale = Vector2.One,
                StartTime = scatterStart, Duration = scatterDuration,
                Easing = EasingType.OutBack
            });
            _ctx.Commands.Add(new RotateTileCommand
            {
                TileId = tile.TileId, FromAngle = gatherRotation, ToAngle = 360f,
                StartTime = scatterStart, Duration = scatterDuration,
                Easing = EasingType.OutCubic
            });
        }

        // Phase 4: Center flash effect
        float effectTime = startTime + staggerMax + gatherDuration;
        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "shuffle_flash",
            Position = new Vector2(centerX, centerY),
            StartTime = effectTime, Duration = 0.1f
        });
    }

    /// <summary>Score events don't generate render commands; UI handles display.</summary>
    internal void Visit(ScoreAddedEvent evt) { }

    /// <summary>Combo events don't generate render commands; UI handles display.</summary>
    internal void Visit(ComboChangedEvent evt) { }

    /// <summary>Move completed events don't generate render commands.</summary>
    internal void Visit(MoveCompletedEvent evt) { }

    /// <summary>Deadlock events don't generate render commands; UI handles notification.</summary>
    internal void Visit(DeadlockDetectedEvent evt) { }

    /// <summary>Objective progress events don't generate render commands.</summary>
    internal void Visit(ObjectiveProgressEvent evt) { }

    /// <summary>Level completed events don't generate render commands.</summary>
    internal void Visit(LevelCompletedEvent evt) { }
}
