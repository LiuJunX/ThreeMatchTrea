using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Random;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Creates spawn conditions from level configuration.
/// Supports per-spawner config (PresetQueue, WeightedDefault) and auto-derived ObjectiveDropConditions.
/// </summary>
public static class SpawnConditionFactory
{
    /// <summary>
    /// Creates a ConditionBasedSpawnModel with isolated RNG streams.
    /// </summary>
    /// <param name="colorCount">Number of active colors (1-6).</param>
    /// <param name="objectives">Level objectives (may contain inactive slots).</param>
    /// <param name="moveLimit">Total moves allowed in the level.</param>
    /// <param name="colorRng">RNG for color generation (DefaultCondition). Use RandomDomain.Refill.</param>
    /// <param name="dropRng">RNG for collectible drop decisions (ObjectiveDropCondition). Use RandomDomain.Drop.</param>
    /// <param name="spawners">Per-spawner configurations (optional). null = all columns use default.</param>
    public static ConditionBasedSpawnModel Create(
        int colorCount,
        LevelObjective[] objectives,
        int moveLimit,
        IRandom colorRng,
        IRandom dropRng,
        SpawnerConfig[]? spawners = null)
    {
        var conditions = new List<ISpawnCondition>();

        // 1. PresetQueue conditions (priority 600) — from spawner config
        if (spawners != null)
        {
            foreach (var spawner in spawners)
            {
                if (spawner.Preset?.Sequence is { Length: > 0 })
                {
                    conditions.Add(new PresetQueueCondition(
                        spawner.Columns, spawner.Preset.Sequence, spawner.Preset.Cycles));
                }
            }
        }

        // 2. ObjectiveDropConditions (priority 400) — auto-derived from objectives
        if (objectives != null)
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].TargetLayer == ObjectiveTargetLayer.None)
                    continue;

                var condition = ObjectiveDropCondition.FromObjective(
                    objectives[i], i, moveLimit, dropRng);
                if (condition != null)
                    conditions.Add(condition);
            }
        }

        // 3. WeightedDefaultConditions (priority 100) — from spawner config
        //    Added before global DefaultCondition so they match first for claimed columns.
        if (spawners != null)
        {
            foreach (var spawner in spawners)
            {
                if (spawner.Weights is { Count: > 0 })
                {
                    conditions.Add(new WeightedDefaultCondition(
                        spawner.Columns, spawner.Weights, colorRng));
                }
            }
        }

        // 4. Global DefaultCondition (priority 100) — fallback for unclaimed columns
        conditions.Add(new DefaultCondition(colorRng, colorCount));

        // Sort by priority descending (stable sort preserves insertion order for same priority)
        conditions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        return new ConditionBasedSpawnModel(conditions.ToArray());
    }

    /// <summary>
    /// Creates a ConditionBasedSpawnModel using a SeedManager for automatic RNG isolation.
    /// </summary>
    public static ConditionBasedSpawnModel Create(
        int colorCount,
        LevelObjective[] objectives,
        int moveLimit,
        SeedManager seedManager,
        SpawnerConfig[]? spawners = null)
    {
        return Create(
            colorCount, objectives, moveLimit,
            seedManager.GetRandom(RandomDomain.Refill),
            seedManager.GetRandom(RandomDomain.Drop),
            spawners);
    }
}
