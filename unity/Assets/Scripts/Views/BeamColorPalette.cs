using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Shared beam color definitions used by both 2D and 3D projectile views.
    /// </summary>
    public static class BeamColorPalette
    {
        /// <summary>
        /// Indexed color palette for color-bomb beams.
        /// Order: Red, Green, Blue, Yellow, Purple, Orange.
        /// </summary>
        public static readonly Color[] BeamColors =
        {
            new Color(1f, 0.3f, 0.3f),    // Red
            new Color(0.3f, 1f, 0.4f),     // Green
            new Color(0.3f, 0.5f, 1f),     // Blue
            new Color(1f, 0.95f, 0.3f),    // Yellow
            new Color(0.8f, 0.4f, 1f),     // Purple
            new Color(1f, 0.6f, 0.2f),     // Orange
        };

        /// <summary>
        /// Returns the beam color for a given color index.
        /// Falls back to the first color if the index is out of range.
        /// </summary>
        public static Color GetBeamColor(byte colorIndex)
        {
            return colorIndex < BeamColors.Length
                ? BeamColors[colorIndex]
                : BeamColors[0];
        }
    }
}
