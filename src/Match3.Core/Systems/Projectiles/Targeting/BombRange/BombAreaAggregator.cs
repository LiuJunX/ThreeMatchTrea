using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting.Capacity;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Projectiles.Targeting.BombRange;

/// <summary>
/// For payload UFOs (Row/Column/Area5x5), evaluates each candidate drop point
/// by summing the value of all cells in the affected range.
/// Picks the drop point with the highest total range value.
/// </summary>
public static class BombAreaAggregator
{
    private static readonly Dictionary<UfoPayload, IBombRangeProvider> Providers = new()
    {
        { UfoPayload.Row, RowRangeProvider.Instance },
        { UfoPayload.Column, ColumnRangeProvider.Instance },
        { UfoPayload.Area5x5, Area5x5RangeProvider.Instance }
    };

    /// <summary>
    /// Select the best drop point for a payload UFO by evaluating range total value.
    /// </summary>
    /// <param name="candidates">Pre-evaluated candidate cells (from CellEvaluator).</param>
    /// <param name="payload">The bomb payload type.</param>
    /// <param name="state">Board state.</param>
    /// <param name="config">Targeting config.</param>
    /// <param name="capacityRule">Capacity rule for cell evaluation.</param>
    /// <param name="random">Deterministic random for tiebreaking.</param>
    /// <returns>Best drop point position, or null if no valid target.</returns>
    public static Position? PickBestDropPoint(
        List<(Position pos, CellScore score)> candidates,
        UfoPayload payload,
        in GameState state,
        UfoTargetConfig config,
        ICapacityRule capacityRule,
        Match3.Random.IRandom random)
    {
        if (!Providers.TryGetValue(payload, out var provider))
            return null;

        if (candidates.Count == 0)
            return null;

        var rangePositions = Pools.ObtainList<Position>();
        var bestCandidates = Pools.ObtainList<(Position pos, int totalValue)>();

        try
        {
            int bestTotal = int.MinValue;

            foreach (var (dropPoint, _) in candidates)
            {
                // Get affected range for this drop point
                rangePositions.Clear();
                provider.GetRange(dropPoint, in state, rangePositions);

                // Sum values across the range
                int totalValue = 0;
                foreach (var rangePos in rangePositions)
                {
                    if (state.IsVoid(rangePos.X, rangePos.Y)) continue;

                    var eval = CellEvaluator.Evaluate(in state, rangePos.X, rangePos.Y, config, capacityRule);
                    if (eval.CanAttack)
                        totalValue += eval.Score.TotalValue;
                }

                if (totalValue > bestTotal)
                {
                    bestTotal = totalValue;
                    bestCandidates.Clear();
                    bestCandidates.Add((dropPoint, totalValue));
                }
                else if (totalValue == bestTotal)
                {
                    bestCandidates.Add((dropPoint, totalValue));
                }
            }

            if (bestCandidates.Count == 0)
                return null;

            if (bestCandidates.Count == 1)
                return bestCandidates[0].pos;

            // Tiebreak: random among equal total values
            int idx = random.Next(0, bestCandidates.Count);
            return bestCandidates[idx].pos;
        }
        finally
        {
            Pools.Release(rangePositions);
            Pools.Release(bestCandidates);
        }
    }
}
