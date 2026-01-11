using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching.Generation;

public class ShapeDetector
{
    public void DetectAll(HashSet<Position> component, List<DetectedShape> candidates)
    {
        if (component == null || component.Count < 4) return;

        GetBounds(component, out int minX, out int maxX, out int minY, out int maxY);

        // Pools for intermediate line storage (used for TNT detection)
        var hLines3 = Pools.ObtainList<HashSet<Position>>();
        var vLines3 = Pools.ObtainList<HashSet<Position>>();

        try 
        {
            DetectHorizontalLines(component, candidates, minX, maxX, minY, maxY, hLines3);
            DetectVerticalLines(component, candidates, minX, maxX, minY, maxY, vLines3);
            DetectSquares(component, candidates, minX, maxX, minY, maxY);
            DetectIntersections(hLines3, vLines3, candidates);
        }
        finally
        {
            // Release pooled sets
            foreach(var s in hLines3) Pools.Release(s);
            foreach(var s in vLines3) Pools.Release(s);
            Pools.Release(hLines3);
            Pools.Release(vLines3);
        }
    }

    private void GetBounds(HashSet<Position> component, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = int.MaxValue; maxX = int.MinValue;
        minY = int.MaxValue; maxY = int.MinValue;

        foreach (var p in component)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }
    }

    private void DetectHorizontalLines(HashSet<Position> component, List<DetectedShape> candidates, int minX, int maxX, int minY, int maxY, List<HashSet<Position>> lines3)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!component.Contains(new Position(x, y))) continue;

                int len = 0;
                while (component.Contains(new Position(x + len, y))) len++;

                if (len >= 3)
                {
                    // Store for TNT
                    var lineCells = Pools.ObtainHashSet<Position>();
                    for (int k = 0; k < len; k++) lineCells.Add(new Position(x + k, y));
                    lines3.Add(lineCells);

                    // Rocket (4)
                    if (len >= 4)
                    {
                        CreateShape(candidates, BombType.Vertical, BombWeights.Rocket, MatchShape.Line4Horizontal, x, y, 4, true);
                    }

                    // Rainbow (5)
                    if (len >= 5)
                    {
                        CreateShape(candidates, BombType.Color, BombWeights.Rainbow, MatchShape.Line5, x, y, 5, true);
                    }
                }
            }
        }
    }

    private void DetectVerticalLines(HashSet<Position> component, List<DetectedShape> candidates, int minX, int maxX, int minY, int maxY, List<HashSet<Position>> lines3)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!component.Contains(new Position(x, y))) continue;

                int len = 0;
                while (component.Contains(new Position(x, y + len))) len++;

                if (len >= 3)
                {
                    // Store for TNT
                    var lineCells = Pools.ObtainHashSet<Position>();
                    for (int k = 0; k < len; k++) lineCells.Add(new Position(x, y + k));
                    lines3.Add(lineCells);

                    // Rocket (4) - Vertical match clears Horizontal row
                    if (len >= 4)
                    {
                        CreateShape(candidates, BombType.Horizontal, BombWeights.Rocket, MatchShape.Line4Vertical, x, y, 4, false);
                    }

                    // Rainbow (5)
                    if (len >= 5)
                    {
                        CreateShape(candidates, BombType.Color, BombWeights.Rainbow, MatchShape.Line5, x, y, 5, false);
                    }
                }
            }
        }
    }

    private void DetectSquares(HashSet<Position> component, List<DetectedShape> candidates, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                var p00 = new Position(x, y);
                var p10 = new Position(x + 1, y);
                var p01 = new Position(x, y + 1);
                var p11 = new Position(x + 1, y + 1);

                if (component.Contains(p00) && component.Contains(p10) && 
                    component.Contains(p01) && component.Contains(p11))
                {
                    var shape = Pools.Obtain<DetectedShape>();
                    shape.Type = BombType.Ufo;
                    shape.Weight = BombWeights.UFO;
                    shape.Shape = MatchShape.Square;
                    shape.Cells = Pools.ObtainHashSet<Position>();
                    shape.Cells.Add(p00); shape.Cells.Add(p10);
                    shape.Cells.Add(p01); shape.Cells.Add(p11);
                    candidates.Add(shape);
                }
            }
        }
    }

    private void DetectIntersections(List<HashSet<Position>> hLines, List<HashSet<Position>> vLines, List<DetectedShape> candidates)
    {
        foreach (var hLine in hLines)
        {
            foreach (var vLine in vLines)
            {
                // Check for intersection
                bool intersects = false;
                foreach (var p in hLine)
                {
                    if (vLine.Contains(p))
                    {
                        intersects = true;
                        break;
                    }
                }

                if (intersects)
                {
                    // L/T shape requires total count >= 5
                    var unionCount = hLine.Count + vLine.Count - 1;
                    if (unionCount >= 5)
                    {
                        var shape = Pools.Obtain<DetectedShape>();
                        shape.Type = BombType.Square3x3; // TNT
                        shape.Weight = BombWeights.TNT;
                        shape.Shape = MatchShape.Cross;
                        shape.Cells = Pools.ObtainHashSet<Position>();
                        
                        foreach(var p in hLine) shape.Cells.Add(p);
                        foreach(var p in vLine) shape.Cells.Add(p);
                        
                        candidates.Add(shape);
                    }
                }
            }
        }
    }

    private void CreateShape(List<DetectedShape> candidates, BombType type, int weight, MatchShape matchShape, int startX, int startY, int length, bool isHorizontal)
    {
        var shape = Pools.Obtain<DetectedShape>();
        shape.Type = type;
        shape.Weight = weight;
        shape.Shape = matchShape;
        shape.Cells = Pools.ObtainHashSet<Position>();
        
        for (int k = 0; k < length; k++)
        {
            shape.Cells.Add(isHorizontal 
                ? new Position(startX + k, startY) 
                : new Position(startX, startY + k));
        }
        candidates.Add(shape);
    }
}
