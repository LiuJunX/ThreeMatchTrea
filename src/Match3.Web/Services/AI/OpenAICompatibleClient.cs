using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Match3.Web.Services.AI
{
    /// <summary>
    /// OpenAI 兼容 API 客户端（支持 DeepSeek、OpenAI 等）
    /// </summary>
    public class OpenAICompatibleClient : ILLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly LLMOptions _options;
        private readonly ILogger<OpenAICompatibleClient> _logger;

        public bool IsAvailable => !string.IsNullOrEmpty(_options.ApiKey);

        public OpenAICompatibleClient(
            HttpClient httpClient,
            IOptions<LLMOptions> options,
            ILogger<OpenAICompatibleClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            // 配置 HttpClient
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        public Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(messages, _options.Model, _options.MaxTokens, cancellationToken);
        }

        public Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            string model,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(messages, model, _options.MaxTokens, cancellationToken);
        }

        public async Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            string model,
            int maxTokens,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return new LLMResponse
                {
                    Success = false,
                    Error = "API Key 未配置"
                };
            }

            try
            {
                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens,
                    temperature = _options.Temperature,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request to LLM API: {Model}", model);

                var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("LLM API error: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);

                    return new LLMResponse
                    {
                        Success = false,
                        Error = $"API 错误: {response.StatusCode}"
                    };
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var messageContent = root
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                var usage = root.GetProperty("usage");
                var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
                var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

                _logger.LogDebug("LLM response received: {PromptTokens} prompt, {CompletionTokens} completion tokens",
                    promptTokens, completionTokens);

                return new LLMResponse
                {
                    Success = true,
                    Content = messageContent,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling LLM API");
                return new LLMResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async IAsyncEnumerable<string> SendStreamAsync(
            IReadOnlyList<LLMMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                yield return "[错误: API Key 未配置]";
                yield break;
            }

            var requestBody = new
            {
                model = _options.Model,
                messages = messages,
                max_tokens = _options.MaxTokens,
                temperature = _options.Temperature,
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("LLM stream error: {StatusCode} - {Response}",
                        response.StatusCode, errorContent);
                    yield return $"[错误: {response.StatusCode}]";
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (!line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6);

                    if (data == "[DONE]")
                        break;

                    var content = TryParseStreamContent(data);
                    if (!string.IsNullOrEmpty(content))
                    {
                        yield return content;
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }
        }

        private string? TryParseStreamContent(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var delta = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentElement))
                {
                    return contentElement.GetString();
                }
            }
            catch (JsonException)
            {
                // 忽略无法解析的行
            }
            return null;
        }

        public async Task<LLMResponse> SendWithToolsAsync(
            IReadOnlyList<LLMMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return new LLMResponse
                {
                    Success = false,
                    Error = "API Key 未配置"
                };
            }

            try
            {
                // 构建消息数组
                var messagesArray = new List<object>();
                foreach (var msg in messages)
                {
                    messagesArray.Add(SerializeMessage(msg));
                }

                // 构建工具数组
                var toolsArray = new List<object>();
                foreach (var tool in tools)
                {
                    toolsArray.Add(SerializeTool(tool));
                }

                var requestBody = new Dictionary<string, object>
                {
                    ["model"] = _options.Model,
                    ["messages"] = messagesArray,
                    ["tools"] = toolsArray,
                    ["max_tokens"] = _options.MaxTokens,
                    ["temperature"] = _options.Temperature,
                    ["stream"] = false
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request with tools to LLM API: {Model}, tools count: {ToolCount}",
                    _options.Model, tools.Count);

                var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("LLM API error: {StatusCode} - {Response}",
                        response.StatusCode, responseJson);

                    return new LLMResponse
                    {
                        Success = false,
                        Error = $"API 错误: {response.StatusCode}"
                    };
                }

                return ParseToolCallResponse(responseJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling LLM API with tools");
                return new LLMResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private object SerializeMessage(LLMMessage msg)
        {
            var result = new Dictionary<string, object>
            {
                ["role"] = msg.Role
            };

            if (msg.Content != null)
            {
                result["content"] = msg.Content;
            }

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCallsArray = new List<object>();
                foreach (var tc in msg.ToolCalls)
                {
                    toolCallsArray.Add(new Dictionary<string, object>
                    {
                        ["id"] = tc.Id,
                        ["type"] = tc.Type,
                        ["function"] = new Dictionary<string, object>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = tc.Function.Arguments
                        }
                    });
                }
                result["tool_calls"] = toolCallsArray;
            }

            if (msg.ToolCallId != null)
            {
                result["tool_call_id"] = msg.ToolCallId;
            }

            return result;
        }

        private object SerializeTool(ToolDefinition tool)
        {
            var properties = new Dictionary<string, object>();
            foreach (var kvp in tool.Function.Parameters.Properties)
            {
                var prop = new Dictionary<string, object>
                {
                    ["type"] = kvp.Value.Type
                };

                if (kvp.Value.Description != null)
                    prop["description"] = kvp.Value.Description;

                if (kvp.Value.Enum != null)
                    prop["enum"] = kvp.Value.Enum;

                if (kvp.Value.Minimum.HasValue)
                    prop["minimum"] = kvp.Value.Minimum.Value;

                if (kvp.Value.Maximum.HasValue)
                    prop["maximum"] = kvp.Value.Maximum.Value;

                properties[kvp.Key] = prop;
            }

            return new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object>
                {
                    ["name"] = tool.Function.Name,
                    ["description"] = tool.Function.Description,
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = tool.Function.Parameters.Required
                    }
                }
            };
        }

        private LLMResponse ParseToolCallResponse(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var choice = root.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            string? content = null;
            if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null)
            {
                content = contentProp.GetString();
            }

            string? finishReason = null;
            if (choice.TryGetProperty("finish_reason", out var finishProp))
            {
                finishReason = finishProp.GetString();
            }

            List<ToolCall>? toolCalls = null;
            if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.ValueKind == JsonValueKind.Array)
            {
                toolCalls = new List<ToolCall>();
                foreach (var tcJson in toolCallsProp.EnumerateArray())
                {
                    var tc = new ToolCall
                    {
                        Id = tcJson.GetProperty("id").GetString() ?? "",
                        Type = tcJson.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "function" : "function"
                    };

                    if (tcJson.TryGetProperty("function", out var funcProp))
                    {
                        tc.Function = new FunctionCall
                        {
                            Name = funcProp.GetProperty("name").GetString() ?? "",
                            Arguments = funcProp.GetProperty("arguments").GetString() ?? "{}"
                        };
                    }

                    toolCalls.Add(tc);
                }
            }

            var usage = root.GetProperty("usage");
            var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

            _logger.LogDebug("LLM response with tools received: {PromptTokens} prompt, {CompletionTokens} completion tokens, tool_calls: {ToolCallCount}",
                promptTokens, completionTokens, toolCalls?.Count ?? 0);

            return new LLMResponse
            {
                Success = true,
                Content = content,
                ToolCalls = toolCalls,
                FinishReason = finishReason,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };
        }
    }
}
