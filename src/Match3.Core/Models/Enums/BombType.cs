using Match3.Core.Attributes;

namespace Match3.Core.Models.Enums
{
    public enum BombType
    {
        None = 0,

        /// <summary>
        /// Clears the entire row.
        /// </summary>
        [AIMapping(0, "Horizontal")]
        Horizontal = 1,

        /// <summary>
        /// Clears the entire column.
        /// </summary>
        [AIMapping(1, "Vertical")]
        Vertical = 2,

        /// <summary>
        /// Clears all tiles of a specific color.
        /// </summary>
        [AIMapping(2, "Color")]
        Color = 3,

        /// <summary>
        /// Homing missile that targets a specific tile.
        /// </summary>
        [AIMapping(3, "Ufo")]
        Ufo = 4,

        /// <summary>
        /// Explodes a 5x5 square area (Radius 2).
        /// </summary>
        [AIMapping(4, "Square5x5")]
        Square5x5 = 5
    }

    public static class BombTypeExtensions
    {
        public static bool IsRocket(this BombType type)
        {
            return type == BombType.Horizontal || type == BombType.Vertical;
        }

        public static bool IsAreaBomb(this BombType type)
        {
            return type == BombType.Square5x5;
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
