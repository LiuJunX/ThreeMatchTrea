using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Column-scoped default condition that generates elements by configured weight table.
/// Priority 100 (same as DefaultCondition). Only fires for columns in its column set;
/// unclaimed columns fall through to the global DefaultCondition.
/// Includes anti-streak safety net for color elements.
/// </summary>
public class WeightedDefaultCondition : ISpawnCondition
{
    private readonly HashSet<int> _columns;
    private readonly ElementType[] _elements;
    private readonly int[] _weights;
    private readonly int _totalWeight;
    private readonly IRandom _rng;

    /// <inheritdoc />
    public int Priority => 100;

    /// <summary>
    /// Creates a weighted default condition.
    /// </summary>
    /// <param name="columns">Column indices this condition applies to.</param>
    /// <param name="weights">Element type → relative weight mapping.</param>
    /// <param name="rng">Random number generator (seed-driven).</param>
    public WeightedDefaultCondition(int[] columns, Dictionary<ElementType, int> weights, IRandom rng)
    {
        _columns = new HashSet<int>(columns);
        _rng = rng;

        // Flatten to parallel arrays, skip zero-weight entries.
        // Sort by key to guarantee deterministic order across .NET versions.
        var elements = new List<ElementType>();
        var weightValues = new List<int>();
        int total = 0;
        foreach (var kv in weights.OrderBy(kv => kv.Key))
        {
            if (kv.Value > 0)
            {
                elements.Add(kv.Key);
                weightValues.Add(kv.Value);
                total += kv.Value;
            }
        }
        _elements = elements.ToArray();
        _weights = weightValues.ToArray();
        _totalWeight = total;
    }

    /// <inheritdoc />
    public bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context)
    {
        return _totalWeight > 0 && _columns.Contains(spawnX);
    }

    /// <inheritdoc />
    public ElementType Generate(ref GameState state, int spawnX, in SpawnContext context)
    {
        var result = PickWeightedRandom();
        return ApplyAntiStreak(ref state, spawnX, result);
    }

    private ElementType PickWeightedRandom()
    {
        int roll = _rng.Next(0, _totalWeight);
        int cumulative = 0;
        for (int i = 0; i < _elements.Length; i++)
        {
            cumulative += _weights[i];
            if (roll < cumulative)
                return _elements[i];
        }
        return _elements[_elements.Length - 1];
    }

    /// <summary>
    /// Anti-streak: if a color element matches the column top, re-roll once from the weight table
    /// excluding that color. Non-color elements (Bird, bombs) are never deflected.
    /// </summary>
    private ElementType ApplyAntiStreak(ref GameState state, int spawnX, ElementType result)
    {
        if (!result.IsColor()) return result;

        var topColor = BoardAnalyzer.GetColumnTopColor(ref state, spawnX);
        if (result != topColor || topColor == ElementType.None) return result;

        // Calculate remaining weight excluding the matched color
        int excludedWeight = 0;
        for (int i = 0; i < _elements.Length; i++)
        {
            if (_elements[i] == result)
                excludedWeight += _weights[i];
        }

        int remaining = _totalWeight - excludedWeight;
        if (remaining <= 0) return result; // no alternatives

        int roll = _rng.Next(0, remaining);
        int cumulative = 0;
        for (int i = 0; i < _elements.Length; i++)
        {
            if (_elements[i] == result) continue;
            cumulative += _weights[i];
            if (roll < cumulative)
                return _elements[i];
        }

        return result;
    }
}
