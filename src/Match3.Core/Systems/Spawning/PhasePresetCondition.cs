using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// A triggered preset queue that activates when a game state condition is met.
/// Priority 550: between start-of-game PresetQueue (600) and ObjectiveDrop (400).
/// Once triggered, emits a fixed preset sequence then deactivates.
/// </summary>
public class PhasePresetCondition : ISpawnCondition
{
    private readonly HashSet<int> _columns;
    private readonly ElementType[] _sequence;
    private readonly int _cycles;
    private readonly ObstacleType _triggerObstacleType;
    private readonly Dictionary<int, int> _columnPositions = new();
    private bool _triggered;

    /// <inheritdoc />
    public int Priority => 550;

    public PhasePresetCondition(
        int[] columns,
        ObstacleType triggerObstacleType,
        ElementType[] sequence,
        int cycles)
    {
        _columns = new HashSet<int>(columns);
        _triggerObstacleType = triggerObstacleType;
        _sequence = sequence;
        _cycles = cycles;
    }

    /// <inheritdoc />
    public bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context)
    {
        if (_sequence.Length == 0) return false;
        if (!_columns.Contains(spawnX)) return false;

        // Check trigger (only while not yet triggered)
        if (!_triggered)
        {
            if (BoardAnalyzer.CountObstacleOnBoard(ref state, _triggerObstacleType) == 0)
                _triggered = true;
            else
                return false;
        }

        // Check exhaustion
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
