using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core.Tests.TestFixtures;

/// <summary>
/// Shared stub implementations for unit tests.
/// Use these instead of defining local stubs to reduce duplication.
/// </summary>

/// <summary>
/// A predictable random implementation for deterministic tests.
/// <para>Modes of operation (highest to lowest priority):</para>
/// <list type="number">
///   <item>Queue — values added via <see cref="EnqueueValues"/> are dequeued first.</item>
///   <item>Sequence — values passed via the constructor are cycled.</item>
///   <item>Fallback — an incrementing state counter produces varying values.</item>
/// </list>
/// </summary>
public class StubRandom : IRandom
{
    private int _counter;
    private readonly int[] _sequence;
    private ulong _state = 12345;
    private readonly Queue<int> _queue = new();

    /// <summary>
    /// Gets or sets a fixed return value used by the fallback path.
    /// When set, the fallback path returns <c>min + (ReturnValue % range)</c>
    /// instead of using the incrementing state counter.
    /// </summary>
    public int ReturnValue
    {
        get => _returnValue;
        set
        {
            _returnValue = value;
            _useReturnValue = true;
        }
    }

    private int _returnValue;
    private bool _useReturnValue;

    /// <summary>
    /// Creates a StubRandom that returns values from a sequence.
    /// If no sequence is provided, uses the fallback path (incrementing state).
    /// </summary>
    public StubRandom(params int[] sequence)
    {
        _sequence = sequence.Length > 0 ? sequence : Array.Empty<int>();
    }

    /// <summary>
    /// Creates a StubRandom with a fixed return value and no sequence.
    /// Equivalent to creating <c>new StubRandom()</c> then setting <see cref="ReturnValue"/>.
    /// </summary>
    public static StubRandom WithFixedValue(int value)
    {
        var stub = new StubRandom();
        stub.ReturnValue = value;
        return stub;
    }

    /// <summary>
    /// Enqueues values that will be consumed before the sequence or fallback path.
    /// </summary>
    public void EnqueueValues(params int[] values)
    {
        foreach (var v in values)
            _queue.Enqueue(v);
    }

    public float NextFloat()
    {
        if (_queue.Count > 0)
            return _queue.Dequeue() / 100f;
        if (_sequence.Length > 0)
            return _sequence[_counter++ % _sequence.Length] / 100f;
        if (_useReturnValue)
            return 0f;
        return (float)(_state++ % 1000) / 1000f;
    }

    public int Next(int max) => Next(0, max);

    public int Next(int min, int max)
    {
        if (max <= min) return min;
        if (_queue.Count > 0)
        {
            var raw = _queue.Dequeue();
            return min + (raw % (max - min));
        }
        if (_sequence.Length > 0)
        {
            var val = _sequence[_counter++ % _sequence.Length];
            return Math.Max(min, Math.Min(max - 1, val));
        }
        if (_useReturnValue)
            return min + (_returnValue % (max - min));
        return min + (int)(_state++ % (ulong)(max - min));
    }

    public void SetState(ulong state) => _state = state;
    public ulong GetState() => _state;
}

/// <summary>
/// A sequential random that returns <c>min + (_counter++ % range)</c>.
/// Useful when tests need deterministic, monotonically-varying values.
/// </summary>
public class SequentialRandom : IRandom
{
    private int _counter;

    public float NextFloat() => 0f;
    public int Next(int max) => Next(0, max);

    public int Next(int min, int max)
    {
        if (max <= min) return min;
        return min + (_counter++ % (max - min));
    }

    public void SetState(ulong state) => _counter = (int)state;
    public ulong GetState() => (ulong)_counter;
}

/// <summary>
/// A no-op logger for tests that don't need logging.
/// </summary>
public class StubLogger : IGameLogger
{
    public static readonly StubLogger Instance = new();

    public void LogInfo(string message) { }
    public void LogInfo<T>(string template, T arg1) { }
    public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) { }
    public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) { }
    public void LogWarning(string message) { }
    public void LogWarning<T>(string template, T arg1) { }
    public void LogError(string message, Exception? ex = null) { }
}

/// <summary>
/// A simple score system that returns fixed values.
/// </summary>
public class StubScoreSystem : IScoreSystem
{
    public int MatchScoreMultiplier { get; set; } = 10;
    public int SpecialMoveScore { get; set; } = 100;

    public int CalculateMatchScore(MatchGroup match) =>
        match.Positions.Count * MatchScoreMultiplier;

    public int CalculateSpecialMoveScore(ElementType t1, ElementType t2) =>
        SpecialMoveScore;
}

/// <summary>
/// A spawn model that returns tiles in a predictable sequence.
/// </summary>
public class StubSpawnModel : ISpawnModel
{
    private int _counter;
    private readonly ElementType[] _types;

    /// <summary>
    /// Creates a StubSpawnModel that cycles through a sequence of tile types.
    /// Default sequence: Red, Blue, Green, Yellow, Purple.
    /// </summary>
    public StubSpawnModel(params ElementType[] types)
    {
        _types = types.Length > 0
            ? types
            : new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };
    }

    public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
    {
        return _types[(_counter++ + spawnX) % _types.Length];
    }
}

/// <summary>
/// A tile generator that returns tiles in a predictable sequence.
/// </summary>
public class StubTileGenerator : ITileGenerator
{
    private readonly ElementType[] _sequence;
    private int _index;

    public StubTileGenerator(params ElementType[] sequence)
    {
        _sequence = sequence.Length > 0
            ? sequence
            : new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2 };
    }

    public ElementType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        return _sequence[_index++ % _sequence.Length];
    }
}

/// <summary>
/// A stub event collector that tracks emitted events for verification.
/// </summary>
public class StubEventCollector : IEventCollector
{
    private readonly System.Collections.Generic.List<GameEvent> _events = new();

    public bool IsEnabled { get; set; } = true;

    public System.Collections.Generic.IReadOnlyList<GameEvent> Events => _events;

    public void Emit(GameEvent evt)
    {
        if (IsEnabled)
            _events.Add(evt);
    }

    public void EmitBatch(System.Collections.Generic.IEnumerable<GameEvent> events)
    {
        if (IsEnabled)
            _events.AddRange(events);
    }

    public void Clear() => _events.Clear();

    public int Count => _events.Count;
}

