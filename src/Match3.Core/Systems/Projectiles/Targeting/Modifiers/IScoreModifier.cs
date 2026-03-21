using System;
using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Modifiers;

/// <summary>
/// Modifies cell scores after layer evaluation (e.g., objective bonus, group coordination).
/// Implementations must be stateless — all precomputed data goes into caller-owned storage.
/// </summary>
public interface IScoreModifier
{
    /// <summary>Execution order (lower runs first).</summary>
    int Order { get; }

    /// <summary>
    /// Pre-compute global data before per-cell evaluation.
    /// Called once per selection round (re-called when pendingAttacks changes).
    /// Write results to <paramref name="precomputeCache"/> — caller owns the storage.
    /// </summary>
    void Precompute(
        in GameState state,
        ReadOnlySpan<PendingAttack> pendingAttacks,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache) { }

    /// <summary>
    /// Modify the evaluation result for a single cell.
    /// </summary>
    void Modify(
        ref EvalResult result,
        in GameState state,
        int x, int y,
        ReadOnlySpan<PendingAttack> pendingAttacks,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache);
}
