using System;
using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Modifiers;

/// <summary>
/// Group coordination for elements that belong to a group (e.g., Buttons, Vines, Batteries).
/// HP=1 → tier=4, urgency=255. Not the weakest in group → tier=0.
/// Framework placeholder — currently no group elements exist in the project.
/// </summary>
public sealed class GroupModifier : IScoreModifier
{
    public static readonly GroupModifier Instance = new();

    public int Order => 20;

    // Key encoding for precompute cache:
    // Group elements would register via IGroupElement interface (future).
    // Cache key: groupKey → minEffectiveHP

    public void Precompute(
        in GameState state,
        ReadOnlySpan<PendingAttack> pendingAttacks,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache)
    {
        // No group elements currently — no-op.
        // When group elements are added:
        // 1. Scan all cells for IGroupElement
        // 2. Group by GroupKey
        // 3. Compute minEffectiveHP per group (HP - pending on same target)
        // 4. Store in precomputeCache[groupKey] = minEffectiveHP
    }

    public void Modify(
        ref EvalResult result,
        in GameState state,
        int x, int y,
        ReadOnlySpan<PendingAttack> pendingAttacks,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache)
    {
        // No group elements currently — no-op.
    }
}
