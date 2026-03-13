using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// RuleBasedSpawnModel 单元测试
/// 测试规则驱动的生成点模型
/// </summary>
public class RuleBasedSpawnModelTests
{

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
            }
        }
        return state;
    }

    #region Basic Prediction Tests

    [Fact]
    public void Predict_EmptyBoard_ReturnsValidColor()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState();
        var context = SpawnContext.Default;

        var type = model.Predict(ref state, 0, in context);

        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void Predict_ZeroElementTypes_ReturnsNone()
    {
        var model = new RuleBasedSpawnModel();
        var state = new GameState(3, 3, 0, StubRandom.WithFixedValue(0));
        var context = SpawnContext.Default;

        var type = model.Predict(ref state, 0, in context);

        Assert.Equal(ElementType.None, type);
    }

    #endregion

    #region Strategy Tests - Help Mode

    [Fact]
    public void Predict_FailedAttempts_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Red would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 10,
            GoalProgress = 0.5f,
            FailedAttempts = 3, // Trigger help mode
            InFlowState = false
        };

        var type = model.Predict(ref state, 2, in context);

        // Should spawn Red to create a match (helping the player)
        Assert.Equal(ElementType.Item1, type);
    }

    [Fact]
    public void Predict_LastFewMoves_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Blue would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 2, // Very few moves left
            GoalProgress = 0.5f, // Not close to goal
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 2, in context);

        // Should spawn Blue to create a match
        Assert.Equal(ElementType.Item3, type);
    }

    [Fact]
    public void Predict_LowDifficulty_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Green would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.2f, // Very easy
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 2, in context);

        // Should spawn Green to create a match
        Assert.Equal(ElementType.Item2, type);
    }

    #endregion

    #region Strategy Tests - Challenge Mode

    [Fact]
    public void Predict_HighDifficulty_AvoidsMatches()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Red would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.9f, // Very hard
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 2, in context);

        // Should NOT spawn Red (avoid creating match)
        Assert.NotEqual(ElementType.Item1, type);
    }

    [Fact]
    public void Predict_PlayerDoingWell_AddsChallenges()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Yellow would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item4, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item4, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 15, // Plenty of moves
            GoalProgress = 0.8f, // Almost done
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 2, in context);

        // Should NOT spawn Yellow (challenge the player)
        Assert.NotEqual(ElementType.Item4, type);
    }

    #endregion

    #region Diversity Guard Tests

    [Fact]
    public void Predict_DominantColor_BiasesTowardRare()
    {
        var state = CreateState(5, 5);

        // Red dominates: 8 Red + 1 each of 5 others = 13 total, Red=61%
        int id = 1;
        for (int x = 0; x < 5; x++)
            state.SetTile(x, 4, new Tile(id++, ElementType.Item1, x, 4));
        state.SetTile(0, 3, new Tile(id++, ElementType.Item1, 0, 3));
        state.SetTile(1, 3, new Tile(id++, ElementType.Item1, 1, 3));
        state.SetTile(2, 3, new Tile(id++, ElementType.Item1, 2, 3));
        state.SetTile(3, 3, new Tile(id++, ElementType.Item2, 3, 3));
        state.SetTile(4, 3, new Tile(id++, ElementType.Item3, 4, 3));
        state.SetTile(0, 2, new Tile(id++, ElementType.Item4, 0, 2));
        state.SetTile(1, 2, new Tile(id++, ElementType.Item5, 1, 2));
        state.SetTile(2, 2, new Tile(id++, ElementType.Item6, 2, 2));

        var context = SpawnContext.Default;
        int redCount = 0;
        const int samples = 100;

        for (int i = 0; i < samples; i++)
        {
            var model = new RuleBasedSpawnModel(StubRandom.WithFixedValue(i));
            var type = model.Predict(ref state, 0, in context);
            if (type == ElementType.Item1) redCount++;
        }

        // Red is 61% of board but guard triggers Balance weighting:
        // Red weight=11/261≈4%, so expect ≤15 out of 100
        Assert.True(redCount < 20,
            $"Red spawned {redCount}/{samples} times; expected <20 with diversity guard active");
    }

    [Fact]
    public void Predict_BalancedBoard_DoesNotTriggerGuard()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(6, 3);

        // Place 2 of each color = perfectly balanced, 12 total
        int id = 1;
        var colors = new[] {
            ElementType.Item1, ElementType.Item2, ElementType.Item3,
            ElementType.Item4, ElementType.Item5, ElementType.Item6
        };
        for (int i = 0; i < 6; i++)
        {
            state.SetTile(i, 2, new Tile(id++, colors[i], i, 2));
            state.SetTile(i, 1, new Tile(id++, colors[i], i, 1));
        }

        var context = SpawnContext.Default;

        // Should NOT trigger diversity guard — normal strategy runs
        var type = model.Predict(ref state, 0, in context);
        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void Predict_FewTilesOnBoard_DoesNotTriggerGuard()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Only 2 tiles of same color — too few to judge
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.2f,
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = false
        };

        var type = model.Predict(ref state, 2, in context);

        // Guard should NOT fire — Help strategy spawns Red to match
        Assert.Equal(ElementType.Item1, type);
    }

    #endregion

    #region Challenge Feedback Fix Tests

    [Fact]
    public void Predict_ChallengeMode_DoesNotSpawnMostCommon()
    {
        var model = new RuleBasedSpawnModel(StubRandom.WithFixedValue(0));
        var state = CreateState(5, 5);

        // Red is most common (4 tiles), others have 1 each = 9 total
        // Red at 44% > threshold, but let's keep it under threshold
        // so diversity guard doesn't fire and we test Challenge logic directly
        // Use 3 Red + 2 each of Green/Blue = 7 total, Red=42% > 33% — guard fires
        // Need: just above colorCount threshold but Red not dominant
        // 2 Red + 1 Green + 1 Blue + 1 Yellow + 1 Purple + 1 Orange = 7 total
        // Red = 2/7 = 28% < 33% — guard won't fire
        int id = 1;
        state.SetTile(0, 4, new Tile(id++, ElementType.Item1, 0, 4));
        state.SetTile(1, 4, new Tile(id++, ElementType.Item1, 1, 4));
        state.SetTile(2, 4, new Tile(id++, ElementType.Item2, 2, 4));
        state.SetTile(3, 4, new Tile(id++, ElementType.Item3, 3, 4));
        state.SetTile(4, 4, new Tile(id++, ElementType.Item4, 4, 4));
        state.SetTile(0, 3, new Tile(id++, ElementType.Item5, 0, 3));
        state.SetTile(1, 3, new Tile(id++, ElementType.Item6, 1, 3));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.9f,
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = false
        };

        var type = model.Predict(ref state, 2, in context);

        // Challenge should NOT prefer the most common color (Red)
        // It should prefer the rarest non-matching color
        Assert.NotEqual(ElementType.Item1, type);
    }

    #endregion

    #region Anti-Streak Tests

    [Fact]
    public void Predict_ColumnTopIsSameColor_AvoidsRepeat()
    {
        var model = new RuleBasedSpawnModel(StubRandom.WithFixedValue(0));
        var state = CreateState(5, 5);

        // Column 2: top tile is Blue, and Blue would also match (Help picks Blue)
        // Place two Blues horizontally so Help strategy wants Blue at (2,0)
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        // Column 2 top tile is also Blue (at row 4)
        state.SetTile(2, 4, new Tile(3, ElementType.Item3, 2, 4));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.2f, // Help mode
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = false
        };

        var type = model.Predict(ref state, 2, in context);

        // Help would pick Blue (match), but column top is Blue
        // Anti-streak should deflect to a different color
        Assert.NotEqual(ElementType.Item3, type);
    }

    [Fact]
    public void Predict_ConsecutiveSpawns_SameColumn_VaryColor()
    {
        var rng = new Match3.Random.XorShift64(42);
        var model = new RuleBasedSpawnModel(rng);
        var state = CreateState(8, 8);
        state.Random = rng;
        var context = SpawnContext.Default;
        const int spawns = 20;

        // Fill bottom half so the column has existing tiles
        var seedColors = new[] {
            ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4
        };
        for (int y = 4; y < 8; y++)
            state.SetTile(0, y, new Tile(y, seedColors[y - 4], 0, y));

        int repeatCount = 0;
        ElementType prev = ElementType.None;

        for (int i = 0; i < spawns; i++)
        {
            var type = model.Predict(ref state, 0, in context);

            // Shift top down, place new tile at row 0 (simulates gravity settling)
            var oldTop = state.GetTile(0, 0);
            if (oldTop.Type != ElementType.None)
                state.SetTile(0, 1, oldTop);
            state.SetTile(0, 0, new Tile(i + 100, type, 0, 0));

            if (type == prev) repeatCount++;
            prev = type;
        }

        // With anti-streak, consecutive same-color should be uncommon
        Assert.True(repeatCount < 10,
            $"Same color repeated {repeatCount}/{spawns - 1} transitions — anti-streak not working");
    }

    [Fact]
    public void Predict_EmptyColumn_NoAntiStreak()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);
        // Column 0 is entirely empty — no top color to avoid

        var context = SpawnContext.Default;
        var type = model.Predict(ref state, 0, in context);

        // Should return a valid color without error
        Assert.NotEqual(ElementType.None, type);
    }

    #endregion

    #region Adapter Tests

    [Fact]
    public void SpawnModelAdapter_WrapsModelCorrectly()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var adapter = new SpawnModelAdapter(model);
        var state = CreateState();

        var type = adapter.GenerateNonMatchingTile(ref state, 0, 0);

        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void SpawnModelAdapter_UsesProvidedContext()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup for match
        state.SetTile(0, 0, new Tile(1, ElementType.Item5, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item5, 1, 0));

        // Context that triggers help mode
        var helpContext = new SpawnContext
        {
            TargetDifficulty = 0.1f,
            RemainingMoves = 20,
            GoalProgress = 0f,
            FailedAttempts = 5,
            InFlowState = false
        };

        var adapter = new SpawnModelAdapter(model, helpContext);
        var type = adapter.GenerateNonMatchingTile(ref state, 2, 0);

        // Should create match in help mode
        Assert.Equal(ElementType.Item5, type);
    }

    #endregion

    #region Legacy Adapter Tests

    [Fact]
    public void LegacySpawnModel_WrapsGeneratorCorrectly()
    {
        var generator = new Match3.Core.Systems.Generation.StandardTileGenerator(new SequentialRandom());
        var legacyModel = new LegacySpawnModel(generator);
        var state = CreateState();
        var context = SpawnContext.Default;

        var type = legacyModel.Predict(ref state, 0, in context);

        Assert.NotEqual(ElementType.None, type);
    }

    #endregion
}

