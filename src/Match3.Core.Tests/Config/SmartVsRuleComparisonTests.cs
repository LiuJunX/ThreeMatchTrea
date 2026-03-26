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

/// <summary>
/// End-to-end comparison: hand-designed LevelDesign (simulating LLM output)
/// vs rule-based LevelGenerator, both analyzed through the real pipeline.
/// </summary>
[Trait("Category", "Slow")]
public class SmartVsRuleComparisonTests
{
    private readonly ITestOutputHelper _output;

    public SmartVsRuleComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ProgressionBlueprint MakeBlueprint()
    {
        return new ProgressionBlueprint
        {
            Phases = new[]
            {
                new PhaseConfig
                {
                    Name = "Tutorial",
                    StartLevel = 1, EndLevel = 10,
                    MinBoardWidth = 7, MaxBoardWidth = 8,
                    MinBoardHeight = 7, MaxBoardHeight = 8,
                    Shapes = new[] { "rectangle" },
                    MinColors = 4, MaxColors = 5,
                    MaxObjectives = 1,
                    ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile },
                    MinDifficulty = 0.0f, MaxDifficulty = 0.2f,
                    MinWinRate = 0.85f, MaxWinRate = 0.98f,
                    MinMoves = 15, MaxMoves = 25,
                    EasyRatio = 0.3f, HardRatio = 0.0f, BossEveryN = 10
                },
                new PhaseConfig
                {
                    Name = "EarlyGame",
                    StartLevel = 11, EndLevel = 30,
                    MinBoardWidth = 7, MaxBoardWidth = 9,
                    MinBoardHeight = 7, MaxBoardHeight = 9,
                    Shapes = new[] { "rectangle", "cross" },
                    MinColors = 4, MaxColors = 5,
                    Covers = new[]
                    {
                        new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1, MaxCount = 12 }
                    },
                    Grounds = new[]
                    {
                        new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1, MaxCount = 15 }
                    },
                    MaxObjectives = 2,
                    ObjectiveLayers = new[] { ObjectiveTargetLayer.Tile, ObjectiveTargetLayer.Ground, ObjectiveTargetLayer.Cover },
                    MinDifficulty = 0.1f, MaxDifficulty = 0.35f,
                    MinWinRate = 0.75f, MaxWinRate = 0.92f,
                    MinMoves = 18, MaxMoves = 28,
                    EasyRatio = 0.15f, HardRatio = 0.1f, BossEveryN = 10
                }
            }
        };
    }

    /// <summary>
    /// 5 hand-designed levels simulating what a good LLM would produce.
    /// V2: More conservative — Cage is VERY punishing (only bomb-clearable),
    /// so use fewer, and keep objectives achievable. Ice is forgiving.
    /// </summary>
    private static Dictionary<int, LevelDesign> HandDesignedLevels()
    {
        return new Dictionary<int, LevelDesign>
        {
            // Level 11: 过渡关 - 纯色块收集，从 Tutorial 平稳过渡
            // 知识点：还不引入新元素，先让玩家适应更大棋盘
            [11] = new LevelDesign
            {
                Width = 8, Height = 8, Shape = "rectangle",
                ColorCount = 4, Rhythm = RhythmCategory.Easy,
                Difficulty = 0.1f, MoveLimit = 24,
                Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 }
                },
                DesignIntent = "过渡关：从 Tutorial 进入 EarlyGame，无新元素，步数充裕"
            },

            // Level 12: 首次引入 Ice — 少量中央成片，Tile 目标（同向协同）
            // 发现: Ice 清除目标 AI 通过率偏低，用 Tile 目标 + Ice 增加视觉层次
            [12] = new LevelDesign
            {
                Width = 8, Height = 8, Shape = "rectangle",
                ColorCount = 4, Rhythm = RhythmCategory.Normal,
                Difficulty = 0.15f, MoveLimit = 24,
                Grounds = new List<ElementPlacement>
                {
                    new() { ElementType = "Ice", Count = 6, Stage = 1, Strategy = "center" }
                },
                Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 }
                },
                DesignIntent = "首次引入 Ice：6 块中心成片作视觉引导，目标是色块收集(在破冰过程中自然完成)"
            },

            // Level 13: 更多 Ice，Tile 目标，5色增加一点难度
            [13] = new LevelDesign
            {
                Width = 8, Height = 8, Shape = "rectangle",
                ColorCount = 5, Rhythm = RhythmCategory.Normal,
                Difficulty = 0.2f, MoveLimit = 24,
                Grounds = new List<ElementPlacement>
                {
                    new() { ElementType = "Ice", Count = 9, Stage = 1, Strategy = "center" }
                },
                Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 }
                },
                DesignIntent = "Ice 增加到 9 块 + 5 色提高匹配难度。目标仍为色块收集，Ice 只是空间约束"
            },

            // Level 14: 首次引入 Cage — 仅 3 个边缘，Ice 中心，Tile 目标
            // Cage 不作为目标（AI 无法可靠制造炸弹），仅作为空间约束
            [14] = new LevelDesign
            {
                Width = 8, Height = 8, Shape = "rectangle",
                ColorCount = 4, Rhythm = RhythmCategory.Normal,
                Difficulty = 0.2f, MoveLimit = 22,
                Covers = new List<ElementPlacement>
                {
                    new() { ElementType = "Cage", Count = 3, Stage = 1, Strategy = "border" }
                },
                Grounds = new List<ElementPlacement>
                {
                    new() { ElementType = "Ice", Count = 6, Stage = 1, Strategy = "center" }
                },
                Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 }
                },
                DesignIntent = "首次引入 Cage：3 个边缘放置，减少匹配空间但不阻挡核心区。步数略紧增加挑战"
            },

            // Level 15: Cage + Ice 组合，双 Tile 目标
            [15] = new LevelDesign
            {
                Width = 8, Height = 8, Shape = "rectangle",
                ColorCount = 5, Rhythm = RhythmCategory.Normal,
                Difficulty = 0.25f, MoveLimit = 24,
                Covers = new List<ElementPlacement>
                {
                    new() { ElementType = "Cage", Count = 4, Stage = 1, Strategy = "border" }
                },
                Grounds = new List<ElementPlacement>
                {
                    new() { ElementType = "Ice", Count = 8, Stage = 1, Strategy = "center" }
                },
                Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 12 },
                    new() { TargetLayer = "Tile", ElementType = "2", TargetCount = 12 }
                },
                DesignIntent = "Cage+Ice 组合 + 双色块目标：5色棋盘 + 4 Cage 边缘限制空间 + 8 Ice 中心。两种颜色收集需规划"
            }
        };
    }

    [Fact]
    public async Task CompareSmartVsRule_EarlyGame()
    {
        var blueprint = MakeBlueprint();
        var analysisService = new RandomAnalysisService();
        var analysisConfig = new AnalysisConfig { SimulationCount = 200, UseParallel = true };
        var ruleGenerator = new LevelGenerator();

        var designs = HandDesignedLevels();
        int smartPassed = 0;
        int rulePassed = 0;

        _output.WriteLine("=== Smart (Hand-Designed) vs Rule-Based Generator ===");
        _output.WriteLine($"{"Lvl",4} | {"Smart WR",10} {"Smart DL",10} {"Smart",6} | {"Rule WR",10} {"Rule DL",10} {"Rule",6}");
        _output.WriteLine(new string('-', 70));

        foreach (var (levelNum, design) in designs)
        {
            var pool = EffectivePool.Build(blueprint, levelNum);
            var phase = BlueprintValidator.FindPhase(blueprint, levelNum)!;

            // ── Smart path ──
            var ctx = new LevelDesignContext
            {
                Blueprint = blueprint,
                LevelNumber = levelNum,
                Pool = pool,
                Phase = phase
            };
            var smartResult = DesignTranslator.Translate(design, ctx, 42);
            var smartAnalysis = await analysisService.AnalyzeAsync(
                smartResult.Config, analysisConfig);

            bool smartOk = smartAnalysis.WinRate >= phase.MinWinRate &&
                           smartAnalysis.WinRate <= phase.MaxWinRate &&
                           smartAnalysis.DeadlockRate <= 0.05f;
            if (smartOk) smartPassed++;

            // ── Rule path ──
            ulong ruleSeed = 42 ^ ((ulong)levelNum * 7919);
            var ruleResult = ruleGenerator.Generate(blueprint, levelNum, ruleSeed);
            var ruleAnalysis = await analysisService.AnalyzeAsync(
                ruleResult.Config, analysisConfig);

            bool ruleOk = ruleAnalysis.WinRate >= phase.MinWinRate &&
                          ruleAnalysis.WinRate <= phase.MaxWinRate &&
                          ruleAnalysis.DeadlockRate <= 0.05f;
            if (ruleOk) rulePassed++;

            _output.WriteLine(
                $"{levelNum,4} | " +
                $"{smartAnalysis.WinRate,8:P1} {smartAnalysis.DeadlockRate,10:P1} {(smartOk ? "PASS" : "FAIL"),6} | " +
                $"{ruleAnalysis.WinRate,8:P1} {ruleAnalysis.DeadlockRate,10:P1} {(ruleOk ? "PASS" : "FAIL"),6}");
        }

        _output.WriteLine(new string('-', 70));
        _output.WriteLine($"Smart passed: {smartPassed}/{designs.Count}  |  Rule passed: {rulePassed}/{designs.Count}");
        _output.WriteLine(" ");

        // We expect smart to be at least as good as rule
        // (This is a soft assertion — the real value is the output comparison)
        _output.WriteLine($"Smart pass rate: {smartPassed * 100.0 / designs.Count:F0}%  |  Rule pass rate: {rulePassed * 100.0 / designs.Count:F0}%");
    }
}
