using System;
using Match3.Core.Config;
using Match3.Core.Events;
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
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Fluent builder for configuring game services.
/// Enables testing with specific component substitutions.
/// </summary>
public sealed class GameServiceFactoryBuilder
{
    private Func<Match3Config, IRandom, IPhysicsSimulation>? _physicsFactory;
    private Func<ISpawnModel, IRefillSystem>? _refillFactory;
    private Func<IBombGenerator, IMatchFinder>? _matchFinderFactory;
    private Func<IScoreSystem, ICellEliminator, BombEffectRegistry, IObstacleSystem?, ICoverSystem?, IMatchProcessor>? _matchProcessorFactory;
    private Func<IScoreSystem, IPowerUpHandler>? _powerUpFactory;
    private Func<IProjectileSystem>? _projectileFactory;
    private Func<IExplosionSystem>? _explosionFactory;
    private Func<bool, IEventCollector>? _eventCollectorFactory;
    private Func<IBombGenerator>? _bombGeneratorFactory;
    private Func<IScoreSystem>? _scoreSystemFactory;
    private Func<BombEffectRegistry>? _bombRegistryFactory;
    private Func<IRandom, ISpawnModel>? _spawnModelFactory;
    private Func<IRandom, ITileGenerator>? _tileGeneratorFactory;
    private Func<IMatchFinder, IDeadlockDetectionSystem>? _deadlockDetectorFactory;
    private Func<IDeadlockDetectionSystem, IBoardShuffleSystem>? _shuffleSystemFactory;

    /// <summary>
    /// Configure custom physics system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithPhysics(Func<Match3Config, IRandom, IPhysicsSimulation> factory)
    {
        _physicsFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom refill system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithRefill(Func<ISpawnModel, IRefillSystem> factory)
    {
        _refillFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom match finder factory.
    /// </summary>
    public GameServiceFactoryBuilder WithMatchFinder(Func<IBombGenerator, IMatchFinder> factory)
    {
        _matchFinderFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom match processor factory.
    /// </summary>
    public GameServiceFactoryBuilder WithMatchProcessor(Func<IScoreSystem, ICellEliminator, BombEffectRegistry, IObstacleSystem?, ICoverSystem?, IMatchProcessor> factory)
    {
        _matchProcessorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom power-up handler factory.
    /// </summary>
    public GameServiceFactoryBuilder WithPowerUpHandler(Func<IScoreSystem, IPowerUpHandler> factory)
    {
        _powerUpFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom projectile system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithProjectileSystem(Func<IProjectileSystem> factory)
    {
        _projectileFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom explosion system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithExplosionSystem(Func<IExplosionSystem> factory)
    {
        _explosionFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom event collector factory.
    /// </summary>
    public GameServiceFactoryBuilder WithEventCollector(Func<bool, IEventCollector> factory)
    {
        _eventCollectorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom bomb generator factory.
    /// </summary>
    public GameServiceFactoryBuilder WithBombGenerator(Func<IBombGenerator> factory)
    {
        _bombGeneratorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom score system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithScoreSystem(Func<IScoreSystem> factory)
    {
        _scoreSystemFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom bomb effect registry factory.
    /// </summary>
    public GameServiceFactoryBuilder WithBombEffectRegistry(Func<BombEffectRegistry> factory)
    {
        _bombRegistryFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom spawn model factory.
    /// </summary>
    public GameServiceFactoryBuilder WithSpawnModel(Func<IRandom, ISpawnModel> factory)
    {
        _spawnModelFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom tile generator factory.
    /// </summary>
    public GameServiceFactoryBuilder WithTileGenerator(Func<IRandom, ITileGenerator> factory)
    {
        _tileGeneratorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom deadlock detection system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithDeadlockDetection(Func<IMatchFinder, IDeadlockDetectionSystem> factory)
    {
        _deadlockDetectorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom shuffle system factory.
    /// </summary>
    public GameServiceFactoryBuilder WithShuffleSystem(Func<IDeadlockDetectionSystem, IBoardShuffleSystem> factory)
    {
        _shuffleSystemFactory = factory;
        return this;
    }

    /// <summary>
    /// Use all default service implementations.
    /// </summary>
    public GameServiceFactoryBuilder UseDefaultServices()
    {
        _physicsFactory = (config, rng) => new RealtimeGravitySystem(config, rng);
        _refillFactory = spawnModel => new RealtimeRefillSystem(spawnModel);
        _matchFinderFactory = bombGen => new ClassicMatchFinder(bombGen);
        _matchProcessorFactory = (score, elim, registry, obs, cover) => new StandardMatchProcessor(score, elim, registry, obs, cover);
        _powerUpFactory = score => new BombResolution(score);
        _projectileFactory = () => new ProjectileSystem();
        _explosionFactory = () => new ExplosionSystem();
        _eventCollectorFactory = enabled => enabled ? new BufferedEventCollector() : NullEventCollector.Instance;
        _bombGeneratorFactory = () => new BombGenerator();
        _scoreSystemFactory = () => new StandardScoreSystem();
        _bombRegistryFactory = () => BombEffectRegistry.CreateDefault();
        _spawnModelFactory = rng => new RuleBasedSpawnModel(rng);
        _tileGeneratorFactory = rng => new StandardTileGenerator(rng);
        _deadlockDetectorFactory = matchFinder => new DeadlockDetectionSystem(matchFinder);
        _shuffleSystemFactory = deadlockDetector => new BoardShuffleSystem(deadlockDetector);

        return this;
    }

    /// <summary>
    /// Build the factory with configured services.
    /// </summary>
    public IGameServiceFactory Build()
    {
        // Ensure defaults are set for any unconfigured services
        UseDefaultServicesIfNotSet();

        return new GameServiceFactory(
            _physicsFactory!,
            _refillFactory!,
            _matchFinderFactory!,
            _matchProcessorFactory!,
            _powerUpFactory!,
            _projectileFactory!,
            _explosionFactory!,
            _eventCollectorFactory!,
            _bombGeneratorFactory!,
            _scoreSystemFactory!,
            _bombRegistryFactory!,
            _spawnModelFactory!,
            _tileGeneratorFactory!,
            _deadlockDetectorFactory!,
            _shuffleSystemFactory!);
    }

    private void UseDefaultServicesIfNotSet()
    {
        _physicsFactory ??= (config, rng) => new RealtimeGravitySystem(config, rng);
        _refillFactory ??= spawnModel => new RealtimeRefillSystem(spawnModel);
        _matchFinderFactory ??= bombGen => new ClassicMatchFinder(bombGen);
        _matchProcessorFactory ??= (score, elim, registry, obs, cover) => new StandardMatchProcessor(score, elim, registry, obs, cover);
        _powerUpFactory ??= score => new BombResolution(score);
        _projectileFactory ??= () => new ProjectileSystem();
        _explosionFactory ??= () => new ExplosionSystem();
        _eventCollectorFactory ??= enabled => enabled ? new BufferedEventCollector() : NullEventCollector.Instance;
        _bombGeneratorFactory ??= () => new BombGenerator();
        _scoreSystemFactory ??= () => new StandardScoreSystem();
        _bombRegistryFactory ??= () => BombEffectRegistry.CreateDefault();
        _spawnModelFactory ??= rng => new RuleBasedSpawnModel(rng);
        _tileGeneratorFactory ??= rng => new StandardTileGenerator(rng);
        _deadlockDetectorFactory ??= matchFinder => new DeadlockDetectionSystem(matchFinder);
        _shuffleSystemFactory ??= deadlockDetector => new BoardShuffleSystem(deadlockDetector);
    }
}
