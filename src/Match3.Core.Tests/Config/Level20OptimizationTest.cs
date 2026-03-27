using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Config;

[Trait("Category", "Slow")]
public class Level20OptimizationTest
{
    private readonly ITestOutputHelper _out;

    public Level20OptimizationTest(ITestOutputHelper output) => _out = output;

    private static ProgressionBlueprint MakeBlueprint() => new()
    {
        Phases = new[]
        {
            new PhaseConfig
            {
                Name = "Tutorial", StartLevel = 1, EndLevel = 10,
                MinBoardWidth = 7, MaxBoardWidth = 8,
                MinBoardHeight = 7, MaxBoardHeight = 8,
                Shapes = new[] { "rectangle" },
                MinColors = 4, MaxColors = 5,
                MaxObjectives = 1,
                ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile },
                MinDifficulty = 0f, MaxDifficulty = 0.2f,
                MinWinRate = 0.85f, MaxWinRate = 0.98f,
                MinMoves = 15, MaxMoves = 25,
                BossEveryN = 10
            },
            new PhaseConfig
            {
                Name = "EarlyGame", StartLevel = 11, EndLevel = 30,
                MinBoardWidth = 7, MaxBoardWidth = 9,
                MinBoardHeight = 7, MaxBoardHeight = 9,
                Shapes = new[] { "rectangle", "cross" },
                MinColors = 4, MaxColors = 5,
                Covers = new[] { new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 12 } },
                Grounds = new[] { new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 } },
                MaxObjectives = 2,
                ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile, ObjectiveTargetLayer.Ground, ObjectiveTargetLayer.Cover },
                MinDifficulty = 0.1f, MaxDifficulty = 0.35f,
                MinWinRate = 0.75f, MaxWinRate = 0.92f,
                MinMoves = 18, MaxMoves = 28,
                EasyRatio = 0.15f, HardRatio = 0.1f, BossEveryN = 10
            }
        }
    };

    private static LevelDesign MakeVariant(
        int moves, int colorCount, int cageCount, int iceCount,
        string shape = "cross", int targetCount = 15,
        string cageStrategy = "border")
    {
        return new LevelDesign
        {
            Width = 9, Height = 9, Shape = shape,
            ColorCount = colorCount,
            Rhythm = RhythmCategory.Boss,
            Difficulty = 0.3f,
            MoveLimit = moves,
            Covers = new List<ElementPlacement>
            {
                new() { ElementType = "Cage", Count = cageCount, Stage = 1, Strategy = cageStrategy }
            },
            Grounds = new List<ElementPlacement>
            {
                new() { ElementType = "Ice", Count = iceCount, Stage = 1, Strategy = "center" }
            },
            Objectives = new List<DesignObjective>
            {
                new() { TargetLayer = "Tile", ElementType = "1", TargetCount = targetCount },
                new() { TargetLayer = "Tile", ElementType = "2", TargetCount = targetCount }
            },
            DesignIntent = $"Boss variant: {moves}步 {colorCount}色 Cage×{cageCount} Ice×{iceCount}"
        };
    }

    [Fact]
    public async Task OptimizeLevel20_CompareVariants()
    {
        var blueprint = MakeBlueprint();
        var pool = EffectivePool.Build(blueprint, 20);
        var phase = BlueprintValidator.FindPhase(blueprint, 20)!;
        var context = new LevelDesignContext
        {
            Blueprint = blueprint, LevelNumber = 20,
            Pool = pool, Phase = phase
        };

        var screeningService = new RandomAnalysisService();
        var validationService = new PlayerSimAnalysisService();
        var deepService = new DeepAnalysisService();
        var screenCfg = new AnalysisConfig { SimulationCount = 200, UseParallel = true };
        var valCfg = new AnalysisConfig { SimulationCount = 500, UseParallel = true };

        // Round 4: 换 Cage 策略降死锁 + 步数/目标微调
        var variants = new (string Name, LevelDesign Design)[]
        {
            ("V0 原版 28步 C4bdr I12",        MakeVariant(28, 5, 4, 12)),
            ("VA 26步 C6scat I12",           MakeVariant(26, 5, 6, 12, cageStrategy: "scattered")),
            ("VB 25步 C6scat I12",           MakeVariant(25, 5, 6, 12, cageStrategy: "scattered")),
            ("VC 26步 C6scat I14",           MakeVariant(26, 5, 6, 14, cageStrategy: "scattered")),
            ("VD 25步 C5scat I14",           MakeVariant(25, 5, 5, 14, cageStrategy: "scattered")),
            ("VE 25步 C6scat I12 tgt18",     MakeVariant(25, 5, 6, 12, cageStrategy: "scattered", targetCount: 18)),
        };

        _out.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════╗");
        _out.WriteLine("║  Level 20 Boss 优化 — 多变体对比                                                ║");
        _out.WriteLine("╚══════════════════════════════════════════════════════════════════════════════════╝");
        _out.WriteLine(" ");
        _out.WriteLine($"  目标胜率: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0} | 死锁上限: 5%");
        _out.WriteLine(" ");
        _out.WriteLine($"  {"变体",-30} | {"Random",8} {"RnDL",5} {"PSim",8} {"PsDL",5} | {"Nov",6} {"Cas",6} {"Cor",6} {"Exp",6} | {"Skill",6} {"Frust",6} {"Luck",6} {"P95",4} | 判定");
        _out.WriteLine(new string('─', 130));

        foreach (var (name, design) in variants)
        {
            var gen = DesignTranslator.Translate(design, context, seed: 42);

            // Stage 1: Screening
            var scr = await screeningService.AnalyzeAsync(gen.Config, screenCfg);

            // Stage 2: Validation
            var val = await validationService.AnalyzeAsync(gen.Config, valCfg);

            // Stage 3: Deep
            var deep = await deepService.AnalyzeAsync(gen.Config, simulationsPerTier: 250);

            // Tier data
            float novice = deep.TierWinRates.GetValueOrDefault("Novice");
            float casual = deep.TierWinRates.GetValueOrDefault("Casual");
            float core = deep.TierWinRates.GetValueOrDefault("Core");
            float expert = deep.TierWinRates.GetValueOrDefault("Expert");

            // Judgment
            bool scrOk = scr.WinRate >= phase.MinWinRate && scr.WinRate <= phase.MaxWinRate && scr.DeadlockRate <= 0.05f;
            bool valOk = val.WinRate >= phase.MinWinRate && val.WinRate <= phase.MaxWinRate && val.DeadlockRate <= 0.05f;
            // Boss should challenge: Casual 应在 75-92%, 不是 97%
            bool bossChallenge = casual <= 0.92f && novice <= 0.95f;
            string judgment = !scrOk ? "粗筛FAIL"
                : !valOk ? "精验FAIL"
                : bossChallenge ? "BOSS OK"
                : "太简单";

            _out.WriteLine(
                $"  {name,-30} | " +
                $"{scr.WinRate,7:P0} {scr.DeadlockRate,4:P0} {val.WinRate,7:P0} {val.DeadlockRate,4:P0} | " +
                $"{novice,5:P0} {casual,5:P0} {core,5:P0} {expert,5:P0} | " +
                $"{deep.SkillSensitivity,5:F2} {deep.FrustrationRisk,5:P0} {deep.LuckDependency,5:P0} {deep.P95ClearAttempts,4} | " +
                $"{judgment}");
        }

        _out.WriteLine(new string('─', 115));
        _out.WriteLine(" ");
        _out.WriteLine("  判定标准: 粗筛/精验在75%-92%, Casual≤92%, Novice≤95%");
    }
}
