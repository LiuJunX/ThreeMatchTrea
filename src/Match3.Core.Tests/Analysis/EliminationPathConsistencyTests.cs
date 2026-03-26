using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Analysis;

/// <summary>
/// Verifies that RandomAnalysisService and PlayerSimAnalysisService
/// produce consistent win rates on the same level when both use random moves.
/// Regression test for SpawnModel divergence (counter-based vs random).
/// </summary>
[Trait("Category", "Slow")]
public class EliminationPathConsistencyTests
{
    private readonly ITestOutputHelper _output;

    public EliminationPathConsistencyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task IceTutorialLevel_BothServices_WinRatesConverge()
    {
        // Arrange — level_015_ice_tutorial: 8x8, 6 ice tiles, 28 moves
        var levelConfig = CreateIceTutorialLevel();
        var config = new AnalysisConfig
        {
            SimulationCount = 500,
            UseParallel = true,
            Mode = SimulationMode.Random
        };

        // Act
        var legacyService = new RandomAnalysisService();
        var strategyService = new PlayerSimAnalysisService();

        var legacyResult = await legacyService.AnalyzeAsync(levelConfig, config);
        var strategyResult = await strategyService.AnalyzeAsync(levelConfig, config);

        // Log
        _output.WriteLine($"RandomAnalysisService:          WinRate={legacyResult.WinRate:P1} ({legacyResult.WinCount}/{legacyResult.TotalSimulations})");
        _output.WriteLine($"PlayerSimAnalysisService:  WinRate={strategyResult.WinRate:P1} ({strategyResult.WinCount}/{strategyResult.TotalSimulations})");
        _output.WriteLine($"Delta: {System.Math.Abs(legacyResult.WinRate - strategyResult.WinRate):P1}");

        // Assert — both use random moves + random spawn now, so win rates should be close.
        // Allow 10% tolerance for statistical variance at 500 sims.
        float delta = System.Math.Abs(legacyResult.WinRate - strategyResult.WinRate);
        Assert.True(delta < 0.10f,
            $"Win rate gap too large: Legacy={legacyResult.WinRate:P1}, Strategy={strategyResult.WinRate:P1}, delta={delta:P1}");
    }

    private static LevelConfig CreateIceTutorialLevel()
    {
        const int w = 8, h = 8;
        int len = w * h;

        // Grid: pre-placed tile colors (no initial matches)
        var grid = new ElementType[]
        {
            (ElementType)3, (ElementType)1, (ElementType)5, (ElementType)2, (ElementType)4, (ElementType)1, (ElementType)3, (ElementType)5,
            (ElementType)2, (ElementType)4, (ElementType)1, (ElementType)3, (ElementType)5, (ElementType)2, (ElementType)4, (ElementType)1,
            (ElementType)5, (ElementType)3, (ElementType)2, (ElementType)4, (ElementType)1, (ElementType)5, (ElementType)3, (ElementType)2,
            (ElementType)1, (ElementType)2, (ElementType)4, (ElementType)3, (ElementType)5, (ElementType)1, (ElementType)2, (ElementType)4,
            (ElementType)4, (ElementType)5, (ElementType)1, (ElementType)2, (ElementType)3, (ElementType)4, (ElementType)5, (ElementType)1,
            (ElementType)3, (ElementType)1, (ElementType)5, (ElementType)4, (ElementType)2, (ElementType)3, (ElementType)1, (ElementType)5,
            (ElementType)2, (ElementType)4, (ElementType)3, (ElementType)1, (ElementType)5, (ElementType)2, (ElementType)4, (ElementType)3,
            (ElementType)5, (ElementType)3, (ElementType)2, (ElementType)4, (ElementType)1, (ElementType)5, (ElementType)3, (ElementType)2
        };

        // Grounds: 6 ice tiles in a 3x2 block at rows 3-4, cols 3-5
        var grounds = new GroundType[len];
        var groundHealths = new byte[len];
        for (int y = 3; y <= 4; y++)
        {
            for (int x = 3; x <= 5; x++)
            {
                int idx = y * w + x;
                grounds[idx] = GroundType.Ice;
                groundHealths[idx] = 1;
            }
        }

        return new LevelConfig
        {
            Width = w,
            Height = h,
            Grid = grid,
            Grounds = grounds,
            GroundHealths = groundHealths,
            MoveLimit = 28,
            TargetDifficulty = 0.5f,
            Objectives = new[]
            {
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)GroundType.Ice,
                    TargetCount = 6
                }
            }
        };
    }
}
