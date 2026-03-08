using System.Collections.Generic;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation.Rules;

public class LineRule : IShapeRule
{
    public void Detect(HashSet<Position> component, ShapeFeature features, List<DetectedShape> results)
    {
        // Check Horizontal Lines - generate sliding window candidates
        foreach (var line in features.HLines)
        {
            DetectHorizontalLineCandidates(line, results);
        }

        // Check Vertical Lines - generate sliding window candidates
        foreach (var line in features.VLines)
        {
            DetectVerticalLineCandidates(line, results);
        }
    }

    /// <summary>
    /// Generate all valid Rainbow (5+) and Rocket (4) candidates from a horizontal line.
    /// Uses sliding window to generate overlapping candidates for optimal partitioning.
    /// </summary>
    private void DetectHorizontalLineCandidates(HashSet<Position> line, List<DetectedShape> results)
    {
        int len = line.Count;
        if (len < BombDefinitions.Rocket.MinLength) return; // < 4, no candidates

        // Find the leftmost position and y coordinate
        int minX = int.MaxValue;
        int y = 0;
        foreach (var p in line)
        {
            if (p.X < minX)
            {
                minX = p.X;
                y = p.Y;
            }
        }

        // Generate 5-length (Rainbow) candidates with sliding window
        if (len >= BombDefinitions.Rainbow.MinLength)
        {
            int windowCount = len - BombDefinitions.Rainbow.MinLength + 1; // len - 5 + 1
            for (int offset = 0; offset < windowCount; offset++)
            {
                var shape = Pools.Obtain<DetectedShape>();
                shape.Type = ElementType.ColorBomb;
                shape.Weight = BombDefinitions.Rainbow.Weight;
                shape.Shape = MatchShape.Line5;
                shape.Cells = Pools.ObtainHashSet<Position>();
                for (int i = 0; i < 5; i++)
                {
                    shape.Cells.Add(new Position(minX + offset + i, y));
                }
                results.Add(shape);
            }
        }

        // Generate 4-length (Rocket) candidates with sliding window
        // Only generate 4-length if they are NOT fully contained in a 5-length
        // This means: only the parts that extend beyond any 5-length window
        if (len >= BombDefinitions.Rocket.MinLength)
        {
            int windowCount = len - BombDefinitions.Rocket.MinLength + 1; // len - 4 + 1
            for (int offset = 0; offset < windowCount; offset++)
            {
                // Skip 4-length windows that are fully contained in any 5-length window
                // A 4-length at offset O is contained if there exists a 5-length at offset O' where O' <= O and O + 4 <= O' + 5
                // Simplified: if len >= 5, only generate 4-length at the edges
                bool isContainedIn5 = len >= 5 && offset > 0 && offset < windowCount - 1;
                if (isContainedIn5) continue;

                var shape = Pools.Obtain<DetectedShape>();
                shape.Type = ElementType.VerticalRocket; // Horizontal match -> Vertical rocket
                shape.Weight = BombDefinitions.Rocket.Weight;
                shape.Shape = MatchShape.Line4Horizontal;
                shape.Cells = Pools.ObtainHashSet<Position>();
                for (int i = 0; i < 4; i++)
                {
                    shape.Cells.Add(new Position(minX + offset + i, y));
                }
                results.Add(shape);
            }
        }
    }

    /// <summary>
    /// Generate all valid Rainbow (5+) and Rocket (4) candidates from a vertical line.
    /// Uses sliding window to generate overlapping candidates for optimal partitioning.
    /// </summary>
    private void DetectVerticalLineCandidates(HashSet<Position> line, List<DetectedShape> results)
    {
        int len = line.Count;
        if (len < BombDefinitions.Rocket.MinLength) return; // < 4, no candidates

        // Find the topmost position and x coordinate
        int minY = int.MaxValue;
        int x = 0;
        foreach (var p in line)
        {
            if (p.Y < minY)
            {
                minY = p.Y;
                x = p.X;
            }
        }

        // Generate 5-length (Rainbow) candidates with sliding window
        if (len >= BombDefinitions.Rainbow.MinLength)
        {
            int windowCount = len - BombDefinitions.Rainbow.MinLength + 1; // len - 5 + 1
            for (int offset = 0; offset < windowCount; offset++)
            {
                var shape = Pools.Obtain<DetectedShape>();
                shape.Type = ElementType.ColorBomb;
                shape.Weight = BombDefinitions.Rainbow.Weight;
                shape.Shape = MatchShape.Line5;
                shape.Cells = Pools.ObtainHashSet<Position>();
                for (int i = 0; i < 5; i++)
                {
                    shape.Cells.Add(new Position(x, minY + offset + i));
                }
                results.Add(shape);
            }
        }

        // Generate 4-length (Rocket) candidates with sliding window
        if (len >= BombDefinitions.Rocket.MinLength)
        {
            int windowCount = len - BombDefinitions.Rocket.MinLength + 1; // len - 4 + 1
            for (int offset = 0; offset < windowCount; offset++)
            {
                // Skip 4-length windows that are fully contained in any 5-length window
                bool isContainedIn5 = len >= 5 && offset > 0 && offset < windowCount - 1;
                if (isContainedIn5) continue;

                var shape = Pools.Obtain<DetectedShape>();
                shape.Type = ElementType.HorizontalRocket; // Vertical match -> Horizontal rocket
                shape.Weight = BombDefinitions.Rocket.Weight;
                shape.Shape = MatchShape.Line4Vertical;
                shape.Cells = Pools.ObtainHashSet<Position>();
                for (int i = 0; i < 4; i++)
                {
                    shape.Cells.Add(new Position(x, minY + offset + i));
                }
                results.Add(shape);
            }
        }
    }
}
