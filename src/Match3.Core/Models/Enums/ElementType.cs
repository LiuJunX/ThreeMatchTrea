using System;
using Match3.Core.Attributes;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the identity of the movable content within a cell.
/// A tile is either a color gem, a bomb, or a blocker — never both.
/// </summary>
public enum ElementType : byte
{
    /// <summary>
    /// No element is present in this cell (Air/Empty).
    /// </summary>
    None = 0,

    // --- Basic Matchable Colors (1-6) ---

    [AIMapping(0, "Red")]
    Item1 = 1, // Red

    [AIMapping(1, "Green")]
    Item2 = 2, // Green

    [AIMapping(2, "Blue")]
    Item3 = 3, // Blue

    [AIMapping(3, "Yellow")]
    Item4 = 4, // Yellow

    [AIMapping(4, "Purple")]
    Item5 = 5, // Purple

    [AIMapping(5, "Orange")]
    Item6 = 6, // Orange

    // --- Bombs (10-14) ---

    /// <summary>Clears the entire row.</summary>
    HorizontalRocket = 10,

    /// <summary>Clears the entire column.</summary>
    VerticalRocket = 11,

    /// <summary>
    /// Color bomb (rainbow ball). Clears all tiles of the most frequent color.
    /// </summary>
    ColorBomb = 12,

    /// <summary>Homing missile that targets a specific tile.</summary>
    Ufo = 13,

    /// <summary>Explodes a 5x5 square area.</summary>
    Square5x5 = 14,

    // --- Special (100-109) ---

    /// <summary>
    /// Collectible element that falls and is collected at Sink cells.
    /// Not matchable, not a bomb — routed to collection logic on reaching Sink.
    /// </summary>
    Bird = 100,

    /// <summary>
    /// Pearl released by Oyster death effect. Collectible, counts toward objectives.
    /// </summary>
    Pearl = 101,

    /// <summary>
    /// Plate released by Cupboard death effect. Collectible, counts toward objectives.
    /// </summary>
    Plate = 102,

    /// <summary>
    /// Envelope spawned by Mailbox generator. Collectible, counts toward objectives.
    /// </summary>
    Envelope = 103,

    /// <summary>
    /// Diamond spawned by MagicHat generator. Collectible, counts toward objectives.
    /// </summary>
    Diamond = 104,

    // --- Blockers ---

    /// <summary>
    /// An unmatchable blocker item (e.g., Stone, Wood Box).
    /// Occupies space, can fall, but doesn't match by color.
    /// </summary>
    Unmatchable = 200,

    // --- Moving Obstacles (210-219) ---

    /// <summary>Moving obstacle: egg, 1 stage. Falls with gravity. Any source eliminates.</summary>
    RoyalEgg = 210,

    /// <summary>Moving obstacle: vase, 2 stages. Falls with gravity. Any source eliminates.</summary>
    Vase = 211,

    /// <summary>Moving obstacle: porcelain piggy, 1 stage. Power-up only. Falls with gravity.</summary>
    PorcelainPiggy = 212,

    /// <summary>Moving obstacle: oyster, 3 stages. Releases Pearl on death. Falls with gravity.</summary>
    Oyster = 213,

    /// <summary>Moving obstacle: flowerpot, 2 stages. Spreads 3×3 Grass on death. Falls with gravity.</summary>
    Flowerpot = 214,

    // --- Config-only sentinel (never stored in GameState) ---

    /// <summary>
    /// Config-only sentinel for LevelConfig.Grid: marks a Slot cell that should start
    /// with no tile. BoardInitializer and analysis skip tile generation for this value.
    /// Never stored in GameState at runtime.
    /// </summary>
    KeepEmpty = 255,
}

/// <summary>
/// Extension methods for ElementType identity queries.
/// </summary>
public static class ElementTypeExtensions
{
    /// <summary>Whether this is a matchable color (Item1-Item6).</summary>
    public static bool IsColor(this ElementType type)
        => type >= ElementType.Item1 && type <= ElementType.Item6;

    /// <summary>Whether this is any bomb type.</summary>
    public static bool IsBomb(this ElementType type)
        => type >= ElementType.HorizontalRocket && type <= ElementType.Square5x5;

    /// <summary>Whether this is a rocket (horizontal or vertical).</summary>
    public static bool IsRocket(this ElementType type)
        => type == ElementType.HorizontalRocket || type == ElementType.VerticalRocket;

    /// <summary>Whether this is the color bomb (rainbow ball).</summary>
    public static bool IsColorBomb(this ElementType type)
        => type == ElementType.ColorBomb;

    /// <summary>Whether this is a UFO bomb.</summary>
    public static bool IsUfo(this ElementType type)
        => type == ElementType.Ufo;

    /// <summary>Whether this bomb can be chain-triggered (by explosions, projectiles, etc.).
    /// ColorBomb is excluded — it can only be activated via player swap.</summary>
    public static bool IsChainActivatable(this ElementType type)
        => type.IsBomb() && type != ElementType.ColorBomb;

    /// <summary>Whether this is an area bomb (5x5).</summary>
    public static bool IsAreaBomb(this ElementType type)
        => type == ElementType.Square5x5;

    /// <summary>Whether this type can participate in color matching (colors only).</summary>
    public static bool IsMatchable(this ElementType type)
        => type.IsColor();

    /// <summary>Whether this is a collectible element (Bird, etc.).</summary>
    public static bool IsCollectible(this ElementType type)
        => type == ElementType.Bird || type == ElementType.Pearl || type == ElementType.Plate || type == ElementType.Envelope || type == ElementType.Diamond;

    /// <summary>Whether this is a moving obstacle tile (non-matchable, falls, has Stage HP).</summary>
    public static bool IsMovingObstacle(this ElementType type)
        => type is ElementType.RoyalEgg or ElementType.Vase or ElementType.PorcelainPiggy
                or ElementType.Oyster or ElementType.Flowerpot;
}
