using System;
using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

/// <summary>
/// Master blueprint for level progression.
/// Defines phases (level ranges) with element unlock schedules,
/// difficulty curves, and generation constraints.
/// </summary>
[Serializable]
public class ProgressionBlueprint
{
    /// <summary>
    /// Ordered, non-overlapping phases covering the full level range.
    /// Generator walks phases in order and accumulates unlocks.
    /// </summary>
    public PhaseConfig[] Phases { get; set; } = Array.Empty<PhaseConfig>();
}

/// <summary>
/// Defines constraints and unlocks for a contiguous range of levels.
/// </summary>
[Serializable]
public class PhaseConfig
{
    // ── Identity ──

    /// <summary>Human-readable phase name (e.g., "Tutorial", "MidGame").</summary>
    public string Name { get; set; } = "";

    /// <summary>First level number in this phase (inclusive).</summary>
    public int StartLevel { get; set; }

    /// <summary>Last level number in this phase (inclusive).</summary>
    public int EndLevel { get; set; }

    // ── Board Constraints ──

    public int MinBoardWidth { get; set; } = 7;
    public int MaxBoardWidth { get; set; } = 9;
    public int MinBoardHeight { get; set; } = 7;
    public int MaxBoardHeight { get; set; } = 9;

    /// <summary>Allowed board shapes. See level-design.md §2.1.</summary>
    public string[] Shapes { get; set; } = new[] { "rectangle" };

    // ── Color Count ──

    public int MinColors { get; set; } = 4;
    public int MaxColors { get; set; } = 5;

    // ── Element Unlocks (new or upgraded in this phase) ──
    // Generator accumulates all unlocks from phase 1 to current phase.
    // If the same type appears in multiple phases, later phase overrides constraints.

    public ObstacleAllowance[] Obstacles { get; set; } = Array.Empty<ObstacleAllowance>();
    public CoverAllowance[] Covers { get; set; } = Array.Empty<CoverAllowance>();
    public GroundAllowance[] Grounds { get; set; } = Array.Empty<GroundAllowance>();

    /// <summary>Collectible elements unlocked (Bird, Pearl, Plate, etc.).</summary>
    public CollectibleAllowance[] Collectibles { get; set; } = Array.Empty<CollectibleAllowance>();

    /// <summary>Moving obstacle elements unlocked (RoyalEgg, Vase, etc.).</summary>
    public MovingObstacleAllowance[] MovingObstacles { get; set; } = Array.Empty<MovingObstacleAllowance>();

    // ── Objective Constraints ──

    /// <summary>Max number of objectives per level in this phase.</summary>
    public int MaxObjectives { get; set; } = 1;

    /// <summary>Allowed objective target layers (Tile, Cover, Ground, Obstacle).</summary>
    public ObjectiveTargetLayer[] ObjectiveLayers { get; set; } = new[] { ObjectiveTargetLayer.Tile };

    // ── Difficulty ──

    /// <summary>TargetDifficulty range for spawn model (0.0-1.0).</summary>
    public float MinDifficulty { get; set; }
    public float MaxDifficulty { get; set; } = 0.3f;

    /// <summary>Acceptable win rate range from analysis pipeline.</summary>
    public float MinWinRate { get; set; } = 0.75f;
    public float MaxWinRate { get; set; } = 0.95f;

    /// <summary>Move limit range.</summary>
    public int MinMoves { get; set; } = 15;
    public int MaxMoves { get; set; } = 25;

    // ── Level Rhythm ──

    /// <summary>
    /// Proportion of "rest" levels (easy, high win rate) within this phase.
    /// </summary>
    public float EasyRatio { get; set; } = 0.1f;

    /// <summary>
    /// Proportion of "challenge" levels (hard, low win rate) within this phase.
    /// Remaining = 1 - EasyRatio - HardRatio = normal levels.
    /// </summary>
    public float HardRatio { get; set; } = 0.2f;

    /// <summary>
    /// Place a boss-style level every N levels within this phase.
    /// null = no boss levels. E.g., 10 = boss at level 10, 20, 30...
    /// </summary>
    public int? BossEveryN { get; set; }
}

// ── Element Allowance Types ──
// Each defines what's permitted for a specific element type in a phase.
// If the same type appears in a later phase, the later constraints replace earlier ones.

[Serializable]
public class ObstacleAllowance
{
    public ObstacleType Type { get; set; }

    /// <summary>Max HP/stage allowed (e.g., Box stage 1-4).</summary>
    public int MaxStage { get; set; } = 1;

    /// <summary>Max instances of this obstacle per level.</summary>
    public int MaxCount { get; set; } = 10;
}

[Serializable]
public class CoverAllowance
{
    public CoverType Type { get; set; }

    /// <summary>Max health allowed.</summary>
    public int MaxHealth { get; set; } = 1;

    /// <summary>Max instances per level.</summary>
    public int MaxCount { get; set; } = 15;
}

[Serializable]
public class GroundAllowance
{
    public GroundType Type { get; set; }

    /// <summary>Max health allowed.</summary>
    public int MaxHealth { get; set; } = 1;

    /// <summary>Max instances per level.</summary>
    public int MaxCount { get; set; } = 20;
}

[Serializable]
public class CollectibleAllowance
{
    public ElementType Type { get; set; }

    /// <summary>Max collectibles of this type per level.</summary>
    public int MaxCount { get; set; } = 5;
}

[Serializable]
public class MovingObstacleAllowance
{
    public ElementType Type { get; set; }

    /// <summary>Max HP/stage (e.g., Vase has 2 stages).</summary>
    public int MaxStage { get; set; } = 1;

    /// <summary>Max instances per level.</summary>
    public int MaxCount { get; set; } = 5;
}
