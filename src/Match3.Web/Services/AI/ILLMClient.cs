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
        public string Content { get; set; } = "";

        public static LLMMessage System(string content) => new LLMMessage { Role = "system", Content = content };
        public static LLMMessage User(string content) => new LLMMessage { Role = "user", Content = content };
        public static LLMMessage Assistant(string content) => new LLMMessage { Role = "assistant", Content = content };
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
        /// 流式发送消息
        /// </summary>
        IAsyncEnumerable<string> SendStreamAsync(
            IReadOnlyList<LLMMessage> messages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查是否可用
        /// </summary>
        bool IsAvailable { get; }
    }
}
