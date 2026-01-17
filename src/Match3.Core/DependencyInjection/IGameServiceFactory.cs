using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Factory for creating game service instances with configured dependencies.
/// Abstracts away the manual assembly of 15+ systems.
/// </summary>
public interface IGameServiceFactory
{
    /// <summary>
    /// Create a SimulationEngine with all required dependencies.
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
}
