using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;

namespace Match3.Core.Config;

/// <summary>
/// Extracts design insights from a design + analysis result pair.
/// Implementation may use LLM or rules.
/// </summary>
public interface IInsightExtractor
{
    /// <summary>
    /// Compare design intent against analysis results and extract 0-N insights.
    /// </summary>
    Task<List<DesignInsight>> ExtractAsync(
        LevelDesign design,
        LevelAnalysisResult analysisResult,
        LevelDesignContext context,
        CancellationToken ct = default);
}
