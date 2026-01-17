using System;
using Match3.Core.Events;
using Match3.Core.Simulation;
using Match3.Random;

namespace Match3.Core.DependencyInjection;

/// <summary>
/// Encapsulates a complete game session with all required services.
/// Replaces the manual assembly in Match3GameService.
/// </summary>
public sealed class GameSession : IDisposable
{
    /// <summary>The simulation engine.</summary>
    public SimulationEngine Engine { get; }

    /// <summary>Event collector (may be null for AI simulation).</summary>
    public IEventCollector EventCollector { get; }

    /// <summary>Seed manager for deterministic random.</summary>
    public SeedManager SeedManager { get; }

    /// <summary>Configuration used to create this session.</summary>
    public GameServiceConfiguration Configuration { get; }

    internal GameSession(
        SimulationEngine engine,
        IEventCollector eventCollector,
        SeedManager seedManager,
        GameServiceConfiguration configuration)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        EventCollector = eventCollector ?? throw new ArgumentNullException(nameof(eventCollector));
        SeedManager = seedManager ?? throw new ArgumentNullException(nameof(seedManager));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Drain events from the collector (if buffered).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<GameEvent> DrainEvents()
    {
        return EventCollector is BufferedEventCollector buffered
            ? buffered.DrainEvents()
            : System.Array.Empty<GameEvent>();
    }

    /// <summary>
    /// Dispose the session and its resources.
    /// </summary>
    public void Dispose()
    {
        Engine.Dispose();
    }
}
