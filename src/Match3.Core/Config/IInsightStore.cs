using System.Collections.Generic;

namespace Match3.Core.Config;

/// <summary>
/// Stores and retrieves design insights by category and element tags.
/// Foundation insights are always loaded; element and combination insights
/// are loaded based on the level's EffectivePool.
/// </summary>
public interface IInsightStore
{
    /// <summary>
    /// Load all insights relevant to the given element pool.
    /// - Foundation: always loaded
    /// - Element: loaded if the element is in the pool
    /// - Combination: loaded if ALL tags are in the pool
    /// </summary>
    List<DesignInsight> LoadRelevant(EffectivePool pool);

    /// <summary>
    /// Save an insight. Deduplicates by Category+Tags+Title:
    /// if exists, merges metrics and updates source; if new, appends.
    /// </summary>
    void Save(DesignInsight insight);

    /// <summary>
    /// Load all stored insights (for diagnostics/export).
    /// </summary>
    List<DesignInsight> LoadAll();

    /// <summary>Total number of stored insights.</summary>
    int Count { get; }
}
