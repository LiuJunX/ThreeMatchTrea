namespace Match3.Core.DependencyInjection;

/// <summary>
/// Immutable configuration for game services.
/// Supports serialization for replay/save functionality.
/// </summary>
public sealed record GameServiceConfiguration
{
    /// <summary>Board width in cells.</summary>
    public int Width { get; init; } = 8;

    /// <summary>Board height in cells.</summary>
    public int Height { get; init; } = 8;

    /// <summary>Number of tile types.</summary>
    public int TileTypesCount { get; init; } = 6;

    /// <summary>Random seed for deterministic replay.</summary>
    public int RngSeed { get; init; }

    /// <summary>Simulation configuration.</summary>
    public Simulation.SimulationConfig SimulationConfig { get; init; } = Simulation.SimulationConfig.ForHumanPlay();

    /// <summary>Whether to enable event collection for presentation.</summary>
    public bool EnableEventCollection { get; init; } = true;

    /// <summary>
    /// Creates a default configuration with a random seed.
    /// </summary>
    public static GameServiceConfiguration CreateDefault()
    {
        return new GameServiceConfiguration
        {
            RngSeed = System.Environment.TickCount
        };
    }

    /// <summary>
    /// Creates a configuration for AI simulation (no events).
    /// </summary>
    public static GameServiceConfiguration ForAISimulation(int seed)
    {
        return new GameServiceConfiguration
        {
            RngSeed = seed,
            EnableEventCollection = false,
            SimulationConfig = Simulation.SimulationConfig.ForAI()
        };
    }
}
