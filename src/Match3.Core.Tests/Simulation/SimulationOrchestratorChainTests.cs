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
using Match3.Core.Systems.Spawning;
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

        // Wave 1: hits (6,5) which has a bomb → triggers chain reaction
        int bombCount = orchestrator.UpdateExplosions(ref state, 0.1f, 11, 5.1f, events);

        // Chain bomb should have been activated with proper tick/simTime/events
        Assert.True(bombCount > 0, "Should have triggered chain bomb");

        // The chain bomb activation should emit BombActivatedEvent
        var bombEvents = events.EmittedEvents.OfType<BombActivatedEvent>().ToList();
        Assert.True(bombEvents.Count > 0, "Chain reaction should emit BombActivatedEvent");

        // The BombActivatedEvent should have the correct tick/simTime
        var chainEvent = bombEvents.First();
        Assert.Equal(11, chainEvent.Tick);
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

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Match3.Core.Models.Gameplay.MatchGroup match) => 0;
        public int CalculateSpecialMoveScore(ElementType t1, ElementType t2) => 0;
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
    }

    private class StubRefill : IRefillSystem
    {
        public void Update(ref GameState state) { }
    }

    #endregion
}

