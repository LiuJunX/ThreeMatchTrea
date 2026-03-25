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
/// Generates valid LevelConfig objects from a ProgressionBlueprint.
/// Pure, deterministic (given same seed), no IO.
/// </summary>
public sealed class LevelGenerator
{
    /// <summary>
    /// Generate a LevelConfig for the given level number.
    /// </summary>
    /// <param name="blueprint">The progression blueprint.</param>
    /// <param name="levelNumber">Target level number (1-based).</param>
    /// <param name="seed">Random seed for deterministic generation.</param>
    /// <returns>A valid LevelConfig ready for BoardInitializer, plus design notes.</returns>
    public LevelGeneratorResult Generate(ProgressionBlueprint blueprint, int levelNumber, ulong seed)
    {
        // 1. Build effective pool
        var pool = EffectivePool.Build(blueprint, levelNumber);
        var rng = new XorShift64(seed);
        var notes = new StringBuilder();

        // 2. Rhythm & difficulty
        var rhythm = DifficultyBudget.ClassifyRhythm(pool, levelNumber, rng);
        var difficulty = DifficultyBudget.ComputeDifficulty(pool, rhythm, rng);
        notes.AppendLine($"Rhythm: {rhythm}, Difficulty: {difficulty:F2}");

        // 3. Board dimensions & shape
        int width = rng.Next(pool.MinBoardWidth, pool.MaxBoardWidth + 1);
        int height = rng.Next(pool.MinBoardHeight, pool.MaxBoardHeight + 1);
        string shape = pool.Shapes[rng.Next(0, pool.Shapes.Length)];
        notes.AppendLine($"Board: {width}x{height} {shape}");

        // 4. Cell layout
        var cells = ShapeTemplates.Generate(shape, width, height, rng);
        int totalSlots = ShapeTemplates.CountSlots(cells);

        // 5. Placement plan
        var plan = DifficultyBudget.ComputePlacementPlan(difficulty, totalSlots, pool, rng);
        notes.AppendLine($"Colors: {plan.ColorCount}, Obstacles: {plan.Obstacles.Count}, Covers: {plan.Covers.Count}, Grounds: {plan.Grounds.Count}");

        // 6. Create config
        var config = new LevelConfig(width, height);
        Array.Copy(cells, config.Cells, cells.Length);
        config.TargetDifficulty = difficulty;

        // 7. Collect placeable positions (Slot cells, not Spawner)
        var slotPositions = CollectSlotPositions(cells, width, height);
        Shuffle(slotPositions, rng);
        int posIdx = 0;

        // 8. Place obstacles
        foreach (var (type, stage, _) in plan.Obstacles)
        {
            if (posIdx >= slotPositions.Count) break;
            int idx = slotPositions[posIdx++];
            config.Obstacles[idx] = type;
            config.ObstacleStages[idx] = stage;
            config.ObstacleStates[idx] = ComputeObstacleState(type, plan.ColorCount, rng);
        }

        // 9. Place moving obstacles (on Grid layer)
        foreach (var (type, stage) in plan.MovingObstacles)
        {
            if (posIdx >= slotPositions.Count) break;
            int idx = slotPositions[posIdx++];
            config.Grid[idx] = type;
            config.TileStages[idx] = stage;
        }

        // 10. Remaining positions: available for covers and grounds
        var tilePositions = new List<int>();
        for (int i = posIdx; i < slotPositions.Count; i++)
            tilePositions.Add(slotPositions[i]);

        // Also include Spawner positions for cover/ground
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] == CellKind.Spawner) tilePositions.Add(i);

        Shuffle(tilePositions, rng);
        int tileIdx = 0;

        // 11. Place covers (on cells that will have tiles)
        foreach (var (type, health) in plan.Covers)
        {
            if (tileIdx >= tilePositions.Count) break;
            int idx = tilePositions[tileIdx++];
            config.Covers[idx] = type;
            config.CoverHealths[idx] = health;
        }

        // 12. Place grounds (can share positions with tiles, separate pass)
        var groundPositions = new List<int>(tilePositions);
        Shuffle(groundPositions, rng);
        int groundIdx = 0;
        foreach (var (type, health) in plan.Grounds)
        {
            if (groundIdx >= groundPositions.Count) break;
            int idx = groundPositions[groundIdx++];
            // Skip if already has obstacle
            if (config.Obstacles[idx] != ObstacleType.None) { groundIdx++; continue; }
            config.Grounds[idx] = type;
            config.GroundHealths[idx] = health;
        }

        // 13. Grid remains ElementType.None for random fill by BoardInitializer

        // 14. Generate objectives
        GenerateObjectives(config, plan, pool, difficulty, rng, notes);

        // 15. Calculate moves
        config.MoveLimit = DifficultyBudget.CalculateMoves(plan, difficulty, pool);
        notes.AppendLine($"Moves: {config.MoveLimit}");

        // Collect used element types for design document
        var usedObstacles = new List<ObstacleType>();
        var usedCovers = new List<CoverType>();
        var usedGrounds = new List<GroundType>();
        foreach (var (t, _, _) in plan.Obstacles) usedObstacles.Add(t);
        foreach (var (t, _) in plan.Covers) usedCovers.Add(t);
        foreach (var (t, _) in plan.Grounds) usedGrounds.Add(t);

        return new LevelGeneratorResult
        {
            Config = config,
            LevelNumber = levelNumber,
            PhaseName = BlueprintValidator.FindPhase(blueprint, levelNumber)?.Name ?? "",
            Rhythm = rhythm,
            Difficulty = difficulty,
            DesignNotes = notes.ToString(),
            UsedObstacles = usedObstacles,
            UsedCovers = usedCovers,
            UsedGrounds = usedGrounds,
            Shape = shape
        };
    }

    // ── Private Helpers ──

    private static List<int> CollectSlotPositions(CellKind[] cells, int width, int height)
    {
        var positions = new List<int>();
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] == CellKind.Slot) // Slot only, not Spawner (keep spawners clear)
                positions.Add(i);
        return positions;
    }

    private static byte ComputeObstacleState(ObstacleType type, int colorCount, XorShift64 rng)
    {
        return type switch
        {
            ObstacleType.ColorBox => (byte)rng.Next(1, colorCount + 1),
            ObstacleType.Curtain => (byte)rng.Next(1, colorCount + 1),
            ObstacleType.PotionBottle => ObstacleRules.GetDefaultState(ObstacleType.PotionBottle),
            _ => 0  // BoardInitializer handles default states for other types
        };
    }

    private static void GenerateObjectives(
        LevelConfig config, PlacementPlan plan, EffectivePool pool,
        float difficulty, XorShift64 rng, StringBuilder notes)
    {
        var candidates = new List<LevelObjective>();

        // Always offer tile collection as a candidate
        if (HasLayer(pool.ObjectiveLayers, ObjectiveTargetLayer.Tile))
        {
            int color = rng.Next(1, plan.ColorCount + 1);
            int target = 8 + (int)(difficulty * 17); // 8-25 based on difficulty
            candidates.Add(new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = color,
                TargetCount = target
            });
        }

        // Obstacle objectives: clear placed obstacles
        if (HasLayer(pool.ObjectiveLayers, ObjectiveTargetLayer.Obstacle))
        {
            var obstacleCounts = CountByType(plan.Obstacles);
            foreach (var (type, count) in obstacleCounts)
            {
                candidates.Add(new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Obstacle,
                    ElementType = (int)type,
                    TargetCount = count
                });
            }
        }

        // Ground objectives: clear placed grounds
        if (HasLayer(pool.ObjectiveLayers, ObjectiveTargetLayer.Ground))
        {
            var groundCounts = CountGroundByType(plan.Grounds);
            foreach (var (type, count) in groundCounts)
            {
                candidates.Add(new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)type,
                    TargetCount = count
                });
            }
        }

        // Cover objectives: clear placed covers
        if (HasLayer(pool.ObjectiveLayers, ObjectiveTargetLayer.Cover))
        {
            var coverCounts = CountCoverByType(plan.Covers);
            foreach (var (type, count) in coverCounts)
            {
                candidates.Add(new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Cover,
                    ElementType = (int)type,
                    TargetCount = count
                });
            }
        }

        // Shuffle and pick up to MaxObjectives
        ShuffleObj(candidates, rng);
        int objCount = Math.Min(pool.MaxObjectives, candidates.Count);
        objCount = Math.Max(1, objCount); // At least 1

        notes.Append("Objectives: ");
        for (int i = 0; i < objCount && i < candidates.Count; i++)
        {
            config.Objectives[i] = candidates[i];
            notes.Append($"[{candidates[i].TargetLayer}:{candidates[i].ElementType}x{candidates[i].TargetCount}] ");
        }
        notes.AppendLine();
    }

    private static bool HasLayer(ObjectiveTargetLayer[] layers, ObjectiveTargetLayer target)
    {
        foreach (var l in layers)
            if (l == target) return true;
        return false;
    }

    private static List<(ObstacleType, int)> CountByType(List<(ObstacleType Type, byte Stage, byte State)> list)
    {
        var counts = new Dictionary<ObstacleType, int>();
        foreach (var (t, _, _) in list)
            counts[t] = counts.GetValueOrDefault(t) + 1;
        var result = new List<(ObstacleType, int)>();
        foreach (var kv in counts) result.Add((kv.Key, kv.Value));
        return result;
    }

    private static List<(GroundType, int)> CountGroundByType(List<(GroundType Type, byte Health)> list)
    {
        var counts = new Dictionary<GroundType, int>();
        foreach (var (t, _) in list)
            counts[t] = counts.GetValueOrDefault(t) + 1;
        var result = new List<(GroundType, int)>();
        foreach (var kv in counts) result.Add((kv.Key, kv.Value));
        return result;
    }

    private static List<(CoverType, int)> CountCoverByType(List<(CoverType Type, byte Health)> list)
    {
        var counts = new Dictionary<CoverType, int>();
        foreach (var (t, _) in list)
            counts[t] = counts.GetValueOrDefault(t) + 1;
        var result = new List<(CoverType, int)>();
        foreach (var kv in counts) result.Add((kv.Key, kv.Value));
        return result;
    }

    private static void Shuffle(List<int> list, XorShift64 rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void ShuffleObj(List<LevelObjective> list, XorShift64 rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>
/// Result of level generation, including the config and design metadata.
/// </summary>
public sealed class LevelGeneratorResult
{
    public LevelConfig Config { get; set; } = new();
    public int LevelNumber { get; set; }
    public string PhaseName { get; set; } = "";
    public RhythmCategory Rhythm { get; set; }
    public float Difficulty { get; set; }
    public string DesignNotes { get; set; } = "";

    /// <summary>Obstacle types placed in this level.</summary>
    public List<ObstacleType> UsedObstacles { get; set; } = new();
    /// <summary>Cover types placed in this level.</summary>
    public List<CoverType> UsedCovers { get; set; } = new();
    /// <summary>Ground types placed in this level.</summary>
    public List<GroundType> UsedGrounds { get; set; } = new();
    /// <summary>Board shape used.</summary>
    public string Shape { get; set; } = "rectangle";

    /// <summary>
    /// Generate the level_XXX.design.md content.
    /// Lists the knowledge docs that should be consulted for this level.
    /// </summary>
    public string GenerateDesignDocument()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Level {LevelNumber} 设计文档");
        sb.AppendLine();

        // ── Summary ──
        sb.AppendLine("## 概览");
        sb.AppendLine();
        sb.AppendLine($"| 属性 | 值 |");
        sb.AppendLine($"|------|-----|");
        sb.AppendLine($"| 阶段 | {PhaseName} |");
        sb.AppendLine($"| 节奏 | {Rhythm} |");
        sb.AppendLine($"| 难度 | {Difficulty:F2} |");
        sb.AppendLine($"| 棋盘 | {Config.Width}×{Config.Height} {Shape} |");
        sb.AppendLine($"| 步数 | {Config.MoveLimit} |");
        sb.AppendLine($"| TargetDifficulty | {Config.TargetDifficulty:F2} |");
        sb.AppendLine();

        // ── Design Intent ──
        sb.AppendLine("## 设计意图");
        sb.AppendLine();
        sb.AppendLine(Rhythm switch
        {
            RhythmCategory.Easy => "休息关：让玩家放松，重建信心。步数充裕，目标简单。",
            RhythmCategory.Hard => "挑战关：考验玩家对已掌握机制的综合运用。步数紧张，需要规划。",
            RhythmCategory.Boss => "Boss 关：阶段性检验，综合使用当前阶段的多种元素。有仪式感的挑战。",
            _ => "普通关：标准难度，平衡的游戏体验。"
        });
        sb.AppendLine();

        // ── Elements Used ──
        sb.AppendLine("## 使用的元素");
        sb.AppendLine();
        if (UsedObstacles.Count > 0)
            sb.AppendLine($"- **障碍物**: {string.Join(", ", UsedObstacles.Distinct())}");
        if (UsedCovers.Count > 0)
            sb.AppendLine($"- **Cover**: {string.Join(", ", UsedCovers.Distinct())}");
        if (UsedGrounds.Count > 0)
            sb.AppendLine($"- **Ground**: {string.Join(", ", UsedGrounds.Distinct())}");
        if (UsedObstacles.Count == 0 && UsedCovers.Count == 0 && UsedGrounds.Count == 0)
            sb.AppendLine("- 纯色块匹配，无特殊元素");
        sb.AppendLine();

        // ── Knowledge References ──
        sb.AppendLine("## 参考知识文档");
        sb.AppendLine();
        sb.AppendLine("制作/迭代本关时应阅读以下文档：");
        sb.AppendLine();
        sb.AppendLine("- `core/matching.md` — 匹配机制");
        sb.AppendLine("- `core/gravity-and-cascade.md` — 掉落与连锁");
        sb.AppendLine("- `core/difficulty-levers.md` — 难度调节");
        if (Rhythm == RhythmCategory.Boss)
            sb.AppendLine("- `patterns/boss-level.md` — Boss 关模板");
        if (UsedObstacles.Count + UsedCovers.Count + UsedGrounds.Count == 0)
            sb.AppendLine("- `patterns/tutorial-intro.md` — 教学关模板");

        foreach (var obs in UsedObstacles.Distinct())
            sb.AppendLine($"- `elements/obstacle-{obs.ToString().ToLowerInvariant()}.md`");
        foreach (var cov in UsedCovers.Distinct())
            sb.AppendLine($"- `elements/cover-{cov.ToString().ToLowerInvariant()}.md`");
        foreach (var gnd in UsedGrounds.Distinct())
            sb.AppendLine($"- `elements/ground-{gnd.ToString().ToLowerInvariant()}.md`");
        sb.AppendLine();

        // ── Objectives ──
        sb.AppendLine("## 目标");
        sb.AppendLine();
        for (int i = 0; i < Config.Objectives.Length; i++)
        {
            var obj = Config.Objectives[i];
            if (obj.TargetCount <= 0) continue;
            sb.AppendLine($"- {obj.TargetLayer} 类型{obj.ElementType} ×{obj.TargetCount}");
        }
        sb.AppendLine();
        if (Config.Objectives.Count(o => o.TargetCount > 0) >= 2)
            sb.AppendLine("参考：`patterns/dual-objective.md` — 双目标组合技巧");
        sb.AppendLine();

        // ── Generation Notes ──
        sb.AppendLine("## 生成参数");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.Append(DesignNotes);
        sb.AppendLine("```");
        sb.AppendLine();

        // ── Iteration Notes (placeholder for human/AI) ──
        sb.AppendLine("## 待迭代");
        sb.AppendLine();
        sb.AppendLine("- [ ] 分析管线验证：胜率是否在目标范围内？");
        sb.AppendLine("- [ ] 死锁率是否可接受？");
        sb.AppendLine("- [ ] 实际游玩体验是否匹配设计意图？");
        sb.AppendLine();

        // ── Design Ideas (placeholder) ──
        sb.AppendLine("## 设计发现");
        sb.AppendLine();
        sb.AppendLine("_（在迭代过程中记录新的设计想法、缺失的机制等）_");
        sb.AppendLine();

        return sb.ToString();
    }
}
