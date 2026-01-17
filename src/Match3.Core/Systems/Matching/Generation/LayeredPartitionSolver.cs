using System.Buffers;
using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Solves the optimal partition problem using a layered approach:
/// exact solving for high-weight candidates, greedy for low-weight.
/// Extracted from the original static PartitionSolver class.
/// </summary>
public sealed class LayeredPartitionSolver : IPartitionSolver
{
    // Weight thresholds for multi-layer solving
    private const int RainbowWeightThreshold = 100;  // Rainbow (130) - always exact
    private const int TntWeightThreshold = 50;       // TNT (60)
    private const int RocketWeightThreshold = 30;    // Rocket (40)
    // UFO (20) is always greedy

    // Max candidates for exact solving per layer
    private const int MaxExactSolveCount = 25;

    /// <inheritdoc />
    public void FindOptimalPartition(
        List<DetectedShape> candidates,
        HashSet<Position> component,
        List<int> bestIndices)
    {
        var positionToIndex = Pools.Obtain<Dictionary<Position, int>>();
        var candidateMasks = ArrayPool<BitMask256>.Shared.Rent(candidates.Count);

        try
        {
            // Map component positions to indices for BitMask
            int posIndex = 0;
            foreach (var p in component)
            {
                positionToIndex[p] = posIndex++;
            }

            // Precompute masks for all candidates
            for (int i = 0; i < candidates.Count; i++)
            {
                var mask = new BitMask256();
                foreach (var p in candidates[i].Cells!)
                {
                    if (positionToIndex.TryGetValue(p, out int index) && index < 256)
                    {
                        mask.Set(index);
                    }
                }
                candidateMasks[i] = mask;
            }

            // Layered Exact Solving: Solve high-weight exactly, low-weight greedily
            FindOptimalPartitionLayered(candidates, candidateMasks, bestIndices);
        }
        finally
        {
            positionToIndex.Clear();
            Pools.Release(positionToIndex);
            ArrayPool<BitMask256>.Shared.Return(candidateMasks);
        }
    }

    private void FindOptimalPartitionLayered(
        List<DetectedShape> candidates,
        BitMask256[] candidateMasks,
        List<int> bestIndices)
    {
        var rainbowIndices = Pools.ObtainList<int>();   // >= 100 (Rainbow: 130)
        var tntIndices = Pools.ObtainList<int>();       // >= 50 (TNT: 60)
        var rocketIndices = Pools.ObtainList<int>();    // >= 30 (Rocket: 40)
        var ufoIndices = Pools.ObtainList<int>();       // < 30 (UFO: 20)

        try
        {
            // Phase 1: Categorize candidates by weight tier
            for (int i = 0; i < candidates.Count; i++)
            {
                int weight = candidates[i].Weight;
                if (weight >= RainbowWeightThreshold)
                    rainbowIndices.Add(i);
                else if (weight >= TntWeightThreshold)
                    tntIndices.Add(i);
                else if (weight >= RocketWeightThreshold)
                    rocketIndices.Add(i);
                else
                    ufoIndices.Add(i);
            }

            var usedMask = new BitMask256();

            // Phase 2: Solve Rainbow layer (highest priority)
            if (rainbowIndices.Count > 0)
            {
                if (rainbowIndices.Count <= MaxExactSolveCount)
                {
                    SolveBranchAndBoundSubset(candidates, rainbowIndices, candidateMasks, bestIndices);
                }
                else
                {
                    // Sort by size DESC (prefer larger rainbows that cover more)
                    rainbowIndices.Sort((a, b) => candidates[b].Cells!.Count.CompareTo(candidates[a].Cells!.Count));
                    SolveGreedySubset(rainbowIndices, candidateMasks, ref usedMask, bestIndices);
                }
                foreach (var idx in bestIndices)
                {
                    usedMask.UnionWith(candidateMasks[idx]);
                }
            }

            // Phase 3: Solve TNT + Rocket together (they compete for space)
            var tntAndRocketIndices = Pools.ObtainList<int>();
            try
            {
                // Filter out candidates that overlap with already-selected Rainbow
                foreach (var idx in tntIndices)
                {
                    if (!usedMask.Overlaps(candidateMasks[idx]))
                        tntAndRocketIndices.Add(idx);
                }
                foreach (var idx in rocketIndices)
                {
                    if (!usedMask.Overlaps(candidateMasks[idx]))
                        tntAndRocketIndices.Add(idx);
                }

                if (tntAndRocketIndices.Count > 0)
                {
                    int prevCount = bestIndices.Count;

                    if (tntAndRocketIndices.Count <= MaxExactSolveCount)
                    {
                        // Exact Branch & Bound
                        SolveBranchAndBoundSubset(candidates, tntAndRocketIndices, candidateMasks, bestIndices);
                    }
                    else
                    {
                        // Too many candidates - use smart greedy
                        tntAndRocketIndices.Sort((a, b) =>
                        {
                            int weightDiff = candidates[b].Weight.CompareTo(candidates[a].Weight);
                            if (weightDiff != 0) return weightDiff;
                            // Same weight: prefer smaller size (blocks less space)
                            return candidates[a].Cells!.Count.CompareTo(candidates[b].Cells!.Count);
                        });
                        SolveGreedySubset(tntAndRocketIndices, candidateMasks, ref usedMask, bestIndices);
                    }

                    // Update used mask with newly selected candidates
                    for (int i = prevCount; i < bestIndices.Count; i++)
                    {
                        usedMask.UnionWith(candidateMasks[bestIndices[i]]);
                    }
                }
            }
            finally
            {
                Pools.Release(tntAndRocketIndices);
            }

            // Phase 4: Greedily fill UFO layer - O(n)
            foreach (var idx in ufoIndices)
            {
                if (!usedMask.Overlaps(candidateMasks[idx]))
                {
                    bestIndices.Add(idx);
                    usedMask.UnionWith(candidateMasks[idx]);
                }
            }

            // Phase 5: Local search optimization
            LocalSearchOptimizer.Optimize(candidates, candidateMasks, bestIndices);
        }
        finally
        {
            Pools.Release(rainbowIndices);
            Pools.Release(tntIndices);
            Pools.Release(rocketIndices);
            Pools.Release(ufoIndices);
        }
    }

    /// <summary>
    /// Exact Branch &amp; Bound solver for a subset of candidates (typically high-weight only).
    /// </summary>
    private static void SolveBranchAndBoundSubset(
        List<DetectedShape> candidates,
        List<int> subsetIndices,
        BitMask256[] candidateMasks,
        List<int> bestIndices)
    {
        if (subsetIndices.Count == 0) return;

        var currentIndices = Pools.ObtainList<int>();
        var suffixSums = ArrayPool<int>.Shared.Rent(subsetIndices.Count + 1);

        try
        {
            // Compute suffix sums for pruning
            suffixSums[subsetIndices.Count] = 0;
            for (int i = subsetIndices.Count - 1; i >= 0; i--)
            {
                suffixSums[i] = suffixSums[i + 1] + candidates[subsetIndices[i]].Weight;
            }

            int bestScore = -1;
            SolveBranchAndBoundSubsetRecursive(
                candidates, subsetIndices, candidateMasks,
                0, currentIndices, new BitMask256(), 0,
                ref bestScore, bestIndices, suffixSums);
        }
        finally
        {
            Pools.Release(currentIndices);
            ArrayPool<int>.Shared.Return(suffixSums);
        }
    }

    private static void SolveBranchAndBoundSubsetRecursive(
        List<DetectedShape> candidates,
        List<int> subsetIndices,
        BitMask256[] candidateMasks,
        int subsetPos,
        List<int> currentIndices,
        BitMask256 usedMask,
        int currentScore,
        ref int bestScore,
        List<int> bestIndices,
        int[] suffixSums)
    {
        // Base case
        if (subsetPos >= subsetIndices.Count)
        {
            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestIndices.Clear();
                bestIndices.AddRange(currentIndices);
            }
            return;
        }

        // Pruning: if remaining max possible score can't beat best, skip
        if (currentScore + suffixSums[subsetPos] <= bestScore)
        {
            return;
        }

        int candidateIdx = subsetIndices[subsetPos];
        var candidateMask = candidateMasks[candidateIdx];

        // Try include
        if (!usedMask.Overlaps(candidateMask))
        {
            currentIndices.Add(candidateIdx);
            var nextMask = usedMask;
            nextMask.UnionWith(candidateMask);

            SolveBranchAndBoundSubsetRecursive(
                candidates, subsetIndices, candidateMasks,
                subsetPos + 1, currentIndices, nextMask,
                currentScore + candidates[candidateIdx].Weight,
                ref bestScore, bestIndices, suffixSums);

            currentIndices.RemoveAt(currentIndices.Count - 1);
        }

        // Try exclude
        SolveBranchAndBoundSubsetRecursive(
            candidates, subsetIndices, candidateMasks,
            subsetPos + 1, currentIndices, usedMask,
            currentScore, ref bestScore, bestIndices, suffixSums);
    }

    /// <summary>
    /// Greedy solver for a subset of candidates.
    /// </summary>
    private static void SolveGreedySubset(
        List<int> subsetIndices,
        BitMask256[] candidateMasks,
        ref BitMask256 usedMask,
        List<int> bestIndices)
    {
        // subsetIndices should already be sorted by weight DESC
        foreach (var idx in subsetIndices)
        {
            if (!usedMask.Overlaps(candidateMasks[idx]))
            {
                bestIndices.Add(idx);
                usedMask.UnionWith(candidateMasks[idx]);
            }
        }
    }
}
