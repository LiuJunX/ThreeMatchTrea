using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Editor.Models;

namespace Match3.Editor.Interfaces
{
    /// <summary>
    /// AI 对话服务接口 - 用于关卡编辑器的智能辅助
    /// </summary>
    public interface ILevelAIChatService
    {
        /// <summary>
        /// 发送消息并获取 AI 响应
        /// </summary>
        /// <param name="message">用户消息</param>
        /// <param name="context">当前关卡上下文</param>
        /// <param name="history">对话历史</param>
        /// <param name="progress">进度报告（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>AI 响应结果</returns>
        Task<AIChatResponse> SendMessageAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 流式发送消息（用于打字机效果）
        /// </summary>
        IAsyncEnumerable<string> SendMessageStreamAsync(
            string message,
            LevelContext context,
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查服务是否可用
        /// </summary>
        bool IsAvailable { get; }
    }
}
