using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Systems.Obstacles;
using Match3.Random;

namespace Match3.Core.Config;

/// <summary>
/// Translates a semantic LevelDesign into a concrete LevelConfig.
/// Pure function — no IO, deterministic given same RNG seed.
/// </summary>
public static class DesignTranslator
{
    /// <summary>
    /// Translate a LevelDesign into a LevelGeneratorResult containing a valid LevelConfig.
    /// </summary>
    public static LevelGeneratorResult Translate(
        LevelDesign design,
        LevelDesignContext context,
        ulong seed)
    {
        var rng = new XorShift64(seed);
        var notes = new StringBuilder();
        var pool = context.Pool;

        // 0. Clamp LLM output to pool constraints
        int width = Math.Clamp(design.Width, pool.MinBoardWidth, pool.MaxBoardWidth);
        int height = Math.Clamp(design.Height, pool.MinBoardHeight, pool.MaxBoardHeight);
        int colorCount = Math.Clamp(design.ColorCount, pool.MinColors, pool.MaxColors);

        // 1. Cell layout from shape
        var cells = ShapeTemplates.Generate(design.Shape, width, height, rng);
        notes.AppendLine($"Board: {width}x{height} {design.Shape}");
        notes.AppendLine($"Colors: {colorCount}, Difficulty: {design.Difficulty:F2}, Rhythm: {design.Rhythm}");

        // 2. Create config
        var config = new LevelConfig(width, height);
        Array.Copy(cells, config.Cells, cells.Length);
        config.TargetDifficulty = design.Difficulty;
        config.MoveLimit = ClampMoves(design.MoveLimit, context.Pool);

        // 3. Track occupied positions to prevent double-placement
        var occupied = new HashSet<int>();

        // 4. Place obstacles
        var usedObstacles = new List<ObstacleType>();
        foreach (var placement in design.Obstacles)
        {
            if (!TryParseObstacleType(placement.ElementType, out var obsType))
            {
                notes.AppendLine($"WARNING: Unknown obstacle type '{placement.ElementType}', skipped");
                continue;
            }

            int count = ClampCount(placement.Count, obsType, context.Pool);
            int stage = ClampStage(placement.Stage, obsType, context.Pool);

            var positions = PlacementResolver.ResolvePositions(
                placement.Strategy, cells, width, height,
                count, placement.Region, occupied, rng);

            foreach (int idx in positions)
            {
                config.Obstacles[idx] = obsType;
                config.ObstacleStages[idx] = (byte)stage;
                config.ObstacleStates[idx] = ComputeObstacleState(
                    obsType, placement.State, colorCount, rng);
                occupied.Add(idx);
                usedObstacles.Add(obsType);
            }

            notes.AppendLine($"Obstacle {obsType}: {positions.Count}/{count} placed [{placement.Strategy}]");
        }

        // 5. Place moving obstacles (on Grid layer)
        foreach (var placement in design.MovingObstacles)
        {
            if (!TryParseElementType(placement.ElementType, out var elemType))
            {
                notes.AppendLine($"WARNING: Unknown moving obstacle type '{placement.ElementType}', skipped");
                continue;
            }

            int count = placement.Count;
            int stage = placement.Stage;

            var positions = PlacementResolver.ResolvePositions(
                placement.Strategy, cells, width, height,
                count, placement.Region, occupied, rng);

            foreach (int idx in positions)
            {
                config.Grid[idx] = elemType;
                config.TileStages[idx] = (byte)stage;
                occupied.Add(idx);
            }

            notes.AppendLine($"MovingObstacle {elemType}: {positions.Count}/{count} placed [{placement.Strategy}]");
        }

        // 6. Place covers (can go on non-obstacle, non-spawner cells)
        var coverOccupied = new HashSet<int>(occupied);
        // Also exclude spawner positions from cover placement
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] == CellKind.Spawner)
                coverOccupied.Add(i);

        var usedCovers = new List<CoverType>();
        foreach (var placement in design.Covers)
        {
            if (!TryParseCoverType(placement.ElementType, out var covType))
            {
                notes.AppendLine($"WARNING: Unknown cover type '{placement.ElementType}', skipped");
                continue;
            }

            int count = ClampCoverCount(placement.Count, covType, context.Pool);
            int health = ClampCoverHealth(placement.Stage, covType, context.Pool);

            // Covers go on positions without obstacles
            var coverCandidateOccupied = new HashSet<int>(coverOccupied);
            var positions = PlacementResolver.ResolvePositions(
                placement.Strategy, cells, width, height,
                count, placement.Region, coverCandidateOccupied, rng);

            foreach (int idx in positions)
            {
                config.Covers[idx] = covType;
                config.CoverHealths[idx] = (byte)health;
                coverOccupied.Add(idx);
                usedCovers.Add(covType);
            }

            notes.AppendLine($"Cover {covType}: {positions.Count}/{count} placed [{placement.Strategy}]");
        }

        // 7. Place grounds (can go on positions with tiles, not on obstacles)
        var groundOccupied = new HashSet<int>();
        // Exclude cells that have obstacles
        for (int i = 0; i < cells.Length; i++)
            if (config.Obstacles[i] != ObstacleType.None)
                groundOccupied.Add(i);

        var usedGrounds = new List<GroundType>();
        foreach (var placement in design.Grounds)
        {
            if (!TryParseGroundType(placement.ElementType, out var gndType))
            {
                notes.AppendLine($"WARNING: Unknown ground type '{placement.ElementType}', skipped");
                continue;
            }

            int count = ClampGroundCount(placement.Count, gndType, context.Pool);
            int health = ClampGroundHealth(placement.Stage, gndType, context.Pool);

            var positions = PlacementResolver.ResolvePositions(
                placement.Strategy, cells, width, height,
                count, placement.Region, groundOccupied, rng);

            foreach (int idx in positions)
            {
                config.Grounds[idx] = gndType;
                config.GroundHealths[idx] = (byte)health;
                groundOccupied.Add(idx);
                usedGrounds.Add(gndType);
            }

            notes.AppendLine($"Ground {gndType}: {positions.Count}/{count} placed [{placement.Strategy}]");
        }

        // 8. Generate objectives
        GenerateObjectives(config, design, notes);

        // 9. Append design intent to notes
        if (!string.IsNullOrEmpty(design.DesignIntent))
            notes.AppendLine($"DesignIntent: {design.DesignIntent}");

        return new LevelGeneratorResult
        {
            Config = config,
            LevelNumber = context.LevelNumber,
            PhaseName = context.Phase.Name,
            Rhythm = design.Rhythm,
            Difficulty = design.Difficulty,
            DesignNotes = notes.ToString(),
            UsedObstacles = usedObstacles,
            UsedCovers = usedCovers,
            UsedGrounds = usedGrounds,
            Shape = design.Shape
        };
    }

    // ── Objectives ──

    private static void GenerateObjectives(LevelConfig config, LevelDesign design, StringBuilder notes)
    {
        notes.Append("Objectives: ");
        for (int i = 0; i < design.Objectives.Count && i < 4; i++)
        {
            var obj = design.Objectives[i];
            var levelObj = TranslateObjective(obj, config);
            if (levelObj.HasValue)
            {
                config.Objectives[i] = levelObj.Value;
                notes.Append($"[{obj.TargetLayer}:{obj.ElementType}x{obj.TargetCount}] ");
            }
        }
        notes.AppendLine();
    }

    private static LevelObjective? TranslateObjective(DesignObjective obj, LevelConfig config)
    {
        if (!TryParseObjectiveLayer(obj.TargetLayer, out var layer))
            return null;

        int elementType = layer switch
        {
            ObjectiveTargetLayer.Tile => int.TryParse(obj.ElementType, out var c) ? c : 1,
            ObjectiveTargetLayer.Obstacle => TryParseObstacleType(obj.ElementType, out var o) ? (int)o : 0,
            ObjectiveTargetLayer.Cover => TryParseCoverType(obj.ElementType, out var cv) ? (int)cv : 0,
            ObjectiveTargetLayer.Ground => TryParseGroundType(obj.ElementType, out var g) ? (int)g : 0,
            _ => 0
        };

        // For obstacle/cover/ground objectives, auto-count from placed elements if targetCount is 0
        int targetCount = obj.TargetCount;
        if (targetCount <= 0)
        {
            targetCount = layer switch
            {
                ObjectiveTargetLayer.Obstacle => CountPlaced(config.Obstacles, (ObstacleType)elementType),
                ObjectiveTargetLayer.Cover => CountPlacedCover(config.Covers, (CoverType)elementType),
                ObjectiveTargetLayer.Ground => CountPlacedGround(config.Grounds, (GroundType)elementType),
                _ => 10
            };
        }

        return new LevelObjective
        {
            TargetLayer = layer,
            ElementType = elementType,
            TargetCount = targetCount
        };
    }

    // ── Clamping ──

    private static int ClampMoves(int moves, EffectivePool pool)
    {
        return Math.Clamp(moves, pool.MinMoves, pool.MaxMoves);
    }

    private static int ClampCount(int count, ObstacleType type, EffectivePool pool)
    {
        if (pool.Obstacles.TryGetValue(type, out var allowance))
            return Math.Min(count, allowance.MaxCount);
        return count;
    }

    private static int ClampStage(int stage, ObstacleType type, EffectivePool pool)
    {
        if (pool.Obstacles.TryGetValue(type, out var allowance))
            return Math.Min(stage, allowance.MaxStage);
        return stage;
    }

    private static int ClampCoverCount(int count, CoverType type, EffectivePool pool)
    {
        if (pool.Covers.TryGetValue(type, out var allowance))
            return Math.Min(count, allowance.MaxCount);
        return count;
    }

    private static int ClampCoverHealth(int health, CoverType type, EffectivePool pool)
    {
        if (pool.Covers.TryGetValue(type, out var allowance))
            return Math.Min(health, allowance.MaxHealth);
        return health;
    }

    private static int ClampGroundCount(int count, GroundType type, EffectivePool pool)
    {
        if (pool.Grounds.TryGetValue(type, out var allowance))
            return Math.Min(count, allowance.MaxCount);
        return count;
    }

    private static int ClampGroundHealth(int health, GroundType type, EffectivePool pool)
    {
        if (pool.Grounds.TryGetValue(type, out var allowance))
            return Math.Min(health, allowance.MaxHealth);
        return health;
    }

    // ── Obstacle State ──

    private static byte ComputeObstacleState(
        ObstacleType type, int designState, int colorCount, XorShift64 rng)
    {
        // Use designer-specified state if provided
        if (designState > 0) return (byte)designState;

        return type switch
        {
            ObstacleType.ColorBox => (byte)rng.Next(1, colorCount + 1),
            ObstacleType.Curtain => (byte)rng.Next(1, colorCount + 1),
            ObstacleType.PotionBottle => ObstacleRules.GetDefaultState(ObstacleType.PotionBottle),
            _ => 0
        };
    }

    // ── Counting ──

    private static int CountPlaced(ObstacleType[] obstacles, ObstacleType type)
    {
        int count = 0;
        foreach (var o in obstacles)
            if (o == type) count++;
        return Math.Max(count, 1);
    }

    private static int CountPlacedCover(CoverType[] covers, CoverType type)
    {
        int count = 0;
        foreach (var c in covers)
            if (c == type) count++;
        return Math.Max(count, 1);
    }

    private static int CountPlacedGround(GroundType[] grounds, GroundType type)
    {
        int count = 0;
        foreach (var g in grounds)
            if (g == type) count++;
        return Math.Max(count, 1);
    }

    // ── Parsing ──

    private static bool TryParseObstacleType(string name, out ObstacleType result)
    {
        return Enum.TryParse(name, ignoreCase: true, out result) && result != ObstacleType.None;
    }

    private static bool TryParseCoverType(string name, out CoverType result)
    {
        return Enum.TryParse(name, ignoreCase: true, out result) && result != CoverType.None;
    }

    private static bool TryParseGroundType(string name, out GroundType result)
    {
        return Enum.TryParse(name, ignoreCase: true, out result) && result != GroundType.None;
    }

    private static bool TryParseElementType(string name, out ElementType result)
    {
        return Enum.TryParse(name, ignoreCase: true, out result) && result != ElementType.None;
    }

    private static bool TryParseObjectiveLayer(string name, out ObjectiveTargetLayer result)
    {
        return Enum.TryParse(name, ignoreCase: true, out result) && result != ObjectiveTargetLayer.None;
    }
}
