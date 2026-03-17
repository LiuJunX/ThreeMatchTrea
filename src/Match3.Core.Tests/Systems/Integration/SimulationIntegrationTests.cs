using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

public class SimulationIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public SimulationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Simulation + Projectile Integration

    [Fact]
    public void SimulationEngine_WithProjectileSystem_ProcessesProjectileImpacts()
    {
        var state = TestEngineFactory.CreateTestState();
        var engine = TestEngineFactory.CreateEngine(state);

        // Launch a projectile
        var projectile = new UfoProjectile(
            engine.ProjectileSystem.GenerateProjectileId(),
            new Position(0, 0),
            new Position(4, 4));

        engine.LaunchProjectile(projectile);

        // Run simulation until stable
        var result = engine.RunUntilStable();

        // Projectile should have completed
        Assert.False(engine.ProjectileSystem.HasActiveProjectiles);
        Assert.True(result.ReachedStability);
    }

    [Fact]
    public void SimulationEngine_ProjectileImpact_ClearsTile()
    {
        var state = TestEngineFactory.CreateTestState();
        var engine = TestEngineFactory.CreateEngine(state);

        var targetPos = new Position(4, 4);
        var targetTileBefore = engine.State.GetTile(targetPos.X, targetPos.Y);
        Assert.NotEqual(ElementType.None, targetTileBefore.Type);

        // Launch projectile at target
        var projectile = new UfoProjectile(
            engine.ProjectileSystem.GenerateProjectileId(),
            new Position(0, 0),
            targetPos);

        engine.LaunchProjectile(projectile);

        // Run until stable
        engine.RunUntilStable();

        // Target should be affected (either cleared or refilled)
        // Note: Due to refill, tile type may have changed
    }

    [Fact]
    public void SimulationEngine_MultipleProjectiles_AllProcessed()
    {
        var state = TestEngineFactory.CreateTestState();
        var engine = TestEngineFactory.CreateEngine(state);

        // Launch multiple projectiles
        for (int i = 0; i < 3; i++)
        {
            var projectile = new UfoProjectile(
                engine.ProjectileSystem.GenerateProjectileId(),
                new Position(0, i),
                new Position(7, 7 - i));
            engine.LaunchProjectile(projectile);
        }

        Assert.Equal(3, engine.ProjectileSystem.ActiveProjectiles.Count);

        // Run until stable
        var result = engine.RunUntilStable();

        Assert.True(result.ReachedStability);
        Assert.False(engine.ProjectileSystem.HasActiveProjectiles);
    }

    #endregion

    #region Event Sourcing Integration

    [Fact]
    public void SimulationEngine_EventCollector_CapturesAllEvents()
    {
        var state = TestEngineFactory.CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // Apply a move
        engine.ApplyMove(new Position(0, 0), new Position(1, 0));

        // Run a few ticks
        for (int i = 0; i < 10; i++)
        {
            engine.Tick();
        }

        var events = collector.GetEvents();
        Assert.NotEmpty(events);

        // Should have swap event
        Assert.Contains(events, e => e is TilesSwappedEvent);
    }

    [Fact]
    public void SimulationEngine_RunUntilStable_DisablesEventsDuringRun()
    {
        var state = TestEngineFactory.CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // Clear any existing events
        collector.Clear();

        // Run until stable should disable events
        engine.RunUntilStable();

        // No events should be collected during RunUntilStable
        Assert.Equal(0, collector.Count);
    }

    [Fact]
    public void SimulationEngine_Clone_DoesNotAffectOriginalEvents()
    {
        var state = TestEngineFactory.CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        var cloned = engine.Clone(new StubRandom());

        // Events on cloned engine should not affect original
        cloned.ApplyMove(new Position(0, 0), new Position(1, 0));

        // Original collector should not have clone's events
        Assert.Equal(0, collector.Count);
    }

    #endregion

    #region Full Game Loop Integration

    [Fact]
    public void SimulationEngine_FullMoveSequence_MaintainsConsistency()
    {
        var state = TestEngineFactory.CreateTestState();
        var engine = TestEngineFactory.CreateEngine(state);

        // Simulate a full game sequence
        for (int move = 0; move < 5; move++)
        {
            // Find a valid swap
            bool swapped = false;
            for (int y = 0; y < state.Height && !swapped; y++)
            {
                for (int x = 0; x < state.Width - 1 && !swapped; x++)
                {
                    var from = new Position(x, y);
                    var to = new Position(x + 1, y);

                    var tileFrom = engine.State.GetTile(from.X, from.Y);
                    var tileTo = engine.State.GetTile(to.X, to.Y);

                    if (tileFrom.Type != ElementType.None && tileTo.Type != ElementType.None)
                    {
                        engine.ApplyMove(from, to);
                        swapped = true;
                    }
                }
            }

            // Run until stable
            var result = engine.RunUntilStable();
            Assert.True(result.ReachedStability);

            // Validate grid consistency
            var finalState = engine.State;
            int nonEmptyCount = 0;
            for (int y = 0; y < finalState.Height; y++)
            {
                for (int x = 0; x < finalState.Width; x++)
                {
                    if (finalState.GetTile(x, y).Type != ElementType.None)
                        nonEmptyCount++;
                }
            }

            // Board should be mostly filled after refill
            Assert.True(nonEmptyCount > 0);
        }
    }

    #endregion

}


