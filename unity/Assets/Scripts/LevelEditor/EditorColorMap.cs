using System.Collections.Generic;
using Match3.Core.Models.Enums;
using UnityEngine;

namespace Match3.Unity.LevelEditor
{
    /// <summary>
    /// Static color/visual mappings for the level editor UI.
    /// </summary>
    public static class EditorColorMap
    {
        private static readonly Dictionary<ElementType, Color> ElementColors = new Dictionary<ElementType, Color>
        {
            { ElementType.Item1,      new Color(0.90f, 0.22f, 0.21f) }, // Red
            { ElementType.Item2,      new Color(0.30f, 0.69f, 0.31f) }, // Green
            { ElementType.Item3,      new Color(0.26f, 0.52f, 0.96f) }, // Blue
            { ElementType.Item4,      new Color(1.00f, 0.76f, 0.03f) }, // Yellow
            { ElementType.Item5,      new Color(0.61f, 0.32f, 0.88f) }, // Purple
            { ElementType.Item6,      new Color(1.00f, 0.60f, 0.00f) }, // Orange
            { ElementType.ColorBomb,  new Color(0.95f, 0.95f, 0.95f) }, // Rainbow
            { ElementType.None,       new Color(0.20f, 0.20f, 0.20f) },
        };

        private static readonly Dictionary<TileType, Color> StructuralColors = new Dictionary<TileType, Color>
        {
            { TileType.None,    new Color(0.20f, 0.20f, 0.20f) },
            { TileType.Wall,    new Color(0.45f, 0.35f, 0.25f) },
            { TileType.Hole,    new Color(0.10f, 0.10f, 0.10f) },
            { TileType.Spawner, new Color(0.00f, 0.84f, 0.76f) },
            { TileType.Sink,    new Color(0.55f, 0.55f, 0.55f) },
        };

        private static readonly Dictionary<CoverType, Color> CoverColors = new Dictionary<CoverType, Color>
        {
            { CoverType.None,    Color.clear },
            { CoverType.Cage,    new Color(0.60f, 0.60f, 0.60f, 0.7f) },
            { CoverType.Chain,   new Color(0.50f, 0.50f, 0.50f, 0.7f) },
            { CoverType.Bubble,  new Color(0.70f, 0.85f, 1.00f, 0.5f) },
            { CoverType.Honey,   new Color(0.95f, 0.75f, 0.20f, 0.7f) },
            { CoverType.Frost,   new Color(0.70f, 0.85f, 1.00f, 0.6f) },
        };

        private static readonly Dictionary<GroundType, Color> GroundColors = new Dictionary<GroundType, Color>
        {
            { GroundType.None, Color.clear },
            { GroundType.Ice,  new Color(0.75f, 0.92f, 1.00f, 0.6f) },
        };

        private static readonly Dictionary<ElementType, string> BombLabels = new Dictionary<ElementType, string>
        {
            { ElementType.None,       "" },
            { ElementType.HorizontalRocket, "─" },
            { ElementType.VerticalRocket,   "│" },
            { ElementType.ColorBomb,      "★" },
            { ElementType.Ufo,        "◎" },
            { ElementType.Square5x5,  "■" },
        };

        public static Color GetTileColor(ElementType type)
        {
            return ElementColors.TryGetValue(type, out var c) ? c : Color.magenta;
        }

        public static Color GetStructuralColor(TileType type)
        {
            return StructuralColors.TryGetValue(type, out var c) ? c : Color.magenta;
        }

        public static Color GetCoverColor(CoverType type)
        {
            return CoverColors.TryGetValue(type, out var c) ? c : Color.clear;
        }

        public static Color GetGroundColor(GroundType type)
        {
            return GroundColors.TryGetValue(type, out var c) ? c : Color.clear;
        }

        public static string GetBombLabel(ElementType type)
        {
            return BombLabels.TryGetValue(type, out var s) ? s : "?";
        }
    }
}
