using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Modifiers;

/// <summary>
/// "Last target" detection: if an objective has remaining=1 AND this cell
/// matches that specific objective, promote to tier=4 + urgency=255.
/// </summary>
public sealed class TargetBonusModifier : IScoreModifier
{
    public static readonly TargetBonusModifier Instance = new();

    public int Order => 10;

    public void Modify(
        ref EvalResult result,
        in GameState state,
        int x, int y,
        ReadOnlySpan<PendingAttack> pendingAttacks,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache)
    {
        if (!result.CanAttack) return;
        if (result.Score.TargetBonus == 0) return; // not an objective target at all

        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            ref var p = ref state.ObjectiveProgress[i];
            if (!p.IsActive || p.IsCompleted) continue;

            int remaining = p.TargetCount - p.CurrentCount;
            if (remaining != 1) continue;

            // Verify this cell actually matches THIS specific objective
            if (CellMatchesObjective(in state, x, y, p.TargetLayer, p.ElementType))
            {
                result.Score.Tier = config.UrgentTier;
                result.Score.Urgency = 255;
                return;
            }
        }
    }

    private static bool CellMatchesObjective(
        in GameState state, int x, int y,
        ObjectiveTargetLayer layer, int elementType)
    {
        return layer switch
        {
            ObjectiveTargetLayer.Cover =>
                state.HasCover(x, y) && (int)state.GetCover(x, y).Type == elementType,
            ObjectiveTargetLayer.Obstacle =>
                state.HasObstacle(x, y) && (int)state.GetObstacle(x, y).Type == elementType,
            ObjectiveTargetLayer.Tile =>
                (int)state.GetType(x, y) == elementType,
            ObjectiveTargetLayer.Ground =>
                state.HasGround(x, y) && (int)state.GetGround(x, y).Type == elementType,
            _ => false
        };
    }
}
