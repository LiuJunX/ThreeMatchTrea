using System;
using System.Collections.Generic;

namespace Match3.Core.Config;

/// <summary>
/// A single design insight extracted from level generation iterations.
/// Organized in three layers: foundation (always loaded), element (per-element),
/// combination (multi-element interactions).
/// </summary>
public sealed class DesignInsight
{
    /// <summary>
    /// Insight category: "foundation", "element", or "combination".
    /// Determines when this insight is loaded for a level design.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// Element tags this insight relates to.
    /// Foundation: empty. Element: ["Cage"]. Combination: ["Cage", "Ice"].
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>Short title for the insight.</summary>
    public string Title { get; set; } = "";

    /// <summary>What was discovered (data-backed).</summary>
    public string Finding { get; set; } = "";

    /// <summary>What to do about it.</summary>
    public string Recommendation { get; set; } = "";

    /// <summary>Source: level number, attempt, etc.</summary>
    public string Source { get; set; } = "";

    /// <summary>Quantitative evidence (e.g., "before" → "47%", "after" → "84.5%").</summary>
    public Dictionary<string, string> Metrics { get; set; } = new();

    /// <summary>
    /// Deduplication key: Category + sorted Tags + Title.
    /// </summary>
    public string DeduplicationKey()
    {
        var sortedTags = Tags.Length > 0 ? string.Join("+", SortedTags()) : "";
        return $"{Category}|{sortedTags}|{Title}";
    }

    private string[] SortedTags()
    {
        var copy = (string[])Tags.Clone();
        Array.Sort(copy, StringComparer.OrdinalIgnoreCase);
        return copy;
    }
}
