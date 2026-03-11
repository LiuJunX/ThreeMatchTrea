using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles;

public class ProjectileSystemTests
{
    #region Launch Tests

    [Fact]
    public void Launch_AddsProjectileToActiveList()
    {
        var system = new ProjectileSystem();
        var projectile = CreateTestProjectile(1);

        system.Launch(projectile, 0, 0f, NullEventCollector.Instance);

        Assert.Single(system.ActiveProjectiles);
        Assert.Contains(projectile, system.ActiveProjectiles);
    }

    [Fact]
    public void Launch_EmitsLaunchedEvent()
    {
        var system = new ProjectileSystem();
        var projectile = CreateTestProjectile(1);
        var collector = new BufferedEventCollector();

        system.Launch(projectile, 1, 0.016f, collector);

        var events = collector.GetEvents();
        Assert.Single(events);
        Assert.IsType<ProjectileLaunchedEvent>(events[0]);
    }

    [Fact]
    public void Launch_EventContainsCorrectData()
    {
        var system = new ProjectileSystem();
        var projectile = new UfoProjectile(42, new Position(1, 2), new Position(5, 6));
        var collector = new BufferedEventCollector();

        system.Launch(projectile, 10, 0.16f, collector);

        var evt = (ProjectileLaunchedEvent)collector.GetEvents()[0];
        Assert.Equal(42, evt.ProjectileId);
        Assert.Equal(10, evt.Tick);
        Assert.Equal(0.16f, evt.SimulationTime);
    }

    #endregion

    #region HasActiveProjectiles Tests

    [Fact]
    public void HasActiveProjectiles_FalseWhenEmpty()
    {
        var system = new ProjectileSystem();

        Assert.False(system.HasActiveProjectiles);
    }

    [Fact]
    public void HasActiveProjectiles_TrueAfterLaunch()
    {
        var system = new ProjectileSystem();
        system.Launch(CreateTestProjectile(1), 0, 0f, NullEventCollector.Instance);

        Assert.True(system.HasActiveProjectiles);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_UpdatesAllProjectiles()
    {
        var system = new ProjectileSystem();
        var p1 = CreateTestProjectile(1);
        var p2 = CreateTestProjectile(2);
        var state = CreateTestState();

        system.Launch(p1, 0, 0f, NullEventCollector.Instance);
        system.Launch(p2, 0, 0f, NullEventCollector.Instance);

        var initialPos1 = p1.Position;
        var initialPos2 = p2.Position;

        system.Update(ref state, 0.1f, 1, 0.1f, NullEventCollector.Instance);

        // Both projectiles should have moved
        Assert.NotEqual(initialPos1, p1.Position);
        Assert.NotEqual(initialPos2, p2.Position);
    }

    [Fact]
    public void Update_RemovesInactiveProjectiles()
    {
        var system = new ProjectileSystem();
        var projectile = CreateTestProjectile(1);

        system.Launch(projectile, 0, 0f, NullEventCollector.Instance);
        projectile.Deactivate();

        var state = CreateTestState();
        system.Update(ref state, 0.1f, 1, 0.1f, NullEventCollector.Instance);

        Assert.Empty(system.ActiveProjectiles);
    }

    [Fact]
    public void Update_EmitsImpactEventOnArrival()
    {
        var system = new ProjectileSystem();
        var projectile = new UfoProjectile(1, new Position(0, 0), new Position(0, 0)); // Same position = immediate arrival
        var state = CreateTestState();
        var collector = new BufferedEventCollector();

        system.Launch(projectile, 0, 0f, NullEventCollector.Instance);

        // Run through takeoff and flight
        for (int i = 0; i < 100; i++)
        {
            system.Update(ref state, 0.1f, i, i * 0.1f, collector);
            if (!system.HasActiveProjectiles) break;
        }

        var events = collector.GetEvents();
        Assert.Contains(events, e => e is ProjectileImpactEvent);
    }

    [Fact]
    public void Update_ReturnsAffectedPositions()
    {
        var system = new ProjectileSystem();
        var target = new Position(1, 1);
        var projectile = new UfoProjectile(1, new Position(1, 1), target); // Same position
        var state = CreateTestState();

        system.Launch(projectile, 0, 0f, NullEventCollector.Instance);

        // Run until projectile completes
        HashSet<Position>? lastAffected = null;
        for (int i = 0; i < 100; i++)
        {
            var affected = system.Update(ref state, 0.1f, i, i * 0.1f, NullEventCollector.Instance);
            if (affected.Count > 0)
            {
                lastAffected = affected;
                break;
            }
        }

        Assert.NotNull(lastAffected);
        Assert.Contains(target, lastAffected!);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllProjectiles()
    {
        var system = new ProjectileSystem();
        system.Launch(CreateTestProjectile(1), 0, 0f, NullEventCollector.Instance);
        system.Launch(CreateTestProjectile(2), 0, 0f, NullEventCollector.Instance);

        system.Clear();

        Assert.Empty(system.ActiveProjectiles);
        Assert.False(system.HasActiveProjectiles);
    }

    #endregion

    #region GenerateProjectileId Tests

    [Fact]
    public void GenerateProjectileId_ReturnsUniqueIds()
    {
        var system = new ProjectileSystem();

        var id1 = system.GenerateProjectileId();
        var id2 = system.GenerateProjectileId();
        var id3 = system.GenerateProjectileId();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.NotEqual(id1, id3);
    }

    [Fact]
    public void GenerateProjectileId_StartsFromOne()
    {
        var system = new ProjectileSystem();

        var id = system.GenerateProjectileId();

        Assert.Equal(1, id);
    }

    #endregion

    #region Multiple Projectiles Tests

    [Fact]
    public void System_HandlesMultipleProjectilesIndependently()
    {
        var system = new ProjectileSystem();
        var p1 = new UfoProjectile(1, new Position(0, 0), new Position(7, 7));
        var p2 = new UfoProjectile(2, new Position(7, 0), new Position(0, 7));
        var state = CreateTestState();

        system.Launch(p1, 0, 0f, NullEventCollector.Instance);
        system.Launch(p2, 0, 0f, NullEventCollector.Instance);

        // Update multiple times
        for (int i = 0; i < 10; i++)
        {
            system.Update(ref state, 0.05f, i, i * 0.05f, NullEventCollector.Instance);
        }

        // Both should still be active and moving in different directions
        Assert.Equal(2, system.ActiveProjectiles.Count);
    }

    #endregion

    #region UfoProjectile Payload Tests

    [Fact]
    public void UfoProjectile_DefaultPayload_AffectsSingleTile()
    {
        var projectile = new UfoProjectile(1, new Position(0, 0), new Position(4, 3));
        var state = CreateTestState();

        var affected = projectile.ApplyEffect(ref state);

        Assert.Single(affected);
        Assert.Contains(new Position(4, 3), affected);
    }

    [Fact]
    public void UfoProjectile_RowPayload_AffectsEntireRow()
    {
        var projectile = new UfoProjectile(1, new Position(0, 0), new Position(4, 3))
        { Payload = UfoPayload.Row };
        var state = CreateTestState();

        var affected = projectile.ApplyEffect(ref state);

        Assert.Equal(8, affected.Count);
        for (int x = 0; x < 8; x++)
            Assert.Contains(new Position(x, 3), affected);
    }

    [Fact]
    public void UfoProjectile_ColumnPayload_AffectsEntireColumn()
    {
        var projectile = new UfoProjectile(1, new Position(0, 0), new Position(4, 3))
        { Payload = UfoPayload.Column };
        var state = CreateTestState();

        var affected = projectile.ApplyEffect(ref state);

        Assert.Equal(8, affected.Count);
        for (int y = 0; y < 8; y++)
            Assert.Contains(new Position(4, y), affected);
    }

    [Fact]
    public void UfoProjectile_Area5x5Payload_AffectsArea()
    {
        var projectile = new UfoProjectile(1, new Position(0, 0), new Position(4, 4))
        { Payload = UfoPayload.Area5x5 };
        var state = CreateTestState();

        var affected = projectile.ApplyEffect(ref state);

        // 5x5 centered at (4,4) = 25 tiles
        Assert.Equal(25, affected.Count);
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                Assert.Contains(new Position(4 + dx, 4 + dy), affected);
    }

    [Fact]
    public void UfoProjectile_Area5x5Payload_ClipsToBoard()
    {
        var projectile = new UfoProjectile(1, new Position(4, 4), new Position(0, 0))
        { Payload = UfoPayload.Area5x5 };
        var state = CreateTestState();

        var affected = projectile.ApplyEffect(ref state);

        // 5x5 centered at (0,0), clipped: x=0-2, y=0-2 = 9 tiles
        Assert.Equal(9, affected.Count);
        for (int y = 0; y <= 2; y++)
            for (int x = 0; x <= 2; x++)
                Assert.Contains(new Position(x, y), affected);
    }

    #endregion

    #region Helper Methods

    private UfoProjectile CreateTestProjectile(int id)
    {
        return new UfoProjectile(id, new Position(0, 0), new Position(5, 5));
    }

    private GameState CreateTestState()
    {
        var state = new GameState(8, 8, 6, new StubRandom());
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 8 + x;
                state.SetTile(x, y, new Tile(idx + 1, ElementType.Item1, x, y));
            }
        }
        return state;
    }

    #endregion
}

