using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting.BombRange;
using Match3.Core.Systems.Projectiles.Targeting.Capacity;
using Match3.Core.Systems.Projectiles.Targeting.Modifiers;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Pure-function entry point for UFO smart target selection.
/// Stateless and parallel-safe — all data via parameters.
/// </summary>
public static class UfoTargetSelector
{
    // Modifiers in execution order
    private static readonly IScoreModifier[] Modifiers =
    {
        TargetBonusModifier.Instance,
        GroupModifier.Instance
    };

    private static readonly ICapacityRule CapacityRule = MaxCapacityRule.Instance;

    /// <summary>
    /// Select the best target position for a UFO projectile.
    /// For payload UFOs (Row/Column/Area5x5), evaluates total range value at each drop point.
    /// </summary>
    /// <param name="state">Board state (read-only).</param>
    /// <param name="origin">UFO launch position.</param>
    /// <param name="inFlightAttacks">Pending attacks from other in-flight UFOs.</param>
    /// <param name="config">Immutable targeting configuration.</param>
    /// <param name="excludeArea">Positions to exclude (e.g., small cross area).</param>
    /// <param name="payload">Bomb payload type. Default=single cell, others use range aggregation.</param>
    /// <returns>Best target position, or null if no valid target exists.</returns>
    public static Position? SelectTarget(
        in GameState state,
        Position origin,
        ReadOnlySpan<PendingAttack> inFlightAttacks,
        UfoTargetConfig config,
        HashSet<Position>? excludeArea = null,
        UfoPayload payload = UfoPayload.Default)
    {
        // Precompute cache (stack-owned, passed to modifiers)
        var precomputeCache = Pools.Obtain<Dictionary<int, int>>();

        // Candidate list
        var candidates = Pools.ObtainList<(Position pos, CellScore score)>();

        try
        {
            // Modifier precompute
            foreach (var mod in Modifiers)
                mod.Precompute(in state, inFlightAttacks, config, precomputeCache);

            // Evaluate all cells
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    // Skip void cells
                    if (state.IsVoid(x, y)) continue;

                    // Skip origin position
                    if (x == origin.X && y == origin.Y) continue;

                    // Skip excluded area
                    var pos = new Position(x, y);
                    if (excludeArea != null && excludeArea.Contains(pos)) continue;

                    // Skip targeting-locked cells
                    if (state.IsLocked(x, y, CellLockType.Targeting)) continue;

                    // Evaluate cell
                    var eval = CellEvaluator.Evaluate(in state, x, y, config, CapacityRule);
                    if (!eval.CanAttack) continue;

                    // Deduct pending attacks on this cell
                    int pendingOnCell = CountPendingOnCell(inFlightAttacks, x, y, state.Width);
                    if (eval.MeaningfulHits <= pendingOnCell) continue;

                    // Apply modifiers
                    foreach (var mod in Modifiers)
                        mod.Modify(ref eval, in state, x, y, inFlightAttacks, config, precomputeCache);

                    if (!eval.CanAttack) continue;

                    candidates.Add((pos, eval.Score));
                }
            }

            if (candidates.Count == 0)
                return null;

            // Payload UFOs: select by total range value instead of single-cell score
            if (payload != UfoPayload.Default)
            {
                return BombAreaAggregator.PickBestDropPoint(
                    candidates, payload, in state, config, CapacityRule, state.Random);
            }

            // Default UFO: pick highest single-cell score
            // Shuffle for random tiebreaking among equal scores
            Shuffle(candidates, state.Random);

            // Sort descending by CellScore
            candidates.Sort(static (a, b) => b.score.CompareTo(a.score));

            return candidates[0].pos;
        }
        finally
        {
            Pools.Release(candidates);
            precomputeCache.Clear();
            Pools.Release(precomputeCache);
        }
    }

    private static int CountPendingOnCell(
        ReadOnlySpan<PendingAttack> pending,
        int x, int y, int width)
    {
        ushort gridIndex = (ushort)(y * width + x);
        int count = 0;
        foreach (ref readonly var pa in pending)
        {
            if (pa.GridIndex == gridIndex)
                count++;
        }
        return count;
    }

    private static void Shuffle<T>(List<T> list, Match3.Random.IRandom random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
