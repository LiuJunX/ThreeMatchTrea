using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Match3.Web.Services.AI
{
    /// <summary>
    /// LLM 消息
    /// </summary>
    public class LLMMessage
    {
        public string Role { get; set; } = "";
        public string? Content { get; set; }

        /// <summary>
        /// 工具调用列表 - 当 Role 为 assistant 且响应包含工具调用时使用
        /// </summary>
        public List<ToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// 工具调用 ID - 当 Role 为 tool 时使用
        /// </summary>
        public string? ToolCallId { get; set; }

        public static LLMMessage System(string content) => new LLMMessage { Role = "system", Content = content };
        public static LLMMessage User(string content) => new LLMMessage { Role = "user", Content = content };
        public static LLMMessage Assistant(string content) => new LLMMessage { Role = "assistant", Content = content };

        /// <summary>
        /// 创建包含工具调用的助手消息
        /// </summary>
        public static LLMMessage AssistantWithToolCalls(List<ToolCall> toolCalls) => new LLMMessage
        {
            Role = "assistant",
            Content = null,
            ToolCalls = toolCalls
        };

        /// <summary>
        /// 创建工具结果消息
        /// </summary>
        public static LLMMessage Tool(string toolCallId, string content) => new LLMMessage
        {
            Role = "tool",
            Content = content,
            ToolCallId = toolCallId
        };
    }

    /// <summary>
    /// LLM 响应
    /// </summary>
    public class LLMResponse
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Error { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }

        /// <summary>
        /// 工具调用列表 - 当模型请求调用工具时填充
        /// </summary>
        public List<ToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// 完成原因：stop, tool_calls, length, content_filter
        /// </summary>
        public string? FinishReason { get; set; }

        /// <summary>
        /// 是否包含工具调用
        /// </summary>
        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
    }

    /// <summary>
    /// LLM 客户端接口 - 可插拔设计
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// 发送消息并获取响应
        /// </summary>
        Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送消息并获取响应（指定模型）
        /// </summary>
        Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            string model,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送消息并获取响应（指定模型和 maxTokens）
        /// </summary>
        Task<LLMResponse> SendAsync(
            IReadOnlyList<LLMMessage> messages,
            string model,
            int maxTokens,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 流式发送消息
        /// </summary>
        IAsyncEnumerable<string> SendStreamAsync(
            IReadOnlyList<LLMMessage> messages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送消息并支持工具调用
        /// </summary>
        Task<LLMResponse> SendWithToolsAsync(
            IReadOnlyList<LLMMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查是否可用
        /// </summary>
        bool IsAvailable { get; }
    }
}
