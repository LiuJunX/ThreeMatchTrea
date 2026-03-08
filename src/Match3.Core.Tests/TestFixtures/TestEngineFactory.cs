using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;

namespace Match3.Core.Tests.TestFixtures;

/// <summary>
/// Shared factory for assembling SimulationEngine instances in tests.
/// Eliminates the duplicated ~25-line CreateEngine pattern across test classes.
/// All dependencies default to hand-written stubs; callers can override any subset.
/// </summary>
public static class TestEngineFactory
{
    /// <summary>
    /// Creates a fully wired SimulationEngine with sensible test defaults.
    /// Override individual dependencies as needed; everything else uses stubs.
    /// </summary>
    public static SimulationEngine CreateEngine(
        GameState state,
        IRandom? random = null,
        IEventCollector? eventCollector = null,
        IScoreSystem? scoreSystem = null,
        ISpawnModel? spawnModel = null,
        SimulationConfig? simulationConfig = null,
        IProjectileSystem? projectileSystem = null)
    {
        var rng = random ?? new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, rng);
        var spawn = spawnModel ?? new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawn);
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var score = scoreSystem ?? new StubScoreSystem();
        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var matchProcessor = new StandardMatchProcessor(
            score, coverSystem, groundSystem, BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(score);

        return new SimulationEngine(
            state,
            simulationConfig ?? SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            eventCollector);
    }

    /// <summary>
    /// Creates a GameState filled with a non-matching tile pattern.
    /// The offset pattern (x + y) % colorCount avoids accidental 3-in-a-row matches.
    /// </summary>
    public static GameState CreateTestState(int width = 8, int height = 8, int colorCount = 5, ulong seed = 12345)
    {
        var random = new StubRandom();
        random.SetState(seed);

        var state = new GameState(width, height, colorCount, random);
        var types = new[]
        {
            ElementType.Item1, ElementType.Item3, ElementType.Item2,
            ElementType.Item4, ElementType.Item5
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int id = y * width + x;
                int typeIdx = (int)(((ulong)x + (ulong)y + seed) % (ulong)types.Length);
                state.SetTile(x, y, new Tile(id + 1, types[typeIdx], x, y));
            }
        }

        return state;
    }
}
