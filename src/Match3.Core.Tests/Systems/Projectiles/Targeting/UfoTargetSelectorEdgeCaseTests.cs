using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles.Targeting;

public class UfoTargetSelectorEdgeCaseTests
{
    private static readonly UfoTargetConfig Config = UfoTargetConfig.Default;

    [Fact]
    public void SelectTarget_SkipsOriginPosition()
    {
        // Only tile on board is at origin → no valid target
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1)))
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1),
            ReadOnlySpan<PendingAttack>.Empty, Config);

        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_SkipsVoidCells()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                // Make (0,0) a void cell but put a tile there (shouldn't be targeted)
                s.Cells[0] = CellKind.Void;
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                // Valid tile at (2,2)
                s.SetTile(2, 2, new Tile(2, ElementType.Item1, 2, 2));
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1),
            ReadOnlySpan<PendingAttack>.Empty, Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(2, 2), result.Value);
    }

    [Fact]
    public void SelectTarget_SkipsTargetingLockedCells()
    {
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(2, 2, new Tile(2, ElementType.Item1, 2, 2));
                // Lock (0,0) for targeting
                s.Lock(0, 0, CellLockType.Targeting);
            })
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1),
            ReadOnlySpan<PendingAttack>.Empty, Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(2, 2), result.Value); // (0,0) is locked, so (2,2)
    }

    [Fact]
    public void SelectTarget_PendingAttacks_ReducesMeaningfulHits()
    {
        // Only one tile (HP=1 / meaningfulHits=1), with 1 pending attack on it → skip
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0)))
            .Build();

        var pending = new PendingAttack[]
        {
            new() { GridIndex = 0, HitLayer = 2 } // (0,0) = gridIndex 0
        };

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2),
            pending.AsSpan(), Config);

        Assert.Null(result); // Only target has pending attack → exhausted → no target
    }

    [Fact]
    public void SelectTarget_MultiplePendingAttacks_SameCell()
    {
        // Obstacle with stage=3 (meaningfulHits=3), 2 pending attacks → 1 remaining → still valid
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 }))
            .Build();

        var pending = new PendingAttack[]
        {
            new() { GridIndex = 0, HitLayer = 1 },
            new() { GridIndex = 0, HitLayer = 1 }
        };

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2),
            pending.AsSpan(), Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value); // Still 1 remaining hit
    }

    [Fact]
    public void SelectTarget_WithPendingAttack_SkipsExhaustedCell()
    {
        // Two tiles: (0,0) has HP=1, (2,0) has HP=1
        // PendingAttack on (0,0) → exhausted → should pick (2,0)
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
            })
            .Build();

        var pending = new PendingAttack[]
        {
            new() { GridIndex = 0, HitLayer = 2 } // (0,0) grid index = 0
        };

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1),
            pending.AsSpan(), Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(2, 0), result.Value); // (0,0) exhausted, picks (2,0)
    }

    [Fact]
    public void SelectTarget_WithPendingAttack_HighHPCellStillValid()
    {
        // Box obstacle with Stage=3 and 1 pending attack → still 2 remaining → valid
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 }))
            .Build();

        var pending = new PendingAttack[]
        {
            new() { GridIndex = 0, HitLayer = 1 }
        };

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2),
            pending.AsSpan(), Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value); // Still valid
    }

    [Fact]
    public void SelectTarget_AllCellsExcluded_ReturnsNull()
    {
        var state = new GameStateBuilder().WithSize(2, 2).WithRandom(new StubRandom())
            .WithAllTiles(ElementType.Item1)
            .Build();

        // Exclude all non-origin cells
        var excludeArea = new System.Collections.Generic.HashSet<Position>
        {
            new(0, 0), new(1, 0), new(0, 1)
        };

        // Origin is (1,1) which is auto-excluded
        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1),
            ReadOnlySpan<PendingAttack>.Empty, Config, excludeArea);

        Assert.Null(result);
    }
}
