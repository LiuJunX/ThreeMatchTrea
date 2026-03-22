using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Layers;

/// <summary>
/// Evaluates the Obstacle layer (Box, Bush, Safe, Cupboard, ColorBox, etc.).
/// Obstacles that can't be hit by UFO (MagicHat, Curtain, Mailbox)
/// return a sentinel value to mark the cell as untargetable.
/// </summary>
public sealed class ObstacleLayerHandler : ILayerHandler
{
    public static readonly ObstacleLayerHandler Instance = new();

    public byte LayerIndex => 1;

    public HitLayer Evaluate(in GameState state, int x, int y, UfoTargetConfig config)
    {
        if (!state.HasObstacle(x, y))
            return default;

        ref readonly var obstacle = ref state.GetObstacle(x, y);
        if (obstacle.Type == ObstacleType.None || obstacle.Stage <= 0)
            return default;

        // Obstacles that UFO can't hit → mark cell as blocked
        if (!UfoTargetConfig.CanUfoHitObstacle(obstacle.Type))
            return new HitLayer { Value = ushort.MaxValue }; // sentinel: untargetable

        ushort value = config.ObstacleBaseValue;

        // Power-up-only obstacles get extra value — UFO is one of few ways to damage them
        if (obstacle.Type == ObstacleType.Safe)
            value += config.SafeExtraValue;
        else if (obstacle.Type == ObstacleType.ColorBox)
            value += config.ColorBoxExtraValue;

        return new HitLayer
        {
            Layer = LayerIndex,
            Value = value,
            HitCapacity = obstacle.Stage,
            IsTarget = IsObjective(in state, obstacle.Type),
            BlocksPenetration = true // Obstacles occupy the cell
        };
    }

    private static bool IsObjective(in GameState state, ObstacleType obstacleType)
    {
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            ref var p = ref state.ObjectiveProgress[i];
            if (p.IsActive && !p.IsCompleted
                && p.TargetLayer == ObjectiveTargetLayer.Obstacle
                && p.ElementType == (int)obstacleType)
                return true;
        }
        return false;
    }
}
