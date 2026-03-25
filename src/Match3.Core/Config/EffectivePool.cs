using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

/// <summary>
/// Accumulated element pool from all phases up to the target level.
/// Created by merging phase unlocks in order; later phases override earlier constraints
/// for the same element type. Board/difficulty/rhythm params come from the current phase.
/// </summary>
public sealed class EffectivePool
{
    // ── Board (from current phase) ──
    public int MinBoardWidth { get; set; }
    public int MaxBoardWidth { get; set; }
    public int MinBoardHeight { get; set; }
    public int MaxBoardHeight { get; set; }
    public string[] Shapes { get; set; } = Array.Empty<string>();
    public int MinColors { get; set; }
    public int MaxColors { get; set; }

    // ── Accumulated element allowances ──
    public Dictionary<ObstacleType, ObstacleAllowance> Obstacles { get; set; } = new();
    public Dictionary<CoverType, CoverAllowance> Covers { get; set; } = new();
    public Dictionary<GroundType, GroundAllowance> Grounds { get; set; } = new();
    public Dictionary<ElementType, CollectibleAllowance> Collectibles { get; set; } = new();
    public Dictionary<ElementType, MovingObstacleAllowance> MovingObstacles { get; set; } = new();

    // ── Objectives (from current phase) ──
    public int MaxObjectives { get; set; }
    public ObjectiveTargetLayer[] ObjectiveLayers { get; set; } = Array.Empty<ObjectiveTargetLayer>();

    // ── Difficulty (from current phase) ──
    public float MinDifficulty { get; set; }
    public float MaxDifficulty { get; set; }
    public float MinWinRate { get; set; }
    public float MaxWinRate { get; set; }
    public int MinMoves { get; set; }
    public int MaxMoves { get; set; }

    // ── Rhythm (from current phase) ──
    public float EasyRatio { get; set; }
    public float HardRatio { get; set; }
    public int? BossEveryN { get; set; }

    /// <summary>
    /// Build an EffectivePool by merging all phases from phases[0] through
    /// the phase containing <paramref name="levelNumber"/>.
    /// </summary>
    public static EffectivePool Build(ProgressionBlueprint blueprint, int levelNumber)
    {
        var pool = new EffectivePool();
        PhaseConfig? currentPhase = null;

        foreach (var phase in blueprint.Phases)
        {
            // Accumulate element unlocks from all phases up to and including the target
            MergeAllowances(pool, phase);

            if (levelNumber >= phase.StartLevel && levelNumber <= phase.EndLevel)
            {
                currentPhase = phase;
                break; // Don't accumulate beyond the current phase
            }
        }

        if (currentPhase == null)
            throw new ArgumentOutOfRangeException(nameof(levelNumber),
                $"Level {levelNumber} not found in any blueprint phase.");

        // Board/difficulty/rhythm come from the current phase only
        pool.MinBoardWidth = currentPhase.MinBoardWidth;
        pool.MaxBoardWidth = currentPhase.MaxBoardWidth;
        pool.MinBoardHeight = currentPhase.MinBoardHeight;
        pool.MaxBoardHeight = currentPhase.MaxBoardHeight;
        pool.Shapes = currentPhase.Shapes;
        pool.MinColors = currentPhase.MinColors;
        pool.MaxColors = currentPhase.MaxColors;

        pool.MaxObjectives = currentPhase.MaxObjectives;
        pool.ObjectiveLayers = currentPhase.ObjectiveLayers;

        pool.MinDifficulty = currentPhase.MinDifficulty;
        pool.MaxDifficulty = currentPhase.MaxDifficulty;
        pool.MinWinRate = currentPhase.MinWinRate;
        pool.MaxWinRate = currentPhase.MaxWinRate;
        pool.MinMoves = currentPhase.MinMoves;
        pool.MaxMoves = currentPhase.MaxMoves;

        pool.EasyRatio = currentPhase.EasyRatio;
        pool.HardRatio = currentPhase.HardRatio;
        pool.BossEveryN = currentPhase.BossEveryN;

        return pool;
    }

    private static void MergeAllowances(EffectivePool pool, PhaseConfig phase)
    {
        if (phase.Obstacles != null)
            foreach (var a in phase.Obstacles)
                pool.Obstacles[a.Type] = a;

        if (phase.Covers != null)
            foreach (var a in phase.Covers)
                pool.Covers[a.Type] = a;

        if (phase.Grounds != null)
            foreach (var a in phase.Grounds)
                pool.Grounds[a.Type] = a;

        if (phase.Collectibles != null)
            foreach (var a in phase.Collectibles)
                pool.Collectibles[a.Type] = a;

        if (phase.MovingObstacles != null)
            foreach (var a in phase.MovingObstacles)
                pool.MovingObstacles[a.Type] = a;
    }
}
