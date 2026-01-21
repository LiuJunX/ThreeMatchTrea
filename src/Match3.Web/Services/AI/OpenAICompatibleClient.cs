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

        public async Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
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
                    model = _options.Model,
                    messages = messages,
                    max_tokens = _options.MaxTokens,
                    temperature = _options.Temperature,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request to LLM API: {Model}", _options.Model);

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
    }
}
