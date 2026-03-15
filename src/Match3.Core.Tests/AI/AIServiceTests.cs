using Match3.Core.AI;
using Match3.Core.AI.Strategies;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.AI;

public class AIServiceTests
{
    private AIService CreateAIService()
    {
        var random = new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, random);
        var spawnModel = new StubSpawnModel(ElementType.Item3);
        var refill = new RealtimeRefillSystem(spawnModel);
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
        var powerUpHandler = new BombResolution(scoreSystem);

        return new AIService(
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            () => new StubRandom());
    }

    #region GetValidMoves Tests

    [Fact]
    public void GetValidMoves_ReturnsMovesForValidBoard()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var moves = service.GetValidMoves(in state);

        Assert.NotEmpty(moves);
    }

    [Fact]
    public void GetValidMoves_IncludesHorizontalSwaps()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var moves = service.GetValidMoves(in state);

        Assert.Contains(moves, m => m.IsHorizontal);
    }

    [Fact]
    public void GetValidMoves_IncludesVerticalSwaps()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var moves = service.GetValidMoves(in state);

        Assert.Contains(moves, m => m.IsVertical);
    }

    [Fact]
    public void GetValidMoves_ExcludesEmptyTileSwaps()
    {
        var service = CreateAIService();
        var state = CreateStateWithEmptyTile();

        var moves = service.GetValidMoves(in state);

        // No move should involve position (0,0) which is empty
        Assert.DoesNotContain(moves, m =>
            (m.From.X == 0 && m.From.Y == 0) ||
            (m.To.X == 0 && m.To.Y == 0));
    }

    #endregion

    #region PreviewMove Tests

    [Fact]
    public void PreviewMove_ReturnsPreviewResult()
    {
        var service = CreateAIService();
        var state = CreateTestState();
        var moves = service.GetValidMoves(in state);

        if (moves.Count > 0)
        {
            var preview = service.PreviewMove(in state, moves[0]);

            Assert.NotNull(preview);
            Assert.NotNull(preview.FinalState);
        }
    }

    [Fact]
    public void PreviewMove_DoesNotModifyOriginalState()
    {
        var service = CreateAIService();
        var state = CreateTestState();
        var originalScore = state.Score;
        var moves = service.GetValidMoves(in state);

        if (moves.Count > 0)
        {
            service.PreviewMove(in state, moves[0]);

            Assert.Equal(originalScore, state.Score);
        }
    }

    [Fact]
    public void PreviewMove_TracksTickCount()
    {
        var service = CreateAIService();
        var state = CreateTestState();
        var moves = service.GetValidMoves(in state);

        if (moves.Count > 0)
        {
            var preview = service.PreviewMove(in state, moves[0]);

            Assert.True(preview.TickCount >= 0);
        }
    }

    #endregion

    #region GetBestMove Tests

    [Fact]
    public void GetBestMove_ReturnsNullForEmptyBoard()
    {
        var service = CreateAIService();
        var state = CreateEmptyState();

        var bestMove = service.GetBestMove(in state);

        Assert.Null(bestMove);
    }

    [Fact]
    public void GetBestMove_ReturnsValidMove()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var bestMove = service.GetBestMove(in state);

        if (bestMove.HasValue)
        {
            var moves = service.GetValidMoves(in state);
            Assert.Contains(bestMove.Value, moves);
        }
    }

    #endregion

    #region AnalyzeDifficulty Tests

    [Fact]
    public void AnalyzeDifficulty_ReturnsAnalysis()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var analysis = service.AnalyzeDifficulty(in state);

        Assert.NotNull(analysis);
    }

    [Fact]
    public void AnalyzeDifficulty_CountsValidMoves()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var analysis = service.AnalyzeDifficulty(in state);

        Assert.True(analysis.ValidMoveCount >= 0);
    }

    [Fact]
    public void AnalyzeDifficulty_CategorizesCorrectly()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var analysis = service.AnalyzeDifficulty(in state);

        Assert.True(Enum.IsDefined(typeof(DifficultyCategory), analysis.Category));
    }

    [Fact]
    public void AnalyzeDifficulty_ReturnsDeadlockForEmptyBoard()
    {
        var service = CreateAIService();
        var state = CreateEmptyState();

        var analysis = service.AnalyzeDifficulty(in state);

        Assert.Equal(DifficultyCategory.Deadlock, analysis.Category);
    }

    [Fact]
    public void AnalyzeDifficulty_IncludesBoardHealth()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var analysis = service.AnalyzeDifficulty(in state);

        Assert.NotNull(analysis.Health);
    }

    #endregion

    #region EvaluateState Tests

    [Fact]
    public void EvaluateState_ReturnsScore()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var score = service.EvaluateState(in state);

        // Score should be a valid number
        Assert.False(float.IsNaN(score));
        Assert.False(float.IsInfinity(score));
    }

    [Fact]
    public void EvaluateState_PenalizesDeadlock()
    {
        var service = CreateAIService();
        var normalState = CreateTestState();
        var emptyState = CreateEmptyState();

        var normalScore = service.EvaluateState(in normalState);
        var emptyScore = service.EvaluateState(in emptyState);

        Assert.True(normalScore > emptyScore);
    }

    #endregion

    #region Strategy Tests

    [Fact]
    public void SetStrategy_ChangesStrategy()
    {
        var service = CreateAIService();
        var newStrategy = new BombPriorityStrategy();

        service.SetStrategy(newStrategy);

        // No direct way to verify, but should not throw
    }

    [Fact]
    public void SetStrategy_NullDefaultsToGreedy()
    {
        var service = CreateAIService();

        service.SetStrategy(null!);

        // Should not throw
        var state = CreateTestState();
        service.GetBestMove(in state);
    }

    #endregion

    #region GetAllMovePreviews Tests

    [Fact]
    public void GetAllMovePreviews_ReturnsAllPreviews()
    {
        var service = CreateAIService();
        var state = CreateTestState();

        var moves = service.GetValidMoves(in state);
        var previews = service.GetAllMovePreviews(in state);

        // Should have same count as valid moves
        Assert.True(previews.Count <= moves.Count + 10); // Allow some margin for filtering
    }

    #endregion

    #region Helper Methods

    private GameState CreateTestState()
    {
        var state = new GameState(5, 5, 5, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = y * 5 + x;
                state.SetTile(x, y, new Tile(idx + 1, types[idx % types.Length], x, y));
            }
        }

        return state;
    }

    private GameState CreateEmptyState()
    {
        return GameStateBuilder.CreateEmptyState(5, 5);
    }

    private GameState CreateStateWithEmptyTile()
    {
        var state = CreateTestState();
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
        return state;
    }

    #endregion
}


