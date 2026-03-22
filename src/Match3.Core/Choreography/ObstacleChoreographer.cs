using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Handles obstacle damage, destruction, and generator activation events.
/// Converts <see cref="ObstacleDamagedEvent"/>, <see cref="ObstacleDestroyedEvent"/>,
/// and <see cref="GeneratorActivatedEvent"/> into render commands.
/// </summary>
internal sealed class ObstacleChoreographer
{
    private readonly ChoreographerContext _ctx;

    internal ObstacleChoreographer(ChoreographerContext ctx) => _ctx = ctx;

    /// <summary>
    /// Emit obstacle damage command (stage decreased but not destroyed).
    /// </summary>
    internal void Visit(ObstacleDamagedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        float duration = evt.Type switch
        {
            ObstacleType.Box => 0.25f,
            ObstacleType.Bush => 0.35f,
            ObstacleType.Safe => 0.30f,
            ObstacleType.Cupboard => 0.25f,
            ObstacleType.Stone => 0.30f,
            ObstacleType.ColorBox => 0.25f,
            _ => 0.25f
        };

        _ctx.Commands.Add(new DamageObstacleCommand
        {
            GridPos = evt.GridPosition,
            ObstacleType = evt.Type,
            NewStage = evt.RemainingStage,
            StartTime = startTime,
            Duration = duration
        });

        _ctx.Commands.Add(new ShowEffectCommand
        {
            EffectType = "obstacle_hit",
            Position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y),
            StartTime = startTime,
            Duration = 0.2f
        });
    }

    /// <summary>
    /// Emit obstacle destroy + remove commands (stage reached zero).
    /// </summary>
    internal void Visit(ObstacleDestroyedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        float destroyDuration = evt.Type switch
        {
            ObstacleType.Box => 0.3f,
            ObstacleType.Bush => 0.4f,
            ObstacleType.Safe => 0.40f,
            ObstacleType.Cupboard => 0.35f,
            ObstacleType.Owl => 0.25f,
            ObstacleType.Stone => 0.35f,
            ObstacleType.ColorBox => 0.30f,
            _ => 0.3f
        };

        _ctx.Commands.Add(new DestroyObstacleCommand
        {
            GridPos = evt.GridPosition,
            ObstacleType = evt.Type,
            StartTime = startTime,
            Duration = destroyDuration
        });

        // Remove from visual state after death animation
        _ctx.Commands.Add(new RemoveObstacleCommand
        {
            GridPos = evt.GridPosition,
            StartTime = startTime + destroyDuration,
            Duration = 0
        });

        if (!evt.IsGoal)
        {
            _ctx.Commands.Add(new ShowEffectCommand
            {
                EffectType = "obstacle_destroyed",
                Position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y),
                StartTime = startTime,
                Duration = 0.3f
            });
        }
    }

    /// <summary>
    /// Emit generator activation command (e.g., mailbox open → envelope fly out → close).
    /// </summary>
    internal void Visit(GeneratorActivatedEvent evt)
    {
        float startTime = _ctx.GetStartTime(evt);

        _ctx.Commands.Add(new ActivateGeneratorCommand
        {
            GridPos = evt.GridPosition,
            ObstacleType = evt.ObstacleType,
            ProductType = evt.ProductType,
            ProductPosition = evt.ProductPosition,
            StartTime = startTime,
            Duration = 0.4f
        });
    }
}
