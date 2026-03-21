using System.Numerics;
using Match3.Core.Events;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles cover and ground layer destruction/spawn/damage events.
/// Converts <see cref="CoverDestroyedEvent"/>, <see cref="GroundDamagedEvent"/>,
/// <see cref="GroundDestroyedEvent"/>, and <see cref="GroundSpawnedEvent"/> into render commands.
/// </summary>
internal sealed class SurfaceChoreographer
{
    private readonly ChoreographerContext _ctx;

    internal SurfaceChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Emit cover destruction and optional visual effect commands.
    /// </summary>
    internal void Visit(CoverDestroyedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        float destroyDuration = 0.25f;

        _ctx.Commands.Add(new DestroyCoverCommand
        {
            GridPos = evt.GridPosition, CoverType = evt.Type,
            StartTime = startTime, Duration = destroyDuration
        });

        // Remove from visual state after death animation
        _ctx.Commands.Add(new RemoveCoverCommand
        {
            GridPos = evt.GridPosition,
            StartTime = startTime + destroyDuration,
            Duration = 0
        });

        if (!evt.IsGoal)
        {
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "cover_destroyed", Position = position,
                StartTime = startTime, Duration = 0.25f
            });
        }
    }

    /// <summary>
    /// Emit ground damage command (health decreased but not destroyed).
    /// </summary>
    internal void Visit(GroundDamagedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        _ctx.Commands.Add(new DamageGroundCommand
        {
            GridPos = evt.GridPosition,
            GroundType = evt.Type,
            NewHealth = evt.RemainingHealth,
            StartTime = startTime,
            Duration = 0.25f
        });

        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "ground_hit",
            Position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y),
            StartTime = startTime,
            Duration = 0.2f
        });
    }

    /// <summary>
    /// Emit ground destroy + remove commands (health reached zero).
    /// </summary>
    internal void Visit(GroundDestroyedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        float destroyDuration = 0.25f;

        _ctx.Commands.Add(new DestroyGroundCommand
        {
            GridPos = evt.GridPosition, GroundType = evt.Type,
            StartTime = startTime, Duration = destroyDuration
        });

        // Remove from visual state after death animation
        _ctx.Commands.Add(new RemoveGroundCommand
        {
            GridPos = evt.GridPosition,
            StartTime = startTime + destroyDuration,
            Duration = 0
        });

        if (!evt.IsGoal)
        {
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "ground_destroyed", Position = position,
                StartTime = startTime, Duration = 0.25f
            });
        }
    }

    /// <summary>
    /// Emit ground spawn command (death effect spread).
    /// </summary>
    internal void Visit(GroundSpawnedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        _ctx.Commands.Add(new SpawnGroundCommand
        {
            GridPos = evt.GridPosition,
            GroundType = evt.Type,
            Health = 1,
            StartTime = startTime,
            Duration = 0.3f
        });
    }
}
