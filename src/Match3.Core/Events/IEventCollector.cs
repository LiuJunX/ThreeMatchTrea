using System.Collections.Generic;
using System.Linq;

namespace Match3.Core.Events;

/// <summary>
/// Interface for collecting events during simulation.
/// AI simulation uses NullEventCollector for zero overhead.
/// Human play uses BufferedEventCollector for presentation.
/// </summary>
public interface IEventCollector
{
    /// <summary>
    /// Emit a single event.
    /// </summary>
    void Emit(GameEvent evt);

    /// <summary>
    /// Emit multiple events at once.
    /// </summary>
    void EmitBatch(IEnumerable<GameEvent> events);

    /// <summary>
    /// Whether event collection is enabled.
    /// Systems can skip event creation when this is false.
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// No-op event collector for AI high-speed simulation.
/// All methods are no-ops with zero allocation overhead.
/// </summary>
public sealed class NullEventCollector : IEventCollector
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullEventCollector Instance = new();

    private NullEventCollector() { }

    /// <inheritdoc />
    public void Emit(GameEvent evt) { }

    /// <inheritdoc />
    public void EmitBatch(IEnumerable<GameEvent> events) { }

    /// <inheritdoc />
    public bool IsEnabled => false;
}

/// <summary>
/// Buffered event collector for presentation layer.
/// Collects events during simulation for later playback.
/// </summary>
public sealed class BufferedEventCollector : IEventCollector
{
    private readonly List<GameEvent> _events = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public void Emit(GameEvent evt)
    {
        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    /// <inheritdoc />
    public void EmitBatch(IEnumerable<GameEvent> events)
    {
        lock (_lock)
        {
            _events.AddRange(events);
        }
    }

    /// <summary>
    /// Get all collected events. Allocates a new list.
    /// For high-performance scenarios, use CopyEventsTo instead.
    /// </summary>
    public IReadOnlyList<GameEvent> GetEvents()
    {
        lock (_lock)
        {
            var result = new List<GameEvent>(_events.Count);
            result.AddRange(_events);
            return result;
        }
    }

    /// <summary>
    /// Get events and clear the buffer. Allocates a new list.
    /// For high-performance scenarios, use DrainEventsTo instead.
    /// </summary>
    public IReadOnlyList<GameEvent> DrainEvents()
    {
        lock (_lock)
        {
            var result = new List<GameEvent>(_events.Count);
            result.AddRange(_events);
            _events.Clear();
            return result;
        }
    }

    /// <summary>
    /// Copy all events to a caller-provided list. Zero allocation.
    /// </summary>
    public void CopyEventsTo(IList<GameEvent> target)
    {
        lock (_lock)
        {
            foreach (var evt in _events)
            {
                target.Add(evt);
            }
        }
    }

    /// <summary>
    /// Drain all events to a caller-provided list and clear the buffer. Zero allocation.
    /// </summary>
    public void DrainEventsTo(IList<GameEvent> target)
    {
        lock (_lock)
        {
            foreach (var evt in _events)
            {
                target.Add(evt);
            }
            _events.Clear();
        }
    }

    /// <summary>
    /// Clear all collected events.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    /// <summary>
    /// Number of events in buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }
    }
}
