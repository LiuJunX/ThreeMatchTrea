using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;

namespace Match3.Core.Config;

/// <summary>
/// Designs levels with semantic understanding of element interactions.
/// Core interface — implementation may use LLM, rules, or manual input.
/// </summary>
public interface ILevelDesigner
{
    /// <summary>
    /// Design a level given blueprint constraints and context.
    /// </summary>
    Task<LevelDesign> DesignAsync(
        LevelDesignContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Revise a previous design based on analysis feedback.
    /// </summary>
    Task<LevelDesign> ReviseAsync(
        LevelDesign previousDesign,
        LevelAnalysisResult analysisResult,
        LevelDesignContext context,
        CancellationToken ct = default);
}

/// <summary>
/// All context needed for designing a single level.
/// Knowledge docs are injected by the outer layer (CLI/IO) to keep Core pure.
/// </summary>
public sealed class LevelDesignContext
{
    /// <summary>The progression blueprint.</summary>
    public ProgressionBlueprint Blueprint { get; set; } = null!;

    /// <summary>Target level number (1-based).</summary>
    public int LevelNumber { get; set; }

    /// <summary>Accumulated element pool for this level.</summary>
    public EffectivePool Pool { get; set; } = null!;

    /// <summary>The phase this level belongs to.</summary>
    public PhaseConfig Phase { get; set; } = null!;

    /// <summary>
    /// Knowledge document contents, keyed by relative path.
    /// e.g., "core/matching.md" → content string.
    /// Populated by the IO layer before calling DesignAsync.
    /// </summary>
    public Dictionary<string, string> KnowledgeDocs { get; set; } = new();

    /// <summary>
    /// Accumulated design insights relevant to this level.
    /// Filtered by InsightSelector based on the EffectivePool.
    /// Foundation insights are always present; element and combination
    /// insights are included only when their tags match the pool.
    /// </summary>
    public List<DesignInsight> Insights { get; set; } = new();
}
