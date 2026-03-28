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
    [InlineData(8, 8, 30, 4, "4c/8r+8b/30m")]
    [InlineData(5, 5, 30, 4, "4c/5r+5b/30m")]
    [InlineData(8, 8, 35, 4, "4c/8r+8b/35m")]
    [InlineData(8, 8, 30, 3, "3c/8r+8b/30m")]
    [InlineData(5, 5, 30, 3, "3c/5r+5b/30m")]
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
}
