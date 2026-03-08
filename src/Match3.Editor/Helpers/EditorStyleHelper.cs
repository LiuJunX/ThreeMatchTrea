using System;
using Match3.Core.Models.Enums;

namespace Match3.Editor.Helpers
{
    public static class EditorStyleHelper
    {
        // Colors
        public const string COLOR_RED = "#dc3545";
        public const string COLOR_GREEN = "#198754";
        public const string COLOR_BLUE = "#0d6efd";
        public const string COLOR_YELLOW = "#ffc107";
        public const string COLOR_PURPLE = "#6f42c1";
        public const string COLOR_ORANGE = "#fd7e14";
        public const string COLOR_RAINBOW = "linear-gradient(45deg, red, orange, yellow, green, blue, indigo, violet)";
        public const string COLOR_NONE = "#f8f9fa";
        public const string COLOR_BOMB_BG = "#dee2e6";
        public const string COLOR_DEFAULT = "#ccc";
        public const string COLOR_TRANSPARENT = "transparent";

        // Ground Colors
        public const string COLOR_ICE = "#b0e0e6"; // PowderBlue

        // Icons
        public const string ICON_BOMB_H = "↔️";
        public const string ICON_BOMB_V = "↕️";
        public const string ICON_BOMB_UFO = "🛸";
        public const string ICON_BOMB_SQUARE = "💣";
        public const string ICON_BOMB_COLOR = "🌈";

        public const string ICON_COVER_CAGE = "🔒";
        public const string ICON_COVER_CHAIN = "⛓️";
        public const string ICON_COVER_BUBBLE = "🫧";

        public static string GetTileColor(ElementType t) => t switch
        {
            ElementType.Item1 => COLOR_RED,
            ElementType.Item2 => COLOR_GREEN,
            ElementType.Item3 => COLOR_BLUE,
            ElementType.Item4 => COLOR_YELLOW,
            ElementType.Item5 => COLOR_PURPLE,
            ElementType.Item6 => COLOR_ORANGE,
            ElementType.ColorBomb => COLOR_RAINBOW,
            ElementType.None => COLOR_NONE,
            _ => COLOR_DEFAULT
        };

        public static string GetTileColorForCanvas(ElementType t) => t switch
        {
            ElementType.Item1 => COLOR_RED,
            ElementType.Item2 => COLOR_GREEN,
            ElementType.Item3 => COLOR_BLUE,
            ElementType.Item4 => COLOR_YELLOW,
            ElementType.Item5 => COLOR_PURPLE,
            ElementType.Item6 => COLOR_ORANGE,
            ElementType.ColorBomb => COLOR_RAINBOW,
            ElementType.None => COLOR_TRANSPARENT,
            _ => COLOR_DEFAULT
        };

        public static string GetGroundColor(GroundType g) => g switch
        {
            GroundType.Ice => COLOR_ICE,
            _ => COLOR_TRANSPARENT
        };

        public static string GetBombIcon(ElementType bomb) => bomb switch
        {
            ElementType.None => "",
            ElementType.HorizontalRocket => ICON_BOMB_H,
            ElementType.VerticalRocket => ICON_BOMB_V,
            ElementType.Ufo => ICON_BOMB_UFO,
            ElementType.Square5x5 => ICON_BOMB_SQUARE,
            ElementType.ColorBomb => ICON_BOMB_COLOR,
            _ => ""
        };

        public static string GetCoverIcon(CoverType c) => c switch
        {
            CoverType.Cage => ICON_COVER_CAGE,
            CoverType.Chain => ICON_COVER_CHAIN,
            CoverType.Bubble => ICON_COVER_BUBBLE,
            _ => ""
        };

        public static string GetTileCheckmarkClass(ElementType t) => t switch
        {
            ElementType.None => "text-dark",
            ElementType.Item4 => "text-dark", // Yellow
            _ => "text-white"
        };
    }
}
