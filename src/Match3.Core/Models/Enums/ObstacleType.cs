namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the type of obstacle occupying a cell.
/// Obstacles are the primary content of a cell (replacing Tile),
/// with complex behavior: multi-stage HP, conditional destruction, death effects.
/// Mutually exclusive within the obstacle layer — at most one per cell.
/// </summary>
public enum ObstacleType : byte
{
    /// <summary>No obstacle at this position.</summary>
    None = 0,

    /// <summary>
    /// Multi-layer box (up to 4 stages). Damaged by any elimination source.
    /// </summary>
    Box = 1,

    /// <summary>
    /// Multi-layer bush (up to 5 stages). Damaged by any elimination source.
    /// Death effect: spawns Grass in adjacent cells.
    /// </summary>
    Bush = 2,

    /// <summary>
    /// Reinforced safe (up to 5 stages). Immune to Match; only damaged by explicit
    /// power-up sources (Bomb, Projectile, ChainReaction, ColorBomb, SideItem, ConsumeBomb).
    /// </summary>
    Safe = 3,

    /// <summary>
    /// Color-specific box. Only damaged by matching its assigned color.
    /// Variant stored in Obstacle.State (maps to ElementType color value).
    /// </summary>
    ColorBox = 4,

    /// <summary>
    /// Generator obstacle. Accumulates adjacent hit count in State.
    /// When State reaches threshold, spawns a special tile (e.g., Diamond).
    /// </summary>
    MagicHat = 5,

    /// <summary>
    /// Curtain covering a cell. Damaged by global color elimination conditions.
    /// Color variant stored in Obstacle.State.
    /// </summary>
    Curtain = 6,

    /// <summary>
    /// Container obstacle (2 stages). Damaged by any source or adjacent match.
    /// Death effect: releases a <see cref="Match3.Core.Models.Enums.ElementType.Plate"/>
    /// collectible tile at its position.
    /// </summary>
    Cupboard = 7,

    /// <summary>
    /// Generator obstacle (permanent, indestructible). Reacts to any adjacent elimination
    /// or power-up hit by spawning an <see cref="Match3.Core.Models.Enums.ElementType.Envelope"/>
    /// collectible tile in an adjacent empty cell.
    /// </summary>
    Mailbox = 8,
}
