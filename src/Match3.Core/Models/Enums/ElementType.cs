using System;
using Match3.Core.Attributes;

namespace Match3.Core.Models.Enums;

/// <summary>
/// Defines the identity of the movable content within a cell.
/// This is the "Item" or "Unit" layer.
/// These elements are subject to gravity and matching rules.
/// </summary>
public enum ElementType : byte
{
    /// <summary>
    /// No element is present in this cell (Air/Empty).
    /// </summary>
    None = 0,

    // --- Basic Matchable Colors ---
    // Using generic names allows for skinning/theming.
    // The underlying logic only cares about equality.

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

    // --- Special Items ---

    /// <summary>
    /// A universal matching item (e.g., Rainbow Ball, Color Bomb).
    /// Matches with any color.
    /// Previously represented by TileType.Rainbow.
    /// </summary>
    Universal = 100,

    /// <summary>
    /// An unmatchable blocker item (e.g., Stone, Wood Box).
    /// Occupies space, can fall, but doesn't match by color.
    /// Usually destroyed by nearby explosions or special conditions.
    /// </summary>
    Unmatchable = 200
}
