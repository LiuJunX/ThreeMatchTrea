using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Match3.Core.Analysis;
using Match3.Core.Config;

namespace Match3.LevelCli.Services;

/// <summary>
/// ILevelDesigner implementation that calls the Anthropic Claude API.
/// Uses Messages API with tool_use for structured output.
/// </summary>
public sealed class LlmLevelDesigner : ILevelDesigner, IDisposable
{
    private readonly LlmConfig _config;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public LlmLevelDesigner(LlmConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<LevelDesign> DesignAsync(
        LevelDesignContext context, CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildDesignPrompt(context);

        return await CallLlmAsync(systemPrompt, userPrompt, ct);
    }

    public async Task<LevelDesign> ReviseAsync(
        LevelDesign previousDesign, LevelAnalysisResult analysisResult,
        LevelDesignContext context, CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildRevisionPrompt(previousDesign, analysisResult, context);

        return await CallLlmAsync(systemPrompt, userPrompt, ct);
    }

    // ── API Call ──

    private async Task<LevelDesign> CallLlmAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var toolSchema = BuildToolSchema();

        var requestBody = new
        {
            model = _config.Model,
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature,
            system = systemPrompt,
            tools = new[] { toolSchema },
            tool_choice = new { type = "tool", name = "design_level" },
            messages = new[]
            {
                new { role = "user", content = userPrompt }
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
        {
            throw new HttpRequestException(
                $"Anthropic API error {response.StatusCode}: {responseBody}");
        }

        return ParseToolUseResponse(responseBody);
    }

    private LevelDesign ParseToolUseResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("content", out var content))
            throw new InvalidOperationException("No 'content' in response");

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) &&
                type.GetString() == "tool_use" &&
                block.TryGetProperty("input", out var input))
            {
                var inputJson = input.GetRawText();
                return JsonSerializer.Deserialize<LevelDesign>(inputJson, JsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize LevelDesign from tool_use input");
            }
        }

        throw new InvalidOperationException("No tool_use block found in response");
    }

    // ── Tool Schema ──

    private static object BuildToolSchema()
    {
        return new
        {
            name = "design_level",
            description = "Output a structured level design with semantic placement strategies.",
            input_schema = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["width"] = new { type = "integer", description = "Board width" },
                    ["height"] = new { type = "integer", description = "Board height" },
                    ["shape"] = new { type = "string", description = "Board shape: rectangle, cross, diamond, l-shape, hourglass, compartment" },
                    ["colorCount"] = new { type = "integer", description = "Number of tile colors (4-7)" },
                    ["rhythm"] = new { type = "string", @enum = new[] { "Easy", "Normal", "Hard", "Boss" } },
                    ["difficulty"] = new { type = "number", description = "Difficulty value (0.0-1.0)" },
                    ["moveLimit"] = new { type = "integer", description = "Move limit" },
                    ["obstacles"] = new
                    {
                        type = "array",
                        items = ElementPlacementSchema(),
                        description = "Obstacle placements"
                    },
                    ["covers"] = new
                    {
                        type = "array",
                        items = ElementPlacementSchema(),
                        description = "Cover placements"
                    },
                    ["grounds"] = new
                    {
                        type = "array",
                        items = ElementPlacementSchema(),
                        description = "Ground placements"
                    },
                    ["movingObstacles"] = new
                    {
                        type = "array",
                        items = ElementPlacementSchema(),
                        description = "Moving obstacle placements"
                    },
                    ["objectives"] = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["targetLayer"] = new { type = "string", description = "Tile, Cover, Ground, or Obstacle" },
                                ["elementType"] = new { type = "string", description = "For Tile: color index (1-based). For others: type name." },
                                ["targetCount"] = new { type = "integer" }
                            },
                            required = new[] { "targetLayer", "elementType", "targetCount" }
                        }
                    },
                    ["designIntent"] = new { type = "string", description = "Core design intent: what this level teaches or tests" },
                    ["moveReasoning"] = new { type = "string", description = "Reasoning for move count" },
                    ["designNotes"] = new { type = "array", items = new { type = "string" } }
                },
                required = new[] { "width", "height", "shape", "colorCount", "moveLimit", "objectives", "designIntent" }
            }
        };
    }

    private static object ElementPlacementSchema()
    {
        return new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["elementType"] = new { type = "string", description = "Element type name (e.g., Box, Cage, Ice)" },
                ["count"] = new { type = "integer" },
                ["stage"] = new { type = "integer", description = "HP/Stage (default 1)" },
                ["strategy"] = new { type = "string", description = "Placement strategy: border, center, cluster, scattered, column_aligned, row_aligned, near_spawner, away_from_spawner, geometric, layered" },
                ["region"] = new { type = "string", description = "Optional region bias: center, top_half, bottom_half, left_half, right_half" },
                ["state"] = new { type = "integer", description = "Optional state value (e.g., color index for ColorBox)" }
            },
            required = new[] { "elementType", "count", "strategy" }
        };
    }

    // ── Prompt Building ──

    private static string BuildSystemPrompt(LevelDesignContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是三消游戏关卡设计师。你的任务是根据蓝图约束和设计知识，设计具有良好游戏体验的关卡。");
        sb.AppendLine();
        sb.AppendLine("## 设计原则");
        sb.AppendLine("1. 每关有一个核心谜题，不是堆砌元素");
        sb.AppendLine("2. 多条通关路径，不依赖唯一解法");
        sb.AppendLine("3. 难度在于规划，不在于运气");
        sb.AppendLine("4. 元素放置要有设计意图，不随机散布");
        sb.AppendLine();
        sb.AppendLine("## 设计三问");
        sb.AppendLine("- 这关教了什么或考了什么？");
        sb.AppendLine("- 玩家的\"啊哈时刻\"是什么？");
        sb.AppendLine("- 如果失败，玩家知道为什么吗？");
        sb.AppendLine();
        sb.AppendLine("## 设计禁忌");
        sb.AppendLine("- Honey + Cage 同区域 → 双重封锁");
        sb.AppendLine("- Safe + Stone + Owl 同关 → 道具需求灾难");
        sb.AppendLine("- 7色 + ColorBox/PotionBottle → 颜色匹配概率过低");
        sb.AppendLine("- Chain + Frost 大量混用 → 功能重叠");
        sb.AppendLine("- Mailbox ×3+ → Envelope 泛滥堵塞棋盘");
        sb.AppendLine("- 障碍堵死 Spawner → 无法生成新方块");
        sb.AppendLine("- 密集 Cage 成片 → 完全无法操作");
        sb.AppendLine();
        sb.AppendLine("## 放置策略说明");
        sb.AppendLine("| 策略 | 含义 |");
        sb.AppendLine("|------|------|");
        sb.AppendLine("| border | 沿棋盘边缘或 Void 旁边 |");
        sb.AppendLine("| center | 中心聚集 |");
        sb.AppendLine("| cluster | 连通块（BFS 扩展） |");
        sb.AppendLine("| scattered | 均匀分散到各区域 |");
        sb.AppendLine("| column_aligned | 纵列放置 |");
        sb.AppendLine("| row_aligned | 横排放置 |");
        sb.AppendLine("| near_spawner | 靠近生成器 |");
        sb.AppendLine("| away_from_spawner | 远离生成器 |");
        sb.AppendLine("| geometric | 几何图案（十字/菱形） |");
        sb.AppendLine("| layered | 分层放置，配合不同 HP |");
        sb.AppendLine();

        // Knowledge docs
        if (context.KnowledgeDocs.Count > 0)
        {
            sb.AppendLine("## 设计知识库");
            sb.AppendLine();
            sb.Append(KnowledgeDocService.ConcatenateDocs(context.KnowledgeDocs));
        }

        // Accumulated insights
        if (context.Insights.Count > 0)
        {
            sb.AppendLine();
            sb.Append(InsightSelector.FormatForPrompt(context.Insights));
        }

        return sb.ToString();
    }

    private static string BuildDesignPrompt(LevelDesignContext context)
    {
        var pool = context.Pool;
        var phase = context.Phase;

        var sb = new StringBuilder();
        sb.AppendLine($"设计第 {context.LevelNumber} 关（阶段：{phase.Name}）。");
        sb.AppendLine();
        sb.AppendLine("## 蓝图约束");
        sb.AppendLine($"- 棋盘尺寸: {pool.MinBoardWidth}-{pool.MaxBoardWidth} × {pool.MinBoardHeight}-{pool.MaxBoardHeight}");
        sb.AppendLine($"- 棋盘形状: {string.Join(", ", pool.Shapes)}");
        sb.AppendLine($"- 颜色数: {pool.MinColors}-{pool.MaxColors}");
        sb.AppendLine($"- 步数范围: {pool.MinMoves}-{pool.MaxMoves}");
        sb.AppendLine($"- 难度范围: {pool.MinDifficulty:F2}-{pool.MaxDifficulty:F2}");
        sb.AppendLine($"- 目标胜率: {pool.MinWinRate:P0}-{pool.MaxWinRate:P0}");
        sb.AppendLine($"- 最多目标: {pool.MaxObjectives}");
        sb.AppendLine($"- 目标层: {string.Join(", ", pool.ObjectiveLayers)}");
        sb.AppendLine();

        sb.AppendLine("## 可用元素");
        if (pool.Obstacles.Count > 0)
        {
            sb.AppendLine("### 障碍物");
            foreach (var (type, a) in pool.Obstacles)
                sb.AppendLine($"- {type}: maxStage={a.MaxStage}, maxCount={a.MaxCount}");
        }
        if (pool.Covers.Count > 0)
        {
            sb.AppendLine("### Cover");
            foreach (var (type, a) in pool.Covers)
                sb.AppendLine($"- {type}: maxHealth={a.MaxHealth}, maxCount={a.MaxCount}");
        }
        if (pool.Grounds.Count > 0)
        {
            sb.AppendLine("### Ground");
            foreach (var (type, a) in pool.Grounds)
                sb.AppendLine($"- {type}: maxHealth={a.MaxHealth}, maxCount={a.MaxCount}");
        }
        if (pool.MovingObstacles.Count > 0)
        {
            sb.AppendLine("### Moving Obstacles");
            foreach (var (type, a) in pool.MovingObstacles)
                sb.AppendLine($"- {type}: maxStage={a.MaxStage}, maxCount={a.MaxCount}");
        }
        sb.AppendLine();

        sb.AppendLine("使用 design_level 工具输出设计方案。");

        return sb.ToString();
    }

    private static string BuildRevisionPrompt(
        LevelDesign previousDesign, LevelAnalysisResult analysis,
        LevelDesignContext context)
    {
        var phase = context.Phase;
        var sb = new StringBuilder();

        sb.AppendLine($"第 {context.LevelNumber} 关的上一版设计分析结果：");
        sb.AppendLine();
        sb.AppendLine($"- 胜率: {analysis.WinRate:P1}（目标: {phase.MinWinRate:P0}-{phase.MaxWinRate:P0}）");
        sb.AppendLine($"- 死锁率: {analysis.DeadlockRate:P1}");
        sb.AppendLine($"- 平均步数: {analysis.AverageMovesUsed:F1} / {previousDesign.MoveLimit}");
        sb.AppendLine($"- 难度评级: {analysis.DifficultyRating}");
        sb.AppendLine();

        // Diagnose problems
        bool tooHard = analysis.WinRate < phase.MinWinRate;
        bool tooEasy = analysis.WinRate > phase.MaxWinRate;
        bool highDeadlock = analysis.DeadlockRate > 0.05f;

        sb.AppendLine("## 问题诊断");
        if (highDeadlock)
            sb.AppendLine("- **死锁率过高**: 检查 Spawner 附近是否有太多障碍物阻挡掉落");
        if (tooHard)
            sb.AppendLine("- **太难**: 考虑减少障碍物数量、增加步数、减少颜色数、简化目标");
        if (tooEasy)
            sb.AppendLine("- **太简单**: 考虑增加障碍物、减少步数、增加颜色数、增加目标难度");
        if (!tooHard && !tooEasy && !highDeadlock)
            sb.AppendLine("- 接近通过，微调步数或障碍物数量即可");
        sb.AppendLine();

        sb.AppendLine("## 上一版设计摘要");
        sb.AppendLine($"- 棋盘: {previousDesign.Width}×{previousDesign.Height} {previousDesign.Shape}");
        sb.AppendLine($"- 颜色: {previousDesign.ColorCount}");
        sb.AppendLine($"- 步数: {previousDesign.MoveLimit}");
        sb.AppendLine($"- 障碍物: {previousDesign.Obstacles.Count} 组");
        sb.AppendLine($"- Cover: {previousDesign.Covers.Count} 组");
        sb.AppendLine($"- Ground: {previousDesign.Grounds.Count} 组");
        sb.AppendLine($"- 意图: {previousDesign.DesignIntent}");
        sb.AppendLine();

        sb.AppendLine("请修改设计方案。使用 design_level 工具输出修改后的完整设计。");

        return sb.ToString();
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
