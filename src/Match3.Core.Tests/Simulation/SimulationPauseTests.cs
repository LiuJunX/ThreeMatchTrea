using System.Linq;
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
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Simulation;

public class SimulationPauseTests
{
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
        public int CalculateMatchScore(MatchGroup match) => 10;
        public int CalculateSpecialMoveScore(ElementType t1, ElementType t2) => 100;
    }

    private class StubSpawnModel : ISpawnModel
    {
        public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context) => ElementType.Item3;
    }

    private SimulationEngine CreateEngine(GameState state)
    {
        var random = new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, random);
        var refill = new RealtimeRefillSystem(new StubSpawnModel());
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        return new SimulationEngine(
            state,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            null,
            null);
    }

    private GameState CreateStableState()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                state.SetTile(x, y, new Tile(y * 5 + x, ElementType.Item3, x, y));
            }
        }
        return state;
    }

    [Fact]
    public void IsPaused_DefaultsToFalse()
    {
        var engine = CreateEngine(CreateStableState());
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void SetPaused_UpdatesIsPaused()
    {
        var engine = CreateEngine(CreateStableState());
        
        engine.SetPaused(true);
        Assert.True(engine.IsPaused);
        
        engine.SetPaused(false);
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void Tick_WhenPaused_DoesNotAdvanceSimulation()
    {
        var engine = CreateEngine(CreateStableState());
        var initialTick = engine.CurrentTick;
        var initialTime = engine.ElapsedTime;

        engine.SetPaused(true);
        engine.Tick(0.1f);

        Assert.Equal(initialTick, engine.CurrentTick);
        Assert.Equal(initialTime, engine.ElapsedTime);
    }

    [Fact]
    public void Tick_WhenPaused_ReturnsZeroDeltaTime()
    {
        var engine = CreateEngine(CreateStableState());
        
        engine.SetPaused(true);
        var result = engine.Tick(0.1f);

        Assert.Equal(0f, result.DeltaTime);
    }
}


