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

        // Setup a board where Blue would create a match at drop target (bottom row)
        state.SetTile(0, 4, new Tile(1, ElementType.Item3, 0, 4));
        state.SetTile(1, 4, new Tile(2, ElementType.Item3, 1, 4));

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

        // Setup a board where Green would create a match at drop target (bottom row)
        state.SetTile(0, 4, new Tile(1, ElementType.Item2, 0, 4));
        state.SetTile(1, 4, new Tile(2, ElementType.Item2, 1, 4));

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

    #region Strategy Tests - No Challenge (Only Help, Never Harm)

    [Fact]
    public void Predict_HighDifficulty_UsesBalance_NeverChallenge()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Red would create a match
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.9f, // Very hard — but no Challenge
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 2, in context);

        // High difficulty now uses Balance, not Challenge
        // Should return a valid color (no match avoidance)
        Assert.NotEqual(ElementType.None, type);
    }

    [Fact]
    public void Predict_PlayerDoingWell_NoChallengeMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);
        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 15,
            GoalProgress = 0.8f, // Almost done — no longer triggers Challenge
            FailedAttempts = 0,
            InFlowState = true
        };

        var type = model.Predict(ref state, 0, in context);

        // "Only help, never harm" — should return valid color without match avoidance
        Assert.NotEqual(ElementType.None, type);
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

        // Red is 61% of board but guard triggers SpawnSafe:
        // With ~5 safe colors at the drop target, expected ≈20 out of 100
        Assert.True(redCount < 30,
            $"Red spawned {redCount}/{samples} times; expected <30 with diversity guard active");
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

    #region Only-Help Principle Tests

    [Fact]
    public void Predict_HighDifficulty_UsesBalance_NotChallenge()
    {
        var model = new RuleBasedSpawnModel(StubRandom.WithFixedValue(0));
        var state = CreateState(5, 5);

        // Board with varied colors — not dominant
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
            TargetDifficulty = 0.9f, // High difficulty — Balance, not Challenge
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = false
        };

        var type = model.Predict(ref state, 2, in context);

        // Should return valid color — no match avoidance behavior
        Assert.NotEqual(ElementType.None, type);
        Assert.True(type.IsColor());
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

        // Setup for match at drop target (bottom row)
        state.SetTile(0, 4, new Tile(1, ElementType.Item5, 0, 4));
        state.SetTile(1, 4, new Tile(2, ElementType.Item5, 1, 4));

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

