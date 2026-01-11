using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation;

public class BombGenerator : IBombGenerator
{
    private readonly ShapeDetector _detector = new();

    public List<MatchGroup> Generate(HashSet<Position> component, IEnumerable<Position>? foci = null)
    {
        // 0. Trivial Case
        if (component.Count < 3)
        {
            return new List<MatchGroup>();
        }

        // 1. Detect All Candidates
        var candidates = Pools.ObtainList<DetectedShape>();
        
        try
        {
            _detector.DetectAll(component, candidates);
            
            // Handle Simple Match (No bomb candidates)
            if (candidates.Count == 0)
            {
                return CreateSimpleMatchGroup(component);
            }

            // 2. Sort Candidates (Weight DESC, then Affinity)
            SortCandidates(candidates, foci);

            // 3. Solve Optimal Partition
            var bestIndices = Pools.ObtainList<int>();
            try 
            {
                FindOptimalPartition(candidates, component, bestIndices);

                // 4. Scrap Absorption & Result Construction
                AbsorbScraps(component, candidates, bestIndices);
                
                // 5. Finalize Results
                return ConstructResults(candidates, bestIndices, component, foci);
            }
            finally
            {
                Pools.Release(bestIndices);
            }
        }
        finally
        {
            // Release detected shapes and their inner sets
            foreach(var c in candidates)
            {
                if (c.Cells != null) Pools.Release(c.Cells);
                c.Cells = null;
                Pools.Release(c);
            }
            Pools.Release(candidates);
        }
    }

    private List<MatchGroup> CreateSimpleMatchGroup(HashSet<Position> component)
    {
        var simpleGroup = Pools.Obtain<MatchGroup>();
        simpleGroup.Positions.Clear();
        foreach(var p in component) simpleGroup.Positions.Add(p);
        simpleGroup.Shape = MatchShape.Simple3;
        simpleGroup.SpawnBombType = BombType.None;
        simpleGroup.Type = TileType.None; // Set by caller
        simpleGroup.BombOrigin = null;
        
        return new List<MatchGroup> { simpleGroup };
    }

    private void SortCandidates(List<DetectedShape> candidates, IEnumerable<Position>? foci)
    {
        var fociSet = Pools.ObtainHashSet<Position>();
        if (foci != null) foreach(var f in foci) fociSet.Add(f);

        try
        {
            candidates.Sort((a, b) =>
            {
                // Primary: Weight
                int weightDiff = b.Weight.CompareTo(a.Weight);
                if (weightDiff != 0) return weightDiff;
                
                // Secondary: Affinity (Does it touch foci?)
                bool aTouches = a.Cells!.Overlaps(fociSet);
                bool bTouches = b.Cells!.Overlaps(fociSet);
                
                if (aTouches && !bTouches) return -1;
                if (!aTouches && bTouches) return 1;
                
                // Tertiary: Size (larger shapes preferred for same weight)
                return b.Cells!.Count.CompareTo(a.Cells!.Count);
            });
        }
        finally
        {
            Pools.Release(fociSet);
        }
    }

    private void FindOptimalPartition(List<DetectedShape> candidates, HashSet<Position> component, List<int> bestIndices)
    {
        var currentIndices = Pools.ObtainList<int>();
        var positionToIndex = Pools.Obtain<Dictionary<Position, int>>();
        
        // Optimization: Branch & Bound with BitMasks
        // Precompute masks and suffix sums
        var candidateMasks = ArrayPool<BitMask256>.Shared.Rent(candidates.Count);
        var suffixSums = ArrayPool<int>.Shared.Rent(candidates.Count + 1);

        try
        {
            // Map component positions to indices for BitMask
            int posIndex = 0;
            foreach (var p in component) 
            {
                positionToIndex[p] = posIndex++;
            }

            suffixSums[candidates.Count] = 0;
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                suffixSums[i] = suffixSums[i + 1] + candidates[i].Weight;
                
                var mask = new BitMask256();
                foreach (var p in candidates[i].Cells!)
                {
                    // Map Position to Index in component
                    if (positionToIndex.TryGetValue(p, out int index) && index < 256)
                    {
                        mask.Set(index);
                    }
                }
                candidateMasks[i] = mask;
            }

            int bestScore = -1;
            SolveBranchAndBound(candidates, 0, currentIndices, new BitMask256(), 0, ref bestScore, bestIndices, suffixSums, candidateMasks);
        }
        finally
        {
            Pools.Release(currentIndices);
            positionToIndex.Clear();
            Pools.Release(positionToIndex);
            ArrayPool<BitMask256>.Shared.Return(candidateMasks);
            ArrayPool<int>.Shared.Return(suffixSums);
        }
    }

    private void SolveBranchAndBound(
        List<DetectedShape> candidates,
        int index,
        List<int> currentIndices,
        BitMask256 usedMask,
        int currentScore,
        ref int bestScore,
        List<int> bestIndices,
        int[] suffixSums,
        BitMask256[] candidateMasks)
    {
         // 1. Base Case / Pruning
         if (index >= candidates.Count)
         {
             if (currentScore > bestScore)
             {
                 bestScore = currentScore;
                 bestIndices.Clear();
                 bestIndices.AddRange(currentIndices);
             }
             return;
         }

         if (currentScore + suffixSums[index] <= bestScore)
         {
             return;
         }

         // 2. Try Include
         var candidateMask = candidateMasks[index];
         if (!usedMask.Overlaps(candidateMask))
         {
             currentIndices.Add(index);
             
             // Create new mask for next level (copy + union)
             var nextMask = usedMask; 
             nextMask.UnionWith(candidateMask);
             
             SolveBranchAndBound(candidates, index + 1, currentIndices, nextMask, currentScore + candidates[index].Weight, ref bestScore, bestIndices, suffixSums, candidateMasks);
             
             currentIndices.RemoveAt(currentIndices.Count - 1);
         }

         // 3. Try Exclude
         SolveBranchAndBound(candidates, index + 1, currentIndices, usedMask, currentScore, ref bestScore, bestIndices, suffixSums, candidateMasks);
    }

    private void AbsorbScraps(HashSet<Position> component, List<DetectedShape> candidates, List<int> bestIndices)
    {
        var allUsed = Pools.ObtainHashSet<Position>();
        var scraps = Pools.ObtainList<Position>();
        var unassignedScraps = Pools.ObtainHashSet<Position>();
        var toRemove = Pools.ObtainList<Position>();
        var ownerMap = Pools.Obtain<Dictionary<Position, DetectedShape>>();
        var solutionShapes = Pools.ObtainList<DetectedShape>();

        try
        {
            foreach(var idx in bestIndices) solutionShapes.Add(candidates[idx]);

            // Mark used cells
            foreach(var shape in solutionShapes) 
            {
                foreach(var p in shape.Cells!) allUsed.Add(p);
            }
            
            // Identify scraps
            foreach(var p in component)
            {
                if (!allUsed.Contains(p)) scraps.Add(p);
            }

            // Assign scraps (BFS / Flood Fill)
            if (scraps.Count > 0 && solutionShapes.Count > 0)
            {
                foreach(var s in scraps) unassignedScraps.Add(s);
                
                // Map each position to its owner shape
                foreach(var shape in solutionShapes)
                {
                    foreach(var p in shape.Cells!) ownerMap[p] = shape;
                }

                bool changed = true;
                while (changed && unassignedScraps.Count > 0)
                {
                    changed = false;
                    toRemove.Clear();

                    foreach(var scrap in unassignedScraps)
                    {
                        DetectedShape? bestOwner = null;
                        
                        // Check 4 neighbors
                        var n1 = new Position(scrap.X-1, scrap.Y);
                        var n2 = new Position(scrap.X+1, scrap.Y);
                        var n3 = new Position(scrap.X, scrap.Y-1);
                        var n4 = new Position(scrap.X, scrap.Y+1);

                        if (ownerMap.TryGetValue(n1, out var o1)) bestOwner = GetBestOwner(bestOwner, o1);
                        if (ownerMap.TryGetValue(n2, out var o2)) bestOwner = GetBestOwner(bestOwner, o2);
                        if (ownerMap.TryGetValue(n3, out var o3)) bestOwner = GetBestOwner(bestOwner, o3);
                        if (ownerMap.TryGetValue(n4, out var o4)) bestOwner = GetBestOwner(bestOwner, o4);

                        if (bestOwner != null)
                        {
                            ownerMap[scrap] = bestOwner;
                            bestOwner.Cells!.Add(scrap); // Add directly to shape
                            toRemove.Add(scrap);
                            changed = true;
                        }
                    }
                    
                    foreach(var p in toRemove) unassignedScraps.Remove(p);
                }
            }
        }
        finally
        {
            Pools.Release(allUsed);
            Pools.Release(scraps);
            Pools.Release(unassignedScraps);
            Pools.Release(toRemove);
            ownerMap.Clear();
            Pools.Release(ownerMap);
            Pools.Release(solutionShapes);
        }
    }

    private DetectedShape? GetBestOwner(DetectedShape? currentBest, DetectedShape candidate)
    {
        if (currentBest == null) return candidate;
        return candidate.Weight > currentBest.Weight ? candidate : currentBest;
    }

    private List<MatchGroup> ConstructResults(
        List<DetectedShape> candidates, 
        List<int> bestIndices, 
        HashSet<Position> component,
        IEnumerable<Position>? foci)
    {
        var results = new List<MatchGroup>();
        var finalUsed = Pools.ObtainHashSet<Position>();
        var orphans = Pools.ObtainList<Position>();

        try
        {
            foreach (var idx in bestIndices)
            {
                var shape = candidates[idx];
                var group = Pools.Obtain<MatchGroup>();
                group.Positions.Clear();
                foreach(var p in shape.Cells!) group.Positions.Add(p);
                
                group.Shape = shape.Shape;
                group.SpawnBombType = shape.Type;
                group.Type = TileType.None; // Set by caller
                
                // Determine Bomb Origin
                Position? origin = null;
                // Priority 1: Foci
                if (foci != null)
                {
                    foreach (var f in foci)
                    {
                        if (shape.Cells!.Contains(f))
                        {
                            origin = f;
                            break;
                        }
                    }
                }
                // Priority 2: Center/Random
                if (origin == null && shape.Cells!.Count > 0)
                {
                    origin = shape.Cells!.First();
                }
                group.BombOrigin = origin;

                results.Add(group);
            }
            
            // Handle Orphans (Islands not connected to any solution shape)
            foreach (var r in results) 
                foreach(var p in r.Positions) finalUsed.Add(p);
            
            foreach(var p in component)
            {
                if (!finalUsed.Contains(p)) orphans.Add(p);
            }
            
            if (orphans.Count > 0)
            {
                 // Create a simple match group for orphans
                 var orphanGroup = Pools.Obtain<MatchGroup>();
                 orphanGroup.Positions.Clear();
                 foreach(var p in orphans) orphanGroup.Positions.Add(p);
                 orphanGroup.Shape = MatchShape.Simple3;
                 orphanGroup.SpawnBombType = BombType.None;
                 orphanGroup.Type = TileType.None;
                 orphanGroup.BombOrigin = null;
                 results.Add(orphanGroup);
            }
            
            return results;
        }
        finally
        {
            Pools.Release(finalUsed);
            Pools.Release(orphans);
        }
    }

    private struct BitMask256
    {
        private ulong _p0;
        private ulong _p1;
        private ulong _p2;
        private ulong _p3;

        public void Set(int index)
        {
            if (index < 64) _p0 |= 1UL << index;
            else if (index < 128) _p1 |= 1UL << (index - 64);
            else if (index < 192) _p2 |= 1UL << (index - 128);
            else if (index < 256) _p3 |= 1UL << (index - 192);
        }

        public bool Overlaps(in BitMask256 other)
        {
            return (_p0 & other._p0) != 0 ||
                   (_p1 & other._p1) != 0 ||
                   (_p2 & other._p2) != 0 ||
                   (_p3 & other._p3) != 0;
        }

        public void UnionWith(in BitMask256 other)
        {
            _p0 |= other._p0;
            _p1 |= other._p1;
            _p2 |= other._p2;
            _p3 |= other._p3;
        }
    }
}
