using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// ISpawnModel implementation that evaluates spawn conditions by priority.
/// Higher priority conditions are checked first; DefaultCondition always matches as fallback.
/// </summary>
public class ConditionBasedSpawnModel : ISpawnModel
{
    private readonly ISpawnCondition[] _conditions;

    /// <summary>
    /// Creates a condition-based spawn model.
    /// Conditions must be pre-sorted by priority descending.
    /// </summary>
    public ConditionBasedSpawnModel(ISpawnCondition[] conditions)
    {
        _conditions = conditions;
    }

    /// <inheritdoc />
    public ElementType Predict(ref GameState state, int spawnX, in SpawnContext context)
    {
        for (int i = 0; i < _conditions.Length; i++)
        {
            if (_conditions[i].IsConditionMet(ref state, spawnX, in context))
            {
                return _conditions[i].Generate(ref state, spawnX, in context);
            }
        }

        // Should never reach here if DefaultCondition is included
        return ElementType.Item1;
    }

    /// <summary>
    /// Number of conditions for testing/inspection.
    /// </summary>
    internal int ConditionCount => _conditions.Length;

    /// <summary>
    /// Gets a condition by index for testing/inspection.
    /// </summary>
    internal ISpawnCondition GetCondition(int index) => _conditions[index];
}
