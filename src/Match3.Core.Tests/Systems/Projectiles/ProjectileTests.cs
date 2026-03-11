using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles;

public class UfoProjectileTests
{
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
    public void Constructor_AcceptsCustomTimingParameters()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1), 0.3f, 4f);

        Assert.True(ufo.IsActive);
        Assert.Equal(new Position(1, 1), ufo.TargetGridPosition);
    }

    #endregion

    #region Timer-Based Flight Tests

    [Fact]
    public void Update_InterpolatesPositionOverTime()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 0);
        var ufo = new UfoProjectile(1, origin, target, overhead: 0f, speed: 2f);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Duration = overhead(0) + distance(4) / speed(2) = 2s
        // At t=1s, should be ~50% of the way
        for (int i = 0; i < 60; i++) // 60 ticks at 1/60 = 1 second
        {
            ufo.Update(ref state, 1f / 60f, i, i / 60f, events);
        }

        // Should have moved roughly halfway
        Assert.True(ufo.Position.X > 1f, $"Expected X > 1, got {ufo.Position.X}");
        Assert.True(ufo.Position.X < 3.5f, $"Expected X < 3.5, got {ufo.Position.X}");
    }

    [Fact]
    public void Update_ReturnsTrueOnTimerExpiry()
    {
        var origin = new Position(0, 0);
        var target = new Position(1, 0);
        // Duration = 0.6 + 1/2 = 1.1s
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        bool arrived = false;
        for (int i = 0; i < 200 && !arrived; i++)
        {
            arrived = ufo.Update(ref state, 1f / 60f, i, i / 60f, events);
        }

        Assert.True(arrived);
    }

    [Fact]
    public void Update_ReturnsFalseBeforeTimerExpiry()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 0);
        // Duration = 0.6 + 4/2 = 2.6s
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Only advance 0.5 seconds
        var result = ufo.Update(ref state, 0.5f, 1, 0.5f, events);

        Assert.False(result);
    }

    #endregion

    #region Dynamic Retargeting Tests

    [Fact]
    public void Update_RetargetsWhenTargetIsEmpty()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 4);
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var collector = new BufferedEventCollector();

        // Clear the target tile
        state.SetTile(4, 4, new Tile(0, ElementType.None, 4, 4));

        // Update should trigger retarget
        ufo.Update(ref state, 0.1f, 1, 0.1f, collector);

        // Should have retargeted to a different position
        Assert.NotEqual(target, ufo.TargetGridPosition);

        // Should have emitted retarget event
        var events = collector.GetEvents();
        Assert.Contains(events, e => e is ProjectileRetargetedEvent);
    }

    [Fact]
    public void TryRetarget_ReturnsFalseWhenNoValidTargets()
    {
        var origin = new Position(0, 0);
        var target = new Position(1, 0);
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Clear ALL tiles
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        var result = ufo.TryRetarget(ref state, 0, 0f, events);

        Assert.False(result);
    }

    [Fact]
    public void TryRetarget_UpdatesTargetAndRecalculatesDuration()
    {
        var origin = new Position(0, 0);
        var target = new Position(7, 7); // Far away target
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Advance a bit
        ufo.Update(ref state, 0.3f, 1, 0.3f, events);

        var result = ufo.TryRetarget(ref state, 2, 0.3f, events);

        Assert.True(result);
        // Target should have changed (StubRandom returns min, so position (1,0) skipping origin)
        Assert.NotEqual(new Position(7, 7), ufo.TargetGridPosition);
    }

    #endregion

    [Fact]
    public void Update_RetargetsAtAnyTime_WhenLockInTimeIsZero()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 0);
        // overhead=0, speed=2 → duration = 4/2 = 2s
        // With LockInTime=0, retarget is allowed at any moment
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var collector = new BufferedEventCollector();

        // Advance to 1.8s (very close to arrival)
        ufo.Update(ref state, 1.8f, 1, 1.8f, NullEventCollector.Instance);

        // Clear the target tile
        state.SetTile(4, 0, new Tile(0, ElementType.None, 4, 0));

        // Update — should retarget even near arrival (LockInTime=0)
        ufo.Update(ref state, 0.01f, 2, 1.81f, collector);

        // Target should have changed (retargeted to any available tile)
        Assert.NotEqual(target, ufo.TargetGridPosition);
    }

    [Fact]
    public void Update_DoesRetarget_WhenOutsideLockInWindow()
    {
        var origin = new Position(0, 0);
        var target = new Position(4, 0);
        // overhead=0, speed=2 → duration = 2s, lock-in at remaining < 0.3s → after 1.7s
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var collector = new BufferedEventCollector();

        // Advance to 0.5s (outside lock-in window: remaining = 1.5s > 0.3s)
        ufo.Update(ref state, 0.5f, 1, 0.5f, NullEventCollector.Instance);

        // Clear the target tile
        state.SetTile(4, 0, new Tile(0, ElementType.None, 4, 0));

        // Update — should retarget because we're outside the lock-in window
        ufo.Update(ref state, 0.01f, 2, 0.51f, collector);

        // Target should have changed
        Assert.NotEqual(target, ufo.TargetGridPosition);
    }

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

        // Simulate target being cleared via reflection
        ufo.GetType().GetProperty("TargetGridPosition")!
            .SetValue(ufo, null);

        var affected = ufo.ApplyEffect(ref state);

        Assert.Empty(affected);
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

    #region SourceTileId Tests

    [Fact]
    public void SourceTileId_CanBeSet()
    {
        var ufo = new UfoProjectile(1, new Position(0, 0), new Position(1, 1))
        {
            SourceTileId = 42
        };

        Assert.Equal(42, ufo.SourceTileId);
    }

    #endregion

    #region Position Continuity Tests

    [Fact]
    public void TryRetarget_PositionIsContinuous()
    {
        var origin = new Position(0, 0);
        var target = new Position(6, 0);
        // overhead=0, speed=2 → duration = 6/2 = 3s
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Advance to 1s → progress = 1/3 ≈ 33%, position ~2.0
        ufo.Update(ref state, 1.0f, 1, 1.0f, events);
        var posBeforeRetarget = ufo.Position;

        // Retarget
        ufo.TryRetarget(ref state, 2, 1.0f, events);
        var posAfterRetarget = ufo.Position;

        // Position should be continuous (no jump)
        Assert.Equal(posBeforeRetarget.X, posAfterRetarget.X, 0.01f);
        Assert.Equal(posBeforeRetarget.Y, posAfterRetarget.Y, 0.01f);
    }

    [Fact]
    public void TryRetarget_MultipleRetargets_PositionNeverJumps()
    {
        var origin = new Position(0, 0);
        var target = new Position(7, 7);
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        Vector2 lastPos = ufo.Position;

        for (int retargetCount = 0; retargetCount < 5; retargetCount++)
        {
            // Advance a bit
            for (int i = 0; i < 10; i++)
            {
                ufo.Update(ref state, 1f / 60f, retargetCount * 10 + i, 0f, events);
            }

            var posBeforeRetarget = ufo.Position;
            float distanceMoved = Vector2.Distance(lastPos, posBeforeRetarget);
            Assert.True(distanceMoved >= 0, "Position should only move forward");

            ufo.TryRetarget(ref state, retargetCount, 0f, events);
            var posAfterRetarget = ufo.Position;

            // No jump
            Assert.Equal(posBeforeRetarget.X, posAfterRetarget.X, 0.01f);
            Assert.Equal(posBeforeRetarget.Y, posAfterRetarget.Y, 0.01f);

            lastPos = posAfterRetarget;
        }
    }

    [Fact]
    public void Update_AfterRetarget_ProgressStartsFromZero()
    {
        var origin = new Position(0, 0);
        var target = new Position(6, 0);
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Advance to 1s
        ufo.Update(ref state, 1.0f, 1, 1.0f, events);
        var posBeforeRetarget = ufo.Position;

        // Retarget
        ufo.TryRetarget(ref state, 2, 1.0f, events);

        // Small step after retarget — position should move slightly from retarget position
        ufo.Update(ref state, 0.01f, 3, 1.01f, events);

        // Should be very close to pre-retarget position (not jumped to 65%+)
        float distFromRetargetPos = Vector2.Distance(posBeforeRetarget, ufo.Position);
        Assert.True(distFromRetargetPos < 0.5f, $"Position jumped {distFromRetargetPos} after retarget");
    }

    #endregion

    #region FindBestTarget Edge Cases

    [Fact]
    public void TryRetarget_SkipsOriginPosition()
    {
        var origin = new Position(0, 0);
        var target = new Position(1, 0);
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Clear all tiles except origin
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (x != 0 || y != 0)
                    state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        // Only origin has a tile, but FindBestTarget skips it
        var result = ufo.TryRetarget(ref state, 0, 0f, events);
        Assert.False(result);
    }

    [Fact]
    public void TryRetarget_OnlyOneTileLeft_TargetsIt()
    {
        var origin = new Position(0, 0);
        var target = new Position(1, 0);
        var ufo = new UfoProjectile(1, origin, target);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Clear all tiles except origin and one other
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (!((x == 0 && y == 0) || (x == 3 && y == 3)))
                    state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        var result = ufo.TryRetarget(ref state, 0, 0f, events);
        Assert.True(result);
        Assert.Equal(new Position(3, 3), ufo.TargetGridPosition);
    }

    #endregion

    #region Duration Recalculation Tests

    [Fact]
    public void TryRetarget_RecalculatesDurationBasedOnNewDistance()
    {
        var origin = new Position(0, 0);
        var target = new Position(6, 0);
        // overhead=0, speed=2 → duration = 6/2 = 3s
        var ufo = new UfoProjectile(1, origin, target, 0f, 2f);
        var state = CreateTestState();
        var events = NullEventCollector.Instance;

        // Advance 0.5s
        ufo.Update(ref state, 0.5f, 1, 0.5f, events);

        ufo.TryRetarget(ref state, 2, 0.5f, events);

        // After retarget, UFO should eventually arrive
        bool arrived = false;
        for (int i = 0; i < 600 && !arrived; i++) // up to 10 seconds at 60fps
        {
            arrived = ufo.Update(ref state, 1f / 60f, 100 + i, 0.5f + i / 60f, events);
        }
        Assert.True(arrived, "UFO should arrive at new target");
    }

    #endregion

    #region MomentumStrength Tests

    [Fact]
    public void MomentumStrength_SameDirection_ReturnsZero()
    {
        var dir = new Vector2(1, 0);
        float strength = UfoConstants.MomentumStrength(dir, dir);
        Assert.Equal(0f, strength, 0.001f);
    }

    [Fact]
    public void MomentumStrength_OppositeDirection_ReturnsMaximum()
    {
        var oldDir = new Vector2(1, 0);
        var newDir = new Vector2(-1, 0);
        float strength = UfoConstants.MomentumStrength(oldDir, newDir);
        // dot=-1 → MomentumBase * 1.0 + MomentumReverseBonus * 1.0
        float expected = UfoConstants.MomentumBase + UfoConstants.MomentumReverseBonus;
        Assert.Equal(expected, strength, 0.001f);
    }

    [Fact]
    public void MomentumStrength_Perpendicular_ReturnsMidValue()
    {
        var oldDir = new Vector2(1, 0);
        var newDir = new Vector2(0, 1);
        float strength = UfoConstants.MomentumStrength(oldDir, newDir);
        // dot=0 → MomentumBase * 0.5 + 0
        float expected = UfoConstants.MomentumBase * 0.5f;
        Assert.Equal(expected, strength, 0.001f);
    }

    [Fact]
    public void MomentumStrength_ZeroVector_ReturnsZero()
    {
        Assert.Equal(0f, UfoConstants.MomentumStrength(Vector2.Zero, new Vector2(1, 0)));
        Assert.Equal(0f, UfoConstants.MomentumStrength(new Vector2(1, 0), Vector2.Zero));
    }

    [Fact]
    public void MomentumStrength_ScalesMonotonically_FromSameToOpposite()
    {
        var oldDir = new Vector2(1, 0);
        // 0°, 45°, 90°, 135°, 180°
        float s0 = UfoConstants.MomentumStrength(oldDir, new Vector2(1, 0));
        float s45 = UfoConstants.MomentumStrength(oldDir, new Vector2(0.707f, 0.707f));
        float s90 = UfoConstants.MomentumStrength(oldDir, new Vector2(0, 1));
        float s135 = UfoConstants.MomentumStrength(oldDir, new Vector2(-0.707f, 0.707f));
        float s180 = UfoConstants.MomentumStrength(oldDir, new Vector2(-1, 0));

        Assert.True(s0 < s45, $"same < 45°: {s0} < {s45}");
        Assert.True(s45 < s90, $"45° < 90°: {s45} < {s90}");
        Assert.True(s90 < s135, $"90° < 135°: {s90} < {s135}");
        Assert.True(s135 < s180, $"135° < 180°: {s135} < {s180}");
    }

    #endregion

    #region CubicBezier Tests

    [Fact]
    public void CubicBezier_AtT0_ReturnsP0()
    {
        var p0 = new Vector2(1, 2);
        var p1 = new Vector2(3, 5);
        var p2 = new Vector2(6, 5);
        var p3 = new Vector2(8, 2);

        var result = UfoConstants.CubicBezier(0f, p0, p1, p2, p3);

        Assert.Equal(p0.X, result.X, 0.001f);
        Assert.Equal(p0.Y, result.Y, 0.001f);
    }

    [Fact]
    public void CubicBezier_AtT1_ReturnsP3()
    {
        var p0 = new Vector2(1, 2);
        var p1 = new Vector2(3, 5);
        var p2 = new Vector2(6, 5);
        var p3 = new Vector2(8, 2);

        var result = UfoConstants.CubicBezier(1f, p0, p1, p2, p3);

        Assert.Equal(p3.X, result.X, 0.001f);
        Assert.Equal(p3.Y, result.Y, 0.001f);
    }

    [Fact]
    public void CubicBezier_AtT05_ReturnsMidpoint_ForStraightLine()
    {
        // Collinear control points → straight line
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(2, 0);
        var p2 = new Vector2(4, 0);
        var p3 = new Vector2(6, 0);

        var result = UfoConstants.CubicBezier(0.5f, p0, p1, p2, p3);

        Assert.Equal(3f, result.X, 0.001f);
        Assert.Equal(0f, result.Y, 0.001f);
    }

    [Fact]
    public void CubicBezierTangent_AtT0_PointsFromP0ToP1()
    {
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(3, 0);
        var p2 = new Vector2(6, 3);
        var p3 = new Vector2(9, 3);

        var tangent = UfoConstants.CubicBezierTangent(0f, p0, p1, p2, p3);

        // At t=0: tangent = 3*(P1-P0) = (9, 0)
        Assert.Equal(9f, tangent.X, 0.001f);
        Assert.Equal(0f, tangent.Y, 0.001f);
    }

    [Fact]
    public void CubicBezierTangent_AtT1_PointsFromP2ToP3()
    {
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(3, 0);
        var p2 = new Vector2(6, 3);
        var p3 = new Vector2(9, 3);

        var tangent = UfoConstants.CubicBezierTangent(1f, p0, p1, p2, p3);

        // At t=1: tangent = 3*(P3-P2) = (9, 0)
        Assert.Equal(9f, tangent.X, 0.001f);
        Assert.Equal(0f, tangent.Y, 0.001f);
    }

    #endregion

    #region HashFloat Tests

    [Fact]
    public void HashFloat_ReturnsBetween0And1()
    {
        for (int seed = -100; seed <= 100; seed++)
        {
            float val = UfoConstants.HashFloat(seed);
            Assert.True(val >= 0f && val < 1f, $"HashFloat({seed}) = {val} out of range");
        }
    }

    [Fact]
    public void HashFloat_IsDeterministic()
    {
        Assert.Equal(UfoConstants.HashFloat(42), UfoConstants.HashFloat(42));
        Assert.Equal(UfoConstants.HashFloat(0), UfoConstants.HashFloat(0));
        Assert.Equal(UfoConstants.HashFloat(-1), UfoConstants.HashFloat(-1));
    }

    [Fact]
    public void HashFloat_ProducesVariedOutput()
    {
        // Different seeds should produce different values (basic distribution check)
        var values = new HashSet<float>();
        for (int i = 0; i < 100; i++)
            values.Add(UfoConstants.HashFloat(i));

        // At least 90% unique values (allow some collisions)
        Assert.True(values.Count >= 90, $"Only {values.Count} unique values from 100 seeds");
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

    #endregion
}
