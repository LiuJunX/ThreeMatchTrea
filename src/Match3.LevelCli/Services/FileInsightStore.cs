using System.Text.Json;
using System.Text.Json.Serialization;
using Match3.Core.Config;

namespace Match3.LevelCli.Services;

/// <summary>
/// File-based insight store. Stores insights as JSON files organized by category:
///   insights/foundation.json
///   insights/element/cage.json
///   insights/combination/cage+ice.json
/// </summary>
public sealed class FileInsightStore : IInsightStore
{
    private readonly string _baseDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public FileInsightStore(string baseDir)
    {
        _baseDir = baseDir;
    }

    public List<DesignInsight> LoadRelevant(EffectivePool pool)
    {
        var all = LoadAll();
        return InsightSelector.SelectRelevant(all, pool);
    }

    public void Save(DesignInsight insight)
    {
        var filePath = GetFilePath(insight);
        var dir = Path.GetDirectoryName(filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Load existing insights from this file
        var existing = LoadFromFile(filePath);

        // Deduplicate
        var key = insight.DeduplicationKey();
        var existingIdx = existing.FindIndex(e => e.DeduplicationKey() == key);

        if (existingIdx >= 0)
        {
            // Merge: update source, merge metrics
            var old = existing[existingIdx];
            old.Source = $"{old.Source}; {insight.Source}";
            foreach (var (k, v) in insight.Metrics)
                old.Metrics[k] = v;
            // Keep the latest recommendation
            if (!string.IsNullOrEmpty(insight.Recommendation))
                old.Recommendation = insight.Recommendation;
        }
        else
        {
            existing.Add(insight);
        }

        // Write back
        var json = JsonSerializer.Serialize(existing, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public List<DesignInsight> LoadAll()
    {
        var result = new List<DesignInsight>();

        if (!Directory.Exists(_baseDir))
            return result;

        foreach (var file in Directory.GetFiles(_baseDir, "*.json", SearchOption.AllDirectories))
        {
            result.AddRange(LoadFromFile(file));
        }

        return result;
    }

    public int Count => LoadAll().Count;

    // ── Private ──

    private string GetFilePath(DesignInsight insight)
    {
        return insight.Category.ToLowerInvariant() switch
        {
            "foundation" => Path.Combine(_baseDir, "foundation.json"),
            "element" => Path.Combine(_baseDir, "element",
                $"{TagsToFileName(insight.Tags)}.json"),
            "combination" => Path.Combine(_baseDir, "combination",
                $"{TagsToFileName(insight.Tags)}.json"),
            _ => Path.Combine(_baseDir, "other.json")
        };
    }

    private static string TagsToFileName(string[] tags)
    {
        if (tags.Length == 0) return "unknown";
        var sorted = (string[])tags.Clone();
        Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);
        return string.Join("+", sorted).ToLowerInvariant();
    }

    private static List<DesignInsight> LoadFromFile(string path)
    {
        if (!File.Exists(path)) return new List<DesignInsight>();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<DesignInsight>>(json, JsonOptions)
                ?? new List<DesignInsight>();
        }
        catch (Exception)
        {
            return new List<DesignInsight>();
        }
    }
}
