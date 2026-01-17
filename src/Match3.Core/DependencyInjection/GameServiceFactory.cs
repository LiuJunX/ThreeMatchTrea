using System;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Default implementation of IGameServiceFactory.
/// Creates game services with configured dependencies.
/// </summary>
public sealed class GameServiceFactory : IGameServiceFactory
{
    private readonly Func<Match3Config, IRandom, IPhysicsSimulation> _physicsFactory;
    private readonly Func<ISpawnModel, IRefillSystem> _refillFactory;
    private readonly Func<IBombGenerator, IMatchFinder> _matchFinderFactory;
    private readonly Func<IScoreSystem, BombEffectRegistry, IMatchProcessor> _matchProcessorFactory;
    private readonly Func<IScoreSystem, IPowerUpHandler> _powerUpFactory;
    private readonly Func<IProjectileSystem> _projectileFactory;
    private readonly Func<IExplosionSystem> _explosionFactory;
    private readonly Func<bool, IEventCollector> _eventCollectorFactory;
    private readonly Func<IBombGenerator> _bombGeneratorFactory;
    private readonly Func<IScoreSystem> _scoreSystemFactory;
    private readonly Func<BombEffectRegistry> _bombRegistryFactory;
    private readonly Func<IRandom, ISpawnModel> _spawnModelFactory;
    private readonly Func<IRandom, ITileGenerator> _tileGeneratorFactory;

    internal GameServiceFactory(
        Func<Match3Config, IRandom, IPhysicsSimulation> physicsFactory,
        Func<ISpawnModel, IRefillSystem> refillFactory,
        Func<IBombGenerator, IMatchFinder> matchFinderFactory,
        Func<IScoreSystem, BombEffectRegistry, IMatchProcessor> matchProcessorFactory,
        Func<IScoreSystem, IPowerUpHandler> powerUpFactory,
        Func<IProjectileSystem> projectileFactory,
        Func<IExplosionSystem> explosionFactory,
        Func<bool, IEventCollector> eventCollectorFactory,
        Func<IBombGenerator> bombGeneratorFactory,
        Func<IScoreSystem> scoreSystemFactory,
        Func<BombEffectRegistry> bombRegistryFactory,
        Func<IRandom, ISpawnModel> spawnModelFactory,
        Func<IRandom, ITileGenerator> tileGeneratorFactory)
    {
        _physicsFactory = physicsFactory;
        _refillFactory = refillFactory;
        _matchFinderFactory = matchFinderFactory;
        _matchProcessorFactory = matchProcessorFactory;
        _powerUpFactory = powerUpFactory;
        _projectileFactory = projectileFactory;
        _explosionFactory = explosionFactory;
        _eventCollectorFactory = eventCollectorFactory;
        _bombGeneratorFactory = bombGeneratorFactory;
        _scoreSystemFactory = scoreSystemFactory;
        _bombRegistryFactory = bombRegistryFactory;
        _spawnModelFactory = spawnModelFactory;
        _tileGeneratorFactory = tileGeneratorFactory;
    }

    /// <inheritdoc />
    public SimulationEngine CreateSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        IEventCollector? eventCollector = null)
    {
        var match3Config = new Match3Config(initialState.Width, initialState.Height, initialState.TileTypesCount);

        // Create all systems
        var bombGenerator = _bombGeneratorFactory();
        var matchFinder = _matchFinderFactory(bombGenerator);
        var scoreSystem = _scoreSystemFactory();
        var bombRegistry = _bombRegistryFactory();
        var matchProcessor = _matchProcessorFactory(scoreSystem, bombRegistry);
        var powerUpHandler = _powerUpFactory(scoreSystem);
        var projectileSystem = _projectileFactory();
        var explosionSystem = _explosionFactory();

        // Use provided event collector or create based on config
        var collector = eventCollector ?? _eventCollectorFactory(true);

        // Physics and refill need random from state
        var spawnModel = _spawnModelFactory(initialState.Random);
        var physics = _physicsFactory(match3Config, initialState.Random);
        var refill = _refillFactory(spawnModel);

        return new SimulationEngine(
            initialState,
            config,
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            collector,
            explosionSystem);
    }

    /// <inheritdoc />
    public GameSession CreateGameSession(LevelConfig? levelConfig = null)
    {
        return CreateGameSession(GameServiceConfiguration.CreateDefault(), levelConfig);
    }

    /// <inheritdoc />
    public GameSession CreateGameSession(GameServiceConfiguration configuration, LevelConfig? levelConfig = null)
    {
        var seedManager = new SeedManager(configuration.RngSeed);
        var mainRng = seedManager.GetRandom(RandomDomain.Main);

        // Determine dimensions
        int width = levelConfig?.Width ?? configuration.Width;
        int height = levelConfig?.Height ?? configuration.Height;

        // Create initial state
        var state = new GameState(width, height, configuration.TileTypesCount, mainRng);

        // Initialize board
        var tileGenerator = _tileGeneratorFactory(seedManager.GetRandom(RandomDomain.Refill));

        if (levelConfig?.Grid != null && HasValidTiles(levelConfig.Grid))
        {
            var initializer = new BoardInitializer(tileGenerator);
            initializer.Initialize(ref state, levelConfig);
        }
        else
        {
            InitializeRandomBoard(ref state, tileGenerator);
        }

        // Create event collector
        var eventCollector = _eventCollectorFactory(configuration.EnableEventCollection);

        // Create simulation engine
        var match3Config = new Match3Config(width, height, configuration.TileTypesCount);
        var spawnModel = _spawnModelFactory(seedManager.GetRandom(RandomDomain.Refill));

        var bombGenerator = _bombGeneratorFactory();
        var matchFinder = _matchFinderFactory(bombGenerator);
        var scoreSystem = _scoreSystemFactory();
        var bombRegistry = _bombRegistryFactory();
        var matchProcessor = _matchProcessorFactory(scoreSystem, bombRegistry);
        var powerUpHandler = _powerUpFactory(scoreSystem);
        var projectileSystem = _projectileFactory();
        var explosionSystem = _explosionFactory();
        var physics = _physicsFactory(match3Config, seedManager.GetRandom(RandomDomain.Physics));
        var refill = _refillFactory(spawnModel);

        var engine = new SimulationEngine(
            state,
            configuration.SimulationConfig,
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem,
            eventCollector,
            explosionSystem);

        return new GameSession(engine, eventCollector, seedManager, configuration);
    }

    private static bool HasValidTiles(TileType[] grid)
    {
        foreach (var t in grid)
        {
            if (t != TileType.None) return true;
        }
        return false;
    }

    private void InitializeRandomBoard(ref GameState state, ITileGenerator tileGenerator)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
            }
        }
    }
}
