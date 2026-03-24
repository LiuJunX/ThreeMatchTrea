using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;

namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Pure, static query methods for tile elimination rules.
/// Mirrors <see cref="Match3.Core.Systems.Obstacles.ObstacleRules"/> for the tile layer.
/// Default: all element types can be eliminated by any source.
/// Override per-type for moving obstacles that require power-up only.
/// </summary>
public static class TileRules
{
    /// <summary>
    /// Can this tile type be eliminated by the given source?
    /// Default: true for all types. Override for power-up-only moving obstacles.
    /// </summary>
    public static bool CanEliminate(ElementType type, ElimSource source)
    {
        // Currently all tile types can be eliminated by any source.
        // When moving obstacles are added (RoyalEgg, Vase, PorcelainPiggy, etc.),
        // add cases here to restrict elimination sources.
        _ = type;
        _ = source;
        return true;
    }

    /// <summary>
    /// Returns the default Stage (HP) for a tile type.
    /// Normal tiles: 1 (one-hit). Moving obstacles: 2-3.
    /// </summary>
    public static byte GetDefaultStage(ElementType type)
    {
        // Currently all tiles have Stage=1.
        // When moving obstacles are added, return their default HP here.
        _ = type;
        return 1;
    }
}
