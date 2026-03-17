using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;

namespace Match3.Core.Systems.Obstacles;

/// <summary>
/// Pure, static query methods for obstacle vulnerability rules.
/// Two separate rule sets for two damage paths:
/// <list type="bullet">
///   <item><see cref="CanHit"/>: direct hit (bomb/projectile targets the obstacle's cell)</item>
///   <item><see cref="CanReactAdjacent"/>: adjacent reaction (neighbor tile died)</item>
/// </list>
/// </summary>
public static class ObstacleRules
{
    /// <summary>
    /// Can a direct hit from this source damage this obstacle?
    /// Used by CellEliminator guard chain (TryHit path).
    /// </summary>
    public static bool CanHit(in Obstacle obstacle, in ElimContext ctx)
    {
        return obstacle.Type switch
        {
            ObstacleType.Box      => true,
            ObstacleType.Bush     => true,
            ObstacleType.Safe     => ctx.Source != ElimSource.Match,
            ObstacleType.ColorBox => false,   // immune to direct hits; only adjacent color match
            ObstacleType.MagicHat => false,   // passive — reacts to adjacent hits
            ObstacleType.Curtain  => false,   // passive — reacts to global color elimination
            _ => true
        };
    }

    /// <summary>
    /// Does this obstacle react when an adjacent tile is eliminated?
    /// <paramref name="triggerType"/> is the ElementType of the tile that died.
    /// <see cref="ElementType.ColorBomb"/> acts as a wildcard (matches any color).
    /// </summary>
    public static bool CanReactAdjacent(in Obstacle obstacle, ElementType triggerType)
    {
        return obstacle.Type switch
        {
            ObstacleType.Box      => true,
            ObstacleType.Bush     => true,
            ObstacleType.Safe     => false,   // only power-up direct hits
            ObstacleType.ColorBox => triggerType == ElementType.ColorBomb
                                  || triggerType == (ElementType)obstacle.State,
            ObstacleType.MagicHat => true,    // accumulates (processing logic separate)
            ObstacleType.Curtain  => false,   // global, not adjacent
            _ => false
        };
    }
}
