using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Models.Enums;
using Match3.Editor.Interfaces;
using Match3.Editor.Logic;
using Match3.Editor.Models;
using Microsoft.Extensions.Logging;

namespace Match3.Web.Services.AI
{
    /// <summary>
    /// AI 关卡编辑服务的 Web 实现
    /// </summary>
    public class WebLevelAIChatService : ILevelAIChatService
    {
        private readonly ILLMClient _llmClient;
        private readonly ILogger<WebLevelAIChatService> _logger;

        public bool IsAvailable => _llmClient.IsAvailable;

        public WebLevelAIChatService(
            ILLMClient llmClient,
            ILogger<WebLevelAIChatService> logger)
        {
            _llmClient = llmClient;
            _logger = logger;
        }

        public async Task<AIChatResponse> SendMessageAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default)
        {
            var messages = BuildMessages(message, context, history);

            var response = await _llmClient.SendAsync(messages, cancellationToken);

            if (!response.Success)
            {
                return new AIChatResponse
                {
                    Success = false,
                    Error = response.Error
                };
            }

            return ParseResponse(response.Content ?? "");
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messages = BuildMessages(message, context, history);

            await foreach (var chunk in _llmClient.SendStreamAsync(messages, cancellationToken))
            {
                yield return chunk;
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

            sb.AppendLine("你是一个 Match3 消除游戏的关卡编辑助手。根据用户的自然语言描述，生成关卡编辑操作。");
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
                    if (obj.TargetLayer != Core.Models.Enums.ObjectiveTargetLayer.None)
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
            sb.AppendLine("## 你可以执行的操作");
            sb.AppendLine("返回 JSON 格式，包含 message 和 intents 数组。每个 intent 有 type 和 parameters。");
            sb.AppendLine();
            sb.AppendLine("操作类型:");
            sb.AppendLine("1. SetGridSize: {width, height} - 设置网格大小 (3-12)");
            sb.AppendLine("2. SetMoveLimit: {moves} - 设置步数限制 (1-99)");
            sb.AppendLine("3. SetObjective: {index?, layer, elementType, count} - 设置目标");
            sb.AppendLine("   - layer: Tile, Cover, Ground");
            sb.AppendLine($"   - elementType (Tile层): {AITypeRegistry.GetPromptDescription<TileType>()}");
            sb.AppendLine($"   - elementType (Cover层): {AITypeRegistry.GetPromptDescription<CoverType>()}");
            sb.AppendLine($"   - elementType (Ground层): {AITypeRegistry.GetPromptDescription<GroundType>()}");
            sb.AppendLine("4. PaintTile: {x, y, tileType, bombType?} - 绘制单个格子");
            sb.AppendLine("5. PaintTileRegion: {x1, y1, x2, y2, tileType, bombType?} - 绘制区域");
            sb.AppendLine("6. PaintCover: {x, y, coverType} - 放置覆盖物");
            sb.AppendLine("7. PaintCoverRegion: {x1, y1, x2, y2, coverType} - 区域覆盖物");
            sb.AppendLine("8. PaintGround: {x, y, groundType} - 放置地面");
            sb.AppendLine("9. PaintGroundRegion: {x1, y1, x2, y2, groundType} - 区域地面");
            sb.AppendLine("10. PlaceBomb: {x, y, bombType, tileType?} - 放置炸弹");
            sb.AppendLine("11. GenerateRandomLevel: {} - 随机生成关卡");
            sb.AppendLine("12. ClearRegion: {x1?, y1?, x2?, y2?} - 清空区域");
            sb.AppendLine("13. ClearAll: {} - 清空整个网格");
            sb.AppendLine();
            sb.AppendLine("## 可用元素");
            sb.AppendLine("- TileType: Red, Green, Blue, Yellow, Purple, Orange, Rainbow, None");
            sb.AppendLine("- BombType: None, Horizontal, Vertical, Color, Ufo, Square5x5");
            sb.AppendLine("- CoverType: None, Cage, Chain, Bubble");
            sb.AppendLine("- GroundType: None, Ice");
            sb.AppendLine();
            sb.AppendLine("## 响应格式");
            sb.AppendLine("必须返回有效的 JSON：");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"message\": \"你的回复内容\",");
            sb.AppendLine("  \"intents\": [");
            sb.AppendLine("    {\"type\": \"SetGridSize\", \"parameters\": {\"width\": 8, \"height\": 8}}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("如果用户只是聊天或提问，intents 可以为空数组。");
            sb.AppendLine("坐标从 (0,0) 开始，x 是列，y 是行。");

            return sb.ToString();
        }

        private AIChatResponse ParseResponse(string content)
        {
            try
            {
                // 尝试提取 JSON
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonStr = content.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    var message = "";
                    if (root.TryGetProperty("message", out var msgProp))
                        message = msgProp.GetString() ?? "";

                    var intents = new List<LevelIntent>();

                    if (root.TryGetProperty("intents", out var intentsProp) &&
                        intentsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var intentJson in intentsProp.EnumerateArray())
                        {
                            var intent = ParseIntent(intentJson);
                            if (intent != null)
                                intents.Add(intent);
                        }
                    }

                    return new AIChatResponse
                    {
                        Success = true,
                        Message = message,
                        Intents = intents
                    };
                }

                // 无法解析 JSON，返回原文
                return new AIChatResponse
                {
                    Success = true,
                    Message = content,
                    Intents = new List<LevelIntent>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI response as JSON");

                return new AIChatResponse
                {
                    Success = true,
                    Message = content,
                    Intents = new List<LevelIntent>()
                };
            }
        }

        private LevelIntent? ParseIntent(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("type", out var typeProp))
                    return null;

                var typeStr = typeProp.GetString();
                if (string.IsNullOrEmpty(typeStr))
                    return null;

                if (!Enum.TryParse<LevelIntentType>(typeStr, true, out var type))
                    return null;

                var parameters = new Dictionary<string, object>();

                if (json.TryGetProperty("parameters", out var paramsProp) &&
                    paramsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in paramsProp.EnumerateObject())
                    {
                        object value = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                        parameters[prop.Name] = value;
                    }
                }

                return new LevelIntent
                {
                    Type = type,
                    Parameters = parameters
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
