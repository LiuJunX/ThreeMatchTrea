using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// RuleBasedSpawnModel 单元测试
/// 测试规则驱动的生成点模型
/// </summary>
public class RuleBasedSpawnModelTests
{
    private class StubRandom : IRandom
    {
        private int _value;
        public StubRandom(int value = 0) => _value = value;
        public void SetValue(int value) => _value = value;
        public int Next(int min, int max) => min + (_value % (max - min));
    }

    private class SequentialRandom : IRandom
    {
        private int _counter = 0;
        public int Next(int min, int max) => min + (_counter++ % (max - min));
    }

    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(y * width + x, TileType.None, x, y));
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

        Assert.NotEqual(TileType.None, type);
    }

    [Fact]
    public void Predict_ZeroTileTypes_ReturnsNone()
    {
        var model = new RuleBasedSpawnModel();
        var state = new GameState(3, 3, 0, new StubRandom());
        var context = SpawnContext.Default;

        var type = model.Predict(ref state, 0, in context);

        Assert.Equal(TileType.None, type);
    }

    #endregion

    #region Strategy Tests - Help Mode

    [Fact]
    public void Predict_FailedAttempts_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Red would create a match
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));

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
        Assert.Equal(TileType.Red, type);
    }

    [Fact]
    public void Predict_LastFewMoves_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Blue would create a match
        state.SetTile(0, 0, new Tile(1, TileType.Blue, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Blue, 1, 0));

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
        Assert.Equal(TileType.Blue, type);
    }

    [Fact]
    public void Predict_LowDifficulty_TriggersHelpMode()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Green would create a match
        state.SetTile(0, 0, new Tile(1, TileType.Green, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));

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
        Assert.Equal(TileType.Green, type);
    }

    #endregion

    #region Strategy Tests - Challenge Mode

    [Fact]
    public void Predict_HighDifficulty_AvoidsMatches()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Red would create a match
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));

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
        Assert.NotEqual(TileType.Red, type);
    }

    [Fact]
    public void Predict_PlayerDoingWell_AddsChallenges()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup a board where Yellow would create a match
        state.SetTile(0, 0, new Tile(1, TileType.Yellow, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Yellow, 1, 0));

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
        Assert.NotEqual(TileType.Yellow, type);
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

        Assert.NotEqual(TileType.None, type);
    }

    [Fact]
    public void SpawnModelAdapter_UsesProvidedContext()
    {
        var model = new RuleBasedSpawnModel(new SequentialRandom());
        var state = CreateState(5, 5);

        // Setup for match
        state.SetTile(0, 0, new Tile(1, TileType.Purple, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Purple, 1, 0));

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
        Assert.Equal(TileType.Purple, type);
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

        Assert.NotEqual(TileType.None, type);
    }

    #endregion
}
