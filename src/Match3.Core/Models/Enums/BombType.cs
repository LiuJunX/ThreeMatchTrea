namespace Match3.Core.Models.Enums
{
    public enum BombType
    {
        None = 0,
        
        /// <summary>
        /// Explodes in a circular area (e.g., radius 1 = 3x3).
        /// </summary>
        Area = 1,
        
        /// <summary>
        /// Clears the entire row.
        /// </summary>
        Horizontal = 2,
        
        /// <summary>
        /// Clears the entire column.
        /// </summary>
        Vertical = 3,
        
        /// <summary>
        /// Clears all tiles of a specific color.
        /// </summary>
        Color = 4,

        /// <summary>
        /// Homing missile that targets a specific tile.
        /// </summary>
        Ufo = 5,

        /// <summary>
        /// Legacy: Explodes a 3x3 square area. Same as Area.
        /// </summary>
        Square3x3 = 6
    }

    public static class BombTypeExtensions
    {
        public static bool IsRocket(this BombType type)
        {
            return type == BombType.Horizontal || type == BombType.Vertical;
        }

        public static bool IsAreaBomb(this BombType type)
        {
            return type == BombType.Area || type == BombType.Square3x3;
        }

        public static bool IsUfo(this BombType type)
        {
            return type == BombType.Ufo;
        }

        public static bool IsRainbow(this BombType type)
        {
            return type == BombType.Color;
        }
    }
}
