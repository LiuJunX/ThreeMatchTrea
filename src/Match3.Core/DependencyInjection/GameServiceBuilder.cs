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

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Fluent builder for configuring game services.
/// Enables testing with specific component substitutions.
/// </summary>
public sealed class GameServiceBuilder
{
    private Func<Match3Config, IRandom, IPhysicsSimulation>? _physicsFactory;
    private Func<ISpawnModel, IRefillSystem>? _refillFactory;
    private Func<IBombGenerator, IMatchFinder>? _matchFinderFactory;
    private Func<IScoreSystem, BombEffectRegistry, IMatchProcessor>? _matchProcessorFactory;
    private Func<IScoreSystem, IPowerUpHandler>? _powerUpFactory;
    private Func<IProjectileSystem>? _projectileFactory;
    private Func<IExplosionSystem>? _explosionFactory;
    private Func<bool, IEventCollector>? _eventCollectorFactory;
    private Func<IBombGenerator>? _bombGeneratorFactory;
    private Func<IScoreSystem>? _scoreSystemFactory;
    private Func<BombEffectRegistry>? _bombRegistryFactory;
    private Func<IRandom, ISpawnModel>? _spawnModelFactory;
    private Func<IRandom, ITileGenerator>? _tileGeneratorFactory;

    /// <summary>
    /// Configure custom physics system factory.
    /// </summary>
    public GameServiceBuilder WithPhysics(Func<Match3Config, IRandom, IPhysicsSimulation> factory)
    {
        _physicsFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom refill system factory.
    /// </summary>
    public GameServiceBuilder WithRefill(Func<ISpawnModel, IRefillSystem> factory)
    {
        _refillFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom match finder factory.
    /// </summary>
    public GameServiceBuilder WithMatchFinder(Func<IBombGenerator, IMatchFinder> factory)
    {
        _matchFinderFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom match processor factory.
    /// </summary>
    public GameServiceBuilder WithMatchProcessor(Func<IScoreSystem, BombEffectRegistry, IMatchProcessor> factory)
    {
        _matchProcessorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom power-up handler factory.
    /// </summary>
    public GameServiceBuilder WithPowerUpHandler(Func<IScoreSystem, IPowerUpHandler> factory)
    {
        _powerUpFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom projectile system factory.
    /// </summary>
    public GameServiceBuilder WithProjectileSystem(Func<IProjectileSystem> factory)
    {
        _projectileFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom explosion system factory.
    /// </summary>
    public GameServiceBuilder WithExplosionSystem(Func<IExplosionSystem> factory)
    {
        _explosionFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom event collector factory.
    /// </summary>
    public GameServiceBuilder WithEventCollector(Func<bool, IEventCollector> factory)
    {
        _eventCollectorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom bomb generator factory.
    /// </summary>
    public GameServiceBuilder WithBombGenerator(Func<IBombGenerator> factory)
    {
        _bombGeneratorFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom score system factory.
    /// </summary>
    public GameServiceBuilder WithScoreSystem(Func<IScoreSystem> factory)
    {
        _scoreSystemFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom bomb effect registry factory.
    /// </summary>
    public GameServiceBuilder WithBombEffectRegistry(Func<BombEffectRegistry> factory)
    {
        _bombRegistryFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom spawn model factory.
    /// </summary>
    public GameServiceBuilder WithSpawnModel(Func<IRandom, ISpawnModel> factory)
    {
        _spawnModelFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure custom tile generator factory.
    /// </summary>
    public GameServiceBuilder WithTileGenerator(Func<IRandom, ITileGenerator> factory)
    {
        _tileGeneratorFactory = factory;
        return this;
    }

    /// <summary>
    /// Use all default service implementations.
    /// </summary>
    public GameServiceBuilder UseDefaultServices()
    {
        _physicsFactory = (config, rng) => new RealtimeGravitySystem(config, rng);
        _refillFactory = spawnModel => new RealtimeRefillSystem(spawnModel);
        _matchFinderFactory = bombGen => new ClassicMatchFinder(bombGen);
        _matchProcessorFactory = (score, registry) => new StandardMatchProcessor(score, registry);
        _powerUpFactory = score => new PowerUpHandler(score);
        _projectileFactory = () => new ProjectileSystem();
        _explosionFactory = () => new ExplosionSystem();
        _eventCollectorFactory = enabled => enabled ? new BufferedEventCollector() : NullEventCollector.Instance;
        _bombGeneratorFactory = () => new BombGenerator();
        _scoreSystemFactory = () => new StandardScoreSystem();
        _bombRegistryFactory = () => BombEffectRegistry.CreateDefault();
        _spawnModelFactory = rng => new RuleBasedSpawnModel(rng);
        _tileGeneratorFactory = rng => new StandardTileGenerator(rng);

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
            _tileGeneratorFactory!);
    }

    private void UseDefaultServicesIfNotSet()
    {
        _physicsFactory ??= (config, rng) => new RealtimeGravitySystem(config, rng);
        _refillFactory ??= spawnModel => new RealtimeRefillSystem(spawnModel);
        _matchFinderFactory ??= bombGen => new ClassicMatchFinder(bombGen);
        _matchProcessorFactory ??= (score, registry) => new StandardMatchProcessor(score, registry);
        _powerUpFactory ??= score => new PowerUpHandler(score);
        _projectileFactory ??= () => new ProjectileSystem();
        _explosionFactory ??= () => new ExplosionSystem();
        _eventCollectorFactory ??= enabled => enabled ? new BufferedEventCollector() : NullEventCollector.Instance;
        _bombGeneratorFactory ??= () => new BombGenerator();
        _scoreSystemFactory ??= () => new StandardScoreSystem();
        _bombRegistryFactory ??= () => BombEffectRegistry.CreateDefault();
        _spawnModelFactory ??= rng => new RuleBasedSpawnModel(rng);
        _tileGeneratorFactory ??= rng => new StandardTileGenerator(rng);
    }
}
