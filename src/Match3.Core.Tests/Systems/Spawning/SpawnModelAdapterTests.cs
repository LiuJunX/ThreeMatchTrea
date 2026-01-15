using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

public class SpawnModelAdapterTests
{
    #region Test Doubles

    private class StubRandom : IRandom
    {
        public int Next(int min, int max) => min;
    }

    private class MockSpawnModel : ISpawnModel
    {
        public TileType ReturnType { get; set; } = TileType.Red;
        public int CallCount { get; private set; }
        public int LastSpawnX { get; private set; }
        public SpawnContext LastContext { get; private set; }

        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            CallCount++;
            LastSpawnX = spawnX;
            LastContext = context;
            return ReturnType;
        }
    }

    private static GameState CreateTestState()
    {
        return new GameState(8, 8, 6, new StubRandom());
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithModelOnly_ShouldUseDefaultContext()
    {
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        adapter.GenerateNonMatchingTile(ref state, 0, 0);

        // Default context should have default values
        Assert.Equal(0.5f, mockModel.LastContext.TargetDifficulty);
        Assert.Equal(20, mockModel.LastContext.RemainingMoves);
        Assert.Equal(0f, mockModel.LastContext.GoalProgress);
        Assert.True(mockModel.LastContext.InFlowState);
    }

    [Fact]
    public void Constructor_WithModelAndContext_ShouldUseProvidedContext()
    {
        var mockModel = new MockSpawnModel();
        var customContext = new SpawnContext
        {
            TargetDifficulty = 0.8f,
            RemainingMoves = 5,
            GoalProgress = 0.7f,
            FailedAttempts = 2,
            InFlowState = false
        };
        var adapter = new SpawnModelAdapter(mockModel, customContext);
        var state = CreateTestState();

        adapter.GenerateNonMatchingTile(ref state, 0, 0);

        Assert.Equal(0.8f, mockModel.LastContext.TargetDifficulty);
        Assert.Equal(5, mockModel.LastContext.RemainingMoves);
        Assert.Equal(0.7f, mockModel.LastContext.GoalProgress);
        Assert.Equal(2, mockModel.LastContext.FailedAttempts);
        Assert.False(mockModel.LastContext.InFlowState);
    }

    #endregion

    #region GenerateNonMatchingTile Tests

    [Fact]
    public void GenerateNonMatchingTile_ShouldDelegateToModel()
    {
        var mockModel = new MockSpawnModel { ReturnType = TileType.Blue };
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        var result = adapter.GenerateNonMatchingTile(ref state, 3, 5);

        Assert.Equal(TileType.Blue, result);
        Assert.Equal(1, mockModel.CallCount);
    }

    [Fact]
    public void GenerateNonMatchingTile_ShouldPassCorrectSpawnX()
    {
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        adapter.GenerateNonMatchingTile(ref state, 7, 3);

        Assert.Equal(7, mockModel.LastSpawnX);
    }

    [Theory]
    [InlineData(TileType.Red)]
    [InlineData(TileType.Blue)]
    [InlineData(TileType.Green)]
    [InlineData(TileType.Yellow)]
    [InlineData(TileType.Purple)]
    [InlineData(TileType.Orange)]
    public void GenerateNonMatchingTile_ShouldReturnModelResult(TileType expectedType)
    {
        var mockModel = new MockSpawnModel { ReturnType = expectedType };
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        var result = adapter.GenerateNonMatchingTile(ref state, 0, 0);

        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void GenerateNonMatchingTile_MultipleCalls_ShouldAllDelegate()
    {
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        adapter.GenerateNonMatchingTile(ref state, 0, 0);
        adapter.GenerateNonMatchingTile(ref state, 1, 0);
        adapter.GenerateNonMatchingTile(ref state, 2, 0);

        Assert.Equal(3, mockModel.CallCount);
    }

    #endregion

    #region SetContext Tests

    [Fact]
    public void SetContext_ShouldUpdateContext()
    {
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        // First call with default context
        adapter.GenerateNonMatchingTile(ref state, 0, 0);
        Assert.Equal(0.5f, mockModel.LastContext.TargetDifficulty);

        // Update context
        var newContext = new SpawnContext
        {
            TargetDifficulty = 0.9f,
            RemainingMoves = 3,
            GoalProgress = 0.95f,
            FailedAttempts = 0,
            InFlowState = true
        };
        adapter.SetContext(newContext);

        // Second call should use new context
        adapter.GenerateNonMatchingTile(ref state, 0, 0);
        Assert.Equal(0.9f, mockModel.LastContext.TargetDifficulty);
        Assert.Equal(3, mockModel.LastContext.RemainingMoves);
        Assert.Equal(0.95f, mockModel.LastContext.GoalProgress);
    }

    [Fact]
    public void SetContext_MultipleUpdates_ShouldUseLatest()
    {
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        adapter.SetContext(new SpawnContext { TargetDifficulty = 0.1f });
        adapter.SetContext(new SpawnContext { TargetDifficulty = 0.5f });
        adapter.SetContext(new SpawnContext { TargetDifficulty = 0.9f });

        adapter.GenerateNonMatchingTile(ref state, 0, 0);

        Assert.Equal(0.9f, mockModel.LastContext.TargetDifficulty);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Adapter_ShouldWorkWithDifferentBoardSizes()
    {
        var mockModel = new MockSpawnModel { ReturnType = TileType.Green };
        var adapter = new SpawnModelAdapter(mockModel);

        // Small board
        var smallState = new GameState(5, 5, 6, new StubRandom());
        var result1 = adapter.GenerateNonMatchingTile(ref smallState, 2, 2);
        Assert.Equal(TileType.Green, result1);

        // Large board
        var largeState = new GameState(12, 12, 6, new StubRandom());
        var result2 = adapter.GenerateNonMatchingTile(ref largeState, 10, 10);
        Assert.Equal(TileType.Green, result2);
    }

    [Fact]
    public void Adapter_YParameterShouldBeIgnored()
    {
        // SpawnModelAdapter passes spawnX but ignores y (per interface signature)
        var mockModel = new MockSpawnModel();
        var adapter = new SpawnModelAdapter(mockModel);
        var state = CreateTestState();

        // Call with different y values
        adapter.GenerateNonMatchingTile(ref state, 5, 0);
        Assert.Equal(5, mockModel.LastSpawnX);

        adapter.GenerateNonMatchingTile(ref state, 5, 7);
        Assert.Equal(5, mockModel.LastSpawnX);

        // Both calls should pass the same x regardless of y
        Assert.Equal(2, mockModel.CallCount);
    }

    #endregion
}
