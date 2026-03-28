using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Analysis;

[Trait("Category", "Slow")]
public class L001AnalysisTests
{
    private readonly ITestOutputHelper _output;

    public L001AnalysisTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(12, 12, 28, 4, "4c/12r+12b/28m")]
    public async Task L001_DamBreak_ParameterSweep(int redCount, int blueCount, int moves, int colors, string label)
    {
        var config = CreateL001Config();
        config.MoveLimit = moves;
        config.TileTypesCount = colors;
        config.Objectives = new[]
        {
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = redCount },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = blueCount }
        };
        var service = new DeepAnalysisService();

        var result = await service.AnalyzeAsync(config, simulationsPerTier: 200);

        _output.WriteLine($"=== L001 Dam Break [{label}] ===");
        _output.WriteLine($"Total simulations: {result.TotalSimulations}");
        _output.WriteLine($"Elapsed: {result.ElapsedMs}ms");
        _output.WriteLine("");
        _output.WriteLine("Tier Win Rates:");
        foreach (var tier in result.TierWinRates)
            _output.WriteLine($"  {tier.Key}: {tier.Value:P1}");
        _output.WriteLine("");
        _output.WriteLine($"Frustration Risk: {result.FrustrationRisk:P1}");
        _output.WriteLine($"Luck Dependency: {result.LuckDependency:P1}");
        _output.WriteLine($"Skill Sensitivity: {result.SkillSensitivity:F2}");
        _output.WriteLine($"P95 Clear Attempts: {result.P95ClearAttempts}");
        _output.WriteLine($"Bottleneck: {result.BottleneckObjective ?? "none"}");
        _output.WriteLine($"Flow Range: {result.FlowMin:F2} - {result.FlowMax:F2}");

        if (result.FlowCurve != null && result.FlowCurve.Length > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("Flow Curve (progress per move):");
            for (int i = 0; i < result.FlowCurve.Length; i++)
                _output.WriteLine($"  Move {i + 1}: {result.FlowCurve[i]:F3}");
        }

        // Basic sanity checks
        Assert.False(result.WasCancelled);
        Assert.True(result.TotalSimulations > 0);

        // L001 should be very winnable
        var casualWinRate = result.TierWinRates.GetValueOrDefault("Casual", 0);
        _output.WriteLine($"\nCasual win rate: {casualWinRate:P1} (target: >80%)");
    }

    private static LevelConfig CreateL001Config()
    {
        var config = new LevelConfig(8, 8)
        {
            MoveLimit = 28,
            TileTypesCount = 4,
            TargetDifficulty = 0.1f,
        };

        // Cells: all Slot, bottom corners Void
        for (int i = 0; i < 64; i++)
            config.Cells[i] = CellKind.Slot;
        config.Cells[56] = CellKind.Void; // (0,7)
        config.Cells[63] = CellKind.Void; // (7,7)

        // Grid: rows 0-4 = None (auto-generate), rows 5-7 = KeepEmpty (start empty)
        for (int y = 5; y < 8; y++)
            for (int x = 0; x < 8; x++)
                config.Grid[y * 8 + x] = ElementType.KeepEmpty;

        // Obstacles: Box dam at row 4 (index 32-39)
        for (int x = 0; x < 8; x++)
        {
            int idx = 4 * 8 + x;
            config.Obstacles[idx] = ObstacleType.Box;
            config.ObstacleStages[idx] = 1;
        }

        // Objectives: collect 12 red + 12 blue
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 12
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item3,
                TargetCount = 12
            }
        };

        return config;
    }

    [Fact]
    public async Task L002_BoomCollect_AnalysisReport()
    {
        var config = CreateL002Config();
        var service = new DeepAnalysisService();
        var result = await service.AnalyzeAsync(config, simulationsPerTier: 200);

        _output.WriteLine("=== L002 Boom & Collect ===");
        _output.WriteLine($"Total simulations: {result.TotalSimulations}");
        _output.WriteLine("Tier Win Rates:");
        foreach (var tier in result.TierWinRates)
            _output.WriteLine($"  {tier.Key}: {tier.Value:P1}");
        _output.WriteLine($"Frustration Risk: {result.FrustrationRisk:P1}");
        _output.WriteLine($"Luck Dependency: {result.LuckDependency:P1}");
        _output.WriteLine($"P95 Clear Attempts: {result.P95ClearAttempts}");
        _output.WriteLine($"Bottleneck: {result.BottleneckObjective ?? "none"}");

        Assert.False(result.WasCancelled);
        var casualWinRate = result.TierWinRates.GetValueOrDefault("Casual", 0);
        _output.WriteLine($"\nCasual win rate: {casualWinRate:P1} (target: >95%)");
    }

    [Fact]
    public async Task L003_ChainBoom_AnalysisReport()
    {
        var config = CreateL003Config();
        var service = new DeepAnalysisService();
        var result = await service.AnalyzeAsync(config, simulationsPerTier: 200);

        _output.WriteLine("=== L003 Chain Boom ===");
        _output.WriteLine($"Total simulations: {result.TotalSimulations}");
        _output.WriteLine("Tier Win Rates:");
        foreach (var tier in result.TierWinRates)
            _output.WriteLine($"  {tier.Key}: {tier.Value:P1}");
        _output.WriteLine($"Frustration Risk: {result.FrustrationRisk:P1}");
        _output.WriteLine($"P95 Clear Attempts: {result.P95ClearAttempts}");
        _output.WriteLine($"Bottleneck: {result.BottleneckObjective ?? "none"}");

        var casualWinRate = result.TierWinRates.GetValueOrDefault("Casual", 0);
        _output.WriteLine($"\nCasual win rate: {casualWinRate:P1} (target: >90%)");
    }

    [Fact]
    public async Task L004_BombFusion_AnalysisReport()
    {
        var config = CreateL004Config();
        var service = new DeepAnalysisService();
        var result = await service.AnalyzeAsync(config, simulationsPerTier: 200);

        _output.WriteLine("=== L004 Bomb Fusion ===");
        _output.WriteLine("Tier Win Rates:");
        foreach (var tier in result.TierWinRates)
            _output.WriteLine($"  {tier.Key}: {tier.Value:P1}");
        _output.WriteLine($"Frustration Risk: {result.FrustrationRisk:P1}");
        _output.WriteLine($"P95 Clear Attempts: {result.P95ClearAttempts}");
        _output.WriteLine($"Bottleneck: {result.BottleneckObjective ?? "none"}");

        var casualWinRate = result.TierWinRates.GetValueOrDefault("Casual", 0);
        _output.WriteLine($"\nCasual win rate: {casualWinRate:P1} (target: >90%)");
    }

    private static LevelConfig CreateL004Config()
    {
        int w = 9, h = 9;
        var config = new LevelConfig(w, h)
        {
            MoveLimit = 10,
            TileTypesCount = 3,
            TargetDifficulty = 0.1f,
        };

        // Cross shape: cols 3-5 full height + rows 3-5 full width
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                bool inVerticalArm = x >= 3 && x <= 5;
                bool inHorizontalArm = y >= 3 && y <= 5;
                config.Cells[idx] = (inVerticalArm || inHorizontalArm) ? CellKind.Slot : CellKind.Void;
            }

        // Square5x5 at (4,3) + HorizontalRocket at (4,4) — adjacent vertically
        config.Grid[3 * w + 4] = ElementType.Square5x5;
        config.Grid[4 * w + 4] = ElementType.HorizontalRocket;

        config.Objectives = new[]
        {
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 10 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item2, TargetCount = 10 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = 10 }
        };

        return config;
    }

    private static LevelConfig CreateL003Config()
    {
        int w = 8, h = 7;
        var config = new LevelConfig(w, h)
        {
            MoveLimit = 12,
            TileTypesCount = 4,
            TargetDifficulty = 0.1f,
        };

        for (int i = 0; i < w * h; i++)
            config.Cells[i] = CellKind.Slot;
        config.Cells[0] = CellKind.Void;
        config.Cells[w - 1] = CellKind.Void;
        config.Cells[w * (h - 1)] = CellKind.Void;
        config.Cells[w * h - 1] = CellKind.Void;

        // HorizontalRocket at (3,1), VerticalRocket at (4,5)
        config.Grid[1 * w + 3] = ElementType.HorizontalRocket;
        config.Grid[5 * w + 4] = ElementType.VerticalRocket;

        config.Objectives = new[]
        {
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 8 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item2, TargetCount = 8 },
            new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = 8 }
        };

        return config;
    }

    private static LevelConfig CreateL002Config()
    {
        var config = new LevelConfig(7, 7)
        {
            MoveLimit = 8,
            TileTypesCount = 3,
            TargetDifficulty = 0.1f,
        };

        // Cells: four corners Void
        int w = 7;
        for (int i = 0; i < w * w; i++)
            config.Cells[i] = CellKind.Slot;
        config.Cells[0] = CellKind.Void;           // (0,0)
        config.Cells[w - 1] = CellKind.Void;       // (6,0)
        config.Cells[w * (w - 1)] = CellKind.Void;     // (0,6)
        config.Cells[w * w - 1] = CellKind.Void;   // (6,6)

        // Grid: pre-placed Square5x5 at center (3,3)
        config.Grid[3 * w + 3] = ElementType.Square5x5;

        // Objectives: collect 6 of each of 3 colors
        config.Objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 6
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item2,
                TargetCount = 6
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item3,
                TargetCount = 6
            }
        };

        return config;
    }
}
