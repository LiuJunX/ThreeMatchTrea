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

public class IntersectionRule : IShapeRule
{
    public void Detect(HashSet<Position> component, ShapeFeature features, List<DetectedShape> results)
    {
        foreach (var hLine in features.HLines)
        {
            foreach (var vLine in features.VLines)
            {
                // Check for intersection and find intersection point
                Position? intersectionPoint = null;
                foreach (var p in hLine)
                {
                    if (vLine.Contains(p))
                    {
                        intersectionPoint = p;
                        break;
                    }
                }

                if (intersectionPoint.HasValue)
                {
                    // L/T/Plus shape requires total count >= 5 (e.g. 3+3-1 = 5)
                    var unionCount = hLine.Count + vLine.Count - 1;
                    if (unionCount >= BombDefinitions.TNT.MinLength)
                    {
                        var shape = Pools.Obtain<DetectedShape>();
                        shape.Intersection = intersectionPoint; // Store intersection for absorption rules
                        shape.Shape = MatchShape.Cross;
                        shape.Cells = Pools.ObtainHashSet<Position>();

                        foreach (var p in hLine) shape.Cells.Add(p);
                        foreach (var p in vLine) shape.Cells.Add(p);

                        // Determine if this is a Plus (+) or T/L shape
                        // Plus: both lines have exactly 3 cells AND intersection is at center of both
                        if (IsPlusShape(hLine, vLine, intersectionPoint.Value))
                        {
                            // Plus generates UFO
                            shape.Type = ElementType.Ufo;
                            shape.Weight = BombDefinitions.UFO.Weight;
                        }
                        else
                        {
                            // T/L generates TNT
                            shape.Type = ElementType.Square5x5;
                            shape.Weight = BombDefinitions.TNT.Weight;
                        }

                        results.Add(shape);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if the intersection forms a Plus (+) shape.
    /// Plus: both lines have exactly 3 cells AND intersection is at center of both lines.
    /// </summary>
    private static bool IsPlusShape(HashSet<Position> hLine, HashSet<Position> vLine, Position intersection)
    {
        // Both lines must have exactly 3 cells
        if (hLine.Count != 3 || vLine.Count != 3)
            return false;

        // Check if intersection is at center of horizontal line
        int hMinX = int.MaxValue, hMaxX = int.MinValue;
        foreach (var p in hLine)
        {
            if (p.X < hMinX) hMinX = p.X;
            if (p.X > hMaxX) hMaxX = p.X;
        }
        int hCenterX = (hMinX + hMaxX) / 2;
        if (intersection.X != hCenterX)
            return false;

        // Check if intersection is at center of vertical line
        int vMinY = int.MaxValue, vMaxY = int.MinValue;
        foreach (var p in vLine)
        {
            if (p.Y < vMinY) vMinY = p.Y;
            if (p.Y > vMaxY) vMaxY = p.Y;
        }
        int vCenterY = (vMinY + vMaxY) / 2;
        if (intersection.Y != vCenterY)
            return false;

        return true;
    }
}
