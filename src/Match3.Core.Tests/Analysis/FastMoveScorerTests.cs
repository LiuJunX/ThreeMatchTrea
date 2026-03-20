using Match3.Core.Analysis;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core.Tests.Analysis;

public class FastMoveScorerTests
{
    private static GameState CreateState(int w = 5, int h = 5)
    {
        return new GameState(w, h, 5, new XorShift64(42));
    }

    private static void FillGrid(ref GameState state, ElementType type)
    {
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                state.SetTile(x, y, new Tile(y * state.Width + x, type, x, y));
    }

    // --- Obstacle objective scoring ---

    [Fact]
    public void Move_near_obstacle_scores_higher_with_obstacle_objective()
    {
        var state = CreateState();
        FillGrid(ref state, ElementType.Item1);

        // Place obstacle at (2,2)
        state.SetObstacle(2, 2, new Obstacle { Type = ObstacleType.Box, Stage = 1 });

        // Set obstacle objective
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.Box,
            TargetCount = 1,
            CurrentCount = 0
        };

        // Move adjacent to obstacle (1,2)→(0,2)
        var nearMove = new ValidMove(new Position(1, 2), new Position(0, 2));
        // Move far from obstacle (0,0)→(1,0)
        var farMove = new ValidMove(new Position(0, 0), new Position(1, 0));

        float nearScore = FastMoveScorer.ScoreMove(in state, nearMove, skillLevel: 0.7f);
        float farScore = FastMoveScorer.ScoreMove(in state, farMove, skillLevel: 0.7f);

        Assert.True(nearScore > farScore,
            $"Near-obstacle move ({nearScore:F1}) should score higher than far move ({farScore:F1})");
    }

    [Fact]
    public void Move_near_obstacle_without_objective_no_bonus()
    {
        var state = CreateState();
        FillGrid(ref state, ElementType.Item1);

        state.SetObstacle(2, 2, new Obstacle { Type = ObstacleType.Box, Stage = 1 });

        // Tile objective only — no obstacle objective
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10,
            CurrentCount = 0
        };

        var nearMove = new ValidMove(new Position(1, 2), new Position(0, 2));
        var farMove = new ValidMove(new Position(0, 4), new Position(1, 4));

        float nearScore = FastMoveScorer.ScoreMove(in state, nearMove, skillLevel: 0.7f);
        float farScore = FastMoveScorer.ScoreMove(in state, farMove, skillLevel: 0.7f);

        // Without obstacle objective, near-obstacle shouldn't get obstacle bonus.
        // Scores may still differ due to position bias (bottom/center preference ~15pts max),
        // so we use 30f as a generous threshold that excludes the obstacle bonus (~25f+).
        float diff = Math.Abs(nearScore - farScore);
        Assert.True(diff < 30f,
            $"Without obstacle objective, score difference ({diff:F1}) should be small");
    }

    // --- Ground objective scoring ---

    [Fact]
    public void Move_near_ground_scores_higher_with_ground_objective()
    {
        var state = CreateState();
        FillGrid(ref state, ElementType.Item1);

        state.SetGround(2, 2, new Ground(GroundType.Ice, 1));

        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Ground,
            ElementType = (int)GroundType.Ice,
            TargetCount = 1,
            CurrentCount = 0
        };

        var nearMove = new ValidMove(new Position(2, 2), new Position(2, 3));
        var farMove = new ValidMove(new Position(0, 0), new Position(1, 0));

        float nearScore = FastMoveScorer.ScoreMove(in state, nearMove, skillLevel: 0.7f);
        float farScore = FastMoveScorer.ScoreMove(in state, farMove, skillLevel: 0.7f);

        Assert.True(nearScore > farScore,
            $"Near-ground move ({nearScore:F1}) should score higher than far move ({farScore:F1})");
    }
}
