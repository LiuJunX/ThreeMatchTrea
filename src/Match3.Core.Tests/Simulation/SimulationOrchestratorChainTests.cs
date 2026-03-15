using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Simulation;

public class SimulationOrchestratorChainTests
{
    [Fact]
    public void UpdateExplosions_TriggeredBombs_PassTickAndEvents()
    {
        // Setup: create an explosion that will trigger a chain bomb
        var events = new StubEventCollector();
        var state = CreateFilledState();

        // Place a horizontal bomb at (6,5) distance 1 from origin
        var bombTile = new Tile(100, ElementType.HorizontalRocket, 6, 5);
        state.SetTile(6, 5, bombTile);

        // Create explosion system and orchestrator
        var explosionSystem = new ExplosionSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var powerUpHandler = new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            explosionSystem);

        var orchestrator = new SimulationOrchestrator(
            new StubPhysics(),
            new StubRefill(),
            new ClassicMatchFinder(new BombGenerator()),
            new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, BombEffectRegistry.CreateDefault()),
            powerUpHandler,
            explosionSystem: explosionSystem);

        // Create explosion at (5,5) with radius 2
        explosionSystem.CreateExplosion(ref state, new Position(5, 5), 2);

        // Wave 0: center (5,5)
        orchestrator.UpdateExplosions(ref state, 0.1f, 10, 5.0f, events);

        // Wave 1: hits (6,5) which has a bomb -- ExplosionSystem handles chain reactions internally
        orchestrator.UpdateExplosions(ref state, 0.1f, 11, 5.1f, events);

        // The chain bomb activation should emit BombActivatedEvent internally
        var bombEvents = events.EmittedEvents.OfType<BombActivatedEvent>().ToList();
        Assert.True(bombEvents.Count > 0, "Chain reaction should emit BombActivatedEvent");

        // The BombActivatedEvent should have the correct tick/simTime
        var chainEvent = bombEvents.First();
        Assert.Equal(11, chainEvent.Tick);
    }

    [Fact]
    public void UpdateProjectiles_UfoHitsBomb_TriggersBombActivation()
    {
        var events = new StubEventCollector();
        var state = CreateFilledState();

        // Place a horizontal rocket at the UFO target
        var bombPos = new Position(5, 5);
        state.SetTile(bombPos.X, bombPos.Y, new Tile(200, ElementType.HorizontalRocket, bombPos.X, bombPos.Y));

        var explosionSystem = new ExplosionSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var projectileSystem = new ProjectileSystem();
        var powerUpHandler = new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            explosionSystem);

        var orchestrator = new SimulationOrchestrator(
            new StubPhysics(),
            new StubRefill(),
            new ClassicMatchFinder(new BombGenerator()),
            new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, BombEffectRegistry.CreateDefault()),
            powerUpHandler,
            projectileSystem: projectileSystem,
            explosionSystem: explosionSystem);

        // Launch UFO targeting the bomb position (same origin = immediate arrival)
        var ufo = new UfoProjectile(1, bombPos, bombPos);
        projectileSystem.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        // Run until projectile arrives
        for (int i = 0; i < 100; i++)
        {
            orchestrator.UpdateProjectiles(ref state, 0.1f, i, i * 0.1f, events);
            if (!projectileSystem.HasActiveProjectiles) break;
        }

        // The bomb should have been activated, not silently destroyed
        var bombActivated = events.EmittedEvents.OfType<BombActivatedEvent>().ToList();
        Assert.True(bombActivated.Count > 0, "UFO hitting a bomb should trigger BombActivatedEvent");
    }

    [Fact]
    public void UpdateProjectiles_UfoHitsNormalTile_EmitsDestroyEvent()
    {
        var events = new StubEventCollector();
        var state = CreateFilledState();

        var targetPos = new Position(3, 3);

        var explosionSystem = new ExplosionSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var projectileSystem = new ProjectileSystem();
        var powerUpHandler = new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            explosionSystem);

        var orchestrator = new SimulationOrchestrator(
            new StubPhysics(),
            new StubRefill(),
            new ClassicMatchFinder(new BombGenerator()),
            new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, BombEffectRegistry.CreateDefault()),
            powerUpHandler,
            projectileSystem: projectileSystem,
            explosionSystem: explosionSystem);

        // Launch UFO targeting a normal tile
        var ufo = new UfoProjectile(1, targetPos, targetPos);
        projectileSystem.Launch(ufo, 0, 0f, NullEventCollector.Instance);

        for (int i = 0; i < 100; i++)
        {
            orchestrator.UpdateProjectiles(ref state, 0.1f, i, i * 0.1f, events);
            if (!projectileSystem.HasActiveProjectiles) break;
        }

        // Normal tile should be destroyed (not activated)
        var destroyed = events.EmittedEvents.OfType<TileDestroyedEvent>().ToList();
        Assert.True(destroyed.Count > 0, "UFO hitting a normal tile should emit TileDestroyedEvent");
        Assert.Equal(ElimSource.Projectile, destroyed[0].Reason);

        // Tile should be cleared
        Assert.Equal(ElementType.None, state.GetTile(targetPos.X, targetPos.Y).Type);
    }

    [Fact]
    public void UpdateExplosions_UfoCaughtInWave_LaunchesProjectile()
    {
        // Arrange: UFO at (6,5), explosion origin at (5,5)
        var events = new StubEventCollector();
        var state = CreateFilledState();

        var ufoTile = new Tile(300, ElementType.Ufo, 6, 5);
        state.SetTile(6, 5, ufoTile);

        var explosionSystem = new ExplosionSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var projectileSystem = new ProjectileSystem();
        var powerUpHandler = new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            explosionSystem,
            projectileSystem);

        var orchestrator = new SimulationOrchestrator(
            new StubPhysics(),
            new StubRefill(),
            new ClassicMatchFinder(new BombGenerator()),
            new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, BombEffectRegistry.CreateDefault()),
            powerUpHandler,
            projectileSystem: projectileSystem,
            explosionSystem: explosionSystem);

        explosionSystem.CreateExplosion(ref state, new Position(5, 5), 2);

        // Wave 0: center (5,5)
        orchestrator.UpdateExplosions(ref state, 0.1f, 10, 5.0f, events);

        // Wave 1: hits UFO at (6,5) — should route through ActivateChainBomb and launch projectile
        orchestrator.UpdateExplosions(ref state, 0.1f, 11, 5.1f, events);

        // Assert: BombActivatedEvent emitted for the UFO
        var bombEvents = events.EmittedEvents.OfType<BombActivatedEvent>().ToList();
        Assert.True(bombEvents.Count > 0, "UFO chain reaction should emit BombActivatedEvent");
        Assert.Equal(ElementType.Ufo, bombEvents[0].BombType);

        // Assert: UFO projectile launched
        Assert.True(projectileSystem.HasActiveProjectiles,
            "UFO caught in explosion wave should launch a projectile via unified ActivateChainBomb path");
    }

    /// <summary>
    /// Regression: 5x5 bomb triggers 3 UFOs at wave distance 2.
    /// All 3 should launch projectiles, not just 1.
    /// Layout:
    ///   U U U   (y=0, Chebyshev distance 2 from bomb)
    ///   A B C   (y=1)
    ///   D X E   (y=2, X = 5x5 bomb origin)
    /// </summary>
    [Fact]
    public void AreaBomb_ThreeUfosAtSameWave_AllThreeLaunchProjectiles()
    {
        var events = new StubEventCollector();
        var state = CreateFilledState(10, 10);

        // Place 5x5 bomb at (4, 4)
        var bombPos = new Position(4, 4);
        state.SetTile(bombPos.X, bombPos.Y, new Tile(500, ElementType.Square5x5, bombPos.X, bombPos.Y));

        // Place 3 UFOs at distance 2 (y=2, x=3,4,5)
        state.SetTile(3, 2, new Tile(501, ElementType.Ufo, 3, 2));
        state.SetTile(4, 2, new Tile(502, ElementType.Ufo, 4, 2));
        state.SetTile(5, 2, new Tile(503, ElementType.Ufo, 5, 2));

        var explosionSystem = new ExplosionSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var projectileSystem = new ProjectileSystem();
        var powerUpHandler = new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            explosionSystem,
            projectileSystem);

        var orchestrator = new SimulationOrchestrator(
            new StubPhysics(),
            new StubRefill(),
            new ClassicMatchFinder(new BombGenerator()),
            new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, BombEffectRegistry.CreateDefault()),
            powerUpHandler,
            projectileSystem: projectileSystem,
            explosionSystem: explosionSystem);

        // Activate the 5x5 bomb
        powerUpHandler.ActivateBomb(ref state, bombPos, 0, 0f, events);

        // Run UpdateExplosions until all waves are processed
        for (int tick = 1; tick <= 50; tick++)
        {
            orchestrator.UpdateExplosions(ref state, 0.05f, tick, tick * 0.05f, events);
        }

        // Assert: all 3 UFOs should have launched projectiles
        var launchEvents = events.EmittedEvents.OfType<ProjectileLaunchedEvent>().ToList();
        Assert.Equal(3, launchEvents.Count);

        // Assert: all 3 projectile IDs should be distinct
        var projectileIds = launchEvents.Select(e => e.ProjectileId).Distinct().ToList();
        Assert.Equal(3, projectileIds.Count);

        // Assert: all 3 should have SpawnVisual = true (chain reaction)
        Assert.All(launchEvents, e => Assert.True(e.SpawnVisual));
    }

    /// <summary>
    /// Full E2E: SimulationEngine with 5x5 bomb tap triggering 3 UFOs.
    /// Verifies all 3 ProjectileLaunchedEvents are emitted with realistic timing.
    /// </summary>
    [Fact]
    public void SimulationEngine_AreaBomb_ThreeUfos_AllLaunch()
    {
        // Create 8x8 board, fill with non-matching tiles
        var state = TestEngineFactory.CreateTestState(8, 8);

        // Place 5x5 bomb at (4, 4)
        state.SetTile(4, 4, new Tile(500, ElementType.Square5x5, 4, 4));

        // Place 3 UFOs at Chebyshev distance 2: (3,2), (4,2), (5,2)
        state.SetTile(3, 2, new Tile(501, ElementType.Ufo, 3, 2));
        state.SetTile(4, 2, new Tile(502, ElementType.Ufo, 4, 2));
        state.SetTile(5, 2, new Tile(503, ElementType.Ufo, 5, 2));

        var events = new StubEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: events);

        // Activate the 5x5 bomb via HandleTap (direct bomb click)
        engine.HandleTap(new Position(4, 4));

        // Run ticks with realistic deltaTime (1/60s) until stable
        for (int tick = 0; tick < 500; tick++)
        {
            engine.Tick(1f / 60f);
            if (engine.IsStable()) break;
        }

        // Count ProjectileLaunchedEvents for UFO type
        var ufoLaunches = events.EmittedEvents
            .OfType<ProjectileLaunchedEvent>()
            .Where(e => e.Type == Match3.Core.Events.Enums.ProjectileType.Ufo)
            .ToList();

        Assert.True(ufoLaunches.Count >= 3,
            $"Expected at least 3 UFO launches, got {ufoLaunches.Count}. " +
            $"SourceTileIds: [{string.Join(", ", ufoLaunches.Select(e => e.SourceTileId))}]");
    }

    #region Helpers

    private static GameState CreateFilledState(int width = 10, int height = 10)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4 };
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
            }
        }
        return state;
    }

    private class StubEventCollector : IEventCollector
    {
        public List<GameEvent> EmittedEvents { get; } = new();
        public bool IsEnabled => true;
        public void Emit(GameEvent evt) => EmittedEvents.Add(evt);
        public void EmitBatch(IEnumerable<GameEvent> events) => EmittedEvents.AddRange(events);
    }

    private class StubPhysics : IPhysicsSimulation
    {
        public void Update(ref GameState state, float deltaTime) { }
        public bool IsStable(in GameState state) => true;
        public IPhysicsSimulation CloneForSimulation(Match3.Random.IRandom newRandom) => this;
    }

    private class StubRefill : IRefillSystem
    {
        public void Update(ref GameState state) { }
    }

    #endregion
}

