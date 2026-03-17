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
    /// Reinforced safe (up to 5 stages). Only damaged by PowerUp sources (Bomb/Projectile/ColorBomb).
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
}
