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

public class SquareRule : IShapeRule
{
    public void Detect(HashSet<Position> component, ShapeFeature features, List<DetectedShape> results)
    {
        // 2x2 Square Detection with redundancy reduction
        // Strategy: Skip 2x2 squares that are fully contained in a 4+ horizontal or vertical line
        // because those lines will always be preferred (higher weight: Rocket=40 vs UFO=20)

        for (int x = features.MinX; x < features.MaxX; x++)
        {
            for (int y = features.MinY; y < features.MaxY; y++)
            {
                var p00 = new Position(x, y);
                var p10 = new Position(x + 1, y);
                var p01 = new Position(x, y + 1);
                var p11 = new Position(x + 1, y + 1);

                if (!component.Contains(p00) || !component.Contains(p10) ||
                    !component.Contains(p01) || !component.Contains(p11))
                {
                    continue;
                }

                // Check if this 2x2 is fully contained in a horizontal line of 4+
                // A 2x2 has 2 rows. If both rows are part of 4+ lines, skip it.
                bool topRowInLine4 = IsPartOfHorizontalLine4(component, x, y, features);
                bool bottomRowInLine4 = IsPartOfHorizontalLine4(component, x, y + 1, features);
                if (topRowInLine4 && bottomRowInLine4)
                {
                    continue; // Will be covered by Rocket candidates
                }

                // Check if fully contained in a vertical line of 4+
                bool leftColInLine4 = IsPartOfVerticalLine4(component, x, y, features);
                bool rightColInLine4 = IsPartOfVerticalLine4(component, x + 1, y, features);
                if (leftColInLine4 && rightColInLine4)
                {
                    continue; // Will be covered by Rocket candidates
                }

                var shape = Pools.Obtain<DetectedShape>();
                shape.Type = ElementType.Ufo;
                shape.Weight = BombDefinitions.UFO.Weight;
                shape.Shape = MatchShape.Square;
                shape.Cells = Pools.ObtainHashSet<Position>();
                shape.Cells.Add(p00); shape.Cells.Add(p10);
                shape.Cells.Add(p01); shape.Cells.Add(p11);
                results.Add(shape);
            }
        }
    }

    /// <summary>
    /// Check if position (x, y) and (x+1, y) are part of a horizontal line of 4+ cells
    /// </summary>
    private bool IsPartOfHorizontalLine4(HashSet<Position> component, int x, int y, ShapeFeature features)
    {
        // Count continuous cells to the left and right
        int left = 0;
        for (int dx = -1; x + dx >= features.MinX; dx--)
        {
            if (component.Contains(new Position(x + dx, y)))
                left++;
            else
                break;
        }

        int right = 0;
        for (int dx = 2; x + dx <= features.MaxX; dx++) // Start from x+2 since we already have x, x+1
        {
            if (component.Contains(new Position(x + dx, y)))
                right++;
            else
                break;
        }

        // Total length including the 2 cells of the 2x2 row
        return (left + 2 + right) >= 4;
    }

    /// <summary>
    /// Check if position (x, y) and (x, y+1) are part of a vertical line of 4+ cells
    /// </summary>
    private bool IsPartOfVerticalLine4(HashSet<Position> component, int x, int y, ShapeFeature features)
    {
        // Count continuous cells above and below
        int above = 0;
        for (int dy = -1; y + dy >= features.MinY; dy--)
        {
            if (component.Contains(new Position(x, y + dy)))
                above++;
            else
                break;
        }

        int below = 0;
        for (int dy = 2; y + dy <= features.MaxY; dy++) // Start from y+2 since we already have y, y+1
        {
            if (component.Contains(new Position(x, y + dy)))
                below++;
            else
                break;
        }

        // Total length including the 2 cells of the 2x2 column
        return (above + 2 + below) >= 4;
    }
}
