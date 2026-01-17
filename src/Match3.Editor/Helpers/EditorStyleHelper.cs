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
        public const string ICON_COVER_ICE = "🧊";

        public static string GetTileColor(TileType t)
        {
            if (t.HasFlag(TileType.Red)) return COLOR_RED;
            if (t.HasFlag(TileType.Green)) return COLOR_GREEN;
            if (t.HasFlag(TileType.Blue)) return COLOR_BLUE;
            if (t.HasFlag(TileType.Yellow)) return COLOR_YELLOW;
            if (t.HasFlag(TileType.Purple)) return COLOR_PURPLE;
            if (t.HasFlag(TileType.Orange)) return COLOR_ORANGE;
            if (t.HasFlag(TileType.Rainbow)) return COLOR_RAINBOW;
            
            return t switch
            {
                TileType.None => COLOR_NONE,
                TileType.Bomb => COLOR_BOMB_BG,
                _ => COLOR_DEFAULT
            };
        }

        public static string GetTileColorForCanvas(TileType t)
        {
             if (t.HasFlag(TileType.Red)) return COLOR_RED;
            if (t.HasFlag(TileType.Green)) return COLOR_GREEN;
            if (t.HasFlag(TileType.Blue)) return COLOR_BLUE;
            if (t.HasFlag(TileType.Yellow)) return COLOR_YELLOW;
            if (t.HasFlag(TileType.Purple)) return COLOR_PURPLE;
            if (t.HasFlag(TileType.Orange)) return COLOR_ORANGE;
            if (t.HasFlag(TileType.Rainbow)) return COLOR_RAINBOW;
            
            return t switch
            {
                TileType.None => COLOR_TRANSPARENT,
                TileType.Bomb => COLOR_BOMB_BG,
                _ => COLOR_DEFAULT
            };
        }

        public static string GetGroundColor(GroundType g) => g switch
        {
            GroundType.Ice => COLOR_ICE,
            _ => COLOR_TRANSPARENT
        };

        public static string GetBombIcon(BombType bomb) => bomb switch
        {
            BombType.None => "",
            BombType.Horizontal => ICON_BOMB_H,
            BombType.Vertical => ICON_BOMB_V,
            BombType.Ufo => ICON_BOMB_UFO,
            BombType.Square5x5 => ICON_BOMB_SQUARE,
            BombType.Color => ICON_BOMB_COLOR,
            _ => ""
        };

        public static string GetCoverIcon(CoverType c) => c switch
        {
            CoverType.Cage => ICON_COVER_CAGE,
            CoverType.Chain => ICON_COVER_CHAIN,
            CoverType.Bubble => ICON_COVER_BUBBLE,
            CoverType.IceCover => ICON_COVER_ICE,
            _ => ""
        };

        public static string GetTileCheckmarkClass(TileType t)
        {
            return t switch
            {
                TileType.None => "text-dark",
                TileType.Yellow => "text-dark",
                _ => "text-white"
            };
        }
    }
}
