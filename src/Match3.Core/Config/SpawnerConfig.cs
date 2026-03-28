using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

/// <summary>
/// Configuration for a single spawner that controls element generation for specific columns.
/// Multiple columns can share one spawner. Columns not claimed by any spawner use global default.
/// </summary>
[Serializable]
public class SpawnerConfig
{
    /// <summary>Unique identifier for this spawner within the level.</summary>
    public int Id { get; set; }

    /// <summary>Column indices this spawner controls (0-based).</summary>
    public int[] Columns { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Element drop weights (optional). Key = ElementType, Value = relative weight.
    /// Probability = weight / sum(weights). Omitted types have weight 0.
    /// null = use global default uniform distribution.
    /// </summary>
    public Dictionary<ElementType, int>? Weights { get; set; }

    /// <summary>
    /// Preset drop queue (optional). Highest priority — fires before all other conditions.
    /// Each column independently maintains its own queue position.
    /// </summary>
    public PresetQueueConfig? Preset { get; set; }

    /// <summary>
    /// Phase-triggered presets (optional). Each phase has a trigger condition and a preset queue.
    /// When the trigger condition is met, the phase's preset queue activates (priority 550).
    /// null or empty = no phase triggers.
    /// </summary>
    public SpawnerPhaseConfig[]? Phases { get; set; }
}

/// <summary>
/// Configuration for a fixed element drop sequence.
/// Elements are consumed one per empty slot fill. After all cycles exhaust,
/// the spawner falls through to lower-priority conditions.
/// </summary>
[Serializable]
public class PresetQueueConfig
{
    /// <summary>Fixed sequence of element types to emit, in order.</summary>
    public ElementType[] Sequence { get; set; } = Array.Empty<ElementType>();

    /// <summary>
    /// Number of times to repeat the sequence.
    /// 1 = play once (default), 0 = infinite loop, N = play N times.
    /// </summary>
    public int Cycles { get; set; } = 1;
}

/// <summary>
/// A spawner phase that activates its preset queue when a trigger condition is met.
/// </summary>
[Serializable]
public class SpawnerPhaseConfig
{
    public PhaseTriggerConfig Trigger { get; set; } = new();
    public PresetQueueConfig Preset { get; set; } = new();
}

/// <summary>
/// Trigger condition for a spawner phase.
/// Type = "obstacleCleared" → activates when all obstacles of ObstacleType are destroyed.
/// </summary>
[Serializable]
public class PhaseTriggerConfig
{
    /// <summary>Trigger type. Currently supported: "obstacleCleared".</summary>
    public string Type { get; set; } = "";

    /// <summary>For "obstacleCleared": the obstacle type name (e.g., "Box").</summary>
    public string ObstacleType { get; set; } = "";
}
