using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Models.Gameplay;

public class DetectedShape
{
    public ElementType Type { get; set; }
    public HashSet<Position>? Cells { get; set; } // Managed by Pool
    public int Weight { get; set; }
    public MatchShape Shape { get; set; }

    /// <summary>
    /// For Cross shapes (T/L/+), this is the intersection point where horizontal and vertical lines meet.
    /// Used for absorption rules: scraps must be collinear with this point to be absorbed.
    /// </summary>
    public Position? Intersection { get; set; }

    // For debugging/logging
    public string DebugName => $"{Type} ({Cells?.Count ?? 0})";

    public void Clear()
    {
        // Cells are released externally
        Cells = null;
        Type = ElementType.None;
        Weight = 0;
        Shape = MatchShape.Simple3;
        Intersection = null;
    }
}
