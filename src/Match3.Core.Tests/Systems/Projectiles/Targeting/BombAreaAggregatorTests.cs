using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles.Targeting;

public class BombAreaAggregatorTests
{
    private static readonly UfoTargetConfig Config = UfoTargetConfig.Default;

    [Fact]
    public void RowPayload_PrefersRowWithMoreObjectives()
    {
        // Row 0: 3 objective tiles (Item1)
        // Row 1: 1 objective tile (Item1)
        // Two candidate drop points: (0,0) and (0,1)
        var state = new GameStateBuilder()
            .WithSize(5, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                // Row 0: 3 objectives
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
                s.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
                // Row 1: 1 objective
                s.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1));
                // Objective: Item1
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 10,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(4, 2), // origin far away
            Config,
            payload: UfoPayload.Row);

        Assert.NotNull(result);
        // Should pick a position in row 0 (higher total row value)
        Assert.Equal(0, result.Value.Y);
    }

    [Fact]
    public void ColumnPayload_PrefersColumnWithMoreValue()
    {
        // Column 0: 2 tiles (objectives)
        // Column 1: 1 tile (objective)
        var state = new GameStateBuilder()
            .WithSize(3, 5)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(0, 1, new Tile(2, ElementType.Item1, 0, 1));
                s.SetTile(1, 0, new Tile(3, ElementType.Item1, 1, 0));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 10,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 4),
            Config,
            payload: UfoPayload.Column);

        Assert.NotNull(result);
        Assert.Equal(0, result.Value.X); // Column 0 has more value
    }

    [Fact]
    public void Area5x5Payload_PrefersClusteredTargets()
    {
        // Cluster at (7,7): 4 objective tiles only reachable from nearby 5x5 centers
        // Isolated at (0,0): 1 objective tile only reachable from (0,0) area
        // Board is 9x9 so the two groups are far apart (>4 apart, 5x5 can't cover both)
        var state = new GameStateBuilder()
            .WithSize(9, 9)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                // Cluster in bottom-right
                s.SetTile(6, 6, new Tile(1, ElementType.Item1, 6, 6));
                s.SetTile(7, 6, new Tile(2, ElementType.Item1, 7, 6));
                s.SetTile(6, 7, new Tile(3, ElementType.Item1, 6, 7));
                s.SetTile(7, 7, new Tile(4, ElementType.Item1, 7, 7));
                // Isolated in top-left
                s.SetTile(0, 0, new Tile(5, ElementType.Item1, 0, 0));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 10,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(4, 4), // origin in center
            Config,
            payload: UfoPayload.Area5x5);

        Assert.NotNull(result);
        // Should pick a position that captures the 4-tile cluster, not the 1-tile isolated
        int x = result.Value.X;
        int y = result.Value.Y;
        Assert.True(x >= 5 && y >= 5,
            $"Expected near cluster (5+,5+), got ({x},{y})");
    }

    [Fact]
    public void DefaultPayload_IgnoresBombRange()
    {
        // With Default payload, should pick single best cell (not range evaluation)
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetObstacle(1, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 });
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Obstacle,
                    ElementType = (int)ObstacleType.Box,
                    TargetCount = 3,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config,
            payload: UfoPayload.Default);

        Assert.NotNull(result);
        // Box objective (tier=3, higher baseValue) > Item1 (tier=1)
        Assert.Equal(new Position(1, 0), result.Value);
    }
}
