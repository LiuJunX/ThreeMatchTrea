using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Systems.Matching.Generation;

public static class BombWeights
{
    public const int Rainbow = 130; // Updated from 100/150 to 130
    public const int TNT = 60;
    public const int Rocket = 40;
    public const int UFO = 20;
}

public class DetectedShape
{
    public BombType Type { get; set; }
    public HashSet<Position> Cells { get; set; } // Managed by Pool
    public int Weight { get; set; }
    public MatchShape Shape { get; set; } 
    
    // For debugging/logging
    public string DebugName => $"{Type} ({Cells?.Count ?? 0})";

    public void Clear()
    {
        // Cells are released externally
        Cells = null;
        Type = BombType.None;
        Weight = 0;
        Shape = MatchShape.Simple3;
    }
}
