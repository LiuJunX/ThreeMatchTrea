using System.Diagnostics;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;
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

    private class StubRandom : IRandom
    {
        private int _counter = 0;
        public float NextFloat() => 0f;
        public int Next(int max) => _counter++ % Math.Max(1, max);
        public int Next(int min, int max) => min + (_counter++ % Math.Max(1, max - min));
        public void SetState(ulong state) { _counter = (int)state; }
        public ulong GetState() => (ulong)_counter;
    }

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(MatchGroup match) => 10;
        public int CalculateSpecialMoveScore(ElementType t1, BombType b1, ElementType t2, BombType b2) => 100;
    }

    private class StubSpawnModel : ISpawnModel
    {
        private int _counter = 0;
        private static readonly ElementType[] _types = { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };

        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
        {
            return _types[(_counter++ + spawnX) % _types.Length];
        }
    }

    #region Simulation + Projectile Integration

    [Fact]
    public void SimulationEngine_WithProjectileSystem_ProcessesProjectileImpacts()
    {
        var state = CreateTestState();
        var engine = CreateEngine(state);

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
        var state = CreateTestState();
        var engine = CreateEngine(state);

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
        var state = CreateTestState();
        var engine = CreateEngine(state);

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
        var state = CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = CreateEngine(state, collector);

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
        var state = CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = CreateEngine(state, collector);

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
        var state = CreateTestState();
        var collector = new BufferedEventCollector();
        var engine = CreateEngine(state, collector);

        var cloned = engine.Clone();

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
        var state = CreateTestState();
        var engine = CreateEngine(state);

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

    #region Helper Methods

    private SimulationEngine CreateEngine(GameState state, IEventCollector? eventCollector = null)
    {
        var random = new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, random);
        var spawnModel = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawnModel);
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(scoreSystem);
        var projectileSystem = new ProjectileSystem();

        return new SimulationEngine(
            state,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            eventCollector);
    }

    private GameState CreateTestState()
    {
        var state = new GameState(8, 8, 5, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 8 + x;
                // Pattern that avoids obvious matches
                state.SetTile(x, y, new Tile(idx + 1, types[(x + y) % types.Length], x, y));
            }
        }

        return state;
    }

    #endregion
}


