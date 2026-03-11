using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Spawning;
using Match3.Random;

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Factory for creating game service instances with configured dependencies.
/// Abstracts away the manual assembly of 15+ systems.
/// </summary>
public interface IGameServiceFactory
{
    /// <summary>
    /// Create a SimulationEngine with all required dependencies.
    /// Uses state.Random for all subsystems (single random stream).
    /// </summary>
    /// <param name="initialState">Initial game state.</param>
    /// <param name="config">Simulation configuration.</param>
    /// <param name="eventCollector">Event collector (null uses default based on config).</param>
    /// <returns>Configured SimulationEngine.</returns>
    SimulationEngine CreateSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        IEventCollector? eventCollector = null);

    /// <summary>
    /// Create a SimulationEngine with multi-domain random streams for deterministic replay.
    /// Each subsystem gets its own random stream derived from the SeedManager.
    /// </summary>
    /// <param name="initialState">Initial game state (state.Random = Main domain).</param>
    /// <param name="config">Simulation configuration.</param>
    /// <param name="seedManager">SeedManager providing per-domain random streams.</param>
    /// <param name="eventCollector">Event collector (null uses default based on config).</param>
    /// <returns>Configured SimulationEngine with deterministic multi-domain randoms.</returns>
    SimulationEngine CreateSimulationEngine(
        GameState initialState,
        SimulationConfig config,
        SeedManager seedManager,
        IEventCollector? eventCollector = null);

    /// <summary>
    /// Create a complete game session with all services.
    /// </summary>
    /// <param name="levelConfig">Optional level configuration.</param>
    /// <returns>Complete GameSession with engine and services.</returns>
    GameSession CreateGameSession(LevelConfig? levelConfig = null);

    /// <summary>
    /// Create a complete game session with custom configuration.
    /// </summary>
    /// <param name="configuration">Service configuration.</param>
    /// <param name="levelConfig">Optional level configuration.</param>
    /// <returns>Complete GameSession with engine and services.</returns>
    GameSession CreateGameSession(GameServiceConfiguration configuration, LevelConfig? levelConfig = null);

    ISpawnModel CreateSpawnModel(IRandom random);
    ITileGenerator CreateTileGenerator(IRandom random);
    IDeadlockDetectionSystem CreateDeadlockDetector(IMatchFinder matchFinder);
    IBoardShuffleSystem CreateShuffleSystem(IDeadlockDetectionSystem deadlockDetector);
    ILevelObjectiveSystem? CreateObjectiveSystem();
}
