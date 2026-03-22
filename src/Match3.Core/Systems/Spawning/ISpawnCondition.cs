using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// A prioritized condition that may generate a specific element during spawning.
/// Higher priority conditions are evaluated first; DefaultCondition always matches as fallback.
/// </summary>
public interface ISpawnCondition
{
    /// <summary>
    /// Priority of this condition. Higher values are evaluated first.
    /// Standard priorities: Default=100, EventDrop=300, ObjectiveDrop=400, PresetQueue=500.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Checks whether this condition should fire for the given spawn position.
    /// May update internal state (e.g., PRD counters).
    /// </summary>
    bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context);

    /// <summary>
    /// Generates the element type. Only called when IsConditionMet returns true.
    /// </summary>
    ElementType Generate(ref GameState state, int spawnX, in SpawnContext context);
}
