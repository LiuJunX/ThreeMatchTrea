using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Layers;

/// <summary>
/// Evaluates the Ground layer (Ice, Grass, Leaves).
/// Ground is damaged when the tile above is destroyed (penetration from Tile layer).
/// </summary>
public sealed class GroundLayerHandler : ILayerHandler
{
    public static readonly GroundLayerHandler Instance = new();

    public byte LayerIndex => 3;

    public HitLayer Evaluate(in GameState state, int x, int y, UfoTargetConfig config)
    {
        if (!state.HasGround(x, y))
            return default;

        ref readonly var ground = ref state.GetGround(x, y);
        if (ground.Type == GroundType.None || ground.Health <= 0)
            return default;

        return new HitLayer
        {
            Layer = LayerIndex,
            Value = config.GroundBaseValue,
            HitCapacity = ground.Health,
            IsTarget = IsObjective(in state, ground.Type),
            BlocksPenetration = false
        };
    }

    private static bool IsObjective(in GameState state, GroundType groundType)
    {
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            ref var p = ref state.ObjectiveProgress[i];
            if (p.IsActive && !p.IsCompleted
                && p.TargetLayer == ObjectiveTargetLayer.Ground
                && p.ElementType == (int)groundType)
                return true;
        }
        return false;
    }
}
