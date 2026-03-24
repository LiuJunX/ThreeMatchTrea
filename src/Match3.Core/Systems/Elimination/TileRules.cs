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
    /// Default: true for all types. PorcelainPiggy requires power-up.
    /// </summary>
    public static bool CanEliminate(ElementType type, ElimSource source)
    {
        return type switch
        {
            // Porcelain Piggy: power-up only, match blocked
            ElementType.PorcelainPiggy
                => source is ElimSource.Bomb or ElimSource.Projectile
                          or ElimSource.ChainReaction or ElimSource.ColorBomb
                          or ElimSource.SideItem or ElimSource.ConsumeBomb,
            _ => true
        };
    }

    /// <summary>
    /// Returns the default Stage (HP) for a tile type.
    /// Normal tiles: 1 (one-hit). Vase: 2 (two-hit).
    /// </summary>
    public static byte GetDefaultStage(ElementType type) => type switch
    {
        ElementType.Vase => 2,
        _ => 1
    };
}
