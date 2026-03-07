using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles;

public class UfoProjectileTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    #region Construction Tests

    [Fact]
    public void Constructor_SetsInitialProperties()
    {
        var origin = new Position(2, 3);
        var target = new Position(5, 6);

        var ufo = new UfoProjectile(1, origin, target);

        Assert.Equal(1, ufo.Id);
        Assert.Equal(origin, ufo.OriginPosition);
        Assert.Equal(target, ufo.TargetGridPosition);
        Assert.True(ufo.IsActive);
        Assert.Equal(UfoPhase.Takeoff, ufo.Phase);
    }

    [Fact]
    public void Constructor_SetsPositionFromOrigin()
    {
        var origin = new Position(2, 3);
        var target = new Position(5, 6);

        var ufo = new UfoProjectile(1, origin, target);

        Assert.Equal(2f, ufo.Position.X);
        Assert.Equal(3f, ufo.Position.Y);
    }

    [Fact]
    public void Constructor_SetsDefaultTargetingMode()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1));

        Assert.Equal(UfoTargetingMode.FixedCell, ufo.TargetingMode);
    }

    [Fact]
    public void Constructor_AcceptsCustomTargetingMode()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1), UfoTargetingMode.Dynamic);

        Assert.Equal(UfoTargetingMode.Dynamic, ufo.TargetingMode);
    }

    #endregion

    #region Takeoff Phase Tests

    [Fact]
    public void Update_TakeoffPhase_MovesVertically()
    {
        var ufo = new UfoProjectile(1, new Position(2, 3), new Position(5, 6));
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        var initialY = ufo.Position.Y;

        ufo.Update(ref state, 0.1f, 1, 0.1f, events);

        // Should move upward (negative Y in screen coords)
        Assert.True(ufo.Position.Y < initialY);
    }

    [Fact]
    public void Update_TakeoffPhase_TransitionsToFlight()
    {
        var ufo = new UfoProjectile(1, new Position(2, 3), new Position(5, 6));
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Run takeoff phase to completion
        for (int i = 0; i < 30; i++)
        {
            ufo.Update(ref state, 0.02f, i, i * 0.02f, events);
        }

        Assert.Equal(UfoPhase.Flight, ufo.Phase);
    }

    [Fact]
    public void Update_TakeoffPhase_ReturnsFalse()
    {
        var ufo = new UfoProjectile(1, new Position(2, 3), new Position(5, 6));
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        var result = ufo.Update(ref state, 0.01f, 1, 0.01f, events);

        Assert.False(result); // Not arrived yet
    }

    #endregion

    #region Flight Phase Tests

    [Fact]
    public void Update_FlightPhase_MovesTowardsTarget()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 0); // Same row, 4 units away
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Complete takeoff
        AdvanceToFlightPhase(ufo, ref state, events);

        var posBeforeFlight = ufo.Position;

        // One flight update
        ufo.Update(ref state, 0.1f, 100, 1.0f, events);

        // Should move towards target (positive X)
        Assert.True(ufo.Position.X > posBeforeFlight.X);
    }

    [Fact]
    public void Update_FlightPhase_ReturnsTrueOnArrival()
    {
        var origin = new Position(0, 0);
        var target = new Position(1, 0); // Very close target
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Complete takeoff
        AdvanceToFlightPhase(ufo, ref state, events);

        // Fly until arrival
        bool arrived = false;
        for (int i = 0; i < 100 && !arrived; i++)
        {
            arrived = ufo.Update(ref state, 0.1f, 100 + i, 1.0f + i * 0.1f, events);
        }

        Assert.True(arrived);
    }

    #endregion

    #region ApplyEffect Tests

    [Fact]
    public void ApplyEffect_ReturnsTargetPosition()
    {
        var target = new Position(3, 4);
        var ufo = new UfoProjectile(1, new Position(0, 0), target);
        var state = CreateTestState();

        var affected = ufo.ApplyEffect(ref state);

        Assert.Contains(target, affected);
    }

    [Fact]
    public void ApplyEffect_ReturnsEmptyForNoTarget()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1));
        var state = CreateTestState();

        // Simulate target being cleared
        ufo.GetType().GetProperty("TargetGridPosition")!
            .SetValue(ufo, null);

        var affected = ufo.ApplyEffect(ref state);

        Assert.Empty(affected);
    }

    #endregion

    #region Event Emission Tests

    [Fact]
    public void Update_EmitsMovementEvents()
    {
        var ufo = new UfoProjectile(1, new Position(2, 3), new Position(5, 6));
        var state = CreateTestState();
        var collector = new BufferedEventCollector();

        ufo.Update(ref state, 0.1f, 1, 0.1f, collector);

        Assert.True(collector.Count > 0);
        var events = collector.GetEvents();
        Assert.Contains(events, e => e is ProjectileMovedEvent);
    }

    #endregion

    #region Deactivation Tests

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1));

        Assert.True(ufo.IsActive);

        ufo.Deactivate();

        Assert.False(ufo.IsActive);
    }

    [Fact]
    public void Update_ReturnsFalseWhenNotActive()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1));
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        ufo.Deactivate();

        var result = ufo.Update(ref state, 0.1f, 1, 0.1f, events);

        Assert.False(result);
    }

    #endregion

    #region Helper Methods

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

    private void AdvanceToFlightPhase(UfoProjectile ufo, ref GameState state, IEventCollector events)
    {
        while (ufo.Phase == UfoPhase.Takeoff)
        {
            ufo.Update(ref state, 0.02f, 0, 0, events);
        }
    }

    #endregion
}

