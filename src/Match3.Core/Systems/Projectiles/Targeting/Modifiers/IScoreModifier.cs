using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Modifiers;

/// <summary>
/// Modifies cell scores after layer evaluation (e.g., objective bonus, group coordination).
/// Implementations must be stateless â€?all precomputed data goes into caller-owned storage.
/// </summary>
public interface IScoreModifier
{
    /// <summary>Execution order (lower runs first).</summary>
    int Order { get; }

    /// <summary>
    /// Pre-compute global data before per-cell evaluation.
    /// Called once per selection round.
    /// Write results to <paramref name="precomputeCache"/> â€?caller owns the storage.
    /// </summary>
    void Precompute(
        in GameState state,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache) { }

    /// <summary>
    /// Modify the evaluation result for a single cell.
    /// </summary>
    void Modify(
        ref EvalResult result,
        in GameState state,
        int x, int y,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache);
}
