using System;
using System.Collections.Generic;
using Match3.Core.AI.Strategies;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.AI;

/// <summary>
/// Static comparers for AI service to avoid Lambda closure allocations.
/// </summary>
internal static class AIComparers
{
    /// <summary>
    /// Compare MovePreview by score descending (valid moves first, then by score).
    /// </summary>
    public static readonly Comparison<MovePreview> ByScoreDescending = static (a, b) =>
    {
        float scoreA = a.IsValidMove ? a.ScoreGained : -1000;
        float scoreB = b.IsValidMove ? b.ScoreGained : -1000;
        return scoreB.CompareTo(scoreA);
    };
}

/// <summary>
/// AI service implementation using high-speed simulation.
/// </summary>
public sealed class AIService : IAIService
{
    private readonly SimulationConfig _config;
    private readonly IPhysicsSimulation _physics;
    private readonly RealtimeRefillSystem _refill;
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IPowerUpHandler _powerUpHandler;
    private readonly Func<IRandom> _randomFactory;
    private readonly BoardHealthAnalyzer _healthAnalyzer = new();
    private readonly DifficultyCalculator _difficultyCalculator = new();

    private IAIStrategy _strategy;

    /// <summary>
    /// Creates a new AI service.
    /// </summary>
    public AIService(
        IPhysicsSimulation physics,
        RealtimeRefillSystem refill,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IPowerUpHandler powerUpHandler,
        Func<IRandom> randomFactory,
        IAIStrategy? strategy = null)
    {
        _config = SimulationConfig.ForAI();
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _refill = refill ?? throw new ArgumentNullException(nameof(refill));
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _matchProcessor = matchProcessor ?? throw new ArgumentNullException(nameof(matchProcessor));
        _powerUpHandler = powerUpHandler ?? throw new ArgumentNullException(nameof(powerUpHandler));
        _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        _strategy = strategy ?? new GreedyStrategy();
    }

    /// <inheritdoc />
    public IReadOnlyList<Move> GetValidMoves(in GameState state)
    {
        var moves = Pools.ObtainList<Move>();

        // Check all horizontal swaps
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);

                if (IsValidSwap(in state, from, to))
                {
                    moves.Add(new Move(from, to));
                }
            }
        }

        // Check all vertical swaps
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);

                if (IsValidSwap(in state, from, to))
                {
                    moves.Add(new Move(from, to));
                }
            }
        }

        return moves;
    }

    /// <inheritdoc />
    public float EvaluateState(in GameState state)
    {
        float score = 0;

        // Base score from current game score
        score += state.Score * 0.1f;

        // Count bombs (valuable)
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Bomb != BombType.None)
                {
                    score += 100f; // Each bomb is valuable
                }
            }
        }

        // Penalize deadlock situations
        var moves = GetValidMoves(in state);
        if (moves.Count == 0)
        {
            score -= 1000f; // Deadlock penalty
        }
        else
        {
            score += moves.Count * 5f; // More options = better
        }
        Pools.Release((List<Move>)moves);

        return score;
    }

    /// <inheritdoc />
    public MovePreview PreviewMove(in GameState state, Move move)
    {
        // Clone state for simulation
        var clonedState = state.Clone();
        clonedState.Random = _randomFactory();

        // Create simulation engine
        using var engine = new SimulationEngine(
            clonedState,
            _config,
            _physics,
            _refill,
            _matchFinder,
            _matchProcessor,
            _powerUpHandler,
            null,
            NullEventCollector.Instance);

        // Apply the move
        engine.ApplyMove(move.From, move.To);

        // Run until stable
        var result = engine.RunUntilStable();

        return new MovePreview
        {
            Move = move,
            TickCount = result.TickCount,
            ScoreGained = result.ScoreGained,
            TilesCleared = result.TilesCleared,
            MatchesProcessed = result.MatchesProcessed,
            BombsActivated = result.BombsActivated,
            MaxCascadeDepth = result.MaxCascadeDepth,
            FinalState = result.FinalState
        };
    }

    /// <inheritdoc />
    public Move? GetBestMove(in GameState state)
    {
        var moves = GetValidMoves(in state);
        if (moves.Count == 0)
        {
            Pools.Release((List<Move>)moves);
            return null;
        }

        Move bestMove = default;
        float bestScore = float.MinValue;

        foreach (var move in moves)
        {
            var preview = PreviewMove(in state, move);
            float score = _strategy.ScoreMove(in state, move, preview);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        Pools.Release((List<Move>)moves);
        return bestMove;
    }

    /// <inheritdoc />
    public DifficultyAnalysis AnalyzeDifficulty(in GameState state)
    {
        var moves = GetValidMoves(in state);
        var previews = Pools.ObtainList<MovePreview>();

        try
        {
            // Preview all moves
            foreach (var move in moves)
            {
                previews.Add(PreviewMove(in state, move));
            }

            Pools.Release((List<Move>)moves);

            // Calculate statistics
            int validMoveCount = 0;
            int bombCreatingMoves = 0;
            long totalScore = 0;
            long maxScore = 0;
            float totalCascade = 0;
            int maxCascade = 0;

            foreach (var preview in previews)
            {
                if (preview.IsValidMove)
                {
                    validMoveCount++;
                    totalScore += preview.ScoreGained;
                    totalCascade += preview.MaxCascadeDepth;

                    if (preview.ScoreGained > maxScore)
                        maxScore = preview.ScoreGained;

                    if (preview.MaxCascadeDepth > maxCascade)
                        maxCascade = preview.MaxCascadeDepth;

                    // Heuristic: large matches likely create bombs
                    if (preview.TilesCleared >= 4)
                        bombCreatingMoves++;
                }
            }

            float avgScore = validMoveCount > 0 ? (float)totalScore / validMoveCount : 0;
            float avgCascade = validMoveCount > 0 ? totalCascade / validMoveCount : 0;

            // Calculate difficulty score
            float difficultyScore = _difficultyCalculator.CalculateScore(validMoveCount, avgScore, maxScore, avgCascade);
            var category = _difficultyCalculator.Categorize(validMoveCount, difficultyScore);

            // Get top moves - use static comparer to avoid closure allocation
            previews.Sort(AIComparers.ByScoreDescending);

            // Create a copy of top moves for return (caller owns the result)
            var topMoves = previews.Count > 5 ? new List<MovePreview>(previews.GetRange(0, 5)) : new List<MovePreview>(previews);

            return new DifficultyAnalysis
            {
                ValidMoveCount = validMoveCount,
                BombCreatingMoves = bombCreatingMoves,
                AverageScorePotential = avgScore,
                MaxScorePotential = maxScore,
                AverageCascadeDepth = avgCascade,
                MaxCascadeDepth = maxCascade,
                DifficultyScore = difficultyScore,
                Category = category,
                BestMove = topMoves.Count > 0 && topMoves[0].IsValidMove ? topMoves[0].Move : null,
                TopMoves = topMoves,
                Health = _healthAnalyzer.Analyze(in state)
            };
        }
        finally
        {
            Pools.Release(previews);
        }
    }

    /// <inheritdoc />
    public void SetStrategy(IAIStrategy strategy)
    {
        _strategy = strategy ?? new GreedyStrategy();
    }

    /// <inheritdoc />
    public IReadOnlyList<MovePreview> GetAllMovePreviews(in GameState state)
    {
        var moves = GetValidMoves(in state);
        var previews = new List<MovePreview>(moves.Count);

        foreach (var move in moves)
        {
            previews.Add(PreviewMove(in state, move));
        }

        Pools.Release((List<Move>)moves);
        return previews;
    }

    private static bool IsValidSwap(in GameState state, Position from, Position to)
    {
        // 使用共享的有效性验证
        return GridUtility.IsSwapValid(in state, from, to);
    }

}
