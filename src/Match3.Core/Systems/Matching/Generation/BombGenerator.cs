using System;
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
        
        // Pools for internal logic
        var fociSet = Pools.ObtainHashSet<Position>();
        var bestIndices = Pools.ObtainList<int>();
        var currentIndices = Pools.ObtainList<int>();
        var usedCells = Pools.ObtainHashSet<Position>();
        var allUsed = Pools.ObtainHashSet<Position>();
        var scraps = Pools.ObtainList<Position>();
        var unassignedScraps = Pools.ObtainHashSet<Position>();
        var toRemove = Pools.ObtainList<Position>();
        var finalUsed = Pools.ObtainHashSet<Position>();
        var orphans = Pools.ObtainList<Position>();
        var ownerMap = Pools.Obtain<Dictionary<Position, DetectedShape>>();

        try
        {
            _detector.DetectAll(component, candidates);
            
            // Handle Simple Match (No bomb candidates)
            if (candidates.Count == 0)
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

            // 2. Sort Candidates (Weight DESC, then Affinity)
            if (foci != null) foreach(var f in foci) fociSet.Add(f);
            
            candidates.Sort((a, b) =>
            {
                // Primary: Weight
                int weightDiff = b.Weight.CompareTo(a.Weight);
                if (weightDiff != 0) return weightDiff;
                
                // Secondary: Affinity (Does it touch foci?)
                bool aTouches = a.Cells.Overlaps(fociSet);
                bool bTouches = b.Cells.Overlaps(fociSet);
                
                if (aTouches && !bTouches) return -1;
                if (!aTouches && bTouches) return 1;
                
                // Tertiary: Size (larger shapes preferred for same weight)
                return b.Cells.Count.CompareTo(a.Cells.Count);
            });

            // 3. Solve Optimal Partition (Backtracking with Pools)
            int bestScore = -1;
            
            Solve(candidates, 0, currentIndices, usedCells, 0, ref bestScore, bestIndices);
            
            // 4. Scrap Absorption & Result Construction
            var results = new List<MatchGroup>();
            
            // Build the best solution shapes
            var solutionShapes = Pools.ObtainList<DetectedShape>();
            foreach(var idx in bestIndices) solutionShapes.Add(candidates[idx]);
            
            // Mark used cells
            foreach(var shape in solutionShapes) 
            {
                foreach(var p in shape.Cells) allUsed.Add(p);
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
                    foreach(var p in shape.Cells) ownerMap[p] = shape;
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
                            bestOwner.Cells.Add(scrap); // Add directly to shape
                            toRemove.Add(scrap);
                            changed = true;
                        }
                    }
                    
                    foreach(var p in toRemove) unassignedScraps.Remove(p);
                }
            }
            
            Pools.Release(solutionShapes); // Just release the list shell, content is from candidates

            // 5. Finalize Results
            foreach (var idx in bestIndices)
            {
                var shape = candidates[idx];
                var group = Pools.Obtain<MatchGroup>();
                group.Positions.Clear();
                foreach(var p in shape.Cells) group.Positions.Add(p);
                
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
                        if (shape.Cells.Contains(f))
                        {
                            origin = f;
                            break;
                        }
                    }
                }
                // Priority 2: Center/Random
                if (origin == null && shape.Cells.Count > 0)
                {
                    origin = shape.Cells.First();
                }
                group.BombOrigin = origin;

                results.Add(group);
            }
            
            // 6. Handle Orphans (Islands not connected to any solution shape)
            // Recalculate what was used
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
            // Release detected shapes and their inner sets
            foreach(var c in candidates)
            {
                Pools.Release(c.Cells);
                c.Cells = null;
                Pools.Release(c);
            }
            Pools.Release(candidates);

            // Release other pooled objects
            Pools.Release(fociSet);
            Pools.Release(bestIndices);
            Pools.Release(currentIndices);
            Pools.Release(usedCells);
            Pools.Release(allUsed);
            Pools.Release(scraps);
            Pools.Release(unassignedScraps);
            Pools.Release(toRemove);
            Pools.Release(finalUsed);
            Pools.Release(orphans);
            
            ownerMap.Clear(); // Must clear manually
            Pools.Release(ownerMap);
        }
    }

    private DetectedShape? GetBestOwner(DetectedShape? currentBest, DetectedShape candidate)
    {
        if (currentBest == null) return candidate;
        return candidate.Weight > currentBest.Weight ? candidate : currentBest;
    }

    private void Solve(
        List<DetectedShape> candidates, 
        int index, 
        List<int> currentIndices, 
        HashSet<Position> usedCells, 
        int currentScore,
        ref int bestScore,
        List<int> bestIndices)
    {
        // Optimization: Pruning?
        // If remaining candidates can't possibly beat bestScore, return.
        // But calculating "potential max" is costly.

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

        var candidate = candidates[index];

        // Option A: Include Candidate
        // Check overlap
        bool overlaps = false;
        foreach (var p in candidate.Cells)
        {
            if (usedCells.Contains(p))
            {
                overlaps = true;
                break;
            }
        }

        if (!overlaps)
        {
            currentIndices.Add(index);
            foreach (var p in candidate.Cells) usedCells.Add(p);
            
            Solve(candidates, index + 1, currentIndices, usedCells, currentScore + candidate.Weight, ref bestScore, bestIndices);
            
            // Backtrack
            foreach (var p in candidate.Cells) usedCells.Remove(p);
            currentIndices.RemoveAt(currentIndices.Count - 1);
        }

        // Option B: Exclude Candidate
        // Optimization: If candidate is crucial? 
        // Just standard subset sum variation.
        Solve(candidates, index + 1, currentIndices, usedCells, currentScore, ref bestScore, bestIndices);
    }
}
