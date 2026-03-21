using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Systems.Projectiles.Targeting.Capacity;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles.Targeting;

public class CellEvaluatorTests
{
    private static readonly UfoTargetConfig Config = UfoTargetConfig.Default;
    private static readonly ICapacityRule CapRule = MaxCapacityRule.Instance;

    #region Empty / Void cells

    [Fact]
    public void Evaluate_EmptyCell_ReturnsCannotAttack()
    {
        var state = GameStateBuilder.CreateEmptyState(3, 3);
        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);
        Assert.False(result.CanAttack);
    }

    #endregion

    #region Single-layer evaluations

    [Fact]
    public void Evaluate_SingleColorTile_ReturnsNormalTier()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1)))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(1, result.Score.Tier); // Normal tier
        Assert.Equal(Config.TileColorBaseValue, result.Score.BaseValue);
        Assert.Equal(1, result.MeaningfulHits);
    }

    [Fact]
    public void Evaluate_BombTile_ReturnsLowerValue()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.HorizontalRocket, 1, 1)))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(Config.TileBombBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_CollectibleTile_ReturnsCollectibleValue()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.Bird, 1, 1)))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(Config.TileCollectibleBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_ColorBomb_ReturnsCannotAttack()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.ColorBomb, 1, 1)))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);
        Assert.False(result.CanAttack);
    }

    [Fact]
    public void Evaluate_BoxObstacle_ReturnsBlockingLayer()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Box, Stage = 4 }))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(2, result.Score.Tier); // Cover/blocking tier
        Assert.Equal(Config.ObstacleBaseValue, result.Score.BaseValue);
        Assert.Equal(4, result.MeaningfulHits);
    }

    [Fact]
    public void Evaluate_SafeObstacle_IncludesExtraValue()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Safe, Stage = 3 }))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal((ushort)(Config.ObstacleBaseValue + Config.SafeExtraValue), result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_CurtainObstacle_ReturnsCannotAttack()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Curtain, Stage = 1 }))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);
        Assert.False(result.CanAttack);
    }

    [Fact]
    public void Evaluate_MailboxObstacle_ReturnsCannotAttack()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s => s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Mailbox, Stage = 1 }))
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);
        Assert.False(result.CanAttack);
    }

    [Fact]
    public void Evaluate_CageCover_BlocksPenetration()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetCover(1, 1, new Cover { Type = CoverType.Cage, Health = 1 });
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(2, result.Score.Tier); // Cover tier
        // BaseValue should be cover value, NOT tile value (cover blocks penetration)
        Assert.Equal(Config.CoverBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_BubbleCover_DoesNotBlock()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetCover(1, 1, new Cover { Type = CoverType.Bubble, Health = 1, IsDynamic = true });
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        // Bubble doesn't block → tile value should be used, not cover value
        Assert.Equal(Config.TileColorBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_GroundOnly_ReturnsCanAttack()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 }))
            .Build();

        // No tile, just ground → should still be evaluable
        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        Assert.Equal(Config.GroundBaseValue, result.Score.BaseValue);
    }

    #endregion

    #region Multi-layer penetration

    [Fact]
    public void Evaluate_TileAndGround_AccumulatesValues()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 });
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        // Tile + Ground values should accumulate (neither blocks)
        Assert.Equal((ushort)(Config.TileColorBaseValue + Config.GroundBaseValue), result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_ObstacleBlocksTileAndGround()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Box, Stage = 2 });
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 });
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        // Obstacle blocks → only obstacle value, not tile or ground
        Assert.Equal(Config.ObstacleBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_CoverBlocksEverythingBelow()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetCover(1, 1, new Cover { Type = CoverType.Honey, Health = 1 });
                s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Box, Stage = 3 });
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 });
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.True(result.CanAttack);
        // Honey cover blocks → only cover value
        Assert.Equal(Config.CoverBaseValue, result.Score.BaseValue);
    }

    #endregion

    #region Objective detection

    [Fact]
    public void Evaluate_TileObjective_SetsTier3()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5, CurrentCount = 0
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.Equal(Config.TargetTier, result.Score.Tier); // 3
        Assert.Equal(Config.TargetBonus, result.Score.TargetBonus);
    }

    [Fact]
    public void Evaluate_ObstacleObjective_SetsTier3()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetObstacle(1, 1, new Obstacle { Type = ObstacleType.Box, Stage = 2 });
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Obstacle,
                    ElementType = (int)ObstacleType.Box,
                    TargetCount = 3, CurrentCount = 0
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.Equal(Config.TargetTier, result.Score.Tier);
        Assert.Equal(Config.TargetBonus, result.Score.TargetBonus);
    }

    [Fact]
    public void Evaluate_GroundObjective_SetsTier3()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 });
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)GroundType.Ice,
                    TargetCount = 5, CurrentCount = 0
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.Equal(Config.TargetTier, result.Score.Tier);
    }

    [Fact]
    public void Evaluate_CoverObjectiveBlocksPenetration_DoesNotDoubleBonus()
    {
        // Cover is objective AND tile below is also objective
        // TargetBonus should only be given once (cover blocks, so tile's bonus is not counted)
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetCover(1, 1, new Cover { Type = CoverType.Cage, Health = 1 });
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Cover,
                    ElementType = (int)CoverType.Cage,
                    TargetCount = 3, CurrentCount = 0
                };
                s.ObjectiveProgress[1] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5, CurrentCount = 0
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        // Cover blocks → only cover value. TargetBonus from cover, not from tile
        Assert.Equal(Config.TargetBonus, result.Score.TargetBonus);
        Assert.Equal(Config.CoverBaseValue, result.Score.BaseValue);
    }

    [Fact]
    public void Evaluate_TileAndGroundBothObjective_TargetBonusOnce()
    {
        // Both tile and ground are objectives, but targetBonus only given once
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 1 });
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5, CurrentCount = 0
                };
                s.ObjectiveProgress[1] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)GroundType.Ice,
                    TargetCount = 3, CurrentCount = 0
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        // TargetBonus only once (first isTarget layer sets it, second is skipped)
        Assert.Equal(Config.TargetBonus, result.Score.TargetBonus);
    }

    [Fact]
    public void Evaluate_CompletedObjective_NotCounted()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1));
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)ElementType.Item1,
                    TargetCount = 5, CurrentCount = 5 // completed
                };
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        Assert.Equal(1, result.Score.Tier); // Normal tier, not objective
        Assert.Equal(0, result.Score.TargetBonus);
    }

    #endregion

    #region Capacity

    [Fact]
    public void Evaluate_TileAndGround_CapacityIsMax()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1)); // cap=1
                s.SetGround(1, 1, new Ground { Type = GroundType.Ice, Health = 2 }); // cap=2
            })
            .Build();

        var result = CellEvaluator.Evaluate(in state, 1, 1, Config, CapRule);

        // MaxCapacityRule: max(1, 2) = 2
        Assert.Equal(2, result.MeaningfulHits);
    }

    #endregion
}
