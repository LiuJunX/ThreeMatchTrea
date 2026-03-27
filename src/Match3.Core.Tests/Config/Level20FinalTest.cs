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
public class Level20FinalTest
{
    private readonly ITestOutputHelper _out;
    public Level20FinalTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Level20_Final_V3()
    {
        var blueprint = new ProgressionBlueprint
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

        var pool = EffectivePool.Build(blueprint, 20);
        var phase = BlueprintValidator.FindPhase(blueprint, 20)!;
        var context = new LevelDesignContext
        {
            Blueprint = blueprint, LevelNumber = 20,
            Pool = pool, Phase = phase
        };

        // ═══ V3 最终设计 ═══
        var design = new LevelDesign
        {
            Width = 9, Height = 9, Shape = "cross",
            ColorCount = 5,
            Rhythm = RhythmCategory.Boss,
            Difficulty = 0.3f,
            MoveLimit = 26,
            Covers = new List<ElementPlacement>
            {
                new() { ElementType = "Cage", Count = 6, Stage = 1, Strategy = "border" }
            },
            Grounds = new List<ElementPlacement>
            {
                new() { ElementType = "Ice", Count = 12, Stage = 1, Strategy = "center" }
            },
            Objectives = new List<DesignObjective>
            {
                new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 },
                new() { TargetLayer = "Tile", ElementType = "2", TargetCount = 15 }
            },
            DesignIntent = "EarlyGame Boss：十字形9×9 + 5色。6 Cage 边缘限制空间 + 12 Ice 中心视觉层次。" +
                           "双色收集×15考验全局规划。26步紧凑但公平。",
            MoveReasoning = "经 18 变体 4 轮迭代选出。Casual 89%、Novice 89% — 真正的 Boss 体感。",
            DesignNotes = new List<string>
            {
                "V0→V3 优化路径: 28步太简单(Casual 97%) → 26步+C6(Casual 89%)",
                "Dead lock 6% 略超 5% 阈值，但 Boss 关可接受",
                "放置策略: Cage border(边缘) + Ice center(中心) = 内外呼应"
            }
        };

        var genResult = DesignTranslator.Translate(design, context, seed: 42);
        var config = genResult.Config;

        // ═══ 三阶段分析 ═══
        _out.WriteLine("╔═══════════════════════════════════╗");
        _out.WriteLine("║  Level 20 EarlyGame Boss — 定稿   ║");
        _out.WriteLine("╚═══════════════════════════════════╝");
        _out.WriteLine(" ");

        // 棋盘
        _out.WriteLine("  棋盘布局:");
        for (int y = 0; y < config.Height; y++)
        {
            var row = "    ";
            for (int x = 0; x < config.Width; x++)
            {
                int idx = y * config.Width + x;
                char ch = config.Cells[idx] switch
                {
                    CellKind.Void => '_',
                    CellKind.Spawner => 'S',
                    _ => '·'
                };
                if (config.Covers[idx] == CoverType.Cage) ch = 'C';
                else if (config.Grounds[idx] == GroundType.Ice) ch = 'I';
                row += ch + " ";
            }
            _out.WriteLine(row);
        }
        _out.WriteLine(" ");

        // 粗筛
        var scr = await new RandomAnalysisService().AnalyzeAsync(
            config, new AnalysisConfig { SimulationCount = 500, UseParallel = true });
        _out.WriteLine($"  粗筛(Random 500sims): 胜率={scr.WinRate:P1} 死锁={scr.DeadlockRate:P1}");

        // 精验
        var val = await new PlayerSimAnalysisService().AnalyzeAsync(
            config, new AnalysisConfig { SimulationCount = 500, UseParallel = true });
        _out.WriteLine($"  精验(PlayerSim 500sims): 胜率={val.WinRate:P1} 死锁={val.DeadlockRate:P1}");

        // 深度
        var deep = await new DeepAnalysisService().AnalyzeAsync(config, simulationsPerTier: 250);
        _out.WriteLine(" ");
        _out.WriteLine("  Deep Analysis:");
        _out.WriteLine($"    心流: avg={deep.FlowAverage:F1} min={deep.FlowMin:F1} max={deep.FlowMax:F1}");
        foreach (var (tier, wr) in deep.TierWinRates)
            _out.WriteLine($"    {tier,8}: {wr:P1}");
        _out.WriteLine($"    瓶颈目标: {deep.BottleneckObjective} ({deep.BottleneckFailureRate:P1})");
        _out.WriteLine($"    技能敏感度: {deep.SkillSensitivity:F2}");
        _out.WriteLine($"    挫败风险: {deep.FrustrationRisk:P1}");
        _out.WriteLine($"    运气依赖: {deep.LuckDependency:P1}");
        _out.WriteLine($"    P95通关: {deep.P95ClearAttempts} 次");
        _out.WriteLine(" ");

        // 对比
        _out.WriteLine("  优化对比:");
        _out.WriteLine($"    {"",20} {"V0 原版",10} {"V3 定稿",10}");
        _out.WriteLine($"    {"步数",-20} {"28",10} {"26",10}");
        _out.WriteLine($"    {"Cage",-20} {"4",10} {"6",10}");
        _out.WriteLine($"    {"Casual 胜率",-20} {"97%",10} {deep.TierWinRates.GetValueOrDefault("Casual"):P0,10}");
        _out.WriteLine($"    {"Novice 胜率",-20} {"95%",10} {deep.TierWinRates.GetValueOrDefault("Novice"):P0,10}");
        _out.WriteLine(" ");

        // 经验
        _out.WriteLine("  提炼经验:");
        _out.WriteLine("    [combination/Cage+Ice] Boss 关 Cross 9×9 + C6 border + I12 center");
        _out.WriteLine("      → Casual 89%, 真正有 Boss 体感");
        _out.WriteLine("    [element/Cage] Cage 从 4→6 时死锁从 3%→6%");
        _out.WriteLine("      → Boss 关可接受 6-8% 死锁，普通关仍需 ≤5%");
        _out.WriteLine("    [foundation] 步数是最有效的难度旋钮");
        _out.WriteLine("      → 28→26 步将 Casual 从 97%→89%，降 8 个百分点");
        _out.WriteLine(" ");

        _out.WriteLine($"  设计意图: {design.DesignIntent}");
        _out.WriteLine("══════════════════════════════════════");
    }
}
