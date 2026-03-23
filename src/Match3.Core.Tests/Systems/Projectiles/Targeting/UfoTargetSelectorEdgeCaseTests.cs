using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
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
        // Only tile on board is at origin -> no valid target
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s => s.SetTile(1, 1, new Tile(1, ElementType.Item1, 1, 1)))
            .Build();

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1), Config);

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
            in state, new Position(1, 1), Config);

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
            in state, new Position(1, 1), Config);

        Assert.NotNull(result);
        Assert.Equal(new Position(2, 2), result.Value); // (0,0) is locked, so (2,2)
    }

    [Fact]
    public void SelectTarget_InFlightProjectiles_ReducesMeaningfulHits()
    {
        // Only one tile (HP=1 / meaningfulHits=1), with 1 in-flight projectile targeting it -> skip
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0)))
            .Build();

        var mock = new MockProjectileSystem();
        mock.SetInFlightCount(new Position(0, 0), 1);

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2), Config,
            projectileSystem: mock);

        Assert.Null(result); // Only target has in-flight projectile -> exhausted -> no target
    }

    [Fact]
    public void SelectTarget_MultipleInFlight_SameCell()
    {
        // Obstacle with stage=3 (meaningfulHits=3), 2 in-flight -> 1 remaining -> still valid
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 }))
            .Build();

        var mock = new MockProjectileSystem();
        mock.SetInFlightCount(new Position(0, 0), 2);

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2), Config,
            projectileSystem: mock);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value); // Still 1 remaining hit
    }

    [Fact]
    public void SelectTarget_WithInFlight_SkipsExhaustedCell()
    {
        // Two tiles: (0,0) has HP=1, (2,0) has HP=1
        // In-flight projectile targeting (0,0) -> exhausted -> should pick (2,0)
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                s.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
                s.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
            })
            .Build();

        var mock = new MockProjectileSystem();
        mock.SetInFlightCount(new Position(0, 0), 1);

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(1, 1), Config,
            projectileSystem: mock);

        Assert.NotNull(result);
        Assert.Equal(new Position(2, 0), result.Value); // (0,0) exhausted, picks (2,0)
    }

    [Fact]
    public void SelectTarget_WithInFlight_HighHPCellStillValid()
    {
        // Box obstacle with Stage=3 and 1 in-flight projectile -> still 2 remaining -> valid
        var state = new GameStateBuilder().WithSize(3, 3).WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
                s.SetObstacle(0, 0, new Obstacle { Type = ObstacleType.Box, Stage = 3 }))
            .Build();

        var mock = new MockProjectileSystem();
        mock.SetInFlightCount(new Position(0, 0), 1);

        var result = UfoTargetSelector.SelectTarget(
            in state, new Position(2, 2), Config,
            projectileSystem: mock);

        Assert.NotNull(result);
        Assert.Equal(new Position(0, 0), result.Value); // Still valid
    }

    private class MockProjectileSystem : IProjectileSystem
    {
        private readonly Dictionary<Position, int> _inFlightCounts = new();

        public void SetInFlightCount(Position pos, int count) => _inFlightCounts[pos] = count;

        public int CountInFlightTargetsAt(Position pos) =>
            _inFlightCounts.TryGetValue(pos, out var c) ? c : 0;

        public IReadOnlyList<Projectile> ActiveProjectiles => Array.Empty<Projectile>();
        public bool HasActiveProjectiles => false;
        public void Launch(Projectile p, int tick, float simTime, IEventCollector events) { }
        public HashSet<Position> Update(ref GameState state, float dt, int tick, float simTime, IEventCollector events) => new();
        public void Clear() { }
        public int GenerateProjectileId() => 0;
    }
}
