using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles tile movement, spawning, destruction, and swap events.
/// Converts <see cref="TileMovedEvent"/>, <see cref="TileSpawnedEvent"/>,
/// <see cref="TileDestroyedEvent"/>, and <see cref="TilesSwappedEvent"/>
/// into render commands with pre-calculated timing.
/// </summary>
internal sealed class TileChoreographer
{
    private readonly ChoreographerContext _ctx;

    /// <summary>
    /// Initializes a new instance with the shared choreographer context.
    /// </summary>
    /// <param name="ctx">Shared state for all sub-processors.</param>
    internal TileChoreographer(ChoreographerContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// Emit a move command for a tile that changed position during simulation.
    /// </summary>
    internal void Visit(TileMovedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        var command = new MoveTileCommand
        {
            TileId = evt.TileId,
            From = evt.FromPosition,
            To = evt.ToPosition,
            StartTime = startTime,
            Duration = _ctx.Config.MoveDuration,
            Easing = EasingType.OutCubic
        };

        _ctx.Commands.Add(command);
    }

    /// <summary>
    /// Emit destruction animation for a tile, handling merge, beam-delay, and standard paths.
    /// </summary>
    internal void Visit(TileDestroyedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        if (evt.MergeTarget.HasValue)
        {
            // Merge animation: move to bomb origin (no scaling -- keep original size)
            var target = new Vector2(evt.MergeTarget.Value.X, evt.MergeTarget.Value.Y);

            _ctx.Commands.Add(new MoveTileCommand
            {
                TileId = evt.TileId,
                From = position,
                To = target,
                StartTime = startTime,
                Duration = _ctx.Config.MergeDuration,
                Easing = EasingType.InOutCubic
            });

            float endTime = startTime + _ctx.Config.MergeDuration;

            // Remove tile after merge completes
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = evt.TileId,
                StartTime = endTime,
                Duration = 0,
                Priority = 10
            });
        }
        else if (_ctx.BeamHitTimes.TryGetValue(evt.GridPosition, out float beamHitTime))
        {
            // Color bomb beam target: delay destruction until beam arrives
            _ctx.BeamHitTimes.Remove(evt.GridPosition);
            EmitBeamTargetDestroy(evt, position, beamHitTime);
        }
        else
        {
            // Standard destroy animation: fade + scale down in place.
            // For goal tiles the DestroyTileCommand still runs (sets IsBeingAnimated
            // to protect from SyncFallingTilesFromGameState), but HiddenTileIds in
            // Board3DView prevents rendering -- so the shrink is invisible.
            _ctx.Commands.Add(new DestroyTileCommand
            {
                TileId = evt.TileId,
                Position = position,
                Reason = evt.Reason,
                StartTime = startTime,
                Duration = _ctx.Config.DestroyDuration
            });

            float endTime = startTime + _ctx.Config.DestroyDuration;

            // Suppress visual effects for goal tiles (ObjectiveDisplayController
            // handles the fly animation; match_pop would be redundant).
            if (!evt.IsGoal)
            {
                string effectType = evt.Reason switch
                {
                    ElimSource.Match => "match_pop",
                    ElimSource.Bomb => "explosion",
                    ElimSource.Projectile => "projectile_hit",
                    ElimSource.ChainReaction => "chain_pop",
                    ElimSource.ColorBomb => "explosion",
                    _ => "pop"
                };

                _ctx.Commands.Add(new ShowEffectCommand
                {
                    EffectType = effectType,
                    Position = position,
                    StartTime = startTime,
                    Duration = _ctx.Config.DestroyDuration
                });
            }

            // Remove tile after destroy animation completes
            _ctx.Commands.Add(new RemoveTileCommand
            {
                TileId = evt.TileId,
                StartTime = endTime,
                Duration = 0,
                Priority = 10
            });
        }
    }

    /// <summary>
    /// Color bomb beam target: delay destruction until beam arrives.
    /// </summary>
    private void EmitBeamTargetDestroy(TileDestroyedEvent evt, Vector2 position, float beamHitTime)
    {
        // Pause after beam impact before tile starts dissolving --
        // gives the player time to read the beam pattern before targets explode.
        const float hitPause = 0.5f;
        float destroyStart = beamHitTime + hitPause;

        // Continuous shake from beam impact until destroy starts
        _ctx.Commands.Add(new ShakeTileCommand
        {
            TileId = evt.TileId,
            Amplitude = _ctx.Config.ColorBombHitShakeAmplitude,
            Frequency = _ctx.Config.ColorBombHitShakeFrequency,
            StartTime = beamHitTime,
            Duration = hitPause
        });

        // Hold tile in place until destroy starts -- keeps IsBeingAnimated=true
        // so SyncFallingTilesFromGameState won't garbage-collect it early.
        float holdStart = _ctx.GetStartTime(evt);
        if (destroyStart > holdStart)
        {
            _ctx.Commands.Add(new MoveTileCommand
            {
                TileId = evt.TileId,
                From = position,
                To = position,
                StartTime = holdStart,
                Duration = destroyStart - holdStart,
                Easing = EasingType.Linear
            });
        }

        // Destroy animation starts after hit pause
        _ctx.Commands.Add(new DestroyTileCommand
        {
            TileId = evt.TileId,
            Position = position,
            Reason = evt.Reason,
            StartTime = destroyStart,
            Duration = _ctx.Config.DestroyDuration
        });

        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "explosion",
            Position = position,
            StartTime = destroyStart,
            Duration = _ctx.Config.DestroyDuration
        });

        float endTime = destroyStart + _ctx.Config.DestroyDuration;

        _ctx.Commands.Add(new RemoveTileCommand
        {
            TileId = evt.TileId,
            StartTime = endTime,
            Duration = 0,
            Priority = 10
        });
    }

    /// <summary>
    /// Emit a spawn command for a newly created tile.
    /// </summary>
    internal void Visit(TileSpawnedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        var spawnCommand = new SpawnTileCommand
        {
            TileId = evt.TileId,
            Type = evt.Type,
            GridPos = evt.GridPosition,
            SpawnPos = evt.SpawnPosition,
            StartTime = startTime,
            Duration = 0
        };
        _ctx.Commands.Add(spawnCommand);
    }

    /// <summary>
    /// Emit a swap command and clear cross-batch state from previous moves.
    /// </summary>
    internal void Visit(TilesSwappedEvent evt)
    {
        // New move boundary -- clear cross-batch state from previous moves
        _ctx.BeamHitTimes.Clear();
        _ctx.ActiveColorBombSessions.Clear();

        float startTime = _ctx.GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        var swapCommand = new SwapTilesCommand
        {
            TileAId = evt.TileAId,
            TileBId = evt.TileBId,
            PosA = posA,
            PosB = posB,
            IsRevert = evt.IsRevert,
            StartTime = startTime,
            Duration = _ctx.Config.SwapDuration,
            Easing = EasingType.OutCubic
        };
        _ctx.Commands.Add(swapCommand);
    }
}
