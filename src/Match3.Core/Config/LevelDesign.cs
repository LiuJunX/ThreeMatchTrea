using System;
using System.Collections.Generic;

namespace Match3.Core.Config;

/// <summary>
/// Semantic level design produced by ILevelDesigner.
/// Captures design INTENT — placement strategies are symbolic,
/// translated to concrete grid positions by DesignTranslator.
/// </summary>
public sealed class LevelDesign
{
    // ── Board ──

    /// <summary>Board width.</summary>
    public int Width { get; set; } = 8;

    /// <summary>Board height.</summary>
    public int Height { get; set; } = 8;

    /// <summary>Board shape name (rectangle, cross, diamond, etc.).</summary>
    public string Shape { get; set; } = "rectangle";

    /// <summary>Number of tile colors (4-7).</summary>
    public int ColorCount { get; set; } = 5;

    // ── Difficulty ──

    /// <summary>Rhythm category for this level.</summary>
    public RhythmCategory Rhythm { get; set; }

    /// <summary>Difficulty value (0.0-1.0).</summary>
    public float Difficulty { get; set; }

    /// <summary>Move limit.</summary>
    public int MoveLimit { get; set; } = 20;

    // ── Element Placements (semantic) ──

    /// <summary>Obstacle placements (Box, Bush, Safe, etc.).</summary>
    public List<ElementPlacement> Obstacles { get; set; } = new();

    /// <summary>Cover placements (Cage, Chain, Bubble, etc.).</summary>
    public List<ElementPlacement> Covers { get; set; } = new();

    /// <summary>Ground placements (Ice, Grass, Leaves).</summary>
    public List<ElementPlacement> Grounds { get; set; } = new();

    /// <summary>Moving obstacle placements (RoyalEgg, Vase, etc.).</summary>
    public List<ElementPlacement> MovingObstacles { get; set; } = new();

    // ── Objectives ──

    /// <summary>Level objectives.</summary>
    public List<DesignObjective> Objectives { get; set; } = new();

    // ── Design Rationale ──

    /// <summary>Core design intent: what this level teaches or tests.</summary>
    public string DesignIntent { get; set; } = "";

    /// <summary>Reasoning for move count estimate.</summary>
    public string MoveReasoning { get; set; } = "";

    /// <summary>Additional design notes.</summary>
    public List<string> DesignNotes { get; set; } = new();
}

/// <summary>
/// Describes how to place a group of elements using a semantic strategy.
/// The actual grid positions are resolved by PlacementResolver.
/// </summary>
public sealed class ElementPlacement
{
    /// <summary>Element type name (e.g., "Box", "Cage", "Ice").</summary>
    public string ElementType { get; set; } = "";

    /// <summary>Number of instances to place.</summary>
    public int Count { get; set; }

    /// <summary>HP/Stage for each instance.</summary>
    public int Stage { get; set; } = 1;

    /// <summary>
    /// Semantic placement strategy name.
    /// Supported values: border, center, cluster, scattered, column_aligned,
    /// row_aligned, near_spawner, away_from_spawner, geometric, layered.
    /// </summary>
    public string Strategy { get; set; } = "scattered";

    /// <summary>
    /// Optional region bias (e.g., "center", "top_half", "bottom_half",
    /// "left_half", "right_half"). Applied as a pre-filter before strategy.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Optional state value. Usage depends on element type:
    /// ColorBox/Curtain = color index (1-based ElementType).
    /// </summary>
    public int State { get; set; }
}

/// <summary>
/// A level objective as specified by the designer.
/// </summary>
public sealed class DesignObjective
{
    /// <summary>Target layer name: "Tile", "Cover", "Ground", "Obstacle".</summary>
    public string TargetLayer { get; set; } = "Tile";

    /// <summary>
    /// Element type identifier.
    /// For Tile: color index (1-based).
    /// For Cover/Ground/Obstacle: type name (e.g., "Cage", "Ice", "Box").
    /// </summary>
    public string ElementType { get; set; } = "";

    /// <summary>Number of elements to collect/clear.</summary>
    public int TargetCount { get; set; }
}
