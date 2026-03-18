using System.Numerics;
using Match3.Core.Events;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles cover and ground layer destruction/spawn events.
/// Converts <see cref="CoverDestroyedEvent"/>, <see cref="GroundDestroyedEvent"/>,
/// and <see cref="GroundSpawnedEvent"/> into render commands.
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

        _ctx.Commands.Add(new DestroyCoverCommand
        {
            GridPos = evt.GridPosition, CoverType = evt.Type,
            StartTime = startTime, Duration = 0.25f
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
    /// Emit ground destruction and optional visual effect commands.
    /// </summary>
    internal void Visit(GroundDestroyedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        _ctx.Commands.Add(new DestroyGroundCommand
        {
            GridPos = evt.GridPosition, GroundType = evt.Type,
            StartTime = startTime, Duration = 0.25f
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

    /// <summary>Ground spawned by death effect — placeholder for future animation.</summary>
    internal void Visit(GroundSpawnedEvent evt) { }
}
