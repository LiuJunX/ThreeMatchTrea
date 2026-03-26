using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Match3.Core.Analysis;
using Match3.Core.Config;

namespace Match3.LevelCli.Services;

/// <summary>
/// Extracts design insights using Claude API.
/// Compares design intent against analysis results and produces 0-3 insights per iteration.
/// </summary>
public sealed class LlmInsightExtractor : IInsightExtractor, IDisposable
{
    private readonly LlmConfig _config;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public LlmInsightExtractor(LlmConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _config.ApiKey);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public async Task<List<DesignInsight>> ExtractAsync(
        LevelDesign design,
        LevelAnalysisResult analysisResult,
        LevelDesignContext context,
        CancellationToken ct = default)
    {
        var prompt = BuildExtractionPrompt(design, analysisResult, context);
        var toolSchema = BuildToolSchema();

        var requestBody = new
        {
            model = _config.Model,
            max_tokens = 2048,
            temperature = 0.3f, // Low temperature for factual extraction
            system = "你是三消游戏关卡设计的经验分析师。从设计和分析结果中提炼可复用的经验。",
            tools = new[] { toolSchema },
            tool_choice = new { type = "tool", name = "save_insights" },
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return new List<DesignInsight>(); // Graceful fallback

        return ParseInsightsResponse(responseBody);
    }

    private static string BuildExtractionPrompt(
        LevelDesign design, LevelAnalysisResult analysis, LevelDesignContext context)
    {
        var phase = context.Phase;
        bool passed = analysis.WinRate >= phase.MinWinRate &&
                      analysis.WinRate <= phase.MaxWinRate &&
                      analysis.DeadlockRate <= 0.05f;

        var sb = new StringBuilder();
        sb.AppendLine($"关卡 {context.LevelNumber}（阶段: {phase.Name}）的设计和分析结果：");
        sb.AppendLine();
        sb.AppendLine("## 设计");
        sb.AppendLine($"- 意图: {design.DesignIntent}");
        sb.AppendLine($"- 棋盘: {design.Width}×{design.Height} {design.Shape}");
        sb.AppendLine($"- 颜色: {design.ColorCount}, 步数: {design.MoveLimit}");

        if (design.Obstacles.Count > 0)
            sb.AppendLine($"- 障碍物: {string.Join(", ", design.Obstacles.Select(o => $"{o.ElementType}×{o.Count}({o.Strategy})"))}");
        if (design.Covers.Count > 0)
            sb.AppendLine($"- Cover: {string.Join(", ", design.Covers.Select(c => $"{c.ElementType}×{c.Count}({c.Strategy})"))}");
        if (design.Grounds.Count > 0)
            sb.AppendLine($"- Ground: {string.Join(", ", design.Grounds.Select(g => $"{g.ElementType}×{g.Count}({g.Strategy})"))}");

        sb.AppendLine($"- 目标: {string.Join(", ", design.Objectives.Select(o => $"{o.TargetLayer}/{o.ElementType}×{o.TargetCount}"))}");
        sb.AppendLine();

        sb.AppendLine("## 分析结果");
        sb.AppendLine($"- 胜率: {analysis.WinRate:P1}（目标: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0}）");
        sb.AppendLine($"- 死锁率: {analysis.DeadlockRate:P1}");
        sb.AppendLine($"- 平均步数: {analysis.AverageMovesUsed:F1}/{design.MoveLimit}");
        sb.AppendLine($"- 结果: {(passed ? "PASS" : "FAIL")}");
        sb.AppendLine();

        // Show existing insights to avoid duplicates
        if (context.Insights.Count > 0)
        {
            sb.AppendLine("## 已有经验（不要重复）");
            foreach (var existing in context.Insights)
                sb.AppendLine($"- [{existing.Category}] {existing.Title}");
            sb.AppendLine();
        }

        sb.AppendLine("请提炼 0-3 条新经验。只提炼有通用价值的发现，不要重复已有经验。");
        sb.AppendLine("经验分类：foundation(基础/通用)、element(单元素相关)、combination(多元素组合)");

        return sb.ToString();
    }

    private static object BuildToolSchema()
    {
        return new
        {
            name = "save_insights",
            description = "保存从本次迭代中提炼的设计经验",
            input_schema = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["insights"] = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["category"] = new { type = "string", @enum = new[] { "foundation", "element", "combination" } },
                                ["tags"] = new { type = "array", items = new { type = "string" }, description = "Related element names" },
                                ["title"] = new { type = "string", description = "Short title" },
                                ["finding"] = new { type = "string", description = "What was discovered (data-backed)" },
                                ["recommendation"] = new { type = "string", description = "What to do about it" }
                            },
                            required = new[] { "category", "tags", "title", "finding", "recommendation" }
                        }
                    }
                },
                required = new[] { "insights" }
            }
        };
    }

    private List<DesignInsight> ParseInsightsResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content))
                return new List<DesignInsight>();

            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) &&
                    type.GetString() == "tool_use" &&
                    block.TryGetProperty("input", out var input) &&
                    input.TryGetProperty("insights", out var insightsArr))
                {
                    var result = new List<DesignInsight>();
                    foreach (var item in insightsArr.EnumerateArray())
                    {
                        result.Add(new DesignInsight
                        {
                            Category = item.GetProperty("category").GetString() ?? "",
                            Tags = item.TryGetProperty("tags", out var tags)
                                ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToArray()
                                : Array.Empty<string>(),
                            Title = item.GetProperty("title").GetString() ?? "",
                            Finding = item.GetProperty("finding").GetString() ?? "",
                            Recommendation = item.GetProperty("recommendation").GetString() ?? ""
                        });
                    }
                    return result;
                }
            }
        }
        catch (JsonException)
        {
            // Graceful fallback: LLM returned unparseable JSON
        }

        return new List<DesignInsight>();
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
