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
            ObstacleType.Cupboard => true,
            ObstacleType.Safe     => ctx.Source != ElimSource.Match,
            ObstacleType.ColorBox => false,   // immune to direct hits; only adjacent color match
            ObstacleType.MagicHat => false,   // passive — reacts to adjacent hits
            ObstacleType.Curtain  => false,   // passive — reacts to global color elimination
            ObstacleType.Mailbox  => false,   // indestructible generator — reacts to adjacent/power-up
            _ => true
        };
    }

    /// <summary>
    /// Returns the default stage (HP) for an obstacle type when not explicitly
    /// specified in the level config. Mirrors GroundRules.GetDefaultHealth pattern.
    /// </summary>
    public static byte GetDefaultStage(ObstacleType type) => type switch
    {
        ObstacleType.Box      => 1,
        ObstacleType.Bush     => 1,
        ObstacleType.Cupboard => 2,
        ObstacleType.Safe     => 1,
        ObstacleType.ColorBox => 1,
        ObstacleType.MagicHat => 1,
        ObstacleType.Curtain  => 1,
        ObstacleType.Mailbox  => 1,
        _ => 1
    };

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
            ObstacleType.Cupboard => true,
            ObstacleType.Safe     => false,   // only power-up direct hits
            ObstacleType.ColorBox => triggerType == ElementType.ColorBomb
                                  || triggerType == (ElementType)obstacle.State,
            ObstacleType.MagicHat => true,    // accumulates (processing logic separate)
            ObstacleType.Curtain  => false,   // global, not adjacent
            ObstacleType.Mailbox  => true,    // any adjacent elimination triggers generation
            _ => false
        };
    }

    /// <summary>
    /// Is this obstacle a generator? Generators are permanent (never damaged)
    /// and spawn product tiles when triggered instead of taking damage.
    /// </summary>
    public static bool IsGenerator(ObstacleType type) => type switch
    {
        ObstacleType.Mailbox => true,
        _ => false
    };
}
