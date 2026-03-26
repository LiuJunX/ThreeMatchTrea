using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core.Config;

/// <summary>
/// Selects insights relevant to a given EffectivePool.
/// Pure logic, no IO.
/// </summary>
public static class InsightSelector
{
    /// <summary>
    /// Filter insights to only those relevant for the given pool.
    /// - foundation: always included
    /// - element: included if the single tag is in the pool
    /// - combination: included if ALL tags are in the pool
    /// </summary>
    public static List<DesignInsight> SelectRelevant(
        List<DesignInsight> allInsights, EffectivePool pool)
    {
        var poolElements = CollectPoolElementNames(pool);
        var result = new List<DesignInsight>();

        foreach (var insight in allInsights)
        {
            if (IsRelevant(insight, poolElements))
                result.Add(insight);
        }

        return result;
    }

    /// <summary>
    /// Check if a single insight is relevant for the given pool elements.
    /// </summary>
    public static bool IsRelevant(DesignInsight insight, HashSet<string> poolElements)
    {
        if (string.Equals(insight.Category, "foundation", StringComparison.OrdinalIgnoreCase))
            return true;

        if (insight.Tags.Length == 0)
            return true; // No tags = universal

        // HashSet already uses OrdinalIgnoreCase comparer; use native Contains (O(1))
        return insight.Tags.All(tag => poolElements.Contains(tag));
    }

    /// <summary>
    /// Collect all element type names from an EffectivePool
    /// (obstacles, covers, grounds, moving obstacles).
    /// </summary>
    public static HashSet<string> CollectPoolElementNames(EffectivePool pool)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in pool.Obstacles.Keys)
            names.Add(key.ToString());

        foreach (var key in pool.Covers.Keys)
            names.Add(key.ToString());

        foreach (var key in pool.Grounds.Keys)
            names.Add(key.ToString());

        foreach (var key in pool.MovingObstacles.Keys)
            names.Add(key.ToString());

        return names;
    }

    /// <summary>
    /// Format insights as text for LLM prompt injection.
    /// Groups by category for readability.
    /// </summary>
    public static string FormatForPrompt(List<DesignInsight> insights)
    {
        if (insights.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 积累经验（来自历史迭代）");
        sb.AppendLine();

        // Group by category
        var foundation = insights.Where(i =>
            string.Equals(i.Category, "foundation", StringComparison.OrdinalIgnoreCase)).ToList();
        var elements = insights.Where(i =>
            string.Equals(i.Category, "element", StringComparison.OrdinalIgnoreCase)).ToList();
        var combos = insights.Where(i =>
            string.Equals(i.Category, "combination", StringComparison.OrdinalIgnoreCase)).ToList();

        if (foundation.Count > 0)
        {
            sb.AppendLine("### 基础经验");
            foreach (var i in foundation)
                sb.AppendLine($"- **{i.Title}**: {i.Recommendation}");
            sb.AppendLine();
        }

        // Group element insights by first tag
        if (elements.Count > 0)
        {
            var byTag = elements.GroupBy(i => i.Tags.Length > 0 ? i.Tags[0] : "");
            foreach (var g in byTag)
            {
                sb.AppendLine($"### {g.Key} 经验");
                foreach (var i in g)
                    sb.AppendLine($"- **{i.Title}**: {i.Recommendation}");
                sb.AppendLine();
            }
        }

        if (combos.Count > 0)
        {
            var byTags = combos.GroupBy(i => string.Join("+", i.Tags));
            foreach (var g in byTags)
            {
                sb.AppendLine($"### {g.Key} 组合经验");
                foreach (var i in g)
                    sb.AppendLine($"- **{i.Title}**: {i.Recommendation}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
