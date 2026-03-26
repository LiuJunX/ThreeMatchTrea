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
/// 完整演示一个新关卡的制作过程（Level 20 EarlyGame Boss）。
/// 走完：设计 → 翻译 → 分析 → 提炼经验 → 修订 → 再分析 全流程。
/// </summary>
[Trait("Category", "Slow")]
public class LevelCreationDemoTest
{
    private readonly ITestOutputHelper _out;

    public LevelCreationDemoTest(ITestOutputHelper output) => _out = output;

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
                EasyRatio = 0.3f, BossEveryN = 10
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

    [Fact]
    public async Task Demo_Level20_BossCreation()
    {
        var blueprint = MakeBlueprint();
        int levelNumber = 20;
        var pool = EffectivePool.Build(blueprint, levelNumber);
        var phase = BlueprintValidator.FindPhase(blueprint, levelNumber)!;

        _out.WriteLine("╔══════════════════════════════════════════════╗");
        _out.WriteLine("║  Level 20 EarlyGame Boss — 完整制作流程演示  ║");
        _out.WriteLine("╚══════════════════════════════════════════════╝");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 1: 阅读约束 + 知识
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 1: 阅读约束 ━━━");
        _out.WriteLine($"  阶段: {phase.Name}");
        _out.WriteLine($"  棋盘: {pool.MinBoardWidth}-{pool.MaxBoardWidth} x {pool.MinBoardHeight}-{pool.MaxBoardHeight}");
        _out.WriteLine($"  形状: {string.Join(", ", pool.Shapes)}");
        _out.WriteLine($"  颜色: {pool.MinColors}-{pool.MaxColors}");
        _out.WriteLine($"  步数: {pool.MinMoves}-{pool.MaxMoves}");
        _out.WriteLine($"  目标胜率: {pool.MinWinRate:P0}-{pool.MaxWinRate:P0}");
        _out.WriteLine($"  可用 Cover: {string.Join(", ", pool.Covers.Keys)}");
        _out.WriteLine($"  可用 Ground: {string.Join(", ", pool.Grounds.Keys)}");
        _out.WriteLine($"  节奏: Boss (20 % 10 == 0)");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 2: 加载积累经验
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 2: 加载积累经验 ━━━");

        // 模拟已有经验（来自前面 Level 11-19 的迭代）
        var accumulatedInsights = new List<DesignInsight>
        {
            new()
            {
                Category = "foundation", Tags = Array.Empty<string>(),
                Title = "Random AI 不主动造炸弹",
                Finding = "纯随机 AI 不会制造炸弹，需要炸弹清除的目标胜率偏低",
                Recommendation = "需要炸弹的目标（如 Cage 清除）改用 Tile 收集目标替代"
            },
            new()
            {
                Category = "foundation", Tags = Array.Empty<string>(),
                Title = "4色+充裕步数=甜点",
                Finding = "4色棋盘匹配空间充裕，24步以上通过率普遍>80%",
                Recommendation = "EarlyGame 默认用 4 色 + 22-28 步"
            },
            new()
            {
                Category = "element", Tags = new[] { "Cage" },
                Title = "Cage 不设为目标",
                Finding = "Cage 清除目标在 Random AI 下胜率≈0%（需要炸弹波及）",
                Recommendation = "Cage 仅做空间约束，不作为通关目标"
            },
            new()
            {
                Category = "element", Tags = new[] { "Ice" },
                Title = "Ice 做装饰比做目标可靠",
                Finding = "Ice 清除目标胜率 47%，改为 Tile 目标后胜率 84.5%",
                Recommendation = "Ice 成片放中心做视觉引导，目标用 Tile 收集"
            },
            new()
            {
                Category = "combination", Tags = new[] { "Cage", "Ice" },
                Title = "Cage+Ice 双目标不可行",
                Finding = "Cage+Ice 各自作为目标时 AI 通过率极低",
                Recommendation = "两者都做空间约束/装饰，目标用 Tile 双色收集"
            }
        };

        // 用 InsightSelector 筛选当前关卡需要的经验
        var poolElements = InsightSelector.CollectPoolElementNames(pool);
        var relevantInsights = InsightSelector.SelectRelevant(accumulatedInsights, pool);
        _out.WriteLine($"  池中元素: {string.Join(", ", poolElements)}");
        _out.WriteLine($"  总经验: {accumulatedInsights.Count} 条");
        _out.WriteLine($"  加载相关: {relevantInsights.Count} 条");
        foreach (var ins in relevantInsights)
            _out.WriteLine($"    [{ins.Category}] {ins.Title}");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 3: 设计 LevelDesign（我充当 LLM）
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 3: LLM 设计决策 ━━━");

        var design = new LevelDesign
        {
            Width = 9, Height = 9,
            Shape = "cross",
            ColorCount = 5,
            Rhythm = RhythmCategory.Boss,
            Difficulty = 0.3f,
            MoveLimit = 28,
            Covers = new List<ElementPlacement>
            {
                new()
                {
                    ElementType = "Cage", Count = 4, Stage = 1,
                    Strategy = "border",
                    // 知识doc: "边缘/半外围，不阻挡核心匹配区"
                }
            },
            Grounds = new List<ElementPlacement>
            {
                new()
                {
                    ElementType = "Ice", Count = 12, Stage = 1,
                    Strategy = "center",
                    // 知识doc: "成片放置如中央区域，视觉清晰"
                }
            },
            Objectives = new List<DesignObjective>
            {
                // 经验: "Cage/Ice 不设为目标，用 Tile 双色收集"
                new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 15 },
                new() { TargetLayer = "Tile", ElementType = "2", TargetCount = 15 }
            },
            DesignIntent = "EarlyGame Boss：十字形地形 + 5色增加匹配难度。" +
                           "Cage 边缘限制空间 + Ice 中心做视觉层次。" +
                           "双色收集目标考验全局规划能力。",
            MoveReasoning = "5色匹配空间受限 + 十字形减少有效面积 + 4 Cage 占边缘位 → 28 步（上限）留足缓冲",
            DesignNotes = new List<string>
            {
                "Boss 关知识：综合检验 Cage+Ice，非 rectangle 形状",
                "经验反馈：Cage/Ice 不作目标，仅做空间约束",
                "十字形 9×9：四角切掉 → 有效面积约 57 格（vs 矩形 81 格）"
            }
        };

        _out.WriteLine($"  意图: {design.DesignIntent}");
        _out.WriteLine($"  棋盘: {design.Width}x{design.Height} {design.Shape}");
        _out.WriteLine($"  颜色: {design.ColorCount}");
        _out.WriteLine($"  步数: {design.MoveLimit}（{design.MoveReasoning}）");
        _out.WriteLine($"  Cage: {design.Covers[0].Count} 个, 策略={design.Covers[0].Strategy}");
        _out.WriteLine($"  Ice: {design.Grounds[0].Count} 个, 策略={design.Grounds[0].Strategy}");
        _out.WriteLine($"  目标: 收集红色×15 + 收集绿色×15");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 4: 翻译 LevelDesign → LevelConfig
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 4: DesignTranslator 翻译 ━━━");

        var context = new LevelDesignContext
        {
            Blueprint = blueprint,
            LevelNumber = levelNumber,
            Pool = pool,
            Phase = phase,
            Insights = relevantInsights
        };

        var genResult = DesignTranslator.Translate(design, context, seed: 42);
        var config = genResult.Config;

        _out.WriteLine($"  实际棋盘: {config.Width}x{config.Height}");
        _out.WriteLine($"  实际步数: {config.MoveLimit}");
        _out.WriteLine($"  Cage 放置: {config.Covers.Count(c => c == CoverType.Cage)} 个");
        _out.WriteLine($"  Ice 放置: {config.Grounds.Count(g => g == GroundType.Ice)} 个");

        // 打印棋盘布局
        _out.WriteLine(" ");
        _out.WriteLine("  棋盘布局（C=Cage, I=Ice, .=空Slot, S=Spawner, _=Void）:");
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
                    _ => '.'
                };
                if (config.Covers[idx] == CoverType.Cage) ch = 'C';
                else if (config.Grounds[idx] == GroundType.Ice) ch = 'I';
                row += ch + " ";
            }
            _out.WriteLine(row);
        }
        _out.WriteLine(" ");
        _out.WriteLine($"  翻译日志:\n{genResult.DesignNotes}");

        // ═══════════════════════════════════════
        // Step 5a: 粗筛 — Random AI（200 sims）
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 5a: 粗筛 — RandomAnalysisService (200 sims) ━━━");

        var screeningService = new RandomAnalysisService();
        var screeningConfig = new AnalysisConfig { SimulationCount = 200, UseParallel = true };
        var screening = await screeningService.AnalyzeAsync(config, screeningConfig);

        bool scrWinOk = screening.WinRate >= phase.MinWinRate && screening.WinRate <= phase.MaxWinRate;
        bool scrDlOk = screening.DeadlockRate <= 0.05f;
        bool screeningPassed = scrWinOk && scrDlOk;

        _out.WriteLine($"  胜率: {screening.WinRate:P1} (目标: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0}) {(scrWinOk ? "OK" : "NG")}");
        _out.WriteLine($"  死锁率: {screening.DeadlockRate:P1} (上限: 5%) {(scrDlOk ? "OK" : "NG")}");
        _out.WriteLine($"  平均步数: {screening.AverageMovesUsed:F1} / {config.MoveLimit}");
        _out.WriteLine($"  结果: {(screeningPassed ? "PASS → 进入精验" : "FAIL → 需要修订")}");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 5b: 精验 — PlayerSim AI（500 sims）
        // ═══════════════════════════════════════
        LevelAnalysisResult? validation = null;
        bool validationPassed = false;
        if (screeningPassed)
        {
            _out.WriteLine("━━━ Step 5b: 精验 — PlayerSimAnalysisService (500 sims) ━━━");

            var validationService = new PlayerSimAnalysisService();
            var validationConfig = new AnalysisConfig { SimulationCount = 500, UseParallel = true };
            validation = await validationService.AnalyzeAsync(config, validationConfig);

            bool valWinOk = validation.WinRate >= phase.MinWinRate && validation.WinRate <= phase.MaxWinRate;
            bool valDlOk = validation.DeadlockRate <= 0.05f;
            validationPassed = valWinOk && valDlOk;

            _out.WriteLine($"  胜率: {validation.WinRate:P1} (目标: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0}) {(valWinOk ? "OK" : "NG")}");
            _out.WriteLine($"  死锁率: {validation.DeadlockRate:P1} {(valDlOk ? "OK" : "NG")}");
            _out.WriteLine($"  平均步数: {validation.AverageMovesUsed:F1} / {config.MoveLimit}");
            _out.WriteLine($"  结果: {(validationPassed ? "PASS → 进入深度分析" : "FAIL → 需要修订")}");
            _out.WriteLine(" ");
        }

        // ═══════════════════════════════════════
        // Step 5c: 深度分析 — DeepAnalysis（7 项指标）
        // ═══════════════════════════════════════
        DeepAnalysisResult? deep = null;
        if (validationPassed)
        {
            _out.WriteLine("━━━ Step 5c: 深度分析 — DeepAnalysisService (250 sims/tier × 4 tiers) ━━━");

            var deepService = new DeepAnalysisService();
            deep = await deepService.AnalyzeAsync(config, simulationsPerTier: 250);

            _out.WriteLine($"  ┌─ 1. 心流曲线: avg={deep.FlowAverage:F1}, min={deep.FlowMin:F1}, max={deep.FlowMax:F1}");
            _out.WriteLine($"  ├─ 2. 分层胜率:");
            foreach (var (tier, wr) in deep.TierWinRates)
                _out.WriteLine($"  │     {tier,8}: {wr:P1}");
            _out.WriteLine($"  ├─ 3. 瓶颈目标: {deep.BottleneckObjective} (失败率 {deep.BottleneckFailureRate:P1})");
            _out.WriteLine($"  ├─ 4. 技能敏感度: {deep.SkillSensitivity:F2} ({(deep.SkillSensitivity > 0.5f ? "技能关" : "运气关")})");
            _out.WriteLine($"  ├─ 5. 挫败风险: {deep.FrustrationRisk:P1} (连续3败概率)");
            _out.WriteLine($"  ├─ 6. 运气依赖: {deep.LuckDependency:P1} (理想 20-40%)");
            _out.WriteLine($"  └─ 7. P95 通关: {deep.P95ClearAttempts} 次");
            _out.WriteLine(" ");
        }

        bool passed = validationPassed; // Deep analysis is informational, not pass/fail
        var analysis = validation ?? screening; // Best available for later use

        // ═══════════════════════════════════════
        // Step 6: 提炼经验
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 6: 提炼经验 ━━━");

        // 模拟 LLM 提炼（实际调 API 时由 LlmInsightExtractor 完成）
        var newInsights = new List<DesignInsight>();

        if (passed)
        {
            newInsights.Add(new DesignInsight
            {
                Category = "combination", Tags = new[] { "Cage", "Ice" },
                Title = "Cage+Ice Boss 关可行配置",
                Finding = $"Cross 9x9 + 5色 + 4 Cage border + 12 Ice center + 双色Tile目标 → {analysis.WinRate:P1} 通过",
                Recommendation = "EarlyGame Boss 可用此模板：十字形+Cage边缘+Ice中心+双色收集",
                Source = "Level 20, attempt 1",
                Metrics = new Dictionary<string, string>
                {
                    ["winRate"] = $"{analysis.WinRate:P1}",
                    ["deadlockRate"] = $"{analysis.DeadlockRate:P1}",
                    ["avgMoves"] = $"{analysis.AverageMovesUsed:F1}/{config.MoveLimit}"
                }
            });
        }
        else
        {
            if (!passed && analysis.WinRate < phase.MinWinRate)
            {
                newInsights.Add(new DesignInsight
                {
                    Category = "foundation", Tags = Array.Empty<string>(),
                    Title = "5色+十字形难度高",
                    Finding = $"Cross 9x9 + 5色胜率仅 {analysis.WinRate:P1}，低于 {phase.MinWinRate:P0}",
                    Recommendation = "Boss 关如果用十字形，考虑降到 4 色或增加步数",
                    Source = "Level 20, attempt 1",
                    Metrics = new Dictionary<string, string>
                    {
                        ["winRate"] = $"{analysis.WinRate:P1}"
                    }
                });
            }
            if (!passed && analysis.WinRate > phase.MaxWinRate)
            {
                newInsights.Add(new DesignInsight
                {
                    Category = "foundation", Tags = Array.Empty<string>(),
                    Title = "Boss 关太简单",
                    Finding = $"胜率 {analysis.WinRate:P1} 超过上限 {phase.MaxWinRate:P0}",
                    Recommendation = "减步数或增加颜色/障碍物",
                    Source = "Level 20, attempt 1",
                    Metrics = new Dictionary<string, string> { ["winRate"] = $"{analysis.WinRate:P1}" }
                });
            }
        }

        foreach (var ins in newInsights)
            _out.WriteLine($"  [{ins.Category}] {ins.Title}: {ins.Finding}");
        if (newInsights.Count == 0)
            _out.WriteLine("  (无新经验)");
        _out.WriteLine(" ");

        // ═══════════════════════════════════════
        // Step 7: 如果失败 → 修订设计
        // ═══════════════════════════════════════
        LevelDesign finalDesign = design;
        LevelAnalysisResult finalAnalysis = analysis;
        int attempts = 1;

        if (!passed)
        {
            _out.WriteLine("━━━ Step 7: 修订设计 ━━━");

            // LLM 根据分析反馈修改设计
            var revised = new LevelDesign
            {
                Width = design.Width, Height = design.Height,
                Shape = design.Shape,
                Rhythm = design.Rhythm,
                Difficulty = design.Difficulty,
                DesignIntent = design.DesignIntent + "（修订版）"
            };

            if (analysis.WinRate < phase.MinWinRate)
            {
                // 太难 → 减颜色或增步数
                revised.ColorCount = 4; // 从 5 降到 4
                revised.MoveLimit = 28;
                revised.Covers = design.Covers; // 保持 Cage
                revised.Grounds = new List<ElementPlacement>
                {
                    new() { ElementType = "Ice", Count = 9, Stage = 1, Strategy = "center" } // 减少 Ice
                };
                revised.Objectives = new List<DesignObjective>
                {
                    new() { TargetLayer = "Tile", ElementType = "1", TargetCount = 12 },
                    new() { TargetLayer = "Tile", ElementType = "2", TargetCount = 12 }
                };
                _out.WriteLine($"  修订: 5色→4色, Ice 12→9, 目标 15→12");
            }
            else if (analysis.WinRate > phase.MaxWinRate)
            {
                // 太简单 → 减步数
                revised.ColorCount = design.ColorCount;
                revised.MoveLimit = Math.Max(phase.MinMoves, design.MoveLimit - 3);
                revised.Covers = design.Covers;
                revised.Grounds = design.Grounds;
                revised.Objectives = design.Objectives;
                _out.WriteLine($"  修订: 步数 {design.MoveLimit}→{revised.MoveLimit}");
            }
            else
            {
                revised = design; // 死锁问题 → 换 seed 再跑
                _out.WriteLine("  修订: 换 seed 重新翻译");
            }

            // 重新翻译+分析
            var genResult2 = DesignTranslator.Translate(revised, context, seed: 12345);
            var analysis2 = await screeningService.AnalyzeAsync(genResult2.Config, screeningConfig);

            bool passed2 = analysis2.WinRate >= phase.MinWinRate &&
                           analysis2.WinRate <= phase.MaxWinRate &&
                           analysis2.DeadlockRate <= 0.05f;
            attempts = 2;

            _out.WriteLine($"  修订后胜率: {analysis2.WinRate:P1} (目标: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0})");
            _out.WriteLine($"  修订后死锁: {analysis2.DeadlockRate:P1}");
            _out.WriteLine($"  结果: {(passed2 ? "PASS ✓" : "FAIL ✗")}");
            _out.WriteLine(" ");

            finalDesign = revised;
            finalAnalysis = analysis2;
            passed = passed2;
        }

        // ═══════════════════════════════════════
        // Step 8: 最终输出
        // ═══════════════════════════════════════
        _out.WriteLine("━━━ Step 8: 最终输出 ━━━");
        _out.WriteLine($"  关卡号: Level {levelNumber}");
        _out.WriteLine($"  阶段: {phase.Name} Boss");
        _out.WriteLine($"  棋盘: {finalDesign.Width}x{finalDesign.Height} {finalDesign.Shape}");
        _out.WriteLine($"  颜色: {finalDesign.ColorCount}");
        _out.WriteLine($"  步数: {finalDesign.MoveLimit}");
        _out.WriteLine($"  元素: Cage×{finalDesign.Covers.Sum(c => c.Count)}(border) + Ice×{finalDesign.Grounds.Sum(g => g.Count)}(center)");
        _out.WriteLine($"  目标: {string.Join(" + ", finalDesign.Objectives.Select(o => $"{o.TargetLayer}/{o.ElementType}×{o.TargetCount}"))}");
        _out.WriteLine(" ");
        _out.WriteLine($"  ┌─ 粗筛(Random): {screening.WinRate:P1}");
        if (validation != null)
            _out.WriteLine($"  ├─ 精验(PlayerSim): {validation.WinRate:P1}");
        if (deep != null)
        {
            _out.WriteLine($"  ├─ 技能敏感度: {deep.SkillSensitivity:F2}");
            _out.WriteLine($"  ├─ 挫败风险: {deep.FrustrationRisk:P1}");
            _out.WriteLine($"  ├─ 运气依赖: {deep.LuckDependency:P1}");
            _out.WriteLine($"  └─ P95 通关: {deep.P95ClearAttempts} 次");
        }
        _out.WriteLine(" ");
        _out.WriteLine($"  尝试次数: {attempts}");
        _out.WriteLine($"  通过: {(passed ? "YES" : "NO")}");
        _out.WriteLine($"  设计意图: {finalDesign.DesignIntent}");
        _out.WriteLine($"  积累经验: {accumulatedInsights.Count + newInsights.Count} 条（本轮新增 {newInsights.Count} 条）");
        _out.WriteLine(" ");
        _out.WriteLine("══════════════════════════════════════════════");
    }
}
