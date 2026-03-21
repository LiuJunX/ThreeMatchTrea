using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Layers;

/// <summary>
/// Evaluates the Cover layer (Cage, Chain, Bubble, Honey, Frost).
/// Covers absorb UFO hits — the tile underneath is NOT damaged.
/// Bubble is dynamic and allows penetration.
/// </summary>
public sealed class CoverLayerHandler : ILayerHandler
{
    public static readonly CoverLayerHandler Instance = new();

    public byte LayerIndex => 0;

    public HitLayer Evaluate(in GameState state, int x, int y, UfoTargetConfig config)
    {
        if (!state.HasCover(x, y))
            return default;

        ref readonly var cover = ref state.GetCover(x, y);
        if (cover.Type == CoverType.None || cover.Health <= 0)
            return default;

        bool blocks = UfoTargetConfig.CoverBlocksPenetration(cover.Type);

        // Bubble doesn't block — skip it, let lower layers be evaluated
        if (!blocks)
            return default;

        return new HitLayer
        {
            Layer = LayerIndex,
            Value = config.CoverBaseValue,
            HitCapacity = cover.Health,
            IsTarget = IsObjective(in state, cover.Type),
            BlocksPenetration = true
        };
    }

    private static bool IsObjective(in GameState state, CoverType coverType)
    {
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            ref var p = ref state.ObjectiveProgress[i];
            if (p.IsActive && !p.IsCompleted
                && p.TargetLayer == ObjectiveTargetLayer.Cover
                && p.ElementType == (int)coverType)
                return true;
        }
        return false;
    }
}
