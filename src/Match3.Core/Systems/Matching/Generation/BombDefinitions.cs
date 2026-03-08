using Match3.Core.Models.Enums;

namespace Match3.Core.Systems.Matching.Generation
{
    /// <summary>
    /// Defines configuration and weights for bomb generation.
    /// </summary>
    public static class BombDefinitions
    {
        public struct BombConfig
        {
            public int Weight;
            public int MinLength; // Minimum length/count to trigger
        }

        // Color Bomb (Rainbow) - 5 in a row
        public static readonly BombConfig Rainbow = new() { Weight = 130, MinLength = 5 };

        // TNT (Area/Square5x5) - L/T Shape (Intersection >= 5 tiles)
        public static readonly BombConfig TNT = new() { Weight = 60, MinLength = 5 };

        // Rocket (Line) - 4 in a row
        public static readonly BombConfig Rocket = new() { Weight = 40, MinLength = 4 };

        // UFO (Square) - 2x2 Square
        public static readonly BombConfig UFO = new() { Weight = 150, MinLength = 4 }; // 4 tiles total
    }
}
