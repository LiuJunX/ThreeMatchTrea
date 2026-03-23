using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Spawns elements from a fixed preset sequence. Highest priority (600).
/// Each column independently maintains its own queue position.
/// After all cycles exhaust, IsConditionMet returns false and lower-priority conditions take over.
/// </summary>
public class PresetQueueCondition : ISpawnCondition
{
    private readonly HashSet<int> _columns;
    private readonly ElementType[] _sequence;
    private readonly int _cycles;
    private readonly Dictionary<int, int> _columnPositions = new();

    /// <inheritdoc />
    public int Priority => 600;

    /// <summary>
    /// Creates a preset queue condition.
    /// </summary>
    /// <param name="columns">Column indices this condition applies to.</param>
    /// <param name="sequence">Fixed sequence of element types.</param>
    /// <param name="cycles">Repeat count: 1 = once, 0 = infinite, N = N times.</param>
    public PresetQueueCondition(int[] columns, ElementType[] sequence, int cycles)
    {
        _columns = new HashSet<int>(columns);
        _sequence = sequence;
        _cycles = cycles;
    }

    /// <inheritdoc />
    public bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context)
    {
        if (_sequence.Length == 0) return false;
        if (!_columns.Contains(spawnX)) return false;

        if (_cycles == 0) return true; // infinite

        int pos = _columnPositions.GetValueOrDefault(spawnX, 0);
        return pos < _sequence.Length * _cycles;
    }

    /// <inheritdoc />
    public ElementType Generate(ref GameState state, int spawnX, in SpawnContext context)
    {
        int pos = _columnPositions.GetValueOrDefault(spawnX, 0);
        var result = _sequence[pos % _sequence.Length];
        _columnPositions[spawnX] = pos + 1;
        return result;
    }
}
