using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Editor.Interfaces;
using Match3.Editor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Match3.Web.Services.AI
{
    /// <summary>
    /// AI 关卡编辑服务的 Web 实现 - 使用 Function Calling + 可选深度思考
    /// </summary>
    public class WebLevelAIChatService : ILevelAIChatService
    {
        private readonly ILLMClient _llmClient;
        private readonly LLMOptions _options;
        private readonly ILogger<WebLevelAIChatService> _logger;
        private readonly ILevelAnalysisService? _analysisService;
        private readonly DeepAnalysisService? _deepAnalysisService;
        private readonly Func<LevelConfig>? _getLevelConfig;

        private const int MaxToolCallRounds = 5;

        public bool IsAvailable => _llmClient.IsAvailable;

        /// <summary>
        /// 是否启用深度思考（需要配置 ReasonerModel）
        /// </summary>
        public bool DeepThinkingEnabled => !string.IsNullOrEmpty(_options.ReasonerModel);

        public WebLevelAIChatService(
            ILLMClient llmClient,
            IOptions<LLMOptions> options,
            ILogger<WebLevelAIChatService> logger,
            ILevelAnalysisService? analysisService = null,
            DeepAnalysisService? deepAnalysisService = null,
            Func<LevelConfig>? getLevelConfig = null)
        {
            _llmClient = llmClient;
            _options = options.Value;
            _logger = logger;
            _analysisService = analysisService;
            _deepAnalysisService = deepAnalysisService;
            _getLevelConfig = getLevelConfig;
        }

        public async Task<AIChatResponse> SendMessageAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(AIProgressStatus.Thinking);

            var messages = BuildMessages(message, context, history);
            var tools = ToolRegistry.GetAllTools();
            var allIntents = new List<LevelIntent>();
            var analysisResults = new StringBuilder();
            var deepThinkingResult = (string?)null;
            var usedDeepThinking = false;

            int round = 0;
            bool editOnlyMode = false; // 深度思考后切换为仅编辑模式
            while (round < MaxToolCallRounds)
            {
                round++;
                _logger.LogDebug("Tool calling round {Round}, editOnlyMode={EditOnly}", round, editOnlyMode);

                var response = await _llmClient.SendWithToolsAsync(messages, tools, cancellationToken);

                if (!response.Success)
                {
                    return new AIChatResponse
                    {
                        Success = false,
                        Error = response.Error
                    };
                }

                // 没有工具调用，返回最终结果
                if (!response.HasToolCalls)
                {
                    var finalMessage = response.Content ?? "";
                    if (deepThinkingResult != null)
                    {
                        finalMessage = $"💭 **深度思考结果**\n\n{deepThinkingResult}\n\n---\n\n{finalMessage}";
                    }
                    if (analysisResults.Length > 0)
                    {
                        finalMessage = analysisResults.ToString() + (string.IsNullOrEmpty(finalMessage) ? "" : "\n\n" + finalMessage);
                    }

                    return new AIChatResponse
                    {
                        Success = true,
                        Message = finalMessage,
                        Intents = allIntents,
                        UsedDeepThinking = usedDeepThinking
                    };
                }

                // 处理工具调用
                var toolResults = new List<ToolResult>();
                var needDeepThinking = false;
                var deepThinkingArgs = (string?)null;

                foreach (var toolCall in response.ToolCalls!)
                {
                    var toolName = toolCall.Function.Name;
                    var arguments = toolCall.Function.Arguments;

                    _logger.LogDebug("Processing tool call: {ToolName} with arguments: {Arguments}", toolName, arguments);

                    if (toolName == ToolRegistry.NeedDeepThinkingTool)
                    {
                        // 路由工具：触发深度思考
                        if (DeepThinkingEnabled)
                        {
                            needDeepThinking = true;
                            deepThinkingArgs = arguments;
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = "正在启动深度思考模式..."
                            });
                        }
                        else
                        {
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = "深度思考模式未启用（未配置 ReasonerModel），将使用普通模式继续"
                            });
                        }
                    }
                    else if (ToolRegistry.AnalysisToolNames.Contains(toolName))
                    {
                        // 分析工具 - 在编辑模式下拒绝调用
                        if (editOnlyMode)
                        {
                            _logger.LogDebug("Rejecting analysis tool {ToolName} in edit-only mode", toolName);
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = "当前为执行模式，请直接使用编辑工具（如 set_grid_size、set_objective）执行操作"
                            });
                        }
                        else
                        {
                            var result = await ExecuteAnalysisToolAsync(toolName, arguments, cancellationToken);
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = result
                            });
                            analysisResults.AppendLine(result);
                        }
                    }
                    else if (ToolRegistry.ToolNameToIntentType.TryGetValue(toolName, out var intentType))
                    {
                        // 编辑工具
                        var intent = ConvertToLevelIntent(intentType, arguments);
                        if (intent != null)
                        {
                            allIntents.Add(intent);
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = $"已执行 {toolName}"
                            });
                        }
                        else
                        {
                            toolResults.Add(new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                Content = $"参数解析失败: {arguments}"
                            });
                        }
                    }
                    else
                    {
                        toolResults.Add(new ToolResult
                        {
                            ToolCallId = toolCall.Id,
                            Content = $"未知工具: {toolName}"
                        });
                    }
                }

                // 如果触发深度思考，调用 R1 然后继续
                if (needDeepThinking && deepThinkingArgs != null)
                {
                    progress?.Report(AIProgressStatus.DeepThinking);
                    usedDeepThinking = true;

                    var thinkingResult = await ExecuteDeepThinkingAsync(
                        message, context, deepThinkingArgs, cancellationToken);

                    deepThinkingResult = thinkingResult;
                    progress?.Report(AIProgressStatus.Executing);

                    // 更新工具结果
                    for (int i = 0; i < toolResults.Count; i++)
                    {
                        if (toolResults[i].Content == "正在启动深度思考模式...")
                        {
                            toolResults[i] = new ToolResult
                            {
                                ToolCallId = toolResults[i].ToolCallId,
                                Content = $@"深度思考完成。请根据以下计划执行：

{thinkingResult}

---
【重要】现在进入执行模式：
- 禁止调用: analyze_level, deep_analyze, get_bottleneck, need_deep_thinking
- 只能调用: set_grid_size, set_move_limit, set_objective, add_objective, paint_tile, paint_cover 等编辑工具

【立即执行】按顺序调用：
1. set_grid_size(width=数值, height=数值)
2. set_move_limit(moves=数值)
3. set_objective(index=0, layer=""Tile"", element_type=0, count=35) // Red=0,Green=1,Blue=2,Yellow=3,Purple=4,Orange=5
4. 如果有更多目标: add_objective(layer=""Cover"", element_type=0, count=10) // Cage=0,Chain=1,Bubble=2
5. 如果需要放置元素: paint_tile/paint_cover

现在开始执行工具调用！"
                            };
                            break;
                        }
                    }

                    // 切换到仅编辑工具，避免调用分析工具或再次触发深度思考
                    tools = ToolRegistry.GetEditToolsOnly();
                    editOnlyMode = true;
                }

                // 添加助手消息和工具结果到对话
                messages.Add(LLMMessage.AssistantWithToolCalls(response.ToolCalls));
                foreach (var result in toolResults)
                {
                    messages.Add(LLMMessage.Tool(result.ToolCallId, result.Content));
                }
            }

            // 达到最大轮数
            var maxRoundMessage = analysisResults.Length > 0 ? analysisResults.ToString() : "操作已完成";
            if (deepThinkingResult != null)
            {
                maxRoundMessage = $"💭 **深度思考结果**\n\n{deepThinkingResult}\n\n---\n\n{maxRoundMessage}";
            }

            return new AIChatResponse
            {
                Success = true,
                Message = maxRoundMessage,
                Intents = allIntents,
                UsedDeepThinking = usedDeepThinking
            };
        }

        /// <summary>
        /// 执行深度思考（调用 R1 推理模型）
        /// </summary>
        private async Task<string> ExecuteDeepThinkingAsync(
            string originalMessage,
            LevelContext context,
            string toolArgs,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Triggering deep thinking mode with ReasonerModel: {Model}", _options.ReasonerModel);

            // 解析工具参数
            string taskSummary;
            string reason;
            try
            {
                using var doc = JsonDocument.Parse(toolArgs);
                var root = doc.RootElement;
                taskSummary = root.TryGetProperty("task_summary", out var ts) ? ts.GetString() ?? originalMessage : originalMessage;
                reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
            }
            catch
            {
                taskSummary = originalMessage;
                reason = "";
            }

            // 构建 R1 提示词
            var r1Prompt = BuildDeepThinkingPrompt(taskSummary, reason, context);
            var r1Messages = new List<LLMMessage>
            {
                LLMMessage.System(r1Prompt),
                LLMMessage.User(originalMessage)
            };

            // 调用 R1（不使用工具，限制输出长度）
            var r1Response = await _llmClient.SendAsync(
                r1Messages,
                _options.ReasonerModel!,
                _options.ReasonerMaxTokens,
                cancellationToken);

            if (!r1Response.Success)
            {
                _logger.LogWarning("Deep thinking failed: {Error}", r1Response.Error);
                return $"深度思考失败: {r1Response.Error}";
            }

            _logger.LogInformation("Deep thinking completed, tokens: {Prompt}+{Completion}",
                r1Response.PromptTokens, r1Response.CompletionTokens);

            return r1Response.Content ?? "（无思考结果）";
        }

        /// <summary>
        /// 构建深度思考的系统提示词
        /// </summary>
        private string BuildDeepThinkingPrompt(string taskSummary, string reason, LevelContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("你是一个 Match3 消除游戏的关卡设计专家。请简洁高效地给出设计方案，避免冗长分析。");
            sb.AppendLine();
            sb.AppendLine("## 任务");
            sb.AppendLine(taskSummary);
            if (!string.IsNullOrEmpty(reason))
            {
                sb.AppendLine();
                sb.AppendLine($"## 为什么需要深度思考");
                sb.AppendLine(reason);
            }
            sb.AppendLine();
            sb.AppendLine("## 当前关卡状态");
            sb.AppendLine($"- 网格大小: {context.Width} x {context.Height}");
            sb.AppendLine($"- 步数限制: {context.MoveLimit}");

            if (context.Objectives != null && context.Objectives.Length > 0)
            {
                sb.AppendLine("- 当前目标:");
                for (int i = 0; i < context.Objectives.Length; i++)
                {
                    var obj = context.Objectives[i];
                    if (obj.TargetLayer != ObjectiveTargetLayer.None)
                    {
                        sb.AppendLine($"  [{i}] {obj.TargetLayer} - {obj.ElementType} x {obj.TargetCount}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(context.GridSummary))
                sb.AppendLine($"- 网格摘要: {context.GridSummary}");

            if (!string.IsNullOrEmpty(context.DifficultyText))
                sb.AppendLine($"- 难度评估: {context.DifficultyText}");

            sb.AppendLine();
            sb.AppendLine("## 可用元素");
            sb.AppendLine("- TileType: Red, Green, Blue, Yellow, Purple, Orange, Rainbow, None");
            sb.AppendLine("- Bomb ElementTypes: HorizontalRocket, VerticalRocket, ColorBomb, Ufo, Square5x5");
            sb.AppendLine("- CoverType: None, Cage, Chain, Bubble");
            sb.AppendLine("- GroundType: None, Ice");
            sb.AppendLine();
            sb.AppendLine("## 输出要求（简洁，控制在200字内）");
            sb.AppendLine();
            sb.AppendLine("直接输出可执行参数：");
            sb.AppendLine("```");
            sb.AppendLine("grid: {width},{height}");
            sb.AppendLine("moves: {步数}");
            sb.AppendLine("objectives:");
            sb.AppendLine("- Tile,{type_id},{count}  // type_id: 0=Red,1=Green,2=Blue,3=Yellow,4=Purple,5=Orange");
            sb.AppendLine("- Cover,{type_id},{count} // type_id: 0=Cage,1=Chain,2=Bubble");
            sb.AppendLine("elements:");
            sb.AppendLine("- {x},{y},tile,{TileType}");
            sb.AppendLine("- {x},{y},cover,{CoverType}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("坐标0-based(0到size-1)。直接用英文类型名(Red/Green/Cage/Chain等)。");

            return sb.ToString();
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 流式模式暂不支持工具调用，降级为普通调用
            var response = await SendMessageAsync(message, context, history, null, cancellationToken);
            if (response.Success && !string.IsNullOrEmpty(response.Message))
            {
                yield return response.Message;
            }
            else if (!response.Success)
            {
                yield return $"[错误: {response.Error}]";
            }
        }

        private List<LLMMessage> BuildMessages(
            string userMessage,
            LevelContext context,
            IReadOnlyList<ChatMessage> history)
        {
            var messages = new List<LLMMessage>
            {
                LLMMessage.System(BuildSystemPrompt(context))
            };

            // 添加历史消息（最多 10 条）
            var startIndex = Math.Max(0, history.Count - 10);
            for (int i = startIndex; i < history.Count; i++)
            {
                var msg = history[i];
                if (msg.Role == ChatRole.User)
                    messages.Add(LLMMessage.User(msg.Content));
                else if (msg.Role == ChatRole.Assistant && !msg.IsError)
                    messages.Add(LLMMessage.Assistant(msg.Content));
            }

            // 添加当前用户消息
            messages.Add(LLMMessage.User(userMessage));

            return messages;
        }

        private string BuildSystemPrompt(LevelContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("你是一个 Match3 消除游戏的关卡编辑助手。根据用户的自然语言描述，使用工具来编辑关卡或分析关卡。");
            sb.AppendLine();
            sb.AppendLine("## 当前关卡状态");
            sb.AppendLine($"- 网格大小: {context.Width} x {context.Height}");
            sb.AppendLine($"- 步数限制: {context.MoveLimit}");

            if (context.Objectives != null && context.Objectives.Length > 0)
            {
                sb.AppendLine("- 当前目标:");
                for (int i = 0; i < context.Objectives.Length; i++)
                {
                    var obj = context.Objectives[i];
                    if (obj.TargetLayer != ObjectiveTargetLayer.None)
                    {
                        sb.AppendLine($"  [{i}] {obj.TargetLayer} - {obj.ElementType} x {obj.TargetCount}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(context.GridSummary))
                sb.AppendLine($"- 网格摘要: {context.GridSummary}");

            if (!string.IsNullOrEmpty(context.DifficultyText))
                sb.AppendLine($"- 难度评估: {context.DifficultyText}");

            sb.AppendLine();
            sb.AppendLine("## 可用元素");
            sb.AppendLine("- TileType: Red, Green, Blue, Yellow, Purple, Orange, Rainbow, None");
            sb.AppendLine("- Bomb ElementTypes: HorizontalRocket, VerticalRocket, ColorBomb, Ufo, Square5x5");
            sb.AppendLine("- CoverType: None, Cage, Chain, Bubble");
            sb.AppendLine("- GroundType: None, Ice");
            sb.AppendLine();
            sb.AppendLine("## 注意事项");
            sb.AppendLine("- 坐标从 (0,0) 开始，x 是列，y 是行");
            sb.AppendLine("- place_bomb 的 x/y 参数使用 -1 表示网格中心");
            sb.AppendLine("- 如果用户只是聊天或提问，直接回复文字即可，不需要调用工具");
            sb.AppendLine("- 用户要求分析关卡时，使用 analyze_level 或 deep_analyze 工具");

            return sb.ToString();
        }

        private LevelIntent? ConvertToLevelIntent(LevelIntentType type, string argumentsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;
                var parameters = new Dictionary<string, object>();

                foreach (var prop in root.EnumerateObject())
                {
                    var key = ConvertSnakeToCamel(prop.Name);
                    object value = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.ToString()
                    };
                    parameters[key] = value;
                }

                // 特殊处理 place_bomb: x=-1 或 y=-1 表示中心位置
                if (type == LevelIntentType.PlaceBomb)
                {
                    bool xIsCenter = parameters.TryGetValue("x", out var xVal) && IsNegativeOne(xVal);
                    bool yIsCenter = parameters.TryGetValue("y", out var yVal) && IsNegativeOne(yVal);
                    if (xIsCenter || yIsCenter)
                    {
                        parameters["center"] = true;
                    }
                }

                return new LevelIntent
                {
                    Type = type,
                    Parameters = parameters
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse tool arguments: {Arguments}", argumentsJson);
                return null;
            }
        }

        private static string ConvertSnakeToCamel(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
                return snakeCase;

            var parts = snakeCase.Split('_');
            if (parts.Length == 1)
                return snakeCase;

            var sb = new StringBuilder(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    sb.Append(char.ToUpperInvariant(parts[i][0]));
                    if (parts[i].Length > 1)
                        sb.Append(parts[i].Substring(1));
                }
            }
            return sb.ToString();
        }

        private static bool IsNegativeOne(object? value)
        {
            if (value == null) return false;
            try
            {
                return Convert.ToInt32(value) == -1;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> ExecuteAnalysisToolAsync(
            string toolName,
            string argumentsJson,
            CancellationToken cancellationToken)
        {
            // 获取当前关卡配置
            var levelConfig = _getLevelConfig?.Invoke();
            if (levelConfig == null)
            {
                return "无法获取当前关卡配置";
            }

            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;

                switch (toolName)
                {
                    case "analyze_level":
                        return await ExecuteAnalyzeLevelAsync(levelConfig, root, cancellationToken);

                    case "deep_analyze":
                        return await ExecuteDeepAnalyzeAsync(levelConfig, root, cancellationToken);

                    case "get_bottleneck":
                        return await ExecuteGetBottleneckAsync(levelConfig, cancellationToken);

                    default:
                        return $"未知分析工具: {toolName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing analysis tool: {ToolName}", toolName);
                return $"分析执行失败: {ex.Message}";
            }
        }

        private async Task<string> ExecuteAnalyzeLevelAsync(
            LevelConfig levelConfig,
            JsonElement args,
            CancellationToken cancellationToken)
        {
            if (_analysisService == null)
            {
                return "分析服务未配置";
            }

            int simulationCount = 500;
            if (args.TryGetProperty("simulation_count", out var simCountProp))
            {
                simulationCount = simCountProp.GetInt32();
            }

            var config = new AnalysisConfig
            {
                SimulationCount = simulationCount,
                UseParallel = true
            };

            var result = await _analysisService.AnalyzeAsync(levelConfig, config, null, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("## 关卡分析结果");
            sb.AppendLine($"- 模拟次数: {result.TotalSimulations}");
            sb.AppendLine($"- 胜率: {result.WinRate:P1}");
            sb.AppendLine($"- 死锁率: {result.DeadlockRate:P1}");
            sb.AppendLine($"- 平均使用步数: {result.AverageMovesUsed:F1}");
            sb.AppendLine($"- 难度评级: {result.DifficultyRating}");
            sb.AppendLine($"- 分析耗时: {result.ElapsedMs:F0}ms");

            return sb.ToString();
        }

        private async Task<string> ExecuteDeepAnalyzeAsync(
            LevelConfig levelConfig,
            JsonElement args,
            CancellationToken cancellationToken)
        {
            if (_deepAnalysisService == null)
            {
                return "深度分析服务未配置";
            }

            int simulationsPerTier = 250;
            if (args.TryGetProperty("simulations_per_tier", out var simPerTierProp))
            {
                simulationsPerTier = simPerTierProp.GetInt32();
            }

            var result = await _deepAnalysisService.AnalyzeAsync(levelConfig, simulationsPerTier, null, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("## 深度分析结果");
            sb.AppendLine();
            sb.AppendLine("### 分层胜率");
            foreach (var kvp in result.TierWinRates)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value:P1}");
            }
            sb.AppendLine();
            sb.AppendLine("### 关键指标");
            sb.AppendLine($"- 技能敏感度: {result.SkillSensitivity:P0} (高=技能关, 低=运气关)");
            sb.AppendLine($"- 运气依赖度: {result.LuckDependency:P0} (理想范围: 20-40%)");
            sb.AppendLine($"- 挫败风险: {result.FrustrationRisk:P1} (连续3局失败概率)");
            sb.AppendLine($"- P95通关次数: {result.P95ClearAttempts}次 (95%玩家在此次数内通关)");
            sb.AppendLine();
            sb.AppendLine("### 瓶颈目标");
            if (!string.IsNullOrEmpty(result.BottleneckObjective))
            {
                sb.AppendLine($"- 最难目标: {result.BottleneckObjective} (占失败的 {result.BottleneckFailureRate:P0})");
            }
            else
            {
                sb.AppendLine("- 无明显瓶颈目标");
            }
            sb.AppendLine();
            sb.AppendLine("### 心流曲线");
            sb.AppendLine($"- 最低爽感: {result.FlowMin:F1}");
            sb.AppendLine($"- 最高爽感: {result.FlowMax:F1}");
            sb.AppendLine($"- 平均爽感: {result.FlowAverage:F1}");
            sb.AppendLine();
            sb.AppendLine($"分析耗时: {result.ElapsedMs:F0}ms, 总模拟次数: {result.TotalSimulations}");

            return sb.ToString();
        }

        private async Task<string> ExecuteGetBottleneckAsync(
            LevelConfig levelConfig,
            CancellationToken cancellationToken)
        {
            // 使用深度分析获取瓶颈信息
            if (_deepAnalysisService == null)
            {
                return "深度分析服务未配置，无法获取瓶颈信息";
            }

            var result = await _deepAnalysisService.AnalyzeAsync(levelConfig, 100, null, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("## 瓶颈分析");

            if (!string.IsNullOrEmpty(result.BottleneckObjective))
            {
                sb.AppendLine($"- 瓶颈目标: {result.BottleneckObjective}");
                sb.AppendLine($"- 失败占比: {result.BottleneckFailureRate:P0}");
                sb.AppendLine();
                sb.AppendLine("### 建议");
                if (result.BottleneckFailureRate > 0.5f)
                {
                    sb.AppendLine("- 该目标是主要失败原因，建议降低目标数量或增加步数");
                }
                else
                {
                    sb.AppendLine("- 目标难度相对均衡");
                }
            }
            else
            {
                sb.AppendLine("- 无明显瓶颈目标，各目标难度相对均衡");
            }

            sb.AppendLine();
            sb.AppendLine($"### 分层胜率参考");
            foreach (var kvp in result.TierWinRates)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value:P1}");
            }

            return sb.ToString();
        }
    }
}
