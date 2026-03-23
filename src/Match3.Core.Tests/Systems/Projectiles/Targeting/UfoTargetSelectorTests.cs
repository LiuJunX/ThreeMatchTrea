using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles.Targeting;

public class UfoTargetSelectorTests
{
    private static readonly UfoTargetConfig Config = UfoTargetConfig.Default;

    [Fact]
    public void SelectTarget_PrefersTileObjective_OverNonObjectiveTile()
    {
        // Board: (0,0)=Item1 (non-objective), (1,0)=Item2 (objective)
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
                // Objective: collect Item2
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item2,
                    TargetCount = 5,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2), // origin far from targets
            Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(1, 0), result.Value); // Should pick the objective tile
    }

    [Fact]
    public void SelectTarget_PrefersObstacleObjective_OverTileObjective()
    {
        // Board: (0,0)=Item1 (tile objective), (1,0)=Box obstacle (obstacle objective)
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetObstacle(1, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 });
                // Objective 1: collect Item1
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5,
                    CurrentCount = 0
                };
                // Objective 2: clear Box
                s.ObjectiveProgress[1] = new ObjectiveProgress
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
            Config);

        Assert.NotNull(result);
        // Box obstacle has higher baseValue than Item1 tile, both are tier=3
        Assert.Equal(new Position(1, 0), result.Value);
    }

    [Fact]
    public void SelectTarget_ExcludesUntargetableObstacles()
    {
        // Only Curtain (untargetable) and Mailbox (untargetable) on board
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Curtain, Stage = 1 });
                s.SetObstacle(1, 0, new Obstacle { Type = ObstacleType.Mailbox, Stage = 1 });
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config);

        Assert.Null(result); // No valid targets
    }

    [Fact]
    public void SelectTarget_CanTargetSafeObstacle()
    {
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Safe, Stage = 3 });
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value);
    }

    [Fact]
    public void SelectTarget_CoverBlocksPenetration_EvaluatesCoverNotTileBelow()
    {
        // Cell (0,0): Cage cover + Item1 tile (objective)
        // Cell (1,0): Item1 tile (objective, no cover)
        // Both are objectives, but (0,0) has Cage blocking -> evaluates as cover (tier=2)
        // (1,0) is direct tile objective -> tier=3
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetCover(0, 0, new Cover { Type = CoverType.Cage, Health = 1 });
                s.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config);

        Assert.NotNull(result);
        // (1,0) has tile objective -> tier=3 > (0,0) cage cover -> tier=2
        Assert.Equal(new Position(1, 0), result.Value);
    }

    [Fact]
    public void SelectTarget_ReturnsNull_WhenNoValidTargets()
    {
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(1, 1),
            Config);

        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_LastObjective_DoesNotFalsePositive_OnDifferentObjectiveCell()
    {
        // Objective A (Item1, remaining=1) and Objective B (Box, remaining=10)
        // Cell has Box (objective B target) but NOT Item1
        // Should NOT get tier=4 -- the "last target" is Item1, not Box
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0)); // objective A target
                s.SetObstacle(1, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 }); // objective B target
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5,
                    CurrentCount = 4 // remaining=1 -> urgent
                };
                s.ObjectiveProgress[1] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Obstacle,
                    ElementType = (int)ObstacleType.Box,
                    TargetCount = 10,
                    CurrentCount = 0 // remaining=10, not urgent
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config);

        Assert.NotNull(result);
        // Item1 at (0,0) should be selected -- it's the ACTUAL last target
        Assert.Equal(new Position(0, 0), result.Value);
    }

    [Fact]
    public void SelectTarget_LastObjectiveTarget_GetsTier4()
    {
        // Two tiles: Item1 (objective, remaining=1) and Item2 (objective, remaining=5)
        var state = new GameStateBuilder()
            .WithSize(3, 3)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
                // Objective 1: Item1, only 1 remaining -> last target -> tier=4
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5,
                    CurrentCount = 4  // remaining = 1
                };
                // Objective 2: Item2, many remaining -> tier=3
                s.ObjectiveProgress[1] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item2,
                    TargetCount = 10,
                    CurrentCount = 0
                };
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state,
            new Position(2, 2),
            Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value); // Last target gets tier=4
    }
}
