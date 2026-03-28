using System;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Obstacles;
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
    private readonly Func<IScoreSystem, ICellEliminator, BombEffectRegistry, IObstacleSystem?, ICoverSystem?, IMatchProcessor> _matchProcessorFactory;
    private readonly Func<IScoreSystem, IPowerUpHandler> _powerUpFactory;
    private readonly Func<IProjectileSystem> _projectileFactory;
    private readonly Func<IExplosionSystem> _explosionFactory;
    private readonly Func<bool, IEventCollector> _eventCollectorFactory;
    private readonly Func<IBombGenerator> _bombGeneratorFactory;
    private readonly Func<IScoreSystem> _scoreSystemFactory;
    private readonly Func<BombEffectRegistry> _bombRegistryFactory;
    private readonly Func<IRandom, ISpawnModel> _spawnModelFactory;
    private readonly Func<IRandom, ITileGenerator> _tileGeneratorFactory;
    private readonly Func<IMatchFinder, IDeadlockDetectionSystem> _deadlockDetectorFactory;
    private readonly Func<IDeadlockDetectionSystem, IBoardShuffleSystem> _shuffleSystemFactory;
    private readonly Func<ILevelObjectiveSystem> _objectiveSystemFactory;

    internal GameServiceFactory(
        Func<Match3Config, IRandom, IPhysicsSimulation> physicsFactory,
        Func<ISpawnModel, IRefillSystem> refillFactory,
        Func<IBombGenerator, IMatchFinder> matchFinderFactory,
        Func<IScoreSystem, ICellEliminator, BombEffectRegistry, IObstacleSystem?, ICoverSystem?, IMatchProcessor> matchProcessorFactory,
        Func<IScoreSystem, IPowerUpHandler> powerUpFactory,
        Func<IProjectileSystem> projectileFactory,
        Func<IExplosionSystem> explosionFactory,
        Func<bool, IEventCollector> eventCollectorFactory,
        Func<IBombGenerator> bombGeneratorFactory,
        Func<IScoreSystem> scoreSystemFactory,
        Func<BombEffectRegistry> bombRegistryFactory,
        Func<IRandom, ISpawnModel> spawnModelFactory,
        Func<IRandom, ITileGenerator> tileGeneratorFactory,
        Func<IMatchFinder, IDeadlockDetectionSystem> deadlockDetectorFactory,
        Func<IDeadlockDetectionSystem, IBoardShuffleSystem> shuffleSystemFactory,
        Func<ILevelObjectiveSystem>? objectiveSystemFactory = null)
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
        _deadlockDetectorFactory = deadlockDetectorFactory;
        _shuffleSystemFactory = shuffleSystemFactory;
        _objectiveSystemFactory = objectiveSystemFactory ?? (() => new LevelObjectiveSystem());
    }

    /// <inheritdoc />
    public SimulationEngine CreateSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        IEventCollector? eventCollector = null)
    {
        // Single-random mode: all domains map to state.Random (used by AI/DryRun).
        // Uses a null-seed SeedManager; RandomStreamFactory.Create(null, domain) produces
        // independent seeded streams per domain, which is acceptable for non-replay use.
        return BuildSimulationEngine(initialState, config, new SeedManager(null), eventCollector);
    }

    /// <inheritdoc />
    public SimulationEngine CreateSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        SeedManager seedManager,
        IEventCollector? eventCollector = null)
    {
        return BuildSimulationEngine(initialState, config, seedManager, eventCollector);
    }

    /// <summary>
    /// Single point of engine assembly. All random domain → subsystem wiring lives here.
    /// Adding a new RandomDomain only requires updating this method.
    /// </summary>
    private SimulationEngine BuildSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        SeedManager seedManager,
        IEventCollector? eventCollector,
        ILevelObjectiveSystem? objectiveSystem = null)
    {
        var match3Config = new Match3Config(initialState.Width, initialState.Height, initialState.TileTypesCount);

        objectiveSystem ??= _objectiveSystemFactory();
        var bombGenerator = _bombGeneratorFactory();
        var matchFinder = _matchFinderFactory(bombGenerator);
        var scoreSystem = _scoreSystemFactory();
        var bombRegistry = _bombRegistryFactory();
        var projectileSystem = _projectileFactory();

        // Create shared SimulationContext — all subsystems share the same infrastructure
        var lockScheduler = new LockScheduler();
        var obstacleSystem = new ObstacleSystem(objectiveSystem, lockScheduler);
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var cellEliminator = new CellEliminator(coverSystem, groundSystem, objectiveSystem, obstacleSystem);
        var matchProcessor = _matchProcessorFactory(scoreSystem, cellEliminator, bombRegistry, obstacleSystem, coverSystem);
        var context = new Simulation.SimulationContext(cellEliminator, bombRegistry, lockScheduler);
        var explosionSystem = new ExplosionSystem(context);
        var colorBombSessionManager = new ColorBombSessionManager(context);
        var basePowerUp = (BombResolution)_powerUpFactory(scoreSystem);
        var powerUpHandler = PowerUpHandlerFactory.CloneForSimulation(
            basePowerUp, context, explosionSystem, projectileSystem, colorBombSessionManager);
        var collector = eventCollector ?? _eventCollectorFactory(true);

        // ── Random domain wiring (single update point) ──
        var spawnModel = _spawnModelFactory(seedManager.GetRandom(RandomDomain.Refill));
        var physics = _physicsFactory(match3Config, seedManager.GetRandom(RandomDomain.Physics));
        var refill = _refillFactory(spawnModel);

        var deadlockDetector = _deadlockDetectorFactory(matchFinder);
        var shuffleSystem = _shuffleSystemFactory(deadlockDetector);

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
            explosionSystem,
            deadlockDetector,
            shuffleSystem,
            objectiveSystem,
            colorBombSessionManager,
            lockScheduler,
            cellEliminator: cellEliminator);
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

        // Seed selection: if level has approved seeds, pick one and override Refill/Drop domains.
        // NOTE: board initialization (tileGenerator) also consumes the Refill stream, so the approved
        // seed covers both initial layout and runtime spawns — the offline analysis pipeline must
        // simulate both together (not refills alone).
        if (levelConfig?.ApprovedSeeds is { Length: > 0 } seeds)
        {
            int selected = seeds[mainRng.Next(0, seeds.Length)];
            seedManager.SetOverride(RandomDomain.Refill, selected);
            // Derive Drop seed from the same approved seed to keep determinism,
            // but XOR-shift so the two domains don't produce correlated sequences.
            seedManager.SetOverride(RandomDomain.Drop, selected ^ unchecked((int)0x9E3779B9));
        }

        // Determine dimensions
        int width = levelConfig?.Width ?? configuration.Width;
        int height = levelConfig?.Height ?? configuration.Height;

        // Create initial state
        int tileTypesCount = levelConfig?.TileTypesCount ?? configuration.TileTypesCount;
        var state = new GameState(width, height, tileTypesCount, mainRng);

        // Initialize board (consumes Refill-domain random for tile generation)
        var objectiveSystem = _objectiveSystemFactory();
        var tileGenerator = _tileGeneratorFactory(seedManager.GetRandom(RandomDomain.Refill));

        if (levelConfig != null)
        {
            var initializer = new BoardInitializer(tileGenerator, objectiveSystem);
            initializer.Initialize(ref state, levelConfig);
        }
        else
        {
            InitializeRandomBoard(ref state, tileGenerator);
        }

        // Create event collector
        var eventCollector = _eventCollectorFactory(configuration.EnableEventCollection);

        // Delegate engine assembly to shared builder (pass objectiveSystem to avoid double creation)
        var engine = BuildSimulationEngine(state, configuration.SimulationConfig, seedManager, eventCollector, objectiveSystem);

        return new GameSession(engine, eventCollector, seedManager, configuration);
    }

    /// <inheritdoc />
    public ISpawnModel CreateSpawnModel(IRandom random) => _spawnModelFactory(random);

    /// <inheritdoc />
    public ITileGenerator CreateTileGenerator(IRandom random) => _tileGeneratorFactory(random);

    public IDeadlockDetectionSystem CreateDeadlockDetector(IMatchFinder matchFinder) => _deadlockDetectorFactory(matchFinder);
    public IBoardShuffleSystem CreateShuffleSystem(IDeadlockDetectionSystem deadlockDetector) => _shuffleSystemFactory(deadlockDetector);
    public ILevelObjectiveSystem? CreateObjectiveSystem() => _objectiveSystemFactory?.Invoke();

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
