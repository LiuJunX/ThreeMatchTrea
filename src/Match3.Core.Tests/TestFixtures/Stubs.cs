using System;
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
/// </summary>
public class StubRandom : IRandom
{
    private int _counter;
    private readonly int[] _sequence;
    private ulong _state = 12345;

    /// <summary>
    /// Creates a StubRandom that returns values from a sequence.
    /// If no sequence is provided, returns 0 for all calls.
    /// </summary>
    public StubRandom(params int[] sequence)
    {
        _sequence = sequence.Length > 0 ? sequence : Array.Empty<int>();
    }

    public float NextFloat()
    {
        if (_sequence.Length > 0)
            return _sequence[_counter++ % _sequence.Length] / 100f;
        return (float)(_state++ % 1000) / 1000f;
    }

    public int Next(int max) => Next(0, max);

    public int Next(int min, int max)
    {
        if (max <= min) return min;
        if (_sequence.Length > 0)
        {
            var val = _sequence[_counter++ % _sequence.Length];
            return Math.Max(min, Math.Min(max - 1, val));
        }
        return min + (int)(_state++ % (ulong)(max - min));
    }

    public void SetState(ulong state) => _state = state;
    public ulong GetState() => _state;
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

    public int CalculateSpecialMoveScore(ElementType t1, BombType b1, ElementType t2, BombType b2) =>
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

