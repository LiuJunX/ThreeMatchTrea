using System.Text;
using Match3.Core.Config;
using Match3.Core.Models.Enums;

namespace Match3.LevelCli.Services;

/// <summary>
/// Loads relevant knowledge documents from the filesystem.
/// Selects docs based on elements available in the EffectivePool.
/// </summary>
public sealed class KnowledgeDocService
{
    private readonly string _knowledgeDir;

    public KnowledgeDocService(string knowledgeDir)
    {
        _knowledgeDir = knowledgeDir;
    }

    /// <summary>
    /// Load knowledge docs relevant for a given level.
    /// </summary>
    public Dictionary<string, string> LoadForLevel(
        EffectivePool pool, RhythmCategory rhythm, int maxObjectives)
    {
        var docs = new Dictionary<string, string>();

        // 1. Always load core docs
        TryLoad(docs, "core/matching.md");
        TryLoad(docs, "core/gravity-and-cascade.md");
        TryLoad(docs, "core/difficulty-levers.md");
        TryLoad(docs, "core/bombs-and-powerups.md");

        // 2. Load per-element docs
        foreach (var obs in pool.Obstacles.Keys)
            TryLoad(docs, $"elements/obstacle-{obs.ToString().ToLowerInvariant()}.md");

        foreach (var cov in pool.Covers.Keys)
            TryLoad(docs, $"elements/cover-{cov.ToString().ToLowerInvariant()}.md");

        foreach (var gnd in pool.Grounds.Keys)
            TryLoad(docs, $"elements/ground-{gnd.ToString().ToLowerInvariant()}.md");

        if (pool.MovingObstacles.Count > 0)
            TryLoad(docs, "elements/moving-obstacles.md");

        // 3. Load pattern docs
        if (rhythm == RhythmCategory.Boss)
            TryLoad(docs, "patterns/boss-level.md");

        if (pool.Obstacles.Count == 0 && pool.Covers.Count == 0 && pool.Grounds.Count == 0)
            TryLoad(docs, "patterns/tutorial-intro.md");

        if (maxObjectives >= 2)
            TryLoad(docs, "patterns/dual-objective.md");

        return docs;
    }

    /// <summary>
    /// Build a knowledge docs loader function for use with SmartQualityPipeline.
    /// </summary>
    public Func<int, Dictionary<string, string>> CreateLoaderForBlueprint(
        ProgressionBlueprint blueprint)
    {
        return levelNumber =>
        {
            var pool = EffectivePool.Build(blueprint, levelNumber);
            var phase = BlueprintValidator.FindPhase(blueprint, levelNumber);
            var rhythm = DifficultyBudget.ClassifyRhythm(
                pool, levelNumber, new Match3.Random.XorShift64((ulong)levelNumber));
            return LoadForLevel(pool, rhythm, phase?.MaxObjectives ?? 1);
        };
    }

    private void TryLoad(Dictionary<string, string> docs, string relativePath)
    {
        var fullPath = Path.Combine(_knowledgeDir, relativePath);
        if (File.Exists(fullPath))
        {
            docs[relativePath] = File.ReadAllText(fullPath);
        }
    }

    /// <summary>
    /// Concatenate all loaded docs into a single string for prompt injection.
    /// </summary>
    public static string ConcatenateDocs(Dictionary<string, string> docs)
    {
        var sb = new StringBuilder();
        foreach (var (path, content) in docs)
        {
            sb.AppendLine($"### {path}");
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
