using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Provides rule lookups for cover types.
/// Rules are determined by cover type and do not change at runtime.
/// </summary>
public static class CoverRules
{
    /// <summary>
    /// Returns true if the cover type blocks the tile from participating in matches.
    /// Static covers can be configured to block or allow matching.
    /// Dynamic covers always allow matching.
    /// </summary>
    public static bool BlocksMatch(CoverType type) => type switch
    {
        CoverType.None => false,
        CoverType.Cage => true,      // Cage blocks matching
        CoverType.Chain => false,    // Chain allows matching
        CoverType.Bubble => false,   // Bubble (dynamic) allows matching
        CoverType.IceCover => true,  // Ice cover blocks matching
        _ => false
    };

    /// <summary>
    /// Returns true if the cover type blocks the tile from being swapped by the player.
    /// All cover types block swap operations.
    /// </summary>
    public static bool BlocksSwap(CoverType type) => type != CoverType.None;

    /// <summary>
    /// Returns true if the cover type blocks the tile from moving (gravity).
    /// Static covers block movement, dynamic covers do not.
    /// </summary>
    public static bool BlocksMovement(CoverType type) => type switch
    {
        CoverType.None => false,
        CoverType.Cage => true,      // Static - blocks movement
        CoverType.Chain => true,     // Static - blocks movement
        CoverType.Bubble => false,   // Dynamic - allows movement
        CoverType.IceCover => true,  // Static - blocks movement
        _ => false
    };

    /// <summary>
    /// Returns true if the cover type is dynamic (moves with the tile).
    /// </summary>
    public static bool IsDynamicType(CoverType type) => type switch
    {
        CoverType.Bubble => true,
        _ => false
    };

    /// <summary>
    /// Returns the default health for a cover type.
    /// </summary>
    public static byte GetDefaultHealth(CoverType type) => type switch
    {
        CoverType.None => 0,
        CoverType.Cage => 1,
        CoverType.Chain => 1,
        CoverType.Bubble => 1,
        CoverType.IceCover => 2,  // Ice cover has 2 HP by default
        _ => 1
    };
}
