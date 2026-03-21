using System;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting.Capacity;
using Match3.Core.Systems.Projectiles.Targeting.Layers;

namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Evaluates a single cell for UFO targeting by penetrating the layer stack.
/// Stateless — all inputs via parameters.
/// </summary>
public static class CellEvaluator
{
    // Layer handlers in penetration order: Cover → Obstacle → Tile → Ground
    private static readonly ILayerHandler[] Handlers =
    {
        CoverLayerHandler.Instance,
        ObstacleLayerHandler.Instance,
        TileLayerHandler.Instance,
        GroundLayerHandler.Instance
    };

    /// <summary>
    /// Evaluate a cell by traversing the layer stack top-down.
    /// Collects all hit layers, then aggregates score and capacity.
    /// </summary>
    public static EvalResult Evaluate(
        in GameState state,
        int x, int y,
        UfoTargetConfig config,
        ICapacityRule capacityRule)
    {
        var result = new EvalResult();

        // Stack-allocated hit layer buffer (max 4 layers)
        Span<HitLayer> hitLayers = stackalloc HitLayer[Handlers.Length];
        int hitCount = 0;

        // Phase 1: Traverse layer stack, collect hit layers
        foreach (var handler in Handlers)
        {
            var hit = handler.Evaluate(in state, x, y, config);

            // Sentinel: untargetable cell
            if (hit.Value == ushort.MaxValue)
                return result; // canAttack = false

            // Empty/inactive layer → skip, continue to next
            if (hit.Value == 0 && hit.HitCapacity == 0 && !hit.IsTarget)
                continue;

            hitLayers[hitCount++] = hit;

            if (hit.BlocksPenetration)
                break;
        }

        if (hitCount == 0)
            return result; // nothing to hit

        result.CanAttack = true;
        var layers = hitLayers[..hitCount];

        // Phase 2: Aggregate score
        result.Score = AggregateScore(layers, config);

        // Phase 3: Aggregate capacity
        result.MeaningfulHits = capacityRule.Calculate(layers);

        return result;
    }

    private static CellScore AggregateScore(ReadOnlySpan<HitLayer> layers, UfoTargetConfig config)
    {
        var score = new CellScore();
        bool targetBonusGiven = false;

        foreach (ref readonly var layer in layers)
        {
            if (layer.BlocksPenetration)
            {
                // Cover/Obstacle: accumulate (not overwrite) + set tier=2
                score.Tier = Math.Max(score.Tier, (byte)2);
                score.BaseValue += layer.Value;

                if (layer.IsTarget && !targetBonusGiven)
                {
                    targetBonusGiven = true;
                    score.Tier = config.TargetTier;
                    score.TargetBonus = config.TargetBonus;
                }
                break; // Don't look below blocking layer
            }

            // Non-blocking: accumulate
            score.BaseValue += layer.Value;

            if (layer.IsTarget && !targetBonusGiven)
            {
                targetBonusGiven = true;
                score.Tier = Math.Max(score.Tier, config.TargetTier);
                score.TargetBonus = config.TargetBonus;
            }
        }

        // If no target bonus was given and tier is still 0, set to normal (1)
        if (score.Tier == 0 && score.BaseValue > 0)
            score.Tier = 1;

        return score;
    }
}
