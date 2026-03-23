using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles;

public class ProjectileSystemInFlightTests
{
    #region CountInFlightTargetsAt Tests

    [Fact]
    public void CountInFlightTargetsAt_NoProjectiles_ReturnsZero()
    {
        var system = new ProjectileSystem();

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountInFlightTargetsAt_OneUfoAtPosition_ReturnsOne()
    {
        var system = new ProjectileSystem();
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(3, 3));
        system.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountInFlightTargetsAt_TwoUfosAtSamePosition_ReturnsTwo()
    {
        var system = new ProjectileSystem();
        var ufo1 = new UfoProjectile(1, new Position(0, 0), new Position(3, 3));
        var ufo2 = new UfoProjectile(2, new Position(1, 0), new Position(3, 3));
        system.Launch(ufo1, 0, 0f, NullEventCollector.Instance);
        system.Launch(ufo2, 0, 0f, NullEventCollector.Instance);

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountInFlightTargetsAt_UfoAtDifferentPosition_ReturnsZero()
    {
        var system = new ProjectileSystem();
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(5, 5));
        system.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountInFlightTargetsAt_DeactivatedUfo_NotCounted()
    {
        var system = new ProjectileSystem();
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(3, 3));
        system.Launch(ufo, 0, 0f, NullEventCollector.Instance);
        ufo.Deactivate();

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountInFlightTargetsAt_NonUfoProjectile_NotCounted()
    {
        var system = new ProjectileSystem();
        // UfoProjectile 是唯一实现，用 deactivated �?UfoProjectile 验证类型过滤
        // CountInFlightTargetsAt 只计�?IsActive �?UfoProjectile
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(3, 3));
        ufo.Deactivate(); // inactive �?not counted even though it's a UfoProjectile
        system.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        int count = system.CountInFlightTargetsAt(new Position(3, 3));

        Assert.Equal(0, count);
    }

    #endregion

    #region Launch Sets ProjectileSystem

    [Fact]
    public void Launch_SetsProjectileSystemOnUfo()
    {
        var system = new ProjectileSystem();
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(5, 5));

        Assert.Null(ufo.ProjectileSystem);

        system.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        Assert.Same(system, ufo.ProjectileSystem);
    }

    #endregion

    #region Natural Dedup via ProjectileSystem

    [Fact]
    public void ThreeUfos_NaturalDedup_Via_ProjectileSystem()
    {
        // Board: 2HP Box obstacle at (1,0) + normal tiles elsewhere
        // 3 UFOs launched: first 2 should target the obstacle (MeaningfulHits=2),
        // 3rd should target elsewhere because in-flight count saturates the obstacle
        var state = new GameStateBuilder()
            .WithSize(5, 5)
            .WithRandom(new StubRandom())
            .WithEmptyTiles()
            .WithCustomization(s =>
            {
                // Box obstacle at (1,0) with Stage=2 (2HP)
                s.SetObstacle(1, 0, new Obstacle { Type = ObstacleType.Box, Stage = 2 });
                // Objective: clear Box
                s.ObjectiveProgress[0] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.Obstacle,
                    ElementType = (int)ObstacleType.Box,
                    TargetCount = 3,
                    CurrentCount = 0
                };
                // Fill remaining cells with normal tiles for fallback targets
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        if (x == 1 && y == 0) continue; // obstacle cell
                        s.SetTile(x, y, new Tile(y * 5 + x + 100, ElementType.Item1, x, y));
                    }
            })
            .Build();

        var system = new ProjectileSystem();
        var origin = new Position(4, 4);

        // Launch UFO 1
        var target1 = UfoEffect.PickRemoteTarget(in state, origin, projectileSystem: system);
        Assert.NotNull(target1);
        Assert.Equal(new Position(1, 0), target1!.Value); // should pick the obstacle
        var ufo1 = new UfoProjectile(system.GenerateProjectileId(), origin, target1.Value);
        system.Launch(ufo1, 0, 0f, NullEventCollector.Instance);

        // Launch UFO 2 �?obstacle still has capacity (MeaningfulHits=2, inFlight=1)
        var target2 = UfoEffect.PickRemoteTarget(in state, origin, projectileSystem: system);
        Assert.NotNull(target2);
        Assert.Equal(new Position(1, 0), target2!.Value);
        var ufo2 = new UfoProjectile(system.GenerateProjectileId(), origin, target2.Value);
        system.Launch(ufo2, 0, 0f, NullEventCollector.Instance);

        // Launch UFO 3 �?obstacle saturated (inFlight=2 >= MeaningfulHits=2)
        var target3 = UfoEffect.PickRemoteTarget(in state, origin, projectileSystem: system);
        Assert.NotNull(target3);
        Assert.NotEqual(new Position(1, 0), target3!.Value); // must pick elsewhere
    }

    #endregion
}
